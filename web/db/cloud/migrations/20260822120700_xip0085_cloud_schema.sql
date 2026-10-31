-- XIP0085: owner-only gallery, entitlement, and durable operations-ledger model.
-- This migration intentionally exposes only public read tables and narrow RPC
-- wrappers. Privileged implementations live in app_private and pin search_path.

create extension if not exists citext with schema extensions;
create extension if not exists pgcrypto with schema extensions;

create schema if not exists app_private;
revoke all on schema app_private from public, anon, authenticated;

create table if not exists public.profiles (
  user_id uuid primary key references auth.users(id) on delete cascade,
  slug extensions.citext not null unique,
  time_zone text not null default 'UTC',
  created_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp(),
  constraint profiles_slug_canonical_check check (
    slug::text = lower(slug::text)
    and slug::text ~ '^[a-z0-9](?:[a-z0-9-]{1,28}[a-z0-9])?$'
    and slug::text <> all (array[
      'api', 'auth', 'billing', 'settings', 'admin', 'login', 'logout',
      'signup', 'support', 'legal', '_next', 'assets', 'static'
    ])
  ),
  constraint profiles_time_zone_length_check check (length(time_zone) between 1 and 63)
);

create table if not exists public.gallery_items (
  id uuid primary key default gen_random_uuid(),
  owner_id uuid not null references auth.users(id) on delete cascade,
  client_item_id uuid not null,
  url text not null,
  url_sha256 bytea not null,
  thumbnail_url text,
  kind text not null,
  file_name text not null,
  title text not null,
  captured_at timestamptz not null,
  published_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp(),
  host text,
  content_type text,
  unpublish_pending_at timestamptz,
  unpublish_event_id uuid,
  constraint gallery_items_owner_client_unique unique (owner_id, client_item_id),
  constraint gallery_items_owner_url_unique unique (owner_id, url_sha256),
  constraint gallery_items_kind_check check (kind in ('screenshot', 'screencast')),
  constraint gallery_items_url_length_check check (length(url) between 9 and 8192),
  constraint gallery_items_thumbnail_length_check check (thumbnail_url is null or length(thumbnail_url) between 9 and 8192),
  constraint gallery_items_file_name_check check (
    length(file_name) between 1 and 255
    and file_name !~ '[\\/]'
    and file_name !~ '[[:cntrl:]]'
  ),
  constraint gallery_items_title_check check (length(title) between 1 and 255 and title !~ '[[:cntrl:]]'),
  constraint gallery_items_host_check check (host is null or (length(host) between 1 and 255 and host !~ '[[:cntrl:]]')),
  constraint gallery_items_content_type_check check (content_type is null or (length(content_type) between 1 and 127 and content_type !~ '[[:cntrl:]]')),
  constraint gallery_items_unpublish_pair_check check (
    (unpublish_pending_at is null and unpublish_event_id is null)
    or (unpublish_pending_at is not null and unpublish_event_id is not null)
  )
);

create table if not exists app_private.runtime_controls (
  singleton boolean primary key default true check (singleton),
  allow_trial_grants boolean not null default false,
  allow_checkout boolean not null default false,
  allow_publish boolean not null default false,
  ledger_hmac_active_version smallint not null default 1 check (ledger_hmac_active_version > 0),
  updated_at timestamptz not null default clock_timestamp()
);

insert into app_private.runtime_controls (singleton)
values (true)
on conflict (singleton) do nothing;

create table if not exists app_private.verified_identities (
  user_id uuid primary key references auth.users(id) on delete cascade,
  identity_hmac bytea not null unique,
  normalization_version smallint not null check (normalization_version > 0),
  hmac_key_version smallint not null check (hmac_key_version > 0),
  created_at timestamptz not null default clock_timestamp(),
  constraint verified_identities_hmac_length_check check (octet_length(identity_hmac) = 32)
);

create table if not exists app_private.entitlements (
  user_id uuid primary key references auth.users(id) on delete cascade,
  status text not null default 'none',
  trial_started_at timestamptz,
  trial_ends_at timestamptz,
  trial_ledger_event_id uuid,
  stripe_customer_id text,
  stripe_subscription_id text,
  stripe_price_id text,
  paid_through timestamptz,
  grace_started_at timestamptz,
  grace_ends_at timestamptz,
  dispute_suspended boolean not null default false,
  last_stripe_event_created_at timestamptz,
  created_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp(),
  constraint entitlements_status_check check (status in (
    'none', 'trial_pending', 'trial_active', 'trial_expired', 'incomplete',
    'active', 'past_due', 'unpaid', 'paused', 'canceled', 'dispute_suspended'
  )),
  constraint entitlements_trial_interval_check check (
    (trial_started_at is null and trial_ends_at is null and trial_ledger_event_id is null)
    or (
      trial_started_at is not null
      and trial_ends_at = trial_started_at + interval '7 days'
      and trial_ledger_event_id is not null
    )
  ),
  constraint entitlements_stripe_id_length_check check (
    (stripe_customer_id is null or length(stripe_customer_id) <= 255)
    and (stripe_subscription_id is null or length(stripe_subscription_id) <= 255)
    and (stripe_price_id is null or length(stripe_price_id) <= 255)
  ),
  constraint entitlements_grace_check check (
    (grace_started_at is null and grace_ends_at is null)
    or (grace_started_at is not null and grace_ends_at = grace_started_at + interval '72 hours')
  )
);

create unique index if not exists entitlements_stripe_customer_unique
  on app_private.entitlements (stripe_customer_id)
  where stripe_customer_id is not null;
create unique index if not exists entitlements_stripe_subscription_unique
  on app_private.entitlements (stripe_subscription_id)
  where stripe_subscription_id is not null;

create table if not exists app_private.trial_grants (
  identity_hmac bytea primary key,
  original_user_id uuid not null,
  normalization_version smallint not null check (normalization_version > 0),
  hmac_key_version smallint not null check (hmac_key_version > 0),
  ledger_event_id uuid not null unique,
  granted_at timestamptz not null,
  ends_at timestamptz not null,
  constraint trial_grants_hmac_length_check check (octet_length(identity_hmac) = 32),
  constraint trial_grants_interval_check check (ends_at = granted_at + interval '7 days')
);

