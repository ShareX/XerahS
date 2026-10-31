-- The knowledge base as a work queue, and NAS-style space management.
--
-- Fix queue: agents claim the next open crash signature (claim_next_issue),
-- fix it, and tick it off (mark_fixed). Fixed signatures leave the queue; a
-- fixed signature reported again from the fixed version or later reopens
-- (is_regression, 0002).
--
-- Space: reclaim_space() runs nightly (Neon Function trigger). When the
-- estimated size of stored report data passes the high watermark it frees
-- space down to the low watermark, in this order:
--   1. whole reports with no open issue left (every issue fixed / wont_fix /
--      noise, or none at all) older than fixed_retention, oldest first;
--   2. raw log text of the oldest reports (events, sessions and system data
--      are kept), older than raw_log_min_age;
--   3. empty monthly partitions older than last month.
-- Crash signatures and their root causes are never purged: they are small,
-- and they are what makes regression detection work.
--
-- The watermarks use a logical estimate (log bytes x overhead factor + a
-- fixed amount per report), not the physical database size: deleted rows
-- free space for reuse without shrinking files, so a physical-size policy
-- would keep deleting night after night.

-- ---------------------------------------------------------------------------
-- Settings
-- ---------------------------------------------------------------------------

alter table diagnostics.settings
  add column if not exists reclaim_high_bytes bigint not null default 300 * 1024 * 1024,
  add column if not exists reclaim_low_bytes bigint not null default 240 * 1024 * 1024,
  add column if not exists fixed_retention interval not null default interval '14 days',
  add column if not exists raw_log_min_age interval not null default interval '7 days',
  add column if not exists bytes_per_log_byte numeric not null default 3.0,
  add column if not exists bytes_per_report integer not null default 16384;

alter table diagnostics.settings drop constraint if exists settings_reclaim_check;
alter table diagnostics.settings add constraint settings_reclaim_check check (
  reclaim_low_bytes > 0 and reclaim_low_bytes < reclaim_high_bytes
  and bytes_per_log_byte >= 1 and bytes_per_report >= 0
  and fixed_retention >= interval '0' and raw_log_min_age >= interval '0'
);

-- ---------------------------------------------------------------------------
-- Queue and purge bookkeeping
-- ---------------------------------------------------------------------------

alter table diagnostics.crash_signatures
  add column if not exists claimed_by text,
  add column if not exists claimed_at timestamptz;

alter table diagnostics.reports add column if not exists logs_purged_at timestamptz;

create table if not exists diagnostics.purge_log (
  purge_id bigint generated always as identity primary key,
  ran_at timestamptz not null default clock_timestamp(),
  trigger text not null,
  estimated_bytes_before bigint not null,
  estimated_bytes_after bigint not null,
  database_bytes bigint not null,
  reports_deleted integer not null default 0,
  reports_logs_trimmed integer not null default 0,
  partitions_dropped integer not null default 0,
  details jsonb not null default '{}'
);

grant select on diagnostics.purge_log to diagnostics_reader;

-- ---------------------------------------------------------------------------
-- Storage estimate
-- ---------------------------------------------------------------------------

create or replace function diagnostics.estimated_bytes(p_settings diagnostics.settings)
returns bigint
language sql
stable
set search_path = ''
as $$
  select coalesce(sum(
    case when r.logs_purged_at is null then r.log_bytes * p_settings.bytes_per_log_byte else 0 end
    + p_settings.bytes_per_report), 0)::bigint
  from diagnostics.reports r
$$;

create or replace view diagnostics.v_storage as
select
  diagnostics.estimated_bytes(s) as estimated_bytes,
  s.reclaim_high_bytes,
  s.reclaim_low_bytes,
  pg_database_size(current_database()) as database_bytes,
  (select count(*) from diagnostics.reports) as reports,
  (select count(*) from diagnostics.reports where logs_purged_at is not null) as reports_with_logs_trimmed,
  (select count(*) from diagnostics.crash_signatures where status in ('new', 'investigating', 'known')) as open_issues,
  (select count(*) from diagnostics.crash_signatures where status = 'fixed') as fixed_issues,
  (select max(ran_at) from diagnostics.purge_log) as last_reclaim_at
from diagnostics.settings s
where s.singleton;

grant select on diagnostics.v_storage to diagnostics_reader;

-- ---------------------------------------------------------------------------
-- Fix queue
-- ---------------------------------------------------------------------------

-- Open issues, most important first: crashes before exceptions before error
-- lines, then by recent volume and recency.
create or replace view diagnostics.v_fix_queue as
select
  s.signature_id,
  s.status,
  s.kind,
  s.title,
  s.component,
  s.exception_type,
  s.report_count,
  s.occurrence_count,
  coalesce(recent.reports_30d, 0) as reports_30d,
  s.first_version,
  s.last_version,
  s.first_seen_at,
  s.last_seen_at,
  s.claimed_by,
  s.claimed_at,
  latest.report_id as latest_report_id,
  s.root_cause,
  s.notes,
  s.tags,
  row_number() over (
    order by case s.kind when 'abnormal_exit' then 0 when 'exception' then 1 else 2 end,
             coalesce(recent.reports_30d, 0) desc,
             s.last_seen_at desc) as priority
