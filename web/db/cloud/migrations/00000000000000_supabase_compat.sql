-- Supabase compatibility layer for XerahS Cloud on plain PostgreSQL (Neon).
--
-- The XIP0085 migrations were written for Supabase. Rather than rewrite
-- them, this recreates the small part of Supabase they depend on, the same
-- way Supabase implements it:
--   * roles anon / authenticated / service_role (the app switches into them
--     with SET LOCAL ROLE, as PostgREST does),
--   * auth.uid() / auth.role() / auth.jwt() reading request.jwt.claims,
--   * auth.users / auth.sessions / auth.mfa_factors.
--
-- Better Auth (web/src/lib/auth) owns sign-in, two-factor, passkeys and the
-- desktop OAuth server. Its hooks mirror users and sessions into auth.users
-- and auth.sessions; a session is aal2 once its second factor is verified.

create schema if not exists extensions;
grant usage on schema extensions to public;

do $$
begin
  if not exists (select 1 from pg_roles where rolname = 'anon') then
    create role anon nologin noinherit;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'authenticated') then
    create role authenticated nologin noinherit;
  end if;
  if not exists (select 1 from pg_roles where rolname = 'service_role') then
    create role service_role nologin noinherit;
  end if;
  -- Only referenced by the retired Supabase access-token hook grants.
  if not exists (select 1 from pg_roles where rolname = 'supabase_auth_admin') then
    create role supabase_auth_admin nologin noinherit;
  end if;
end;
$$;

-- The application connection switches into these roles per request.
grant anon, authenticated, service_role to current_user with inherit false, set true;

grant usage on schema public to anon, authenticated, service_role;

create schema if not exists auth;
grant usage on schema auth to anon, authenticated, service_role;

create table if not exists auth.users (
  id uuid primary key,
  email text,
  email_confirmed_at timestamptz,
  banned_until timestamptz,
  last_sign_in_at timestamptz,
  created_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp(),
  -- Columns the local seed and tests insert; unused at runtime.
  instance_id uuid,
  aud text,
  role text,
  encrypted_password text,
  confirmation_token text,
  email_change text,
  email_change_token_new text,
  recovery_token text,
  raw_app_meta_data jsonb not null default '{}',
  raw_user_meta_data jsonb not null default '{}'
);

create unique index if not exists users_email_key on auth.users (lower(email)) where email is not null;

create table if not exists auth.sessions (
  id uuid primary key,
  user_id uuid not null references auth.users (id) on delete cascade,
  aal text not null default 'aal1' check (aal in ('aal1', 'aal2')),
  created_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp(),
  not_after timestamptz
);

create index if not exists sessions_user_id_idx on auth.sessions (user_id);

create table if not exists auth.mfa_factors (
  id uuid primary key,
  user_id uuid not null references auth.users (id) on delete cascade,
  friendly_name text,
  factor_type text not null,
  status text not null,
  created_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp()
);

revoke all on auth.users, auth.sessions, auth.mfa_factors from public, anon, authenticated;

-- Supabase's definitions: claims come from the request.jwt.claims setting
-- that the caller sets for the transaction.
create or replace function auth.jwt()
returns jsonb
language sql
stable
as $$
  select coalesce(
    nullif(current_setting('request.jwt.claim', true), ''),
    nullif(current_setting('request.jwt.claims', true), '')
  )::jsonb
$$;

create or replace function auth.uid()
returns uuid
language sql
stable
as $$
  select coalesce(
    nullif(current_setting('request.jwt.claim.sub', true), ''),
    (nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'sub')
  )::uuid
$$;

create or replace function auth.role()
returns text
language sql
stable
as $$
  select coalesce(
    nullif(current_setting('request.jwt.claim.role', true), ''),
    (nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'role')
  )::text
$$;

create or replace function auth.email()
returns text
language sql
stable
as $$
  select coalesce(
    nullif(current_setting('request.jwt.claim.email', true), ''),
    (nullif(current_setting('request.jwt.claims', true), '')::jsonb ->> 'email')
  )::text
$$;

grant execute on function auth.jwt(), auth.uid(), auth.role(), auth.email() to anon, authenticated, service_role;