create table if not exists app_private.stripe_webhook_events (
  event_id text primary key,
  event_type text not null,
  stripe_created_at timestamptz not null,
  livemode boolean not null,
  state text not null default 'pending',
  attempt_count integer not null default 0 check (attempt_count >= 0),
  error_code text,
  received_at timestamptz not null default clock_timestamp(),
  processed_at timestamptz,
  constraint stripe_webhook_events_type_length_check check (length(event_type) between 1 and 255),
  constraint stripe_webhook_events_state_check check (state in ('pending', 'processed', 'failed', 'dead_letter')),
  constraint stripe_webhook_events_error_check check (error_code is null or (length(error_code) <= 64 and error_code ~ '^[A-Z0-9_:-]+$'))
);

create index if not exists stripe_webhook_events_pending_idx
  on app_private.stripe_webhook_events (received_at, event_id)
  where state in ('pending', 'failed');

create table if not exists app_private.entitlement_transitions (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  prior_status text not null,
  result_status text not null,
  reason text not null,
  stripe_event_id text,
  stripe_customer_id text,
  stripe_subscription_id text,
  created_at timestamptz not null default clock_timestamp(),
  constraint entitlement_transitions_status_length_check check (
    length(prior_status) between 1 and 32 and length(result_status) between 1 and 32
  ),
  constraint entitlement_transitions_reason_check check (length(reason) between 1 and 255 and reason !~ '[[:cntrl:]]')
);

create index if not exists entitlement_transitions_user_created_idx
  on app_private.entitlement_transitions (user_id, created_at desc, id desc);
create index if not exists entitlement_transitions_stripe_event_idx
  on app_private.entitlement_transitions (stripe_event_id)
  where stripe_event_id is not null;

create table if not exists app_private.account_deletion_requests (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null,
  idempotency_key uuid not null,
  state text not null default 'pending',
  ledger_event_id uuid not null unique,
  stripe_customer_id text,
  stripe_subscription_id text,
  attempt_count integer not null default 0 check (attempt_count >= 0),
  next_attempt_at timestamptz,
  last_error_code text,
  billing_cancelled_at timestamptz,
  tombstoned_at timestamptz,
  completed_at timestamptz,
  created_at timestamptz not null default clock_timestamp(),
  updated_at timestamptz not null default clock_timestamp(),
  constraint account_deletion_user_idempotency_unique unique (user_id, idempotency_key),
  constraint account_deletion_state_check check (state in (
    'blocked', 'pending', 'canceling', 'tombstoned', 'deleting', 'completed', 'failed'
  )),
  constraint account_deletion_error_check check (
    last_error_code is null or (length(last_error_code) <= 64 and last_error_code ~ '^[A-Z0-9_:-]+$')
  )
);

create index if not exists account_deletion_pending_idx
  on app_private.account_deletion_requests (coalesce(next_attempt_at, created_at), id)
  where state in ('pending', 'canceling', 'failed', 'tombstoned', 'deleting');

create table if not exists app_private.operations_ledger_outbox (
  event_id uuid primary key,
  event_type text not null,
  source_user_id uuid not null,
  source_row_id uuid,
  schema_version smallint not null default 1,
  canonical_payload jsonb not null,
  payload_sha256 bytea not null,
  payload_hmac bytea,
  hmac_key_version smallint not null,
  state text not null default 'pending',
  attempt_count integer not null default 0 check (attempt_count >= 0),
  leased_by uuid,
  lease_expires_at timestamptz,
  next_attempt_at timestamptz not null default clock_timestamp(),
  last_error_code text,
  r2_object_key text,
  r2_etag text,
  created_at timestamptz not null default clock_timestamp(),
  replicated_at timestamptz,
  constraint operations_ledger_event_type_check check (event_type in (
    'trial_grant_created', 'gallery_item_unpublished', 'account_deleted'
  )),
  constraint operations_ledger_schema_check check (schema_version = 1),
  constraint operations_ledger_sha_length_check check (octet_length(payload_sha256) = 32),
  constraint operations_ledger_hmac_check check (payload_hmac is null or octet_length(payload_hmac) = 32),
  constraint operations_ledger_hmac_version_check check (hmac_key_version > 0),
  constraint operations_ledger_state_check check (state in ('pending', 'leased', 'replicated', 'failed')),
  constraint operations_ledger_error_check check (
    last_error_code is null or (length(last_error_code) <= 64 and last_error_code ~ '^[A-Z0-9_:-]+$')
  ),
  constraint operations_ledger_replication_check check (
    (state = 'replicated' and replicated_at is not null and r2_object_key is not null and r2_etag is not null and payload_hmac is not null)
    or state <> 'replicated'
  )
);

create index if not exists operations_ledger_claim_idx
  on app_private.operations_ledger_outbox (next_attempt_at, created_at, event_id)
  where state in ('pending', 'failed', 'leased');
create index if not exists operations_ledger_user_created_idx
  on app_private.operations_ledger_outbox (source_user_id, created_at desc, event_id desc);

create table if not exists app_private.recovery_codes (
  id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  batch_id uuid not null,
  code_hmac bytea not null,
  pepper_version smallint not null check (pepper_version > 0),
  created_at timestamptz not null default clock_timestamp(),
  used_at timestamptz,
  revoked_at timestamptz,
  constraint recovery_codes_hash_length_check check (octet_length(code_hmac) = 32),
  constraint recovery_codes_user_hash_unique unique (user_id, code_hmac)
);

create index if not exists recovery_codes_active_idx
  on app_private.recovery_codes (user_id, code_hmac)
  where used_at is null and revoked_at is null;
create index if not exists recovery_codes_batch_idx
  on app_private.recovery_codes (user_id, batch_id);

create table if not exists app_private.audit_events (
  id uuid primary key default gen_random_uuid(),
  actor_user_id uuid,
  action text not null,
  target_id uuid,
  request_id uuid,
  succeeded boolean not null,
  metadata jsonb not null default '{}'::jsonb,
  created_at timestamptz not null default clock_timestamp(),
  constraint audit_events_action_check check (length(action) between 1 and 127 and action ~ '^[a-z0-9_.:-]+$'),
  constraint audit_events_metadata_check check (pg_column_size(metadata) <= 4096)
);

create index if not exists audit_events_actor_created_idx
  on app_private.audit_events (actor_user_id, created_at desc, id desc)
  where actor_user_id is not null;
create index if not exists audit_events_action_created_idx
  on app_private.audit_events (action, created_at desc, id desc);

create index if not exists gallery_items_owner_captured_idx
  on public.gallery_items (owner_id, captured_at desc, id desc)
  where unpublish_pending_at is null;
create index if not exists gallery_items_owner_kind_captured_idx
  on public.gallery_items (owner_id, kind, captured_at desc, id desc)
  where unpublish_pending_at is null;
create index if not exists gallery_items_owner_published_idx
  on public.gallery_items (owner_id, published_at desc, id desc)
  where unpublish_pending_at is null;

