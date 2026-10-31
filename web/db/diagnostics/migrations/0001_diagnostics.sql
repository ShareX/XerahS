-- XerahS crash and diagnostics knowledge base.
--
-- Anonymous desktop reports (system data, monitor layout, hardware, FFmpeg,
-- scrubbed logs) are grouped into crash signatures. Signatures double as the
-- knowledge base: agents and maintainers read them, search logs, and record
-- root causes and fixes. Retention is permanent; only the purge functions
-- remove data.
--
-- Plain PostgreSQL 16+ (Neon). Partitioned by month on received_at so the
-- hot tables stay small per partition; partitions are created on demand.

create extension if not exists pgcrypto;

create schema if not exists diagnostics;
revoke all on schema diagnostics from public;

-- ---------------------------------------------------------------------------
-- Knowledge base: one row per distinct failure
-- ---------------------------------------------------------------------------

create table if not exists diagnostics.crash_signatures (
  signature_id bigint generated always as identity primary key,
  fingerprint bytea not null unique,
  kind text not null,
  title text not null,
  exception_type text,
  component text,
  message_template text not null,
  top_frames text[] not null default '{}',
  first_seen_at timestamptz not null default clock_timestamp(),
  last_seen_at timestamptz not null default clock_timestamp(),
  first_version text not null,
  last_version text not null,
  report_count bigint not null default 0,
  occurrence_count bigint not null default 0,
  -- Curated knowledge. Written by maintainers and agents through annotate_signature().
  status text not null default 'new',
  root_cause text,
  fix_summary text,
  fixed_in_version text,
  fix_commit text,
  issue_url text,
  notes text,
  tags text[] not null default '{}',
  kb_updated_at timestamptz,
  kb_updated_by text,
  search tsvector generated always as (
    setweight(to_tsvector('simple', coalesce(title, '') || ' ' || coalesce(exception_type, '') || ' ' || coalesce(component, '')), 'A') ||
    setweight(to_tsvector('simple', message_template), 'B') ||
    setweight(to_tsvector('english', coalesce(root_cause, '') || ' ' || coalesce(fix_summary, '') || ' ' || coalesce(notes, '')), 'C')
  ) stored,
  constraint crash_signatures_kind_check check (kind in ('exception', 'abnormal_exit', 'error_line')),
  constraint crash_signatures_status_check check (status in ('new', 'investigating', 'known', 'fixed', 'wont_fix', 'noise')),
  constraint crash_signatures_title_check check (length(title) between 1 and 300),
  constraint crash_signatures_template_check check (length(message_template) <= 2000),
  constraint crash_signatures_fingerprint_check check (length(fingerprint) = 32)
);

create index if not exists crash_signatures_last_seen_idx on diagnostics.crash_signatures (last_seen_at desc);
create index if not exists crash_signatures_status_idx on diagnostics.crash_signatures (status, last_seen_at desc);
create index if not exists crash_signatures_search_idx on diagnostics.crash_signatures using gin (search);

-- ---------------------------------------------------------------------------
-- Reports (partitioned)
-- ---------------------------------------------------------------------------

