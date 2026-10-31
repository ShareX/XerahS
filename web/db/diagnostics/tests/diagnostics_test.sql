-- Behavioural tests for db/migrations. Run against a scratch database after
-- applying every migration in order:
--   node scripts/db-migrate.ts --dir db/diagnostics/migrations
--   psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f db/diagnostics/tests/diagnostics_test.sql
-- Any failed assertion raises and aborts the run.

update diagnostics.settings set allow_ingest = true;

create temporary table t_results (name text primary key, result jsonb);

-- A report from `install` whose logs run until `end_at`. Each report carries
-- one old line (12:00 on the day before end_at) and one line at end_at, so
-- overlapping reports share their older half.
create or replace function pg_temp.report(
  p_install text,
  p_client text,
  p_end timestamptz,
  p_patch jsonb default '{}')
returns jsonb language sql as $$
  select jsonb_build_object(
    'schemaVersion', 1, 'redactionVersion', 1, 'trigger', 'manual',
    'installId', p_install, 'clientReportId', p_client,
    'app', jsonb_build_object('version', '0.31.3', 'buildFlavor', 'Release', 'dotnetVersion', '10.0.12'),
    'os', jsonb_build_object('family', 'windows', 'description', 'Microsoft Windows 10.0.26200', 'version', '10.0.26200',
                             'architecture', 'X64', 'processArchitecture', 'X64', 'elevated', false),
    'capture', jsonb_build_object('captureBackend', 'dxgi',
                                  'ffmpeg', jsonb_build_object('version', '7.1', 'source', 'bundled', 'hwEncoders', jsonb_build_array('h264_nvenc'))),
    'hardware', jsonb_build_object('cpuModel', 'AMD Ryzen 9', 'logicalCores', 32, 'ramGbBucket', 64,
                                   'gpus', jsonb_build_array(jsonb_build_object('name', 'NVIDIA RTX 4090'))),
    'displays', jsonb_build_array(
      jsonb_build_object('ordinal', 0, 'width', 3840, 'height', 2160, 'scale', 1.5, 'rotation', 0, 'isHdr', false),
      jsonb_build_object('ordinal', 1, 'width', 1440, 'height', 2560, 'scale', 1.0, 'rotation', 270, 'isHdr', false)),
    'window', jsonb_build_object('kind', '24h', 'start', p_end - interval '1 day', 'end', p_end),
    'sessions', jsonb_build_array(jsonb_build_object(
      'startedAt', p_end - interval '2 hours', 'endedAt', p_end - interval '1 hour', 'appVersion', '0.31.3',
      'exitKind', 'abnormal', 'lastComponent', 'CaptureFullScreenDxgi',
      'lastMessageTemplate', 'EnumDisplaySettings orientation for <display> => dmDisplayOrientation=3, mappedRotation=Rotate270')),
    'events', jsonb_build_array(
      jsonb_build_object('kind', 'abnormal_exit', 'component', 'CaptureFullScreenDxgi',
        'messageTemplate', 'EnumDisplaySettings orientation for <display> => dmDisplayOrientation=3, mappedRotation=Rotate270',
        'firstAt', p_end - interval '1 hour', 'lastAt', p_end - interval '1 hour'),
      jsonb_build_object('kind', 'error_line', 'component', 'DxgiOutputDuplicationHelper',
        'messageTemplate', 'DuplicateOutput1 failed, using DuplicateOutput. HRESULT: [0x80070057]',
        'sampleMessage', 'DuplicateOutput1 failed E_INVALIDARG', 'occurrences', 12,
        'firstAt', p_end - interval '3 hours', 'lastAt', p_end - interval '10 minutes'),
      jsonb_build_object('kind', 'exception', 'exceptionType', 'System.NullReferenceException',
        'messageTemplate', 'Object reference not set', 'topFrames', jsonb_build_array('XerahS.Core.Foo.Bar()', 'XerahS.Core.Foo.Baz()'),
        'firstAt', p_end - interval '5 minutes', 'lastAt', p_end - interval '5 minutes')),
    'logs', jsonb_build_array(jsonb_build_object(
      'fileName', 'XerahS.log',
      'content', to_char((p_end - interval '1 day') at time zone 'UTC', 'YYYY-MM-DD HH24:MI:SS.MS') || ' - Capture start: Job=RectangleRegion' || E'\n' ||
                 '   at XerahS.Core.Old.Frame()' || E'\n' ||
                 to_char(p_end at time zone 'UTC', 'YYYY-MM-DD HH24:MI:SS.MS') || ' - CaptureFullScreenDxgi: orientation for DISPLAY4 => Rotate270'))
  ) || p_patch
