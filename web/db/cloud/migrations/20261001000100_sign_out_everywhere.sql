-- Signs the current user out of every browser and desktop session.
-- Deleting auth.sessions rows cascades through the Better Auth mirror
-- triggers: browser sessions end and desktop grants lose their tokens.
-- Replaces Supabase Auth's global sign-out.

create or replace function public.sign_out_everywhere()
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := auth.uid();
  v_count integer;
begin
  if v_user_id is null then
    raise exception using errcode = '42501', message = 'session_required';
  end if;
  delete from auth.sessions where user_id = v_user_id;
  get diagnostics v_count = row_count;
  return v_count;
end;
$$;

revoke all on function public.sign_out_everywhere() from public, anon, service_role;
grant execute on function public.sign_out_everywhere() to authenticated;