create table if not exists diagnostics.reports (
  report_id uuid not null,
  received_at timestamptz not null default clock_timestamp(),
  install_id uuid not null,
  client_report_id uuid not null,
  schema_version smallint not null,
  redaction_version smallint not null,
  trigger text not null,
  -- app
  app_version text not null,
  build_flavor text not null,
  dotnet_version text,
  -- operating system
  os_family text not null,
  os_description text,
  os_version text,
  os_arch text,
  process_arch text,
  elevated boolean,
  sandbox text,
  session_type text,
  desktop_env text,
  ui_language text,
  -- capture and recording stack
  capture_backend text,
  recording_backend text,
  ffmpeg_version text,
  ffmpeg_source text,
  ffmpeg_hw_encoders text[] not null default '{}',
  -- hardware (coarse on purpose)
  cpu_model text,
  cpu_logical_cores smallint,
  ram_gb_bucket smallint,
  gpus jsonb not null default '[]',
  displays jsonb not null default '[]',
  display_count smallint generated always as (jsonb_array_length(displays)) stored,
  -- log window and summary
  window_kind text not null,
  window_start timestamptz not null,
  window_end timestamptz not null,
  file_count smallint not null default 0,
  line_count integer not null default 0,
  log_bytes integer not null default 0,
  session_count smallint not null default 0,
  abnormal_exit_count smallint not null default 0,
  event_count integer not null default 0,
  primary_signature_id bigint references diagnostics.crash_signatures (signature_id) on delete set null,
  user_comment text,
  delete_token_hash bytea not null,
  -- Forward-compatible bag for client fields not yet promoted to columns.
  extra jsonb not null default '{}',
  primary key (report_id, received_at),
  constraint reports_trigger_check check (trigger in ('manual', 'after_crash')),
  constraint reports_os_family_check check (os_family in ('windows', 'linux', 'macos', 'other')),
  constraint reports_window_kind_check check (window_kind in ('24h', '7d', '30d')),
  constraint reports_window_order_check check (window_start <= window_end),
  constraint reports_app_version_check check (length(app_version) between 1 and 64),
  constraint reports_comment_check check (user_comment is null or length(user_comment) <= 2000),
  constraint reports_gpus_check check (jsonb_typeof(gpus) = 'array' and jsonb_array_length(gpus) <= 16),
  constraint reports_displays_check check (jsonb_typeof(displays) = 'array' and jsonb_array_length(displays) <= 32),
  constraint reports_extra_check check (jsonb_typeof(extra) = 'object'),
  constraint reports_delete_token_check check (length(delete_token_hash) = 32)
) partition by range (received_at);

create index if not exists reports_received_idx on diagnostics.reports (received_at desc);
create index if not exists reports_signature_idx on diagnostics.reports (primary_signature_id, received_at desc);
create index if not exists reports_version_idx on diagnostics.reports (app_version, received_at desc);
create index if not exists reports_install_idx on diagnostics.reports (install_id, received_at desc);
create index if not exists reports_displays_idx on diagnostics.reports using gin (displays jsonb_path_ops);

-- Global lookup and idempotency. Partitioned tables cannot carry unique keys
-- that omit the partition key, so these live in a small side table.
create table if not exists diagnostics.report_keys (
  install_id uuid not null,
  client_report_id uuid not null,
  report_id uuid not null unique,
  received_at timestamptz not null,
  primary key (install_id, client_report_id)
);

create table if not exists diagnostics.report_sessions (
  report_id uuid not null,
  received_at timestamptz not null,
  ordinal smallint not null,
  started_at timestamptz not null,
  ended_at timestamptz,
  app_version text,
  exit_kind text not null,
  last_component text,
  last_message_template text,
  primary key (report_id, received_at, ordinal),
  foreign key (report_id, received_at) references diagnostics.reports (report_id, received_at) on delete cascade,
  constraint report_sessions_exit_kind_check check (exit_kind in ('clean', 'abnormal', 'running', 'unknown'))
) partition by range (received_at);

create table if not exists diagnostics.report_events (
  report_id uuid not null,
  received_at timestamptz not null,
  ordinal integer not null,
  kind text not null,
  component text,
  exception_type text,
  message_template text not null,
  sample_message text,
  top_frames text[] not null default '{}',
  first_at timestamptz,
  last_at timestamptz,
  occurrences integer not null default 1,
  signature_id bigint references diagnostics.crash_signatures (signature_id) on delete set null,
  search tsvector generated always as (
    to_tsvector('simple',
      coalesce(component, '') || ' ' || coalesce(exception_type, '') || ' ' ||
      message_template || ' ' || coalesce(sample_message, ''))
  ) stored,
  primary key (report_id, received_at, ordinal),
  foreign key (report_id, received_at) references diagnostics.reports (report_id, received_at) on delete cascade,
  constraint report_events_kind_check check (kind in ('exception', 'abnormal_exit', 'error_line')),
  constraint report_events_occurrences_check check (occurrences >= 1)
) partition by range (received_at);

create index if not exists report_events_signature_idx on diagnostics.report_events (signature_id, received_at desc);
create index if not exists report_events_search_idx on diagnostics.report_events using gin (search);