create or replace function app_private.touch_updated_at()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $$
begin
  new.updated_at := clock_timestamp();
  return new;
end;
$$;

drop trigger if exists profiles_touch_updated_at on public.profiles;
create trigger profiles_touch_updated_at
before update on public.profiles
for each row execute function app_private.touch_updated_at();

drop trigger if exists entitlements_touch_updated_at on app_private.entitlements;
create trigger entitlements_touch_updated_at
before update on app_private.entitlements
for each row execute function app_private.touch_updated_at();

drop trigger if exists account_deletion_touch_updated_at on app_private.account_deletion_requests;
create trigger account_deletion_touch_updated_at
before update on app_private.account_deletion_requests
for each row execute function app_private.touch_updated_at();

create or replace function app_private.derive_title(p_file_name text)
returns text
language plpgsql
immutable
strict
security invoker
set search_path = ''
as $$
declare
  v_title text;
begin
  if length(p_file_name) > 255 or p_file_name ~ '[\\/]' or p_file_name ~ '[[:cntrl:]]' then
    raise exception using errcode = '22023', message = 'invalid_file_name';
  end if;

  v_title := regexp_replace(p_file_name, '\.[^.]+$', '');
  if v_title is null or length(v_title) = 0 then
    raise exception using errcode = '22023', message = 'invalid_file_title';
  end if;
  return v_title;
end;
$$;

create or replace function app_private.normalize_safe_https_url(p_url text)
returns text
language plpgsql
immutable
strict
security invoker
set search_path = ''
as $$
declare
  v_url text := split_part(p_url, '#', 1);
  v_authority text;
  v_host text;
  v_ip inet;
begin
  if length(v_url) > 8192 or v_url !~ '^https://[^/?#]+(?:[/?#]|$)' or v_url ~ '[[:cntrl:]]' then
    raise exception using errcode = '22023', message = 'invalid_https_url';
  end if;

  v_authority := substring(v_url from '^https://([^/?#]+)');
  if v_authority is null or position('@' in v_authority) > 0 then
    raise exception using errcode = '22023', message = 'credentialed_or_missing_host';
  end if;

  if left(v_authority, 1) = '[' then
    v_host := substring(v_authority from '^\[([^]]+)\](?::[0-9]{1,5})?$');
  else
    v_host := regexp_replace(v_authority, ':[0-9]{1,5}$', '');
  end if;
  v_host := lower(v_host);

  if v_host is null or v_host = '' or v_host = 'localhost' or right(v_host, 6) = '.local' then
    raise exception using errcode = '22023', message = 'local_host_not_allowed';
  end if;

  if v_host ~ '^[0-9]+(?:\.[0-9]+){3}$' or position(':' in v_host) > 0 then
    begin
      v_ip := v_host::inet;
    exception when invalid_text_representation then
      raise exception using errcode = '22023', message = 'invalid_ip_literal';
    end;

    if v_ip <<= inet '0.0.0.0/8'
      or v_ip <<= inet '10.0.0.0/8'
      or v_ip <<= inet '100.64.0.0/10'
      or v_ip <<= inet '127.0.0.0/8'
      or v_ip <<= inet '169.254.0.0/16'
      or v_ip <<= inet '172.16.0.0/12'
      or v_ip <<= inet '192.0.0.0/24'
      or v_ip <<= inet '192.0.2.0/24'
      or v_ip <<= inet '192.168.0.0/16'
      or v_ip <<= inet '198.18.0.0/15'
      or v_ip <<= inet '198.51.100.0/24'
      or v_ip <<= inet '203.0.113.0/24'
      or v_ip <<= inet '224.0.0.0/4'
      or v_ip <<= inet '240.0.0.0/4'
      or v_ip <<= inet '::/128'
      or v_ip <<= inet '::1/128'
      or v_ip <<= inet 'fc00::/7'
      or v_ip <<= inet 'fe80::/10'
      or v_ip <<= inet 'ff00::/8'
      or v_ip <<= inet '2001:db8::/32'
    then
      raise exception using errcode = '22023', message = 'non_public_ip_not_allowed';
    end if;
  end if;

  return v_url;
end;
$$;

create or replace function app_private.prepare_gallery_item()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $$
begin
  new.url := app_private.normalize_safe_https_url(new.url);
  if new.thumbnail_url is not null then
    new.thumbnail_url := app_private.normalize_safe_https_url(new.thumbnail_url);
  end if;
  new.url_sha256 := extensions.digest(convert_to(new.url, 'UTF8'), 'sha256');
  new.title := app_private.derive_title(new.file_name);
  new.updated_at := clock_timestamp();
  return new;
end;
$$;

drop trigger if exists gallery_items_prepare on public.gallery_items;
create trigger gallery_items_prepare
before insert or update of url, thumbnail_url, file_name, title, url_sha256 on public.gallery_items
for each row execute function app_private.prepare_gallery_item();

create or replace function app_private.prevent_trial_field_changes()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $$
begin
  if old.trial_started_at is not null and (
    new.trial_started_at is distinct from old.trial_started_at
    or new.trial_ends_at is distinct from old.trial_ends_at
    or new.trial_ledger_event_id is distinct from old.trial_ledger_event_id
  ) then
    raise exception using errcode = '55000', message = 'trial_fields_are_immutable';
  end if;
  return new;
end;
$$;

drop trigger if exists entitlements_prevent_trial_field_changes on app_private.entitlements;
create trigger entitlements_prevent_trial_field_changes
before update on app_private.entitlements
for each row execute function app_private.prevent_trial_field_changes();

create or replace function app_private.prevent_append_only_changes()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $$
begin
  raise exception using errcode = '55000', message = 'append_only_relation';
end;
$$;

drop trigger if exists trial_grants_append_only on app_private.trial_grants;
create trigger trial_grants_append_only
before update or delete on app_private.trial_grants
for each row execute function app_private.prevent_append_only_changes();

drop trigger if exists audit_events_append_only on app_private.audit_events;
create trigger audit_events_append_only
before update or delete on app_private.audit_events
for each row execute function app_private.prevent_append_only_changes();

create or replace function app_private.protect_outbox_payload()
returns trigger
language plpgsql
security invoker
set search_path = ''
as $$
begin
  if new.event_id is distinct from old.event_id
    or new.event_type is distinct from old.event_type
    or new.source_user_id is distinct from old.source_user_id
    or new.source_row_id is distinct from old.source_row_id
    or new.schema_version is distinct from old.schema_version
    or new.canonical_payload is distinct from old.canonical_payload
    or new.payload_sha256 is distinct from old.payload_sha256
    or new.hmac_key_version is distinct from old.hmac_key_version
    or new.created_at is distinct from old.created_at
  then
    raise exception using errcode = '55000', message = 'ledger_payload_is_immutable';
  end if;
  return new;