from diagnostics.crash_signatures s
left join lateral (
  select sum(d.reports)::bigint as reports_30d
  from diagnostics.signature_daily d
  where d.signature_id = s.signature_id and d.day >= current_date - 30
) recent on true
left join lateral (
  select e.report_id
  from diagnostics.report_events e
  where e.signature_id = s.signature_id
  order by e.received_at desc
  limit 1
) latest on true
where s.status in ('new', 'investigating', 'known');

grant select on diagnostics.v_fix_queue to diagnostics_reader;

-- Claims the most important unclaimed open issue (or one whose claim lapsed)
-- for p_agent. Concurrent agents never get the same issue.
create or replace function diagnostics.claim_next_issue(p_agent text, p_lease interval default interval '4 hours')
returns diagnostics.crash_signatures
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_row diagnostics.crash_signatures;
begin
  if coalesce(length(trim(p_agent)), 0) = 0 then
    raise exception 'claim_next_issue requires an agent name';
  end if;

  select s.* into v_row
  from diagnostics.crash_signatures s
  join diagnostics.v_fix_queue q on q.signature_id = s.signature_id
  where s.status in ('new', 'investigating')
    and (s.claimed_at is null or s.claimed_at < clock_timestamp() - p_lease)
  order by q.priority
  limit 1
  for update of s skip locked;

  if not found then
    return null;
  end if;

  update diagnostics.crash_signatures set
    status = 'investigating',
    claimed_by = left(trim(p_agent), 100),
    claimed_at = clock_timestamp()
  where signature_id = v_row.signature_id
  returning * into v_row;
  return v_row;
end;
$$;

-- Gives an issue back to the queue without changing what was learnt.
create or replace function diagnostics.release_issue(p_signature_id bigint, p_agent text)
returns boolean
language sql
security definer
set search_path = ''
as $$
  update diagnostics.crash_signatures set claimed_by = null, claimed_at = null
  where signature_id = p_signature_id and claimed_by = left(trim(p_agent), 100)
  returning true
$$;

-- Ticks an issue off: records the fix, releases the claim, and takes it out
-- of the queue. Its reports become eligible for purging after
-- fixed_retention.
create or replace function diagnostics.mark_fixed(
  p_signature_id bigint,
  p_author text,
  p_fixed_in_version text,
  p_fix_commit text,
  p_root_cause text default null,
  p_fix_summary text default null)
returns diagnostics.crash_signatures
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_row diagnostics.crash_signatures;
begin
  if diagnostics.version_array(p_fixed_in_version) is null then
    raise exception 'mark_fixed requires the version that contains the fix (got %)', p_fixed_in_version;
  end if;
  if coalesce(length(trim(p_fix_commit)), 0) = 0 then
    raise exception 'mark_fixed requires the fixing commit';
  end if;

  perform diagnostics.annotate_signature(
    p_signature_id, p_author,
    p_status => 'fixed',
    p_root_cause => p_root_cause,
    p_fix_summary => p_fix_summary,
    p_fixed_in_version => p_fixed_in_version,
    p_fix_commit => p_fix_commit);

  update diagnostics.crash_signatures set claimed_by = null, claimed_at = null
  where signature_id = p_signature_id
  returning * into v_row;
  return v_row;
end;
$$;

-- ---------------------------------------------------------------------------
-- Space reclamation
-- ---------------------------------------------------------------------------

create or replace function diagnostics.reclaim_space(p_trigger text default 'manual', p_force boolean default false)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_settings diagnostics.settings;
  v_before bigint;
  v_estimate bigint;
  v_need bigint;
  v_report record;
  v_deleted integer := 0;
  v_trimmed integer := 0;
  v_dropped integer := 0;
  v_installs uuid[] := '{}';
  v_install uuid;
  v_partition record;
  v_suffix text;
  v_parent text;
  v_cutoff timestamptz := date_trunc('month', clock_timestamp() at time zone 'UTC') at time zone 'UTC' - interval '1 month';
  v_result jsonb;