-- Scrubbed log text, split at line boundaries into <= 64 KB chunks so every
-- chunk fits in a tsvector and a whole-corpus search stays index-backed.
create table if not exists diagnostics.report_log_chunks (
  report_id uuid not null,
  received_at timestamptz not null,
  file_name text not null,
  chunk_index integer not null,
  first_line integer not null,
  line_count integer not null,
  started_at timestamptz,
  ended_at timestamptz,
  content text not null,
  search tsvector generated always as (to_tsvector('simple', content)) stored,
  primary key (report_id, received_at, file_name, chunk_index),
  foreign key (report_id, received_at) references diagnostics.reports (report_id, received_at) on delete cascade,
  constraint report_log_chunks_file_name_check check (
    length(file_name) between 1 and 128 and file_name !~ '[\\/[:cntrl:]]'
  ),
  constraint report_log_chunks_content_check check (length(content) <= 65536)
) partition by range (received_at);

create index if not exists report_log_chunks_search_idx on diagnostics.report_log_chunks using gin (search);

-- ---------------------------------------------------------------------------
-- Rollups and abuse limits
-- ---------------------------------------------------------------------------

create table if not exists diagnostics.signature_daily (
  signature_id bigint not null references diagnostics.crash_signatures (signature_id) on delete cascade,
  day date not null,
  app_version text not null,
  os_family text not null,
  process_arch text not null,
  reports integer not null default 0,
  occurrences bigint not null default 0,
  primary key (signature_id, day, app_version, os_family, process_arch)
);

create index if not exists signature_daily_day_idx on diagnostics.signature_daily (day desc);

create table if not exists diagnostics.ingest_limits (
  bucket_key bytea not null,
  window_start timestamptz not null,
  request_count integer not null default 0,
  primary key (bucket_key, window_start)
);

create table if not exists diagnostics.settings (
  singleton boolean primary key default true check (singleton),
  allow_ingest boolean not null default false,
  reports_per_install_per_day integer not null default 10 check (reports_per_install_per_day > 0),
  reports_per_network_per_day integer not null default 50 check (reports_per_network_per_day > 0),
  updated_at timestamptz not null default clock_timestamp()
);

insert into diagnostics.settings (singleton) values (true) on conflict (singleton) do nothing;

-- ---------------------------------------------------------------------------
-- Partition management
-- ---------------------------------------------------------------------------

create or replace function diagnostics.ensure_month_partitions(p_at timestamptz)
returns void
language plpgsql
set search_path = ''
as $$
declare
  v_start timestamptz := date_trunc('month', p_at at time zone 'UTC') at time zone 'UTC';
  v_end timestamptz := v_start + interval '1 month';
  v_suffix text := to_char(v_start at time zone 'UTC', '"y"YYYY"m"MM');
  v_parent text;
begin
  foreach v_parent in array array['reports', 'report_sessions', 'report_events', 'report_log_chunks'] loop
    if to_regclass(format('diagnostics.%I', v_parent || '_' || v_suffix)) is null then
      begin
        execute format(
          'create table diagnostics.%I partition of diagnostics.%I for values from (%L) to (%L)',
          v_parent || '_' || v_suffix, v_parent, v_start, v_end);
      exception when duplicate_table or unique_violation then
        null; -- A concurrent submit created it first.
      end;
    end if;
  end loop;
end;
$$;

select diagnostics.ensure_month_partitions(clock_timestamp());
select diagnostics.ensure_month_partitions(clock_timestamp() + interval '1 month');

-- ---------------------------------------------------------------------------
-- Fingerprinting
-- ---------------------------------------------------------------------------

