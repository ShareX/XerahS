-- A fixed signature only reopens when it recurs in the fixed version or later.
-- 0001 reopened on any other version, so reports from older, still-broken
-- builds flipped fixed signatures back to 'investigating'.

-- '0.32.0', 'v0.32.1-beta', '10.0.26200' -> {0,32,0} etc. Null when unparsable.
create or replace function diagnostics.version_array(p_version text)
returns integer[]
language sql
immutable
parallel safe
set search_path = ''
as $$
  select case
    when p_version ~ '^\s*v?\d+(\.\d+)*'
      then string_to_array(substring(p_version from '^\s*v?(\d+(?:\.\d+)*)'), '.')::integer[]
  end
$$;

grant execute on function diagnostics.version_array(text) to diagnostics_reader;

-- Reopen check used by submit_report.
create or replace function diagnostics.is_regression(p_status text, p_fixed_in_version text, p_reported_version text)
returns boolean
language sql
immutable
parallel safe
set search_path = ''
as $$
  select p_status = 'fixed'
    and diagnostics.version_array(p_fixed_in_version) is not null
    and diagnostics.version_array(p_reported_version) is not null
    and diagnostics.version_array(p_reported_version) >= diagnostics.version_array(p_fixed_in_version)
$$;

-- Same function body as 0001 except the status rule in the signature upsert.
do $migration$
declare
  v_definition text := pg_get_functiondef('diagnostics.submit_report(jsonb, bytea, bytea)'::regprocedure);
  v_old text := $old$status = case when c.status = 'fixed' and c.fixed_in_version is distinct from excluded.last_version
                    then 'investigating' else c.status end$old$;
  v_new text := $new$status = case when diagnostics.is_regression(c.status, c.fixed_in_version, excluded.last_version)
                    then 'investigating' else c.status end$new$;
begin
  if position(v_new in v_definition) > 0 then
    return; -- already applied
  end if;
  if position(v_old in v_definition) = 0 then
    raise exception 'submit_report does not contain the expected 0001 status rule';
  end if;
  execute replace(v_definition, v_old, v_new);
end;
$migration$;
