-- 1. Log windows are limited to the past 24 hours or the past week.
-- 2. Duplicate and overlapping reports are resolved by timestamp: each install
--    has a high-water mark (the newest log timestamp already stored). A report
--    with nothing newer is a duplicate and returns the earlier report; an
--    overlapping report keeps only what is newer, so the same crash or log
--    line is never stored or counted twice.

alter table diagnostics.reports drop constraint if exists reports_window_kind_check;
alter table diagnostics.reports add constraint reports_window_kind_check check (window_kind in ('24h', '7d'));

-- The high-water mark this report was trimmed against (null for the first report).
alter table diagnostics.reports add column if not exists deduplicated_before timestamptz;

create table if not exists diagnostics.install_state (
  install_id uuid primary key,
  last_log_at timestamptz not null,
  last_report_id uuid not null,
  last_received_at timestamptz not null,
  report_count integer not null default 0
);

grant select on diagnostics.install_state to diagnostics_reader;

insert into diagnostics.install_state (install_id, last_log_at, last_report_id, last_received_at, report_count)
select distinct on (install_id)
  install_id, max(window_end) over w, report_id, received_at, count(*) over w
from diagnostics.reports
window w as (partition by install_id)
order by install_id, window_end desc
on conflict (install_id) do nothing;

-- Recomputes an install's mark from its remaining reports (after deletes).
create or replace function diagnostics.refresh_install_state(p_install_id uuid)
returns void
language plpgsql
set search_path = ''
as $$
declare
  v_last record;
begin
  select report_id, received_at, window_end, count(*) over () as n
  into v_last
  from diagnostics.reports
  where install_id = p_install_id
  order by window_end desc
  limit 1;

  if not found then
    delete from diagnostics.install_state where install_id = p_install_id;
  else
    update diagnostics.install_state set
      last_log_at = v_last.window_end,
      last_report_id = v_last.report_id,
      last_received_at = v_last.received_at,
      report_count = v_last.n
    where install_id = p_install_id;
  end if;
end;
$$;

-- Same chunking as before, but drops entries at or before p_after. A
-- continuation line (stack frame, wrapped message) follows its entry.
drop function if exists diagnostics.insert_log_chunks(uuid, timestamptz, text, text);

create or replace function diagnostics.insert_log_chunks(
  p_report_id uuid,
  p_received_at timestamptz,
  p_file_name text,
  p_content text,
  p_after timestamptz)
returns integer
language plpgsql
set search_path = ''
as $$
declare
  v_lines text[] := string_to_array(replace(p_content, E'\r\n', E'\n'), E'\n');
  v_total integer := coalesce(array_length(v_lines, 1), 0);
  v_chunk text := '';
  v_chunk_first integer := 1;
  v_chunk_count integer := 0;
  v_index integer := 0;
  v_kept integer := 0;
  v_line text;
  v_i integer;
  v_started timestamptz;
  v_ended timestamptz;
  v_ts timestamptz;
  v_keep boolean := p_after is null;
begin
  for v_i in 1 .. v_total loop
    v_line := left(v_lines[v_i], 8192);

    v_ts := null;
    if v_line ~ '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} - ' then
      begin
        v_ts := substr(v_line, 1, 23)::timestamp at time zone 'UTC';
      exception when others then
        v_ts := null;
      end;
      if v_ts is not null then
        v_keep := p_after is null or v_ts > p_after;
      end if;
    end if;

    continue when not v_keep;

    if v_chunk_count > 0 and length(v_chunk) + length(v_line) + 1 > 65536 then
      insert into diagnostics.report_log_chunks
        (report_id, received_at, file_name, chunk_index, first_line, line_count, started_at, ended_at, content)
      values (p_report_id, p_received_at, p_file_name, v_index, v_chunk_first, v_chunk_count, v_started, v_ended, v_chunk);
      v_index := v_index + 1;
      v_chunk := '';
      v_chunk_count := 0;
      v_started := null;
      v_ended := null;
    end if;

    if v_chunk_count = 0 then
      v_chunk_first := v_i;
    end if;
    if v_ts is not null then
      v_started := coalesce(v_started, v_ts);
      v_ended := v_ts;
    end if;

    v_chunk := case when v_chunk_count = 0 then v_line else v_chunk || E'\n' || v_line end;
    v_chunk_count := v_chunk_count + 1;
    v_kept := v_kept + 1;
  end loop;

  if v_chunk_count > 0 then
    insert into diagnostics.report_log_chunks
      (report_id, received_at, file_name, chunk_index, first_line, line_count, started_at, ended_at, content)
    values (p_report_id, p_received_at, p_file_name, v_index, v_chunk_first, v_chunk_count, v_started, v_ended, v_chunk);
  end if;

  return v_kept;