-- Collapses volatile tokens so the same failure groups across machines and runs.
-- Only standalone numbers are replaced: identifiers such as DuplicateOutput1 or
-- Rotate270 and HRESULT failure codes (0x8xxxxxxx) carry meaning and are kept.
create or replace function diagnostics.normalize_template(p_text text)
returns text
language sql
immutable
parallel safe
set search_path = ''
as $$
  select left(
    regexp_replace(
      regexp_replace(
        regexp_replace(
          regexp_replace(coalesce(p_text, ''),
            '[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}', '<guid>', 'g'),
          '\m0x(?![89aAbBcCdDeEfF][0-9a-fA-F]{7}\M)[0-9a-fA-F]+\M', '<hex>', 'g'),
        '\m[0-9]+(\.[0-9]+)*\M', '#', 'g'),
      '\s+', ' ', 'g'),
    2000)
$$;

create or replace function diagnostics.fingerprint(
  p_kind text,
  p_exception_type text,
  p_component text,
  p_template text,
  p_frames text[])
returns bytea
language sql
immutable
parallel safe
set search_path = ''
as $$
  select public.digest(
    concat_ws(E'\x1f',
      p_kind,
      coalesce(p_exception_type, ''),
      -- Exceptions group by type and stack; the message varies too much.
      case when p_kind = 'exception' and cardinality(p_frames) > 0 then '' else coalesce(p_component, '') end,
      case when p_kind = 'exception' and cardinality(p_frames) > 0 then '' else diagnostics.normalize_template(p_template) end,
      array_to_string(coalesce(p_frames[1:5], '{}'), E'\x1e')),
    'sha256')
$$;

-- ---------------------------------------------------------------------------
-- Ingest
-- ---------------------------------------------------------------------------

-- Splits a log into <= 64 KB chunks at line boundaries and records the
-- timestamp range of each chunk (lines start with "yyyy-MM-dd HH:mm:ss.fff - ").
create or replace function diagnostics.insert_log_chunks(
  p_report_id uuid,
  p_received_at timestamptz,
  p_file_name text,
  p_content text)
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
  v_line text;
  v_i integer;
  v_started timestamptz;
  v_ended timestamptz;
  v_ts timestamptz;
begin
  for v_i in 1 .. v_total loop
    v_line := left(v_lines[v_i], 8192);
    if v_chunk_count > 0 and length(v_chunk) + length(v_line) + 1 > 65536 then
      insert into diagnostics.report_log_chunks
        (report_id, received_at, file_name, chunk_index, first_line, line_count, started_at, ended_at, content)
      values (p_report_id, p_received_at, p_file_name, v_index, v_chunk_first, v_chunk_count, v_started, v_ended, v_chunk);
      v_index := v_index + 1;
      v_chunk := '';
      v_chunk_first := v_i;
      v_chunk_count := 0;
      v_started := null;
      v_ended := null;
    end if;

    if v_line ~ '^\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} - ' then
      begin
        v_ts := substr(v_line, 1, 23)::timestamp at time zone 'UTC';
        v_started := coalesce(v_started, v_ts);
        v_ended := v_ts;
      exception when others then
        null;
      end;
    end if;

    v_chunk := case when v_chunk_count = 0 then v_line else v_chunk || E'\n' || v_line end;
    v_chunk_count := v_chunk_count + 1;
  end loop;

  if v_chunk_count > 0 then
    insert into diagnostics.report_log_chunks
      (report_id, received_at, file_name, chunk_index, first_line, line_count, started_at, ended_at, content)
    values (p_report_id, p_received_at, p_file_name, v_index, v_chunk_first, v_chunk_count, v_started, v_ended, v_chunk);
    v_index := v_index + 1;
  end if;

  return v_index;
end;
$$;

-- Stores one validated report. The API validates shape and size first; the
-- constraints here are the last line of defence. Returns
-- {reportId, receivedAt, duplicate, primarySignatureId}.
-- Raises 'diagnostics_ingest_disabled' or 'diagnostics_rate_limited'.
create or replace function diagnostics.submit_report(
  p_report jsonb,
  p_delete_token_hash bytea,
  p_network_key bytea)
