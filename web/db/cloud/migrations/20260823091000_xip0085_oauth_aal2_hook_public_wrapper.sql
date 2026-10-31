-- Dashboard Auth Hooks UI lists functions in the public schema.
create or replace function public.custom_access_token_hook(event jsonb)
returns jsonb
language sql
stable
security definer
set search_path = ''
as $$
  select app_private.custom_access_token_hook(event);
$$;

revoke all on function public.custom_access_token_hook(jsonb) from public, anon, authenticated;
grant execute on function public.custom_access_token_hook(jsonb) to supabase_auth_admin;
grant usage on schema public to supabase_auth_admin;