begin
  select * into v_settings from diagnostics.settings where singleton;
  v_before := diagnostics.estimated_bytes(v_settings);
  v_estimate := v_before;

  if v_before >= v_settings.reclaim_high_bytes or p_force then
    v_need := greatest(v_before - v_settings.reclaim_low_bytes, 0);

    -- 1. Reports with no open issue left, oldest first.
    for v_report in
      select r.report_id, r.received_at, r.install_id,
             (case when r.logs_purged_at is null then r.log_bytes * v_settings.bytes_per_log_byte else 0 end
              + v_settings.bytes_per_report)::bigint as bytes
      from diagnostics.reports r
      where r.received_at < clock_timestamp() - v_settings.fixed_retention
        and not exists (
          select 1 from diagnostics.report_events e
          join diagnostics.crash_signatures s on s.signature_id = e.signature_id
          where e.report_id = r.report_id and e.received_at = r.received_at
            and s.status not in ('fixed', 'wont_fix', 'noise'))
      order by r.received_at
    loop
      exit when v_need <= 0 and not p_force;
      delete from diagnostics.reports where report_id = v_report.report_id and received_at = v_report.received_at;
      delete from diagnostics.report_keys where report_id = v_report.report_id;
      v_installs := array_append(v_installs, v_report.install_id);
      v_need := v_need - v_report.bytes;
      v_estimate := v_estimate - v_report.bytes;
      v_deleted := v_deleted + 1;
    end loop;

    -- 2. Raw log text of the oldest remaining reports; their events,
    --    sessions and system data stay.
    if v_need > 0 then
      for v_report in
        select r.report_id, r.received_at, (r.log_bytes * v_settings.bytes_per_log_byte)::bigint as bytes
        from diagnostics.reports r
        where r.logs_purged_at is null
          and r.received_at < clock_timestamp() - v_settings.raw_log_min_age
        order by r.received_at
      loop
        exit when v_need <= 0;
        delete from diagnostics.report_log_chunks where report_id = v_report.report_id and received_at = v_report.received_at;
        update diagnostics.reports set logs_purged_at = clock_timestamp()
        where report_id = v_report.report_id and received_at = v_report.received_at;
        v_need := v_need - v_report.bytes;
        v_estimate := v_estimate - v_report.bytes;
        v_trimmed := v_trimmed + 1;
      end loop;
    end if;

    foreach v_install in array (select array_agg(distinct x) from unnest(v_installs) x) loop
      perform diagnostics.refresh_install_state(v_install);
    end loop;
  end if;

  -- 3. Empty partitions older than last month (dropping returns space at once).
  for v_partition in
    select c.relname
    from pg_catalog.pg_inherits i
    join pg_catalog.pg_class c on c.oid = i.inhrelid
    join pg_catalog.pg_class p on p.oid = i.inhparent
    join pg_catalog.pg_namespace n on n.oid = p.relnamespace
    where n.nspname = 'diagnostics' and p.relname = 'reports' and c.relname ~ '^reports_y\d{4}m\d{2}$'
  loop
    v_suffix := substring(v_partition.relname from '_(y\d{4}m\d{2})$');
    continue when to_date(substring(v_suffix from 2), 'YYYY"m"MM') >= v_cutoff::date;
    continue when exists (select 1 from diagnostics.reports r
                          where r.received_at >= to_date(substring(v_suffix from 2), 'YYYY"m"MM')
                            and r.received_at < to_date(substring(v_suffix from 2), 'YYYY"m"MM') + interval '1 month');
    -- Referencing partitions first; the reports partition is referenced by
    -- their foreign keys, so it is detached before it is dropped.
    foreach v_parent in array array['report_log_chunks', 'report_events', 'report_sessions'] loop
      execute format('drop table if exists diagnostics.%I', v_parent || '_' || v_suffix);
    end loop;
    execute format('alter table diagnostics.reports detach partition diagnostics.%I', 'reports_' || v_suffix);
    execute format('drop table diagnostics.%I', 'reports_' || v_suffix);
    v_dropped := v_dropped + 1;
  end loop;

  v_result := jsonb_build_object(
    'estimatedBytesBefore', v_before,
    'estimatedBytesAfter', v_estimate,
    'highWatermarkBytes', v_settings.reclaim_high_bytes,
    'lowWatermarkBytes', v_settings.reclaim_low_bytes,
    'reportsDeleted', v_deleted,
    'reportsLogsTrimmed', v_trimmed,
    'partitionsDropped', v_dropped,
    'stillAboveLow', v_estimate > v_settings.reclaim_low_bytes);

  insert into diagnostics.purge_log (trigger, estimated_bytes_before, estimated_bytes_after, database_bytes,
    reports_deleted, reports_logs_trimmed, partitions_dropped, details)
  values (left(p_trigger, 64), v_before, v_estimate, pg_database_size(current_database()),
    v_deleted, v_trimmed, v_dropped, v_result);

  return v_result;
end;
$$;

-- Nightly maintenance entry point for the ingest Function (as diagnostics_ingest).
create or replace function diagnostics.run_maintenance(p_trigger text default 'schedule')
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
begin
  perform diagnostics.ensure_month_partitions(clock_timestamp());
  perform diagnostics.ensure_month_partitions(clock_timestamp() + interval '1 month');
  return diagnostics.reclaim_space(p_trigger, false);
end;
$$;

revoke all on function diagnostics.estimated_bytes(diagnostics.settings),
  diagnostics.claim_next_issue(text, interval), diagnostics.release_issue(bigint, text),
  diagnostics.mark_fixed(bigint, text, text, text, text, text),
  diagnostics.reclaim_space(text, boolean), diagnostics.run_maintenance(text) from public;

grant execute on function diagnostics.estimated_bytes(diagnostics.settings) to diagnostics_reader;
grant execute on function diagnostics.claim_next_issue(text, interval), diagnostics.release_issue(bigint, text),
  diagnostics.mark_fixed(bigint, text, text, text, text, text) to diagnostics_curator;
grant execute on function diagnostics.run_maintenance(text) to diagnostics_ingest;