$$;

create or replace function pg_temp.token(p_byte text)
returns bytea language sql as $$ select decode(repeat(p_byte, 32), 'hex') $$;

create or replace function pg_temp.submit(p_report jsonb)
returns jsonb language sql as $$ select diagnostics.submit_report(p_report, pg_temp.token('ab'), null) $$;

-- ---------------------------------------------------------------------------
-- Normalization, grouping, idempotency, timestamp dedup
-- ---------------------------------------------------------------------------
do $$
declare
  a jsonb;
  r jsonb;
  c jsonb;
  v_row record;
  v_install text := '11111111-1111-4111-8111-111111111111';
begin
  assert diagnostics.normalize_template('DuplicateOutput1 failed HRESULT [0x80070057] hwnd=0x17103C took 25 ms')
       = 'DuplicateOutput1 failed HRESULT [0x80070057] hwnd=<hex> took # ms',
    'normalize_template keeps identifiers and HRESULT codes';

  a := pg_temp.submit(pg_temp.report(v_install, '22222222-2222-4222-8222-222222222222', '2026-09-29 14:00Z'));
  assert (a ->> 'duplicate')::boolean = false and a ->> 'deduplicatedBefore' is null, 'first report is new and untrimmed';
  insert into t_results values ('a', a);

  r := pg_temp.submit(pg_temp.report(v_install, '22222222-2222-4222-8222-222222222222', '2026-09-29 14:00Z'));
  assert (r ->> 'duplicate')::boolean and r ->> 'reason' = 'same_report' and r ->> 'reportId' = a ->> 'reportId',
    'exact resend returns the stored report';

  -- Pressing Send again with the same logs: new client id, nothing newer.
  r := pg_temp.submit(pg_temp.report(v_install, '33333333-3333-4333-8333-333333333333', '2026-09-29 14:00Z'));
  assert (r ->> 'duplicate')::boolean and r ->> 'reason' = 'no_new_entries' and r ->> 'reportId' = a ->> 'reportId',
    format('same logs again are a duplicate: %s', r);
  r := pg_temp.submit(pg_temp.report(v_install, '33333333-3333-4333-8333-333333333334', '2026-09-29 13:00Z'));
  assert r ->> 'reason' = 'no_new_entries', 'older logs are a duplicate';
  assert (select count(*) from diagnostics.reports) = 1, 'duplicates store nothing';

  assert (select count(*) from diagnostics.crash_signatures) = 3, 'three signatures';
  assert not exists (select 1 from diagnostics.crash_signatures where report_count <> 1), 'counted once';

  -- One week later from the same install, but the window overlaps: only newer
  -- lines, sessions and events are kept.
  c := pg_temp.submit(pg_temp.report(v_install, '44444444-4444-4444-8444-444444444444', '2026-09-30 10:00Z',
         '{"window": {"kind": "7d", "start": "2026-09-23T10:00:00Z", "end": "2026-09-30T10:00:00Z"}}'));
  assert (c ->> 'duplicate')::boolean = false, 'overlapping report with newer lines is accepted';
  assert (c ->> 'deduplicatedBefore')::timestamptz = '2026-09-29 14:00Z', 'trimmed at the previous mark';
  insert into t_results values ('c', c);

  select * into v_row from diagnostics.reports where report_id = (c ->> 'reportId')::uuid;
  assert v_row.window_start = '2026-09-29 14:00Z', 'stored window starts at the mark';
  assert v_row.line_count = 1, format('only the new log line is stored, got %s', v_row.line_count);
  assert v_row.event_count = 3 and v_row.session_count = 1, 'new events and sessions kept';
  assert not exists (select 1 from diagnostics.report_log_chunks where report_id = (c ->> 'reportId')::uuid and content like '%Old.Frame%'),
    'continuation lines of old entries are dropped with them';

  assert (select last_log_at from diagnostics.install_state where install_id = v_install::uuid) = '2026-09-30 10:00Z',
    'high-water mark advanced';
  assert (diagnostics.install_status(v_install::uuid) ->> 'lastReportId') = c ->> 'reportId', 'install_status reports the mark';
  assert (diagnostics.install_status('99999999-9999-4999-8999-999999999999') ->> 'reportCount')::int = 0, 'unknown install';

  -- A different install sending identical content is a separate report.
  r := pg_temp.submit(pg_temp.report('55555555-5555-4555-8555-555555555555', '66666666-6666-4666-8666-666666666666', '2026-09-29 14:00Z'));
  assert (r ->> 'duplicate')::boolean = false, 'other installs are independent';
  assert (select report_count from diagnostics.crash_signatures where kind = 'exception') = 3, 'grouped across installs';

  select * into v_row from diagnostics.v_reports where report_id = (a ->> 'reportId')::uuid;
  assert v_row.abnormal_exit_count = 1 and v_row.event_count = 3 and v_row.display_count = 2, 'report summary columns';
  assert v_row.primary_signature_id = (a ->> 'primarySignatureId')::bigint, 'primary signature returned';
  assert (select kind from diagnostics.crash_signatures where signature_id = v_row.primary_signature_id) = 'abnormal_exit',
    'abnormal exit outranks other events';

  assert exists (select 1 from diagnostics.search('DuplicateOutput1', 20) where source = 'signature'), 'signature search';
  assert exists (select 1 from diagnostics.search('E_INVALIDARG', 20) where source = 'event'), 'event search';
  assert exists (select 1 from diagnostics.search('RectangleRegion', 20) where source = 'log'), 'log search';
  assert (select count(*) from diagnostics.report_log((a ->> 'reportId')::uuid)) = 1, 'report_log returns the chunk';