returns jsonb
language plpgsql
set search_path = ''
as $$
declare
  v_settings diagnostics.settings;
  v_install_id uuid := (p_report ->> 'installId')::uuid;
  v_client_report_id uuid := (p_report ->> 'clientReportId')::uuid;
  v_existing diagnostics.report_keys;
  v_now timestamptz := clock_timestamp();
  v_day timestamptz := date_trunc('day', v_now at time zone 'UTC') at time zone 'UTC';
  v_report_id uuid := gen_random_uuid();
  v_app_version text := p_report #>> '{app,version}';
  v_os_family text := p_report #>> '{os,family}';
  v_process_arch text := coalesce(p_report #>> '{os,processArchitecture}', 'unknown');
  v_count integer;
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
  v_log_bytes integer := 0;
  v_abnormal integer := 0;
begin
  select * into v_settings from diagnostics.settings where singleton;
  if not coalesce(v_settings.allow_ingest, false) then
    raise exception using errcode = 'P0001', message = 'diagnostics_ingest_disabled';
  end if;

  select * into v_existing from diagnostics.report_keys
  where install_id = v_install_id and client_report_id = v_client_report_id;
  if found then
    return jsonb_build_object(
      'reportId', v_existing.report_id,
      'receivedAt', v_existing.received_at,
      'duplicate', true,
      'primarySignatureId', null);
  end if;

  -- Per-install and per-network daily limits. The network key is an HMAC of
  -- the client IP and the day; the IP itself is never stored.
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
    window_kind, window_start, window_end, user_comment, delete_token_hash, extra)
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
    (p_report #>> '{window,start}')::timestamptz,
    (p_report #>> '{window,end}')::timestamptz,
    nullif(p_report ->> 'comment', ''),
    p_delete_token_hash,
    coalesce(p_report -> 'extra', '{}'));

  insert into diagnostics.report_sessions (
    report_id, received_at, ordinal, started_at, ended_at, app_version, exit_kind, last_component, last_message_template)
  select v_report_id, v_now, (s.ordinality - 1)::smallint,
    (s.value ->> 'startedAt')::timestamptz,
    (s.value ->> 'endedAt')::timestamptz,
    s.value ->> 'appVersion',
    s.value ->> 'exitKind',
    s.value ->> 'lastComponent',
    s.value ->> 'lastMessageTemplate'
  from jsonb_array_elements(coalesce(p_report -> 'sessions', '[]')) with ordinality as s(value, ordinality);
  get diagnostics v_count = row_count;

  select count(*) into v_abnormal
  from jsonb_array_elements(coalesce(p_report -> 'sessions', '[]')) s
  where s ->> 'exitKind' = 'abnormal';

  for v_event in select value from jsonb_array_elements(coalesce(p_report -> 'events', '[]')) loop
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
      -- A signature marked fixed that recurs in a newer build is a regression.
      status = case when c.status = 'fixed' and c.fixed_in_version is distinct from excluded.last_version
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
    perform diagnostics.insert_log_chunks(v_report_id, v_now, v_log ->> 'fileName', v_log ->> 'content');
    v_file_count := v_file_count + 1;
    v_log_bytes := v_log_bytes + octet_length(v_log ->> 'content');
    v_line_count := v_line_count + coalesce(array_length(string_to_array(v_log ->> 'content', E'\n'), 1), 0);
  end loop;

  update diagnostics.reports set
    file_count = v_file_count,
    line_count = v_line_count,
    log_bytes = v_log_bytes,
    session_count = v_count,
    abnormal_exit_count = v_abnormal,
    event_count = v_ordinal,
    primary_signature_id = v_primary_signature_id
  where report_id = v_report_id and received_at = v_now;

  return jsonb_build_object(
    'reportId', v_report_id,
    'receivedAt', v_now,
    'duplicate', false,
    'primarySignatureId', v_primary_signature_id);
end;
$$;

-- Lets a reporter withdraw their own report with the token returned at submit.
create or replace function diagnostics.delete_report_with_token(p_report_id uuid, p_delete_token_hash bytea)
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

  delete from diagnostics.reports
  where report_id = p_report_id
    and received_at = v_key.received_at
    and delete_token_hash = p_delete_token_hash;
  if not found then
    return false;
  end if;

  delete from diagnostics.report_keys where report_id = p_report_id;
  return true;
end;
$$;

-- ---------------------------------------------------------------------------
-- Maintainer and agent access
-- ---------------------------------------------------------------------------

create or replace view diagnostics.v_signatures as
select
  s.signature_id,
  s.status,
  s.kind,
  s.title,
  s.exception_type,
  s.component,
  s.message_template,
  s.top_frames,
  s.report_count,
  s.occurrence_count,
  s.first_seen_at,
  s.last_seen_at,
  s.first_version,
  s.last_version,
  coalesce(recent.reports_30d, 0) as reports_30d,
  recent.os_families,
  recent.process_archs,
  s.root_cause,
  s.fix_summary,
  s.fixed_in_version,
  s.fix_commit,
  s.issue_url,
  s.notes,
  s.tags,
  s.kb_updated_at,
  s.kb_updated_by
from diagnostics.crash_signatures s
left join lateral (
  select
    sum(d.reports)::bigint as reports_30d,
    array_agg(distinct d.os_family) as os_families,
    array_agg(distinct d.process_arch) as process_archs
  from diagnostics.signature_daily d
  where d.signature_id = s.signature_id
    and d.day >= current_date - 30
) recent on true;

create or replace view diagnostics.v_reports as
select
  r.report_id,
  r.received_at,
  r.trigger,
  r.app_version,
  r.os_family,
  r.os_description,
  r.os_arch,
  r.process_arch,
  r.session_type,
  r.desktop_env,
  r.capture_backend,
  r.recording_backend,
  r.ffmpeg_version,
  r.cpu_model,
  r.ram_gb_bucket,
  r.display_count,
  r.displays,
  r.gpus,
  r.window_kind,
  r.session_count,
  r.abnormal_exit_count,
  r.event_count,
  r.primary_signature_id,
  s.title as primary_signature_title,
  s.status as primary_signature_status,
  r.user_comment
from diagnostics.reports r
left join diagnostics.crash_signatures s on s.signature_id = r.primary_signature_id;

-- One full-text search over the knowledge base, extracted events, and raw log
-- text. Accepts web-search syntax: "exact phrase", -exclude, OR.
create or replace function diagnostics.search(p_query text, p_limit integer default 50)
returns table (
  source text,
  signature_id bigint,
  report_id uuid,
  received_at timestamptz,
  title text,
  snippet text,
  rank real)
language sql
stable
set search_path = ''
as $$
  with q as (
    select websearch_to_tsquery('simple', p_query) as simple_q,
           websearch_to_tsquery('english', p_query) as english_q
  )
  (select 'signature' as source, s.signature_id, null::uuid as report_id, s.last_seen_at as received_at, s.title,
          ts_headline('simple', s.message_template || ' ' || coalesce(s.root_cause, '') || ' ' || coalesce(s.notes, ''),
                      q.simple_q, 'MaxFragments=2,MaxWords=30,MinWords=8') as snippet,
          ts_rank(s.search, q.simple_q || q.english_q) * 3 as rank
   from diagnostics.crash_signatures s, q
   where s.search @@ (q.simple_q || q.english_q))
  union all
  (select 'event', e.signature_id, e.report_id, e.received_at,
          coalesce(e.exception_type, e.component, e.kind),
          ts_headline('simple', e.message_template || ' ' || coalesce(e.sample_message, ''), q.simple_q,
                      'MaxFragments=2,MaxWords=30,MinWords=8'),
          ts_rank(e.search, q.simple_q) * 2
   from diagnostics.report_events e, q
   where e.search @@ q.simple_q
   order by e.received_at desc
   limit greatest(p_limit, 1) * 4)
  union all
  (select 'log', null::bigint, c.report_id, c.received_at,
          c.file_name || ' #' || c.chunk_index,
          ts_headline('simple', c.content, q.simple_q, 'MaxFragments=3,MaxWords=25,MinWords=6'),
          ts_rank(c.search, q.simple_q)
   from diagnostics.report_log_chunks c, q
   where c.search @@ q.simple_q
   order by c.received_at desc
   limit greatest(p_limit, 1) * 4)
  order by rank desc, received_at desc nulls last
  limit greatest(p_limit, 1)
$$;

-- Full scrubbed log text of one report, in file and line order.
create or replace function diagnostics.report_log(p_report_id uuid, p_file_name text default null)
returns table (file_name text, first_line integer, content text)
language sql
stable
set search_path = ''
as $$
  select c.file_name, c.first_line, c.content
  from diagnostics.report_log_chunks c
  join diagnostics.report_keys k on k.report_id = c.report_id and k.received_at = c.received_at
  where c.report_id = p_report_id
    and (p_file_name is null or c.file_name = p_file_name)
  order by c.file_name, c.chunk_index
$$;

-- Records what was learnt about a signature. Null arguments keep the current value.
create or replace function diagnostics.annotate_signature(
  p_signature_id bigint,
  p_author text,
  p_status text default null,
  p_root_cause text default null,
  p_fix_summary text default null,
  p_fixed_in_version text default null,
  p_fix_commit text default null,
  p_issue_url text default null,
  p_notes text default null,
  p_tags text[] default null)
returns diagnostics.crash_signatures
language plpgsql
set search_path = ''
as $$
declare
  v_row diagnostics.crash_signatures;
begin
  if coalesce(length(trim(p_author)), 0) = 0 then
    raise exception 'annotate_signature requires an author';
  end if;

  update diagnostics.crash_signatures set
    status = coalesce(p_status, status),
    root_cause = coalesce(p_root_cause, root_cause),
    fix_summary = coalesce(p_fix_summary, fix_summary),
    fixed_in_version = coalesce(p_fixed_in_version, fixed_in_version),
    fix_commit = coalesce(p_fix_commit, fix_commit),
    issue_url = coalesce(p_issue_url, issue_url),
    notes = coalesce(p_notes, notes),
    tags = coalesce(p_tags, tags),
    kb_updated_at = clock_timestamp(),
    kb_updated_by = left(trim(p_author), 100)
  where signature_id = p_signature_id
  returning * into v_row;

  if not found then
    raise exception 'signature % not found', p_signature_id;
  end if;
  return v_row;
end;
$$;

-- Owner-only removal. Nothing expires on its own.
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
  return v_count;
end;
$$;

-- ---------------------------------------------------------------------------
-- Roles
-- ---------------------------------------------------------------------------
-- diagnostics_ingest: the web API. Can only submit and self-delete.
-- diagnostics_reader: agents and dashboards. Read-only, plus search.
-- diagnostics_curator: reader plus annotate_signature.
-- Grant these to login roles (e.g. `grant diagnostics_reader to xerahs_agent`).

do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'diagnostics_ingest') then
    create role diagnostics_ingest nologin;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'diagnostics_reader') then
    create role diagnostics_reader nologin;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'diagnostics_curator') then
    create role diagnostics_curator nologin;
  end if;