end;
$$;

drop trigger if exists operations_ledger_protect_payload on app_private.operations_ledger_outbox;
create trigger operations_ledger_protect_payload
before update on app_private.operations_ledger_outbox
for each row execute function app_private.protect_outbox_payload();

create or replace function app_private.current_user_aal2(p_require_recent boolean default false)
returns uuid
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := auth.uid();
  v_jwt jsonb := auth.jwt();
  v_recent boolean;
begin
  if v_user_id is null or coalesce(v_jwt ->> 'aal', 'aal1') <> 'aal2' then
    raise exception using errcode = '42501', message = 'aal2_required';
  end if;

  if coalesce(v_jwt ->> 'session_id', '') = '' then
    raise exception using errcode = '42501', message = 'session_required';
  end if;

  if not exists (
    select 1
    from auth.users as u
    where u.id = v_user_id
      and u.email_confirmed_at is not null
      and (u.banned_until is null or u.banned_until <= clock_timestamp())
  ) then
    raise exception using errcode = '42501', message = 'verified_email_required';
  end if;

  if p_require_recent then
    select coalesce(bool_or(
      factor ->> 'method' in ('totp', 'mfa')
      and to_timestamp((factor ->> 'timestamp')::double precision) >= clock_timestamp() - interval '10 minutes'
    ), false)
    into v_recent
    from jsonb_array_elements(coalesce(v_jwt -> 'amr', '[]'::jsonb)) as factor;

    if not v_recent then
      raise exception using errcode = '42501', message = 'recent_strong_auth_required';
    end if;
  end if;

  return v_user_id;
end;
$$;

create or replace function app_private.can_publish(p_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select coalesce((
    select controls.allow_publish
      and not entitlement.dispute_suspended
      and (
        (entitlement.status = 'trial_active' and clock_timestamp() < entitlement.trial_ends_at)
        or (entitlement.status = 'active' and entitlement.paid_through > clock_timestamp())
        or (
          entitlement.status = 'past_due'
          and entitlement.grace_ends_at >= clock_timestamp()
          and entitlement.paid_through > clock_timestamp()
        )
      )
    from app_private.entitlements as entitlement
    cross join app_private.runtime_controls as controls
    where entitlement.user_id = p_user_id and controls.singleton
  ), false);
$$;

create or replace function app_private.create_profile(p_slug text, p_time_zone text default 'UTC')
returns public.profiles
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_slug text := lower(trim(p_slug));
  v_profile public.profiles;
begin
  if not exists (select 1 from pg_catalog.pg_timezone_names where name = p_time_zone) then
    raise exception using errcode = '22023', message = 'invalid_time_zone';
  end if;

  insert into public.profiles (user_id, slug, time_zone)
  values (v_user_id, v_slug, p_time_zone)
  on conflict (user_id) do update
    set slug = excluded.slug, time_zone = excluded.time_zone
  returning * into v_profile;

  insert into app_private.entitlements (user_id)
  values (v_user_id)
  on conflict (user_id) do nothing;

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'profile.upserted', v_user_id, true);

  return v_profile;
end;
$$;

create or replace function app_private.register_verified_identity(
  p_user_id uuid,
  p_identity_hmac bytea,
  p_normalization_version smallint,
  p_hmac_key_version smallint
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if octet_length(p_identity_hmac) <> 32 then
    raise exception using errcode = '22023', message = 'invalid_identity_hmac';
  end if;
  if not exists (select 1 from auth.users where id = p_user_id and email_confirmed_at is not null) then
    raise exception using errcode = '22023', message = 'verified_user_required';
  end if;

  insert into app_private.verified_identities (
    user_id, identity_hmac, normalization_version, hmac_key_version
  ) values (
    p_user_id, p_identity_hmac, p_normalization_version, p_hmac_key_version
  )
  on conflict (user_id) do update set
    identity_hmac = excluded.identity_hmac,
    normalization_version = excluded.normalization_version,
    hmac_key_version = excluded.hmac_key_version;

  insert into app_private.entitlements (user_id)
  values (p_user_id)
  on conflict (user_id) do nothing;
end;
$$;

create or replace function app_private.start_trial()
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
  v_identity app_private.verified_identities;
  v_entitlement app_private.entitlements;
  v_event_id uuid := gen_random_uuid();
  v_started_at timestamptz := clock_timestamp();
  v_payload jsonb;
  v_hmac_version smallint;
begin
  if not (select allow_trial_grants from app_private.runtime_controls where singleton) then
    raise exception using errcode = '55000', message = 'trial_grants_disabled';
  end if;

  select * into v_identity
  from app_private.verified_identities
  where user_id = v_user_id;
  if not found then
    raise exception using errcode = '42501', message = 'verified_identity_not_registered';
  end if;

  insert into app_private.entitlements (user_id)
  values (v_user_id)
  on conflict (user_id) do nothing;

  select * into v_entitlement
  from app_private.entitlements
  where user_id = v_user_id
  for update;

  if v_entitlement.trial_ledger_event_id is not null then
    return v_entitlement.trial_ledger_event_id;
  end if;

  v_hmac_version := (select ledger_hmac_active_version from app_private.runtime_controls where singleton);
  v_payload := jsonb_build_object(
    'schemaVersion', 1,
    'eventId', v_event_id,
    'eventType', 'trial_grant_created',
    'occurredAt', v_started_at,
    'userId', v_user_id,
    'identityHmac', encode(v_identity.identity_hmac, 'hex'),
    'identityNormalizationVersion', v_identity.normalization_version,
    'identityHmacKeyVersion', v_identity.hmac_key_version
  );

  begin
    insert into app_private.trial_grants (
      identity_hmac, original_user_id, normalization_version, hmac_key_version,
      ledger_event_id, granted_at, ends_at
    ) values (
      v_identity.identity_hmac, v_user_id, v_identity.normalization_version,
      v_identity.hmac_key_version, v_event_id, v_started_at, v_started_at + interval '7 days'
    );
  exception when unique_violation then
    raise exception using errcode = '23505', message = 'trial_already_granted_for_identity';
  end;

  insert into app_private.operations_ledger_outbox (
    event_id, event_type, source_user_id, source_row_id, canonical_payload,
    payload_sha256, hmac_key_version, created_at, next_attempt_at
  ) values (
    v_event_id, 'trial_grant_created', v_user_id, v_user_id, v_payload,
    extensions.digest(convert_to(v_payload::text, 'UTF8'), 'sha256'),
    v_hmac_version, v_started_at, v_started_at
  );

  update app_private.entitlements
  set status = 'trial_pending', trial_started_at = v_started_at,
      trial_ends_at = v_started_at + interval '7 days', trial_ledger_event_id = v_event_id
  where user_id = v_user_id;

  insert into app_private.entitlement_transitions (
    user_id, prior_status, result_status, reason
  ) values (v_user_id, v_entitlement.status, 'trial_pending', 'trial_requested');

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'trial.requested', v_event_id, true);

  return v_event_id;