end;
$$;

-- Only 24h and 7d windows are accepted.
do $$
begin
  perform pg_temp.submit(pg_temp.report('77777777-7777-4777-8777-777777777777', '77777777-7777-4777-8777-777777777771', '2026-09-29 14:00Z',
    '{"window": {"kind": "30d", "start": "2026-08-30T14:00:00Z", "end": "2026-09-29T14:00:00Z"}}'));
  assert false, '30d window must be rejected';
exception when check_violation then
  null;
end;
$$;

-- Large logs split at line boundaries into <= 64 KB chunks.
do $$
declare
  c jsonb;
  v_row record;
begin
  c := pg_temp.submit(pg_temp.report('88888888-8888-4888-8888-888888888888', '88888888-8888-4888-8888-888888888881', '2026-09-29 14:00Z',
    jsonb_build_object('logs', jsonb_build_array(jsonb_build_object(
      'fileName', 'big.log',
      'content', (select string_agg('2026-09-29 10:00:00.000 - line ' || i || ' ' || repeat('x', 40), E'\n')
                  from generate_series(0, 3999) i))))));
  select count(*) as n, max(length(content)) as m, sum(line_count) as l into v_row
  from diagnostics.report_log_chunks where report_id = (c ->> 'reportId')::uuid;
  assert v_row.n > 1 and v_row.m <= 65536 and v_row.l = 4000, format('chunking: %s', row_to_json(v_row));
end;
$$;

-- Knowledge base annotation; a fixed signature reopens only from the fixed version on.
do $$
declare
  v_sid bigint := (select (result ->> 'primarySignatureId')::bigint from t_results where name = 'a');
begin
  perform diagnostics.annotate_signature(v_sid, 'test',
    p_status => 'fixed',
    p_root_cause => 'DuplicateOutput1 native access violation on SDR x64 outputs',
    p_fixed_in_version => '0.32.0',
    p_fix_commit => '824b9ba6');
  assert exists (select 1 from diagnostics.search('access violation', 5) where signature_id = v_sid),
    'root cause text is searchable';

  perform pg_temp.submit(pg_temp.report('a1111111-1111-4111-8111-111111111111', 'a1111111-1111-4111-8111-111111111112', '2026-09-29 14:00Z'));
  assert (select status from diagnostics.crash_signatures where signature_id = v_sid) = 'fixed',
    'reports from versions before the fix keep the signature fixed';

  perform pg_temp.submit(pg_temp.report('a2222222-2222-4222-8222-222222222222', 'a2222222-2222-4222-8222-222222222223', '2026-09-29 14:00Z',
    '{"app": {"version": "0.32.1", "buildFlavor": "Release"}}'));
  assert (select status from diagnostics.crash_signatures where signature_id = v_sid) = 'investigating',
    'regression reopens a fixed signature';

  assert diagnostics.version_array('v0.32.1-beta') = array[0, 32, 1], 'version_array parses tags';
  assert diagnostics.version_array('unknown') is null, 'version_array rejects non-versions';

  begin
    perform diagnostics.annotate_signature(v_sid, ' ');
    assert false, 'annotate without author must fail';
  exception when raise_exception then
    null;
  end;