end;
$$;

-- Stores one validated report, trimmed against the install's high-water mark.
-- Returns {reportId, receivedAt, duplicate, reason, primarySignatureId,
-- deduplicatedBefore, lastLogAt}. reason: null | 'same_report' | 'no_new_entries'.
-- Raises 'diagnostics_ingest_disabled' or 'diagnostics_rate_limited'.
create or replace function diagnostics.submit_report(
  p_report jsonb,
  p_delete_token_hash bytea,
  p_network_key bytea)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_settings diagnostics.settings;
  v_install_id uuid := (p_report ->> 'installId')::uuid;
  v_client_report_id uuid := (p_report ->> 'clientReportId')::uuid;
  v_window_end timestamptz := (p_report #>> '{window,end}')::timestamptz;
  v_existing diagnostics.report_keys;
  v_state diagnostics.install_state;
  v_hw timestamptz;
  v_now timestamptz := clock_timestamp();
  v_day timestamptz := date_trunc('day', v_now at time zone 'UTC') at time zone 'UTC';
  v_report_id uuid := gen_random_uuid();
  v_app_version text := p_report #>> '{app,version}';
  v_os_family text := p_report #>> '{os,family}';
  v_process_arch text := coalesce(p_report #>> '{os,processArchitecture}', 'unknown');
  v_count integer;
  v_sessions integer := 0;
  v_event jsonb;
  v_ordinal integer := 0;
  v_signature_id bigint;
  v_primary_signature_id bigint;
  v_primary_rank integer := 99;
  v_rank integer;
  v_kind text;
  v_frames text[];
  v_template text;
  v_occurrences integer;
  v_log jsonb;
  v_file_count integer := 0;
  v_line_count integer := 0;
  v_kept integer;
  v_abnormal integer := 0;
begin
  select * into v_settings from diagnostics.settings where singleton;
  if not coalesce(v_settings.allow_ingest, false) then
    raise exception using errcode = 'P0001', message = 'diagnostics_ingest_disabled';
  end if;

  -- Exact resend (same client report id): return the stored report.
  select * into v_existing from diagnostics.report_keys
  where install_id = v_install_id and client_report_id = v_client_report_id;
  if found then
    return jsonb_build_object(
      'reportId', v_existing.report_id,
      'receivedAt', v_existing.received_at,
      'duplicate', true,
      'reason', 'same_report',
      'primarySignatureId', null,
      'lastLogAt', (select last_log_at from diagnostics.install_state where install_id = v_install_id));
  end if;

  -- Serialise submits per install so two concurrent sends cannot both pass
  -- the high-water check.
  perform pg_advisory_xact_lock(hashtext('diagnostics.install:' || v_install_id::text));
  select * into v_state from diagnostics.install_state where install_id = v_install_id;
  v_hw := v_state.last_log_at;

  -- Nothing newer than what this install already sent.
  if v_hw is not null and v_window_end is not null and v_window_end <= v_hw then
    return jsonb_build_object(
      'reportId', v_state.last_report_id,
      'receivedAt', v_state.last_received_at,
      'duplicate', true,
      'reason', 'no_new_entries',
      'primarySignatureId', null,
      'lastLogAt', v_hw);
  end if;

  insert into diagnostics.ingest_limits as l (bucket_key, window_start, request_count)
  values (public.digest('install:' || v_install_id::text, 'sha256'), v_day, 1)
  on conflict (bucket_key, window_start) do update set request_count = l.request_count + 1
  returning request_count into v_count;
  if v_count > v_settings.reports_per_install_per_day then
    raise exception using errcode = 'P0001', message = 'diagnostics_rate_limited';
  end if;

  if p_network_key is not null then
    insert into diagnostics.ingest_limits as l (bucket_key, window_start, request_count)
    values (p_network_key, v_day, 1)
    on conflict (bucket_key, window_start) do update set request_count = l.request_count + 1
    returning request_count into v_count;
    if v_count > v_settings.reports_per_network_per_day then
      raise exception using errcode = 'P0001', message = 'diagnostics_rate_limited';
    end if;
  end if;

  if random() < 0.01 then
    delete from diagnostics.ingest_limits where window_start < v_day - interval '2 days';
  end if;

  if to_regclass(format('diagnostics.%I', 'reports_' || to_char(v_now at time zone 'UTC', '"y"YYYY"m"MM'))) is null then
    perform diagnostics.ensure_month_partitions(v_now);
  end if;

  insert into diagnostics.report_keys (install_id, client_report_id, report_id, received_at)
  values (v_install_id, v_client_report_id, v_report_id, v_now);

  insert into diagnostics.reports (
    report_id, received_at, install_id, client_report_id, schema_version, redaction_version, trigger,
    app_version, build_flavor, dotnet_version,
    os_family, os_description, os_version, os_arch, process_arch, elevated, sandbox, session_type, desktop_env, ui_language,
    capture_backend, recording_backend, ffmpeg_version, ffmpeg_source, ffmpeg_hw_encoders,
    cpu_model, cpu_logical_cores, ram_gb_bucket, gpus, displays,
    window_kind, window_start, window_end, deduplicated_before, user_comment, delete_token_hash, extra)
  values (
    v_report_id, v_now, v_install_id, v_client_report_id,
    (p_report ->> 'schemaVersion')::smallint,
    (p_report ->> 'redactionVersion')::smallint,
    p_report ->> 'trigger',
    v_app_version,
    p_report #>> '{app,buildFlavor}',
    p_report #>> '{app,dotnetVersion}',
    v_os_family,
    p_report #>> '{os,description}',
    p_report #>> '{os,version}',
    p_report #>> '{os,architecture}',
    v_process_arch,
    (p_report #>> '{os,elevated}')::boolean,
    p_report #>> '{os,sandbox}',
    p_report #>> '{os,sessionType}',
    p_report #>> '{os,desktopEnvironment}',
    p_report #>> '{os,uiLanguage}',
    p_report #>> '{capture,captureBackend}',
    p_report #>> '{capture,recordingBackend}',
    p_report #>> '{capture,ffmpeg,version}',
    p_report #>> '{capture,ffmpeg,source}',
    coalesce(array(select jsonb_array_elements_text(p_report #> '{capture,ffmpeg,hwEncoders}')), '{}'),
    p_report #>> '{hardware,cpuModel}',
    (p_report #>> '{hardware,logicalCores}')::smallint,
    (p_report #>> '{hardware,ramGbBucket}')::smallint,
    coalesce(p_report #> '{hardware,gpus}', '[]'),
    coalesce(p_report -> 'displays', '[]'),
    p_report #>> '{window,kind}',
    greatest((p_report #>> '{window,start}')::timestamptz, v_hw),
    v_window_end,
    v_hw,
    nullif(p_report ->> 'comment', ''),
    p_delete_token_hash,
    coalesce(p_report -> 'extra', '{}'));

  -- Sessions that ended before the mark were already reported.
  insert into diagnostics.report_sessions (
    report_id, received_at, ordinal, started_at, ended_at, app_version, exit_kind, last_component, last_message_template)
  select v_report_id, v_now, (row_number() over (order by s.ordinality) - 1)::smallint,
    (s.value ->> 'startedAt')::timestamptz,
    (s.value ->> 'endedAt')::timestamptz,
    s.value ->> 'appVersion',
    s.value ->> 'exitKind',
    s.value ->> 'lastComponent',
    s.value ->> 'lastMessageTemplate'
  from jsonb_array_elements(coalesce(p_report -> 'sessions', '[]')) with ordinality as s(value, ordinality)
  where v_hw is null or coalesce((s.value ->> 'endedAt')::timestamptz, 'infinity') > v_hw;
  get diagnostics v_sessions = row_count;

  select count(*) into v_abnormal
  from diagnostics.report_sessions
  where report_id = v_report_id and received_at = v_now and exit_kind = 'abnormal';

  for v_event in select value from jsonb_array_elements(coalesce(p_report -> 'events', '[]')) loop
    -- Every occurrence of this event was already reported.
    continue when v_hw is not null and (v_event ->> 'lastAt') is not null
      and (v_event ->> 'lastAt')::timestamptz <= v_hw;

    v_kind := v_event ->> 'kind';
    v_frames := coalesce(array(select jsonb_array_elements_text(v_event -> 'topFrames')), '{}');
    v_template := diagnostics.normalize_template(v_event ->> 'messageTemplate');
    v_occurrences := greatest(coalesce((v_event ->> 'occurrences')::integer, 1), 1);

    insert into diagnostics.crash_signatures as c (
      fingerprint, kind, title, exception_type, component, message_template, top_frames,
      first_seen_at, last_seen_at, first_version, last_version, report_count, occurrence_count)
    values (
      diagnostics.fingerprint(v_kind, v_event ->> 'exceptionType', v_event ->> 'component', v_template, v_frames),
      v_kind,
      left(concat_ws(': ',
        coalesce(v_event ->> 'exceptionType', nullif(v_event ->> 'component', ''),
          case v_kind when 'abnormal_exit' then 'Abnormal exit' else 'Error' end),
        v_template), 300),
      v_event ->> 'exceptionType',
      v_event ->> 'component',
      v_template,
      v_frames[1:10],
      v_now, v_now, v_app_version, v_app_version, 1, v_occurrences)
    on conflict (fingerprint) do update set
      last_seen_at = excluded.last_seen_at,
      last_version = excluded.last_version,
      report_count = c.report_count + 1,
      occurrence_count = c.occurrence_count + excluded.occurrence_count,
      status = case when diagnostics.is_regression(c.status, c.fixed_in_version, excluded.last_version)
                    then 'investigating' else c.status end
    returning signature_id into v_signature_id;

    insert into diagnostics.report_events (
      report_id, received_at, ordinal, kind, component, exception_type, message_template, sample_message,
      top_frames, first_at, last_at, occurrences, signature_id)
    values (
      v_report_id, v_now, v_ordinal, v_kind,
      v_event ->> 'component',
      v_event ->> 'exceptionType',
      v_template,
      left(v_event ->> 'sampleMessage', 4000),
      v_frames[1:30],
      (v_event ->> 'firstAt')::timestamptz,
      (v_event ->> 'lastAt')::timestamptz,
      v_occurrences,
      v_signature_id);

    insert into diagnostics.signature_daily as d (signature_id, day, app_version, os_family, process_arch, reports, occurrences)
    values (v_signature_id, v_day::date, v_app_version, v_os_family, v_process_arch, 1, v_occurrences)
    on conflict (signature_id, day, app_version, os_family, process_arch) do update set
      reports = d.reports + 1,
      occurrences = d.occurrences + excluded.occurrences;

    v_rank := case v_kind when 'abnormal_exit' then 0 when 'exception' then 1 else 2 end;
    if v_rank < v_primary_rank then
      v_primary_rank := v_rank;
      v_primary_signature_id := v_signature_id;
    end if;
    v_ordinal := v_ordinal + 1;
  end loop;

  for v_log in select value from jsonb_array_elements(coalesce(p_report -> 'logs', '[]')) loop
    v_kept := diagnostics.insert_log_chunks(v_report_id, v_now, v_log ->> 'fileName', v_log ->> 'content', v_hw);
    if v_kept > 0 then
      v_file_count := v_file_count + 1;
      v_line_count := v_line_count + v_kept;
    end if;
  end loop;

  update diagnostics.reports set
    file_count = v_file_count,
    line_count = v_line_count,
    log_bytes = coalesce((select sum(octet_length(content)) from diagnostics.report_log_chunks
                          where report_id = v_report_id and received_at = v_now), 0),
    session_count = v_sessions,
    abnormal_exit_count = v_abnormal,
    event_count = v_ordinal,
    primary_signature_id = v_primary_signature_id
  where report_id = v_report_id and received_at = v_now;

  insert into diagnostics.install_state as st (install_id, last_log_at, last_report_id, last_received_at, report_count)
  values (v_install_id, v_window_end, v_report_id, v_now, 1)
  on conflict (install_id) do update set
    last_log_at = greatest(st.last_log_at, excluded.last_log_at),
    last_report_id = excluded.last_report_id,
    last_received_at = excluded.last_received_at,
    report_count = st.report_count + 1;

  return jsonb_build_object(
    'reportId', v_report_id,
    'receivedAt', v_now,
    'duplicate', false,
    'reason', null,
    'primarySignatureId', v_primary_signature_id,
    'deduplicatedBefore', v_hw,
    'lastLogAt', greatest(v_hw, v_window_end));
end;
$$;

-- What an install has already sent, so the app can say "nothing new" without
-- uploading. Needs the install id, which only that install knows.
create or replace function diagnostics.install_status(p_install_id uuid)
returns jsonb
language sql
stable
security definer
set search_path = ''
as $$
  select coalesce(
    (select jsonb_build_object(
       'lastLogAt', last_log_at,
       'lastReportId', last_report_id,
       'lastReceivedAt', last_received_at,
       'reportCount', report_count)
     from diagnostics.install_state where install_id = p_install_id),
    jsonb_build_object('lastLogAt', null, 'lastReportId', null, 'lastReceivedAt', null, 'reportCount', 0))
$$;

-- Deleting reports rolls the high-water mark back so the logs can be sent again.
create or replace function diagnostics.delete_report_with_token(p_report_id uuid, p_delete_token_hash bytea)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_key diagnostics.report_keys;
begin
  select * into v_key from diagnostics.report_keys where report_id = p_report_id;
  if not found then
    return false;
  end if;

  delete from diagnostics.reports
  where report_id = p_report_id
    and received_at = v_key.received_at
    and delete_token_hash = p_delete_token_hash;
  if not found then
    return false;
  end if;

  delete from diagnostics.report_keys where report_id = p_report_id;
  perform diagnostics.refresh_install_state(v_key.install_id);
  return true;
end;
$$;

create or replace function diagnostics.purge_report(p_report_id uuid)
returns boolean
language plpgsql
set search_path = ''
as $$
declare
  v_key diagnostics.report_keys;
begin
  select * into v_key from diagnostics.report_keys where report_id = p_report_id;
  if not found then
    return false;
  end if;
  delete from diagnostics.reports where report_id = p_report_id and received_at = v_key.received_at;
  delete from diagnostics.report_keys where report_id = p_report_id;
  perform diagnostics.refresh_install_state(v_key.install_id);
  return true;
end;
$$;

create or replace function diagnostics.purge_install(p_install_id uuid)
returns integer
language plpgsql
set search_path = ''
as $$
declare
  v_count integer;
begin
  delete from diagnostics.reports where install_id = p_install_id;
  get diagnostics v_count = row_count;
  delete from diagnostics.report_keys where install_id = p_install_id;
  delete from diagnostics.install_state where install_id = p_install_id;
  return v_count;
end;
$$;

revoke all on function diagnostics.insert_log_chunks(uuid, timestamptz, text, text, timestamptz) from public;
revoke all on function diagnostics.refresh_install_state(uuid) from public;
revoke all on function diagnostics.install_status(uuid) from public;
grant execute on function diagnostics.install_status(uuid) to diagnostics_ingest;

-- The ingest Function connects with the branch owner's DATABASE_URL and runs
-- each request under `set local role diagnostics_ingest`, so it can only call
-- submit_report, install_status and delete_report_with_token.
grant diagnostics_ingest to current_user with inherit false, set true;