end;
$$;

create or replace function app_private.publish_gallery_item(
  p_client_item_id uuid,
  p_url text,
  p_thumbnail_url text,
  p_kind text,
  p_file_name text,
  p_captured_at timestamptz,
  p_host text,
  p_content_type text
)
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_item_id uuid;
  v_url text := app_private.normalize_safe_https_url(p_url);
  v_thumbnail text;
  v_url_sha bytea;
begin
  if p_client_item_id is null or p_captured_at is null then
    raise exception using errcode = '22004', message = 'client_item_id_and_captured_at_required';
  end if;
  if p_kind not in ('screenshot', 'screencast') then
    raise exception using errcode = '22023', message = 'invalid_gallery_kind';
  end if;
  if p_thumbnail_url is not null then
    v_thumbnail := app_private.normalize_safe_https_url(p_thumbnail_url);
  end if;
  perform app_private.derive_title(p_file_name);
  if p_host is not null and (length(p_host) > 255 or p_host ~ '[[:cntrl:]]') then
    raise exception using errcode = '22023', message = 'invalid_host';
  end if;
  if p_content_type is not null and (length(p_content_type) > 127 or p_content_type ~ '[[:cntrl:]]') then
    raise exception using errcode = '22023', message = 'invalid_content_type';
  end if;

  perform 1 from app_private.entitlements where user_id = v_user_id for update;
  if not app_private.can_publish(v_user_id) then
    raise exception using errcode = '42501', message = 'publish_entitlement_required';
  end if;

  v_url_sha := extensions.digest(convert_to(v_url, 'UTF8'), 'sha256');
  select id into v_item_id
  from public.gallery_items
  where owner_id = v_user_id and client_item_id = p_client_item_id
  for update;

  if found then
    if (select url_sha256 from public.gallery_items where id = v_item_id) <> v_url_sha then
      raise exception using errcode = '23505', message = 'client_item_url_is_immutable';
    end if;
    update public.gallery_items
    set thumbnail_url = v_thumbnail, kind = p_kind, file_name = p_file_name,
        captured_at = p_captured_at, host = p_host, content_type = p_content_type,
        unpublish_pending_at = null, unpublish_event_id = null
    where id = v_item_id;
  else
    begin
      insert into public.gallery_items (
        owner_id, client_item_id, url, url_sha256, thumbnail_url, kind,
        file_name, title, captured_at, host, content_type
      ) values (
        v_user_id, p_client_item_id, v_url, v_url_sha, v_thumbnail, p_kind,
        p_file_name, app_private.derive_title(p_file_name), p_captured_at, p_host, p_content_type
      ) returning id into v_item_id;
    exception when unique_violation then
      select id into v_item_id
      from public.gallery_items
      where owner_id = v_user_id and url_sha256 = v_url_sha
      for update;
      if v_item_id is null then
        raise;
      end if;
      update public.gallery_items
      set thumbnail_url = v_thumbnail, kind = p_kind, file_name = p_file_name,
          captured_at = p_captured_at, host = p_host, content_type = p_content_type
      where id = v_item_id;
    end;
  end if;

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'gallery.published', v_item_id, true);
  return v_item_id;
end;
$$;

create or replace function app_private.unpublish_gallery_item(p_item_id uuid)
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_item public.gallery_items;
  v_event_id uuid := gen_random_uuid();
  v_occurred_at timestamptz := clock_timestamp();
  v_payload jsonb;
  v_hmac_version smallint;
begin
  select * into v_item
  from public.gallery_items
  where id = p_item_id and owner_id = v_user_id
  for update;

  if not found then
    return null;
  end if;
  if v_item.unpublish_event_id is not null then
    return v_item.unpublish_event_id;
  end if;

  v_hmac_version := (select ledger_hmac_active_version from app_private.runtime_controls where singleton);
  v_payload := jsonb_build_object(
    'schemaVersion', 1,
    'eventId', v_event_id,
    'eventType', 'gallery_item_unpublished',
    'occurredAt', v_occurred_at,
    'userId', v_user_id,
    'itemId', v_item.id
  );

  update public.gallery_items
  set unpublish_pending_at = v_occurred_at, unpublish_event_id = v_event_id
  where id = v_item.id;

  insert into app_private.operations_ledger_outbox (
    event_id, event_type, source_user_id, source_row_id, canonical_payload,
    payload_sha256, hmac_key_version, created_at, next_attempt_at
  ) values (
    v_event_id, 'gallery_item_unpublished', v_user_id, v_item.id, v_payload,
    extensions.digest(convert_to(v_payload::text, 'UTF8'), 'sha256'),
    v_hmac_version, v_occurred_at, v_occurred_at
  );

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'gallery.unpublish_requested', v_item.id, true);
  return v_event_id;
end;
$$;

create or replace function app_private.request_account_deletion(p_idempotency_key uuid)
returns uuid
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
  v_request app_private.account_deletion_requests;
  v_event_id uuid := gen_random_uuid();
  v_occurred_at timestamptz := clock_timestamp();
  v_payload jsonb;
  v_entitlement app_private.entitlements;
  v_hmac_version smallint;