end;
$$;

-- Self-service delete needs the right token, cascades, and rolls the mark back
-- so the same logs can be sent again.
do $$
declare
  v_install uuid := '11111111-1111-4111-8111-111111111111';
  v_c uuid := (select (result ->> 'reportId')::uuid from t_results where name = 'c');
  r jsonb;
begin
  assert not diagnostics.delete_report_with_token(v_c, pg_temp.token('cd')), 'wrong token is rejected';
  assert diagnostics.delete_report_with_token(v_c, pg_temp.token('ab')), 'right token deletes';
  assert not exists (select 1 from diagnostics.report_events where report_id = v_c), 'events cascade';
  assert not exists (select 1 from diagnostics.report_keys where report_id = v_c), 'key removed';
  assert (select last_log_at from diagnostics.install_state where install_id = v_install) = '2026-09-29 14:00Z',
    'mark rolled back to the remaining report';

  r := pg_temp.submit(pg_temp.report(v_install::text, '44444444-4444-4444-8444-444444444445', '2026-09-30 10:00Z',
         '{"window": {"kind": "7d", "start": "2026-09-23T10:00:00Z", "end": "2026-09-30T10:00:00Z"}}'));
  assert (r ->> 'duplicate')::boolean = false, 'deleted logs can be sent again';
end;
$$;

-- Rate limit: 10 accepted reports per install per day. Duplicates do not count.
do $$
declare
  i integer;
  v_limited boolean := false;
  v_install text := 'b1111111-1111-4111-8111-111111111111';
begin
  for i in 1 .. 12 loop
    perform pg_temp.submit(pg_temp.report(v_install, format('b2222222-2222-4222-8222-%s', lpad(i::text, 12, '0')), '2026-09-29 14:00Z'));
  end loop;
  assert (select report_count from diagnostics.install_state where install_id = v_install::uuid) = 1, 'duplicates are free';

  for i in 1 .. 12 loop
    begin
      perform pg_temp.submit(pg_temp.report(v_install, format('b3333333-3333-4333-8333-%s', lpad(i::text, 12, '0')),
        timestamptz '2026-09-29 14:00Z' + i * interval '1 minute'));
    exception when others then
      assert sqlerrm = 'diagnostics_rate_limited', sqlerrm;
      v_limited := true;
      exit;
    end;
  end loop;
  assert v_limited, 'install rate limit applies';
end;
$$;

-- Fix queue: claim, release, tick off.
do $$
declare
  v_claim diagnostics.crash_signatures;
  v_other diagnostics.crash_signatures;
begin
  assert (select kind from diagnostics.v_fix_queue order by priority limit 1) = 'abnormal_exit', 'crashes come first';
  v_claim := diagnostics.claim_next_issue('agent-a');
  v_other := diagnostics.claim_next_issue('agent-b');
  assert v_claim.signature_id is not null and v_other.signature_id is not null, 'issues were claimed';
  assert v_claim.signature_id <> v_other.signature_id, 'two agents never get the same issue';
  assert v_claim.claimed_by = 'agent-a' and v_claim.status = 'investigating', 'claim recorded';
  assert diagnostics.release_issue(v_other.signature_id, 'agent-b'), 'release';
  assert (select claimed_by is null from diagnostics.crash_signatures where signature_id = v_other.signature_id), 'released';

  perform diagnostics.mark_fixed(v_claim.signature_id, 'agent-a', '0.33.0', 'abc1234', 'root cause', 'the fix');
  assert not exists (select 1 from diagnostics.v_fix_queue where signature_id = v_claim.signature_id), 'fixed issues leave the queue';
  assert (select status = 'fixed' and fixed_in_version = '0.33.0' and claimed_by is null
          from diagnostics.crash_signatures where signature_id = v_claim.signature_id), 'ticked off';
  begin
    perform diagnostics.mark_fixed(v_other.signature_id, 'agent-b', 'unknown', 'abc1234');
    assert false, 'mark_fixed needs a real version';
  exception when raise_exception then
    null;
  end;
end;
$$;