end;
$$;

grant diagnostics_reader to diagnostics_curator;

revoke all on all functions in schema diagnostics from public;

grant usage on schema diagnostics to diagnostics_ingest, diagnostics_reader;

-- Ingest goes through security-definer wrappers so the API role holds no table rights.
alter function diagnostics.submit_report(jsonb, bytea, bytea) security definer;
alter function diagnostics.delete_report_with_token(uuid, bytea) security definer;
alter function diagnostics.ensure_month_partitions(timestamptz) security definer;
grant execute on function diagnostics.submit_report(jsonb, bytea, bytea) to diagnostics_ingest;
grant execute on function diagnostics.delete_report_with_token(uuid, bytea) to diagnostics_ingest;

grant select on all tables in schema diagnostics to diagnostics_reader;
revoke select on diagnostics.ingest_limits from diagnostics_reader;
alter default privileges in schema diagnostics grant select on tables to diagnostics_reader;
grant execute on function diagnostics.search(text, integer) to diagnostics_reader;
grant execute on function diagnostics.report_log(uuid, text) to diagnostics_reader;
grant execute on function diagnostics.normalize_template(text) to diagnostics_reader;

alter function diagnostics.annotate_signature(bigint, text, text, text, text, text, text, text, text, text[]) security definer;
grant execute on function diagnostics.annotate_signature(bigint, text, text, text, text, text, text, text, text, text[]) to diagnostics_curator;