begin
  if p_idempotency_key is null then
    raise exception using errcode = '22004', message = 'idempotency_key_required';
  end if;

  select * into v_request
  from app_private.account_deletion_requests
  where user_id = v_user_id and idempotency_key = p_idempotency_key
  for update;
  if found then
    return v_request.ledger_event_id;
  end if;

  select * into v_entitlement
  from app_private.entitlements
  where user_id = v_user_id
  for update;

  v_hmac_version := (select ledger_hmac_active_version from app_private.runtime_controls where singleton);
  v_payload := jsonb_build_object(
    'schemaVersion', 1,
    'eventId', v_event_id,
    'eventType', 'account_deleted',
    'occurredAt', v_occurred_at,
    'userId', v_user_id
  );

  insert into app_private.account_deletion_requests (
    user_id, idempotency_key, state, ledger_event_id,
    stripe_customer_id, stripe_subscription_id
  ) values (
    v_user_id, p_idempotency_key, 'pending', v_event_id,
    v_entitlement.stripe_customer_id, v_entitlement.stripe_subscription_id
  ) returning * into v_request;

  insert into app_private.operations_ledger_outbox (
    event_id, event_type, source_user_id, source_row_id, canonical_payload,
    payload_sha256, hmac_key_version, created_at, next_attempt_at
  ) values (
    v_event_id, 'account_deleted', v_user_id, v_request.id, v_payload,
    extensions.digest(convert_to(v_payload::text, 'UTF8'), 'sha256'),
    v_hmac_version, v_occurred_at, v_occurred_at
  );

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'account.deletion_requested', v_request.id, true);
  return v_event_id;
end;
$$;

create or replace function app_private.claim_ledger_events(
  p_worker_id uuid,
  p_limit integer default 25,
  p_lease_seconds integer default 300
)
returns table (
  event_id uuid,
  event_type text,
  canonical_payload jsonb,
  payload_sha256 bytea,
  hmac_key_version smallint,
  attempt_count integer
)
language plpgsql
security definer
set search_path = ''
as $$
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_worker_id is null or p_limit not between 1 and 100 or p_lease_seconds not between 30 and 900 then
    raise exception using errcode = '22023', message = 'invalid_lease_request';
  end if;

  return query
  with candidates as (
    select outbox.event_id
    from app_private.operations_ledger_outbox as outbox
    where outbox.next_attempt_at <= clock_timestamp()
      and (
        outbox.state in ('pending', 'failed')
        or (outbox.state = 'leased' and outbox.lease_expires_at <= clock_timestamp())
      )
    order by outbox.next_attempt_at, outbox.created_at, outbox.event_id
    for update skip locked
    limit p_limit
  ), claimed as (
    update app_private.operations_ledger_outbox as outbox
    set state = 'leased', leased_by = p_worker_id,
        lease_expires_at = clock_timestamp() + make_interval(secs => p_lease_seconds),
        attempt_count = outbox.attempt_count + 1,
        last_error_code = null
    from candidates
    where outbox.event_id = candidates.event_id
    returning outbox.*
  )
  select claimed.event_id, claimed.event_type, claimed.canonical_payload,
         claimed.payload_sha256, claimed.hmac_key_version, claimed.attempt_count
  from claimed
  order by claimed.created_at, claimed.event_id;
end;
$$;