-- Space reclamation: resolved reports first, then raw logs; the knowledge base stays.
do $$
declare
  r jsonb;
  v_reports integer := (select count(*) from diagnostics.reports);
  v_signatures integer := (select count(*) from diagnostics.crash_signatures);
  v_open integer;
begin
  -- Below the watermark nothing happens.
  r := diagnostics.reclaim_space('test');
  assert (r ->> 'reportsDeleted')::int = 0 and (r ->> 'reportsLogsTrimmed')::int = 0, format('no-op below watermark: %s', r);

  update diagnostics.settings set reclaim_high_bytes = 2, reclaim_low_bytes = 1,
    fixed_retention = interval '0', raw_log_min_age = interval '0';
  -- Error lines stay open. Reports with no open issue (none left after
  -- de-duplication) go first; the rest only lose their raw logs.
  update diagnostics.crash_signatures set status = 'fixed', fixed_in_version = '9.0.0' where kind <> 'error_line';
  select count(*) into v_open from diagnostics.reports r
  where exists (select 1 from diagnostics.report_events e join diagnostics.crash_signatures s on s.signature_id = e.signature_id
                where e.report_id = r.report_id and s.status not in ('fixed', 'wont_fix', 'noise'));
  assert v_open > 0 and v_open < v_reports, format('fixture has open and resolved reports (%s of %s)', v_open, v_reports);
  r := diagnostics.reclaim_space('test');
  assert (r ->> 'reportsDeleted')::int = v_reports - v_open, format('only resolved reports deleted: %s', r);
  assert (r ->> 'reportsLogsTrimmed')::int = v_open, format('raw logs trimmed from the rest: %s', r);
  assert not exists (select 1 from diagnostics.report_log_chunks), 'log text removed';
  assert (select count(*) from diagnostics.reports) = v_open, 'reports with open issues kept';
  assert exists (select 1 from diagnostics.report_events), 'events kept';
  v_reports := v_open;

  -- Once everything is fixed, whole reports go.
  update diagnostics.crash_signatures set status = 'fixed', fixed_in_version = '9.0.0';
  r := diagnostics.reclaim_space('test');
  assert (r ->> 'reportsDeleted')::int = v_reports, format('resolved reports deleted: %s', r);
  assert not exists (select 1 from diagnostics.reports), 'no reports left';
  assert (select count(*) from diagnostics.crash_signatures) = v_signatures, 'knowledge base kept';
  assert (select count(*) from diagnostics.purge_log where trigger = 'test') = 3, 'every run logged';

  -- Empty partitions older than last month are dropped.
  perform diagnostics.ensure_month_partitions(clock_timestamp() - interval '3 months');
  r := diagnostics.run_maintenance('test');
  assert (r ->> 'partitionsDropped')::int >= 1, format('old empty partitions dropped: %s', r);
  assert to_regclass(format('diagnostics.%I', 'reports_' || to_char((clock_timestamp() - interval '3 months') at time zone 'UTC', '"y"YYYY"m"MM'))) is null,
    'partition gone';
  assert (select estimated_bytes from diagnostics.v_storage) = 0, 'storage view';

  update diagnostics.settings set reclaim_high_bytes = 300 * 1024 * 1024, reclaim_low_bytes = 240 * 1024 * 1024,
    fixed_retention = interval '14 days', raw_log_min_age = interval '7 days';
end;
$$;

-- Ingest switch, partitions on demand, owner purge.
do $$
begin
  update diagnostics.settings set allow_ingest = false;
  begin
    perform pg_temp.submit(pg_temp.report('c1111111-1111-4111-8111-111111111111', 'c1111111-1111-4111-8111-111111111112', '2026-09-29 14:00Z'));
    assert false, 'disabled ingest must fail';
  exception when others then
    assert sqlerrm = 'diagnostics_ingest_disabled', sqlerrm;
  end;

  perform diagnostics.ensure_month_partitions(clock_timestamp() + interval '5 months');
  perform diagnostics.ensure_month_partitions(clock_timestamp() + interval '5 months');

  perform diagnostics.purge_install('11111111-1111-4111-8111-111111111111');
  assert not exists (select 1 from diagnostics.install_state where install_id = '11111111-1111-4111-8111-111111111111'),
    'purge_install clears the mark';
  assert exists (select 1 from diagnostics.crash_signatures), 'knowledge base survives report purges';
end;
$$;

select 'diagnostics tests passed' as result;
