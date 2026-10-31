create or replace function app_private.custom_access_token_hook(event jsonb)
returns jsonb
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  claims jsonb := event->'claims';
  v_user_id uuid := nullif(event->>'user_id', '')::uuid;
  has_verified_mfa boolean := false;
begin
  if v_user_id is not null
     and coalesce(claims->>'client_id', '') <> ''
     and coalesce(claims->>'aal', 'aal1') <> 'aal2' then
    select exists (
      select 1
      from auth.mfa_factors as factor
      where factor.user_id = v_user_id
        and factor.status = 'verified'
    ) into has_verified_mfa;

    if has_verified_mfa then
      claims := jsonb_set(claims, '{aal}', '"aal2"'::jsonb, true);
    end if;
  end if;

  return jsonb_build_object('claims', claims);
end;
$$;