create or replace function app_private.fail_ledger_event(
  p_event_id uuid,
  p_worker_id uuid,
  p_error_code text,
  p_retry_after_seconds integer default 60
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_error_code !~ '^[A-Z0-9_:-]{1,64}$' or p_retry_after_seconds not between 1 and 86400 then
    raise exception using errcode = '22023', message = 'invalid_ledger_failure';
  end if;

  update app_private.operations_ledger_outbox
  set state = 'failed', leased_by = null, lease_expires_at = null,
      last_error_code = p_error_code,
      next_attempt_at = clock_timestamp() + make_interval(secs => p_retry_after_seconds)
  where event_id = p_event_id and state = 'leased' and leased_by = p_worker_id;

  if not found then
    raise exception using errcode = '55000', message = 'ledger_lease_not_owned';
  end if;
end;
$$;

create or replace function app_private.acknowledge_ledger_event(
  p_event_id uuid,
  p_worker_id uuid,
  p_object_key text,
  p_etag text,
  p_payload_sha256 bytea,
  p_payload_hmac bytea
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_event app_private.operations_ledger_outbox;
  v_prior_status text;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if octet_length(p_payload_sha256) <> 32 or octet_length(p_payload_hmac) <> 32
    or length(p_etag) not between 1 and 255
    or p_object_key !~ ('^(trial-grants|deletions)/v1/[0-9]{4}/[0-9]{2}/[0-9]{2}/' || p_event_id::text || '\.json$')
  then
    raise exception using errcode = '22023', message = 'invalid_ledger_acknowledgement';
  end if;

  select * into v_event
  from app_private.operations_ledger_outbox
  where event_id = p_event_id
  for update;
  if not found then
    raise exception using errcode = 'P0002', message = 'ledger_event_not_found';
  end if;
  if v_event.state = 'replicated' then
    if v_event.r2_object_key = p_object_key
      and v_event.r2_etag = p_etag
      and v_event.payload_sha256 = p_payload_sha256
      and v_event.payload_hmac = p_payload_hmac
    then
      return;
    end if;
    raise exception using errcode = '55000', message = 'ledger_acknowledgement_mismatch';
  end if;
  if v_event.state <> 'leased' or v_event.leased_by <> p_worker_id then
    raise exception using errcode = '55000', message = 'ledger_lease_not_owned';
  end if;
  if v_event.payload_sha256 <> p_payload_sha256 then
    raise exception using errcode = '55000', message = 'ledger_payload_digest_mismatch';
  end if;
  if (v_event.event_type = 'trial_grant_created' and p_object_key !~ '^trial-grants/v1/')
    or (v_event.event_type <> 'trial_grant_created' and p_object_key !~ '^deletions/v1/')
  then
    raise exception using errcode = '55000', message = 'ledger_prefix_mismatch';
  end if;

  update app_private.operations_ledger_outbox
  set state = 'replicated', payload_hmac = p_payload_hmac,
      r2_object_key = p_object_key, r2_etag = p_etag,
      replicated_at = clock_timestamp(), leased_by = null, lease_expires_at = null,
      last_error_code = null
  where event_id = p_event_id;

  if v_event.event_type = 'trial_grant_created' then
    select status into v_prior_status
    from app_private.entitlements
    where user_id = v_event.source_user_id
    for update;
    update app_private.entitlements
    set status = case when trial_ends_at > clock_timestamp() then 'trial_active' else 'trial_expired' end
    where user_id = v_event.source_user_id and trial_ledger_event_id = p_event_id;
    insert into app_private.entitlement_transitions (user_id, prior_status, result_status, reason)
    select v_event.source_user_id, v_prior_status, status, 'trial_ledger_replicated'
    from app_private.entitlements where user_id = v_event.source_user_id;
  elsif v_event.event_type = 'gallery_item_unpublished' then
    delete from public.gallery_items
    where id = v_event.source_row_id and owner_id = v_event.source_user_id
      and unpublish_event_id = p_event_id;
  elsif v_event.event_type = 'account_deleted' then
    update app_private.account_deletion_requests
    set state = 'tombstoned', tombstoned_at = clock_timestamp()
    where id = v_event.source_row_id and user_id = v_event.source_user_id
      and ledger_event_id = p_event_id and state <> 'completed';
  end if;
end;
$$;

create or replace function app_private.apply_stripe_entitlement(
  p_event_id text,
  p_event_type text,
  p_stripe_created_at timestamptz,
  p_livemode boolean,
  p_user_id uuid,
  p_result_status text,
  p_reason text,
  p_customer_id text,
  p_subscription_id text,
  p_price_id text,
  p_paid_through timestamptz,
  p_grace_started_at timestamptz,
  p_dispute_suspended boolean
)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_entitlement app_private.entitlements;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_result_status not in ('incomplete', 'active', 'past_due', 'unpaid', 'paused', 'canceled', 'dispute_suspended')
    or length(p_event_id) not between 1 and 255
    or length(p_event_type) not between 1 and 255
    or length(p_reason) not between 1 and 255
  then
    raise exception using errcode = '22023', message = 'invalid_stripe_transition';
  end if;

  insert into app_private.stripe_webhook_events (
    event_id, event_type, stripe_created_at, livemode, state, attempt_count
  ) values (
    p_event_id, p_event_type, p_stripe_created_at, p_livemode, 'pending', 1
  ) on conflict (event_id) do update
    set attempt_count = app_private.stripe_webhook_events.attempt_count + 1
    where app_private.stripe_webhook_events.state <> 'processed';

  if (select state from app_private.stripe_webhook_events where event_id = p_event_id) = 'processed' then
    return false;
  end if;

  insert into app_private.entitlements (user_id)
  values (p_user_id)
  on conflict (user_id) do nothing;
  select * into v_entitlement
  from app_private.entitlements
  where user_id = p_user_id
  for update;

  if v_entitlement.stripe_customer_id is not null and v_entitlement.stripe_customer_id <> p_customer_id then
    raise exception using errcode = '23505', message = 'stripe_customer_mapping_mismatch';
  end if;

  if v_entitlement.last_stripe_event_created_at is null
    or p_stripe_created_at >= v_entitlement.last_stripe_event_created_at
  then
    update app_private.entitlements
    set status = p_result_status,
        stripe_customer_id = coalesce(p_customer_id, stripe_customer_id),
        stripe_subscription_id = p_subscription_id,
        stripe_price_id = p_price_id,
        paid_through = p_paid_through,
        grace_started_at = p_grace_started_at,
        grace_ends_at = case when p_grace_started_at is null then null else p_grace_started_at + interval '72 hours' end,
        dispute_suspended = p_dispute_suspended,
        last_stripe_event_created_at = p_stripe_created_at
    where user_id = p_user_id;

    insert into app_private.entitlement_transitions (
      user_id, prior_status, result_status, reason, stripe_event_id,
      stripe_customer_id, stripe_subscription_id
    ) values (
      p_user_id, v_entitlement.status, p_result_status, p_reason, p_event_id,
      p_customer_id, p_subscription_id
    );
  end if;

  update app_private.stripe_webhook_events
  set state = 'processed', processed_at = clock_timestamp(), error_code = null
  where event_id = p_event_id;
  return true;
end;
$$;

create or replace function app_private.replace_recovery_code_batch(
  p_user_id uuid,
  p_batch_id uuid,
  p_code_hmacs bytea[],
  p_pepper_version smallint
)
returns integer
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_count integer;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  v_count := coalesce(array_length(p_code_hmacs, 1), 0);
  if p_batch_id is null or v_count <> 10 or p_pepper_version <= 0
    or exists (select 1 from unnest(p_code_hmacs) as code where octet_length(code) <> 32)
  then
    raise exception using errcode = '22023', message = 'invalid_recovery_code_batch';
  end if;

  update app_private.recovery_codes
  set revoked_at = clock_timestamp()
  where user_id = p_user_id and used_at is null and revoked_at is null;

  insert into app_private.recovery_codes (user_id, batch_id, code_hmac, pepper_version)
  select p_user_id, p_batch_id, code, p_pepper_version
  from unnest(p_code_hmacs) as code;

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded, metadata)
  values (p_user_id, 'recovery.batch_replaced', p_batch_id, true, jsonb_build_object('count', v_count));
  return v_count;
end;
$$;

create or replace function app_private.consume_recovery_code(p_user_id uuid, p_code_hmac bytea)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_code_id uuid;
  v_batch_id uuid;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  update app_private.recovery_codes
  set used_at = clock_timestamp()
  where user_id = p_user_id and code_hmac = p_code_hmac
    and used_at is null and revoked_at is null
  returning id, batch_id into v_code_id, v_batch_id;

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded, metadata)
  values (
    p_user_id, 'recovery.code_consumed', v_batch_id, v_code_id is not null,
    jsonb_build_object('batchId', v_batch_id)
  );
  return v_code_id is not null;
end;
$$;

-- Public, security-invoker RPC wrappers. The privileged implementations remain
-- outside exposed schemas, perform their own caller checks, and expose scalars.
create or replace function public.create_gallery_profile(p_slug text, p_time_zone text default 'UTC')
returns public.profiles
language sql
security invoker
set search_path = ''
as $$ select app_private.create_profile(p_slug, p_time_zone); $$;

create or replace function public.register_verified_identity(
  p_user_id uuid, p_identity_hmac bytea, p_normalization_version smallint, p_hmac_key_version smallint
)
returns void
language sql
security invoker
set search_path = ''
as $$ select app_private.register_verified_identity(p_user_id, p_identity_hmac, p_normalization_version, p_hmac_key_version); $$;

create or replace function public.start_gallery_trial()
returns uuid
language sql
security invoker
set search_path = ''
as $$ select app_private.start_trial(); $$;

create or replace function public.publish_gallery_item(
  p_client_item_id uuid, p_url text, p_thumbnail_url text, p_kind text,
  p_file_name text, p_captured_at timestamptz, p_host text, p_content_type text
)
returns uuid
language sql
security invoker
set search_path = ''
as $$
  select app_private.publish_gallery_item(
    p_client_item_id, p_url, p_thumbnail_url, p_kind,
    p_file_name, p_captured_at, p_host, p_content_type
  );
$$;

create or replace function public.unpublish_gallery_item(p_item_id uuid)
returns uuid
language sql
security invoker
set search_path = ''
as $$ select app_private.unpublish_gallery_item(p_item_id); $$;

create or replace function public.request_gallery_account_deletion(p_idempotency_key uuid)
returns uuid
language sql
security invoker
set search_path = ''
as $$ select app_private.request_account_deletion(p_idempotency_key); $$;

