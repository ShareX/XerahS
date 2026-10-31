-- Better Auth <-> auth.users/auth.sessions mirroring and sign_out_everywhere.
-- Run after the cloud migrations: psql "$DATABASE_URL" -v ON_ERROR_STOP=1 -f <this file>
begin;

insert into better_auth."user" (id, name, email, "emailVerified", "createdAt", "updatedAt")
values ('b0000000-0000-4000-8000-000000000001', 'Mirror', 'mirror@example.test', false, now(), now());

do $$
begin
  assert (select email_confirmed_at is null from auth.users where id = 'b0000000-0000-4000-8000-000000000001'),
    'new users are mirrored unconfirmed';
end;
$$;

update better_auth."user" set "emailVerified" = true where id = 'b0000000-0000-4000-8000-000000000001';

insert into better_auth.session (id, "expiresAt", token, "updatedAt", "userId")
values ('c0000000-0000-4000-8000-000000000001', now() + interval '7 days', 'mirror-token', now(),
        'b0000000-0000-4000-8000-000000000001');

-- A desktop grant approved from that session, with outstanding OAuth tokens.
insert into auth.sessions (id, user_id, aal, strong_auth_at, strong_auth_method, kind, parent_session_id)
values ('d0000000-0000-4000-8000-000000000001', 'b0000000-0000-4000-8000-000000000001', 'aal2', now(),
        'desktop_grant', 'desktop_grant', 'c0000000-0000-4000-8000-000000000001');
insert into better_auth."oauthClient" ("clientId", "redirectUris", "createdAt", "updatedAt")
values ('mirror-client', '["https://example.test/cb"]', now(), now());
insert into better_auth."oauthRefreshToken" (token, "clientId", "userId", "referenceId", "expiresAt", "createdAt", scopes)
values ('mirror-refresh', 'mirror-client', 'b0000000-0000-4000-8000-000000000001',
        'd0000000-0000-4000-8000-000000000001', now() + interval '30 days', now(), '["openid"]');

do $$
begin
  assert (select email_confirmed_at is not null from auth.users where id = 'b0000000-0000-4000-8000-000000000001'),
    'email verification is mirrored';
  assert (select aal = 'aal1' and kind = 'browser' from auth.sessions where id = 'c0000000-0000-4000-8000-000000000001'),
    'new browser sessions start at aal1';
  begin
    insert into auth.sessions (id, user_id, aal, kind) values (gen_random_uuid(), 'b0000000-0000-4000-8000-000000000001', 'aal2', 'browser');
    assert false, 'aal2 without a strong-auth time must be rejected';
  exception when check_violation then
    null;
  end;
end;
$$;

-- sign_out_everywhere as the user.
set local role authenticated;
select set_config('request.jwt.claims', '{"sub":"b0000000-0000-4000-8000-000000000001","role":"authenticated"}', true);
do $$
begin
  assert public.sign_out_everywhere() = 2, 'browser session and desktop grant revoked';
end;
$$;
reset role;

do $$
begin
  assert not exists (select 1 from better_auth.session where id = 'c0000000-0000-4000-8000-000000000001'),
    'Better Auth session removed';
  assert not exists (select 1 from better_auth."oauthRefreshToken" where token = 'mirror-refresh'),
    'desktop refresh token removed';
end;
$$;

-- Anonymous callers cannot sign anyone out.
set local role anon;
do $$
begin
  perform public.sign_out_everywhere();
  assert false, 'anon must not execute sign_out_everywhere';
exception when insufficient_privilege then
  null;
end;
$$;
reset role;

-- Account deletion in the XIP0085 SQL deletes auth.users; Better Auth follows.
delete from auth.users where id = 'b0000000-0000-4000-8000-000000000001';
do $$
begin
  assert not exists (select 1 from better_auth."user" where id = 'b0000000-0000-4000-8000-000000000001'),
    'Better Auth user removed with the account';
end;
$$;

select 'better auth mirror tests passed' as result;
rollback;