create or replace function public.claim_operations_ledger_events(
  p_worker_id uuid, p_limit integer default 25, p_lease_seconds integer default 300
)
returns table (
  event_id uuid, event_type text, canonical_payload jsonb, payload_sha256 bytea,
  hmac_key_version smallint, attempt_count integer
)
language sql
security invoker
set search_path = ''
as $$ select * from app_private.claim_ledger_events(p_worker_id, p_limit, p_lease_seconds); $$;

create or replace function public.fail_operations_ledger_event(
  p_event_id uuid, p_worker_id uuid, p_error_code text, p_retry_after_seconds integer default 60
)
returns void
language sql
security invoker
set search_path = ''
as $$ select app_private.fail_ledger_event(p_event_id, p_worker_id, p_error_code, p_retry_after_seconds); $$;

create or replace function public.acknowledge_operations_ledger_event(
  p_event_id uuid, p_worker_id uuid, p_object_key text, p_etag text,
  p_payload_sha256 bytea, p_payload_hmac bytea
)
returns void
language sql
security invoker
set search_path = ''
as $$
  select app_private.acknowledge_ledger_event(
    p_event_id, p_worker_id, p_object_key, p_etag, p_payload_sha256, p_payload_hmac
  );
$$;

create or replace function public.apply_stripe_entitlement(
  p_event_id text, p_event_type text, p_stripe_created_at timestamptz,
  p_livemode boolean, p_user_id uuid, p_result_status text, p_reason text,
  p_customer_id text, p_subscription_id text, p_price_id text,
  p_paid_through timestamptz, p_grace_started_at timestamptz,
  p_dispute_suspended boolean
)
returns boolean
language sql
security invoker
set search_path = ''
as $$
  select app_private.apply_stripe_entitlement(
    p_event_id, p_event_type, p_stripe_created_at, p_livemode, p_user_id,
    p_result_status, p_reason, p_customer_id, p_subscription_id, p_price_id,
    p_paid_through, p_grace_started_at, p_dispute_suspended
  );
$$;

create or replace function public.replace_recovery_code_batch(
  p_user_id uuid, p_batch_id uuid, p_code_hmacs bytea[], p_pepper_version smallint
)
returns integer
language sql
security invoker
set search_path = ''
as $$ select app_private.replace_recovery_code_batch(p_user_id, p_batch_id, p_code_hmacs, p_pepper_version); $$;

create or replace function public.consume_recovery_code(p_user_id uuid, p_code_hmac bytea)
returns boolean
language sql
security invoker
set search_path = ''
as $$ select app_private.consume_recovery_code(p_user_id, p_code_hmac); $$;

alter table public.profiles enable row level security;
alter table public.gallery_items enable row level security;

drop policy if exists profiles_owner_select on public.profiles;
create policy profiles_owner_select
on public.profiles for select
to authenticated
using (
  (select auth.uid()) = user_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
);

drop policy if exists profiles_owner_update on public.profiles;
create policy profiles_owner_update
on public.profiles for update
to authenticated
using (
  (select auth.uid()) = user_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
)
with check (
  (select auth.uid()) = user_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
);

drop policy if exists gallery_items_owner_strong_select on public.gallery_items;
create policy gallery_items_owner_strong_select
on public.gallery_items for select
to authenticated
using (
  (select auth.uid()) = owner_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
  and unpublish_pending_at is null
);

revoke all on table public.profiles, public.gallery_items from public, anon, authenticated;
grant select on table public.profiles, public.gallery_items to authenticated;
grant update (slug, time_zone) on table public.profiles to authenticated;

revoke all on all tables in schema app_private from public, anon, authenticated;
revoke all on all sequences in schema app_private from public, anon, authenticated;
revoke execute on all functions in schema app_private from public, anon, authenticated;
revoke execute on all functions in schema public from public, anon, authenticated;

-- authenticated may reach only the private functions behind the four owner RPCs.
grant usage on schema app_private to authenticated, service_role;
grant execute on function app_private.create_profile(text, text) to authenticated;
grant execute on function app_private.start_trial() to authenticated;
grant execute on function app_private.publish_gallery_item(uuid, text, text, text, text, timestamptz, text, text) to authenticated;
grant execute on function app_private.unpublish_gallery_item(uuid) to authenticated;
grant execute on function app_private.request_account_deletion(uuid) to authenticated;
grant execute on function app_private.current_user_aal2(boolean) to authenticated;
grant execute on function app_private.can_publish(uuid) to authenticated;
grant execute on function app_private.derive_title(text) to authenticated;
grant execute on function app_private.normalize_safe_https_url(text) to authenticated;

grant execute on function public.create_gallery_profile(text, text) to authenticated;
grant execute on function public.start_gallery_trial() to authenticated;
grant execute on function public.publish_gallery_item(uuid, text, text, text, text, timestamptz, text, text) to authenticated;
grant execute on function public.unpublish_gallery_item(uuid) to authenticated;
grant execute on function public.request_gallery_account_deletion(uuid) to authenticated;

-- service_role is reserved for verified server jobs and signed webhook handlers.
grant execute on all functions in schema app_private to service_role;
grant execute on function public.register_verified_identity(uuid, bytea, smallint, smallint) to service_role;
grant execute on function public.claim_operations_ledger_events(uuid, integer, integer) to service_role;
grant execute on function public.fail_operations_ledger_event(uuid, uuid, text, integer) to service_role;
grant execute on function public.acknowledge_operations_ledger_event(uuid, uuid, text, text, bytea, bytea) to service_role;
grant execute on function public.apply_stripe_entitlement(text, text, timestamptz, boolean, uuid, text, text, text, text, text, timestamptz, timestamptz, boolean) to service_role;
grant execute on function public.replace_recovery_code_batch(uuid, uuid, bytea[], smallint) to service_role;
grant execute on function public.consume_recovery_code(uuid, bytea) to service_role;

alter default privileges in schema public revoke all on tables from public, anon, authenticated;
alter default privileges in schema public revoke execute on functions from public, anon, authenticated;
alter default privileges in schema app_private revoke all on tables from public, anon, authenticated;
alter default privileges in schema app_private revoke execute on functions from public, anon, authenticated;

comment on schema app_private is 'XIP0085 private schema; never expose through PostgREST.';
comment on table app_private.runtime_controls is 'Operational kill switches default closed in every environment.';
comment on table app_private.operations_ledger_outbox is 'Transactional R2 ledger queue; payload identity is immutable after insert.';
