-- XIP0085 production hardening. Forward-only additions and compatible RPC
-- replacements for identity bootstrap, idempotency, billing reconciliation,
-- session revocation, and deletion processing.

alter table app_private.entitlements
  add column if not exists billing_status text not null default 'none',
  add column if not exists account_state text not null default 'active';

update app_private.entitlements
set billing_status = case
  when status in ('incomplete', 'active', 'past_due', 'unpaid', 'paused', 'canceled') then status
  else 'none'
end
where billing_status = 'none';

alter table app_private.entitlements
  drop constraint if exists entitlements_billing_status_check;
alter table app_private.entitlements
  add constraint entitlements_billing_status_check check (
    billing_status in ('none', 'incomplete', 'active', 'past_due', 'unpaid', 'paused', 'canceled')
  );
alter table app_private.entitlements
  drop constraint if exists entitlements_account_state_check;
alter table app_private.entitlements
  add constraint entitlements_account_state_check check (
    account_state in ('active', 'deletion_pending', 'deleted')
  );

create table if not exists app_private.idempotency_records (
  user_id uuid not null,
  operation text not null,
  idempotency_key text not null,
  request_sha256 bytea not null,
  result_id uuid,
  event_id uuid,
  created_at timestamptz not null default clock_timestamp(),
  expires_at timestamptz not null default (clock_timestamp() + interval '24 hours'),
  primary key (user_id, operation, idempotency_key),
  constraint idempotency_operation_check check (operation in ('publish', 'unpublish')),
  constraint idempotency_key_check check (length(idempotency_key) between 1 and 255),
  constraint idempotency_sha_check check (octet_length(request_sha256) = 32)
);

create index if not exists idempotency_records_expiry_idx
  on app_private.idempotency_records (expires_at);

create table if not exists app_private.stripe_checkout_attempts (
  attempt_id uuid primary key default gen_random_uuid(),
  user_id uuid not null references auth.users(id) on delete cascade,
  plan text not null,
  stripe_idempotency_key text not null unique,
  stripe_session_id text unique,
  state text not null default 'pending',
  created_at timestamptz not null default clock_timestamp(),
  expires_at timestamptz not null default (clock_timestamp() + interval '24 hours'),
  completed_at timestamptz,
  constraint stripe_checkout_plan_check check (plan in ('monthly', 'annual')),
  constraint stripe_checkout_state_check check (state in ('pending', 'completed', 'expired', 'failed'))
);

create unique index if not exists stripe_checkout_one_pending_per_user
  on app_private.stripe_checkout_attempts (user_id)
  where state = 'pending';

create table if not exists app_private.ledger_replay_events (
  event_id uuid primary key,
  event_type text not null,
  object_key text not null unique,
  payload_sha256 bytea not null,
  replayed_at timestamptz not null default clock_timestamp(),
  constraint ledger_replay_type_check check (
    event_type in ('trial_grant_created', 'gallery_item_unpublished', 'account_deleted')
  ),
  constraint ledger_replay_sha_check check (octet_length(payload_sha256) = 32)
);

alter table app_private.account_deletion_requests
  add column if not exists leased_by uuid,
  add column if not exists lease_expires_at timestamptz;

alter table app_private.operations_ledger_outbox
  drop constraint if exists operations_ledger_state_check;
alter table app_private.operations_ledger_outbox
  add constraint operations_ledger_state_check check (
    state in ('pending', 'leased', 'replicated', 'failed', 'dead_letter')
  );

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
  set state = case when attempt_count >= 8 then 'dead_letter' else 'failed' end,
      leased_by = null, lease_expires_at = null,
      last_error_code = p_error_code,
      next_attempt_at = clock_timestamp() + make_interval(secs => p_retry_after_seconds)
  where event_id = p_event_id and state = 'leased' and leased_by = p_worker_id;
  if not found then
    raise exception using errcode = '55000', message = 'ledger_lease_not_owned';
  end if;
end;
$$;

create or replace function app_private.current_session_active(p_user_id uuid)
returns boolean
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_session_id uuid;
begin
  begin
    v_session_id := nullif(auth.jwt() ->> 'session_id', '')::uuid;
  exception when invalid_text_representation then
    return false;
  end;
  return v_session_id is not null and exists (
    select 1 from auth.sessions as session
    where session.id = v_session_id and session.user_id = p_user_id
  );
end;
$$;

create or replace function app_private.current_account_active(p_user_id uuid)
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select coalesce((
    select entitlement.account_state = 'active'
    from app_private.entitlements as entitlement
    where entitlement.user_id = p_user_id
  ), true);
$$;

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
  if not app_private.current_session_active(v_user_id) then
    raise exception using errcode = '42501', message = 'session_revoked';
  end if;
  if not app_private.current_account_active(v_user_id) then
    raise exception using errcode = '42501', message = 'account_deletion_pending';
  end if;
  if not exists (
    select 1 from auth.users as u
    where u.id = v_user_id
      and u.email_confirmed_at is not null
      and (u.banned_until is null or u.banned_until <= clock_timestamp())
  ) then
    raise exception using errcode = '42501', message = 'verified_email_required';
  end if;
  if p_require_recent then
    select coalesce(bool_or(
      factor ->> 'method' in ('totp', 'mfa', 'mfa/totp', 'mfa/webauthn')
      and to_timestamp((factor ->> 'timestamp')::double precision)
        >= clock_timestamp() - interval '10 minutes'
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
      and entitlement.account_state = 'active'
      and not entitlement.dispute_suspended
      and (
        (entitlement.status = 'trial_active' and clock_timestamp() < entitlement.trial_ends_at)
        or (entitlement.billing_status = 'active' and entitlement.paid_through > clock_timestamp())
        or (
          entitlement.billing_status = 'past_due'
          and entitlement.grace_ends_at >= clock_timestamp()
        )
      )
    from app_private.entitlements as entitlement
    cross join app_private.runtime_controls as controls
    where entitlement.user_id = p_user_id and controls.singleton
  ), false);
$$;

create or replace function public.current_request_account_active()
returns boolean
language sql
stable
security definer
set search_path = ''
as $$
  select auth.uid() is not null
    and app_private.current_session_active(auth.uid())
    and app_private.current_account_active(auth.uid());
$$;

drop policy if exists profiles_owner_select on public.profiles;
create policy profiles_owner_select on public.profiles for select to authenticated
using (
  (select auth.uid()) = user_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
  and public.current_request_account_active()
);

drop policy if exists profiles_owner_update on public.profiles;
create policy profiles_owner_update on public.profiles for update to authenticated
using (
  (select auth.uid()) = user_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
  and public.current_request_account_active()
)
with check (
  (select auth.uid()) = user_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
  and public.current_request_account_active()
);

drop policy if exists gallery_items_owner_strong_select on public.gallery_items;
create policy gallery_items_owner_strong_select on public.gallery_items for select to authenticated
using (
  (select auth.uid()) = owner_id
  and (select auth.jwt() ->> 'aal') = 'aal2'
  and public.current_request_account_active()
  and unpublish_pending_at is null
);

create or replace function public.publish_gallery_item(
  p_client_item_id uuid,
  p_url text,
  p_thumbnail_url text,
  p_kind text,
  p_file_name text,
  p_title text,
  p_captured_at timestamptz,
  p_host text,
  p_content_type text,
  p_idempotency_key text
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_item_id uuid;
  v_item public.gallery_items;
  v_request_sha bytea;
  v_record app_private.idempotency_records;
begin
  if p_idempotency_key is null or length(p_idempotency_key) not between 1 and 255 then
    raise exception using errcode = '22023', message = 'invalid_idempotency_key';
  end if;
  if p_title is distinct from app_private.derive_title(p_file_name) then
    raise exception using errcode = '22023', message = 'title_mismatch';
  end if;
  perform pg_advisory_xact_lock(hashtextextended(
    v_user_id::text || ':publish:' || p_idempotency_key, 0
  ));
  v_request_sha := extensions.digest(convert_to(jsonb_build_object(
    'clientItemId', p_client_item_id, 'url', p_url, 'thumbnailUrl', p_thumbnail_url,
    'kind', p_kind, 'fileName', p_file_name, 'capturedAt', p_captured_at,
    'host', p_host, 'contentType', p_content_type
  )::text, 'UTF8'), 'sha256');
  select * into v_record from app_private.idempotency_records
  where user_id = v_user_id and operation = 'publish'
    and idempotency_key = p_idempotency_key and expires_at > clock_timestamp()
  for update;
  if found then
    if v_record.request_sha256 <> v_request_sha then
      raise exception using errcode = '23505', message = 'idempotency_key_reused';
    end if;
    select * into v_item from public.gallery_items
    where id = v_record.result_id and owner_id = v_user_id and unpublish_pending_at is null;
    if found then
      return app_private.gallery_item_json(v_item);
    end if;
    delete from app_private.idempotency_records
    where user_id = v_user_id and operation = 'publish' and idempotency_key = p_idempotency_key;
  end if;
  v_item_id := app_private.publish_gallery_item(
    p_client_item_id, p_url, p_thumbnail_url, p_kind, p_file_name,
    p_captured_at, p_host, p_content_type
  );
  select * into strict v_item from public.gallery_items
  where id = v_item_id and owner_id = v_user_id and unpublish_pending_at is null;
  insert into app_private.idempotency_records (
    user_id, operation, idempotency_key, request_sha256, result_id
  ) values (v_user_id, 'publish', p_idempotency_key, v_request_sha, v_item_id)
  on conflict (user_id, operation, idempotency_key) do update
    set request_sha256 = excluded.request_sha256, result_id = excluded.result_id,
        event_id = null, created_at = clock_timestamp(),
        expires_at = clock_timestamp() + interval '24 hours';
  return app_private.gallery_item_json(v_item);
end;
$$;

create or replace function public.request_gallery_item_unpublish(
  p_client_item_id uuid,
  p_idempotency_key text
)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_item_id uuid;
  v_event_id uuid;
  v_replicated boolean;
  v_request_sha bytea;
  v_record app_private.idempotency_records;
begin
  if p_idempotency_key is null or length(p_idempotency_key) not between 1 and 255 then
    raise exception using errcode = '22023', message = 'invalid_idempotency_key';
  end if;
  perform pg_advisory_xact_lock(hashtextextended(
    v_user_id::text || ':unpublish:' || p_idempotency_key, 0
  ));
  v_request_sha := extensions.digest(convert_to(
    jsonb_build_object('clientItemId', p_client_item_id)::text, 'UTF8'
  ), 'sha256');
  select * into v_record from app_private.idempotency_records
  where user_id = v_user_id and operation = 'unpublish'
    and idempotency_key = p_idempotency_key and expires_at > clock_timestamp()
  for update;
  if found then
    if v_record.request_sha256 <> v_request_sha then
      raise exception using errcode = '23505', message = 'idempotency_key_reused';
    end if;
    if v_record.event_id is null then
      return jsonb_build_object('operationId', null, 'replicated', true);
    end if;
    select state = 'replicated' into v_replicated
    from app_private.operations_ledger_outbox where event_id = v_record.event_id;
    return jsonb_build_object(
      'operationId', v_record.event_id,
      'replicated', coalesce(v_replicated, false)
    );
  end if;
  select id, unpublish_event_id into v_item_id, v_event_id
  from public.gallery_items
  where owner_id = v_user_id and client_item_id = p_client_item_id
  for update;
  if v_item_id is not null and v_event_id is null then
    v_event_id := app_private.unpublish_gallery_item(v_item_id);
  end if;
  insert into app_private.idempotency_records (
    user_id, operation, idempotency_key, request_sha256, result_id, event_id
  ) values (v_user_id, 'unpublish', p_idempotency_key, v_request_sha, v_item_id, v_event_id);
  if v_event_id is null then
    return jsonb_build_object('operationId', null, 'replicated', true);
  end if;
  select state = 'replicated' into v_replicated
  from app_private.operations_ledger_outbox where event_id = v_event_id;
  return jsonb_build_object('operationId', v_event_id, 'replicated', coalesce(v_replicated, false));
end;
$$;

create or replace function public.prepare_my_stripe_checkout(p_plan text)
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
  v_entitlement app_private.entitlements;
  v_attempt app_private.stripe_checkout_attempts;
begin
  if p_plan not in ('monthly', 'annual') then
    raise exception using errcode = '22023', message = 'invalid_billing_plan';
  end if;
  if not (select allow_checkout from app_private.runtime_controls where singleton) then
    raise exception using errcode = '55000', message = 'checkout_disabled';
  end if;
  perform pg_advisory_xact_lock(hashtextextended(v_user_id::text || ':checkout', 0));
  insert into app_private.entitlements (user_id) values (v_user_id)
  on conflict (user_id) do nothing;
  select * into v_entitlement from app_private.entitlements
  where user_id = v_user_id for update;
  if v_entitlement.stripe_subscription_id is not null
    and v_entitlement.billing_status in ('incomplete', 'active', 'past_due', 'unpaid', 'paused')
  then
    raise exception using errcode = '23505', message = 'subscription_already_exists';
  end if;
  update app_private.stripe_checkout_attempts
  set state = 'expired'
  where user_id = v_user_id and state = 'pending' and expires_at <= clock_timestamp();
  select * into v_attempt from app_private.stripe_checkout_attempts
  where user_id = v_user_id and state = 'pending' for update;
  if not found then
    insert into app_private.stripe_checkout_attempts (
      user_id, plan, stripe_idempotency_key
    ) values (
      v_user_id, p_plan, 'xerahs-checkout-' || gen_random_uuid()::text
    ) returning * into v_attempt;
  elsif v_attempt.plan <> p_plan then
    raise exception using errcode = '23505', message = 'checkout_already_pending';
  end if;
  return jsonb_build_object(
    'attemptId', v_attempt.attempt_id,
    'customerId', v_entitlement.stripe_customer_id,
    'customerIdempotencyKey', 'xerahs-customer-' || v_user_id::text,
    'checkoutIdempotencyKey', v_attempt.stripe_idempotency_key
  );
end;
$$;

create or replace function public.finalize_my_stripe_checkout(
  p_attempt_id uuid,
  p_session_id text
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
begin
  if p_session_id !~ '^cs_(test_|live_)?[A-Za-z0-9_]{8,255}$' then
    raise exception using errcode = '22023', message = 'invalid_checkout_session';
  end if;
  update app_private.stripe_checkout_attempts
  set stripe_session_id = p_session_id
  where attempt_id = p_attempt_id and user_id = v_user_id and state = 'pending'
    and (stripe_session_id is null or stripe_session_id = p_session_id);
  if not found then
    raise exception using errcode = '55000', message = 'checkout_attempt_not_pending';
  end if;
end;
$$;

create or replace function public.record_stripe_checkout_event(
  p_event_id text,
  p_session_id text,
  p_user_id uuid,
  p_attempt_id uuid,
  p_plan text,
  p_event_type text,
  p_created_at timestamptz,
  p_livemode boolean
)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_state text;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_event_type not in (
    'checkout.session.completed', 'checkout.session.expired',
    'checkout.session.async_payment_succeeded', 'checkout.session.async_payment_failed'
  ) then
    raise exception using errcode = '22023', message = 'invalid_checkout_event';
  end if;
  v_state := case
    when p_event_type in ('checkout.session.completed', 'checkout.session.async_payment_succeeded') then 'completed'
    when p_event_type = 'checkout.session.expired' then 'expired'
    else 'failed'
  end;
  insert into app_private.stripe_webhook_events (
    event_id, event_type, stripe_created_at, livemode, state, attempt_count, processed_at
  ) values (p_event_id, p_event_type, p_created_at, p_livemode, 'processed', 1, clock_timestamp())
  on conflict (event_id) do update
    set attempt_count = app_private.stripe_webhook_events.attempt_count + 1,
        state = 'pending', processed_at = null, error_code = null
    where app_private.stripe_webhook_events.state <> 'processed';
  if not found then return false; end if;
  update app_private.stripe_checkout_attempts
  set stripe_session_id = coalesce(stripe_session_id, p_session_id),
      state = v_state,
      completed_at = case when v_state = 'completed' then clock_timestamp() else null end
  where attempt_id = p_attempt_id and user_id = p_user_id and plan = p_plan
    and (stripe_session_id is null or stripe_session_id = p_session_id);
  if not found then
    raise exception using errcode = '55000', message = 'checkout_attempt_not_found';
  end if;
  return true;
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
  v_dispute boolean;
  v_grace_started timestamptz;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_result_status not in ('incomplete', 'active', 'past_due', 'unpaid', 'paused', 'canceled')
    or length(p_event_id) not between 1 and 255
    or length(p_event_type) not between 1 and 255
    or length(p_reason) not between 1 and 255
  then
    raise exception using errcode = '22023', message = 'invalid_stripe_transition';
  end if;
  insert into app_private.stripe_webhook_events (
    event_id, event_type, stripe_created_at, livemode, state, attempt_count
  ) values (p_event_id, p_event_type, p_stripe_created_at, p_livemode, 'pending', 1)
  on conflict (event_id) do update
    set attempt_count = app_private.stripe_webhook_events.attempt_count + 1
    where app_private.stripe_webhook_events.state <> 'processed';
  if (select state from app_private.stripe_webhook_events where event_id = p_event_id) = 'processed' then
    return false;
  end if;
  insert into app_private.entitlements (user_id) values (p_user_id)
  on conflict (user_id) do nothing;
  select * into v_entitlement from app_private.entitlements
  where user_id = p_user_id for update;
  if v_entitlement.stripe_customer_id is not null
    and p_customer_id is not null
    and v_entitlement.stripe_customer_id <> p_customer_id
  then
    raise exception using errcode = '23505', message = 'stripe_customer_mapping_mismatch';
  end if;
  -- Callers retrieve the current canonical Subscription before invoking this
  -- function, so even an older delivery can safely repair the snapshot.
  v_dispute := v_entitlement.dispute_suspended or p_dispute_suspended;
  v_grace_started := case
    when p_result_status = 'past_due' then coalesce(v_entitlement.grace_started_at, p_grace_started_at)
    else null
  end;
  update app_private.entitlements
  set status = case when v_dispute then 'dispute_suspended' else p_result_status end,
      billing_status = p_result_status,
      stripe_customer_id = coalesce(p_customer_id, stripe_customer_id),
      stripe_subscription_id = p_subscription_id,
      stripe_price_id = p_price_id,
      paid_through = p_paid_through,
      grace_started_at = v_grace_started,
      grace_ends_at = case when v_grace_started is null then null else v_grace_started + interval '72 hours' end,
      dispute_suspended = v_dispute,
      last_stripe_event_created_at = greatest(
        coalesce(last_stripe_event_created_at, '-infinity'::timestamptz), p_stripe_created_at
      )
  where user_id = p_user_id;
  insert into app_private.entitlement_transitions (
    user_id, prior_status, result_status, reason, stripe_event_id,
    stripe_customer_id, stripe_subscription_id
  ) values (
    p_user_id, v_entitlement.status,
    case when v_dispute then 'dispute_suspended' else p_result_status end,
    p_reason, p_event_id, p_customer_id, p_subscription_id
  );
  update app_private.stripe_webhook_events
  set state = 'processed', processed_at = clock_timestamp(), error_code = null
  where event_id = p_event_id;
  return true;
end;
$$;

create or replace function public.apply_stripe_dispute(
  p_event_id text,
  p_event_type text,
  p_created_at timestamptz,
  p_livemode boolean,
  p_customer_id text,
  p_suspended boolean
)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid;
  v_prior_status text;
  v_result_status text;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  v_user_id := public.resolve_stripe_webhook_owner(p_customer_id, null);
  insert into app_private.stripe_webhook_events (
    event_id, event_type, stripe_created_at, livemode, state, attempt_count
  ) values (p_event_id, p_event_type, p_created_at, p_livemode, 'pending', 1)
  on conflict (event_id) do update
    set attempt_count = app_private.stripe_webhook_events.attempt_count + 1,
        state = 'pending', processed_at = null, error_code = null
    where app_private.stripe_webhook_events.state <> 'processed';
  if not found then return false; end if;
  select status into v_prior_status from app_private.entitlements
  where user_id = v_user_id for update;
  update app_private.entitlements
  set dispute_suspended = p_suspended,
      status = case when p_suspended then 'dispute_suspended' else billing_status end,
      last_stripe_event_created_at = greatest(
        coalesce(last_stripe_event_created_at, '-infinity'::timestamptz), p_created_at
      )
  where user_id = v_user_id
  returning status into v_result_status;
  insert into app_private.entitlement_transitions (
    user_id, prior_status, result_status, reason, stripe_event_id, stripe_customer_id
  ) values (
    v_user_id, v_prior_status, v_result_status,
    case when p_suspended then 'stripe_dispute_suspended' else 'stripe_dispute_won' end,
    p_event_id, p_customer_id
  );
  update app_private.stripe_webhook_events
  set state = 'processed', processed_at = clock_timestamp()
  where event_id = p_event_id;
  return true;
end;
$$;

create or replace function public.record_stripe_webhook_failure(
  p_event_id text,
  p_event_type text,
  p_created_at timestamptz,
  p_livemode boolean,
  p_error_code text
)
returns text
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_state text;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_error_code !~ '^[A-Z0-9_:-]{1,64}$' then
    raise exception using errcode = '22023', message = 'invalid_webhook_failure';
  end if;
  insert into app_private.stripe_webhook_events (
    event_id, event_type, stripe_created_at, livemode, state, attempt_count, error_code
  ) values (p_event_id, p_event_type, p_created_at, p_livemode, 'failed', 1, p_error_code)
  on conflict (event_id) do update
    set attempt_count = app_private.stripe_webhook_events.attempt_count + 1,
        state = case when app_private.stripe_webhook_events.attempt_count + 1 >= 8
          then 'dead_letter' else 'failed' end,
        error_code = excluded.error_code
    where app_private.stripe_webhook_events.state <> 'processed'
  returning state into v_state;
  return coalesce(v_state, 'processed');
end;
$$;

create or replace function public.list_stripe_reconciliation_targets(p_limit integer default 100)
returns table (
  user_id uuid,
  stripe_customer_id text,
  stripe_subscription_id text
)
language plpgsql
security definer
set search_path = ''
as $$
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_limit not between 1 and 500 then
    raise exception using errcode = '22023', message = 'invalid_reconciliation_limit';
  end if;
  return query select entitlement.user_id, entitlement.stripe_customer_id,
    entitlement.stripe_subscription_id
  from app_private.entitlements as entitlement
  where entitlement.stripe_subscription_id is not null
    and entitlement.account_state <> 'deleted'
  order by entitlement.updated_at
  limit p_limit;
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
  select * into v_request from app_private.account_deletion_requests
  where user_id = v_user_id and idempotency_key = p_idempotency_key for update;
  if found then return v_request.ledger_event_id; end if;
  select * into v_entitlement from app_private.entitlements
  where user_id = v_user_id for update;
  v_hmac_version := (select ledger_hmac_active_version from app_private.runtime_controls where singleton);
  v_payload := jsonb_build_object(
    'schemaVersion', 1, 'eventId', v_event_id, 'eventType', 'account_deleted',
    'occurredAt', v_occurred_at, 'userId', v_user_id
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
  update app_private.entitlements set account_state = 'deletion_pending'
  where user_id = v_user_id;
  delete from auth.sessions where user_id = v_user_id;
  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'account.deletion_requested', v_request.id, true);
  return v_event_id;
end;
$$;

create or replace function public.claim_account_deletions(
  p_worker_id uuid,
  p_limit integer default 25,
  p_lease_seconds integer default 300
)
returns table (
  request_id uuid,
  user_id uuid,
  state text,
  stripe_customer_id text,
  stripe_subscription_id text,
  tombstoned_at timestamptz,
  billing_cancelled_at timestamptz
)
language plpgsql
security definer
set search_path = ''
as $$
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  return query
  with candidates as (
    select request.id from app_private.account_deletion_requests as request
    where request.state in ('pending', 'canceling', 'tombstoned', 'deleting', 'failed')
      and coalesce(request.next_attempt_at, request.created_at) <= clock_timestamp()
      and (request.lease_expires_at is null or request.lease_expires_at <= clock_timestamp())
    order by coalesce(request.next_attempt_at, request.created_at), request.id
    for update skip locked limit greatest(1, least(p_limit, 100))
  ), claimed as (
    update app_private.account_deletion_requests as request
    set leased_by = p_worker_id,
        lease_expires_at = clock_timestamp() + make_interval(secs => greatest(30, least(p_lease_seconds, 900))),
        attempt_count = request.attempt_count + 1,
        state = case when request.billing_cancelled_at is null then 'canceling' else request.state end
    from candidates where request.id = candidates.id returning request.*
  )
  select claimed.id, claimed.user_id, claimed.state, claimed.stripe_customer_id,
    claimed.stripe_subscription_id, claimed.tombstoned_at, claimed.billing_cancelled_at
  from claimed;
end;
$$;

create or replace function public.mark_account_deletion_billing_cancelled(
  p_request_id uuid,
  p_worker_id uuid
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
  update app_private.account_deletion_requests
  set billing_cancelled_at = coalesce(billing_cancelled_at, clock_timestamp()),
      state = case when tombstoned_at is null then 'pending' else 'deleting' end,
      leased_by = null, lease_expires_at = null, next_attempt_at = clock_timestamp()
  where id = p_request_id and leased_by = p_worker_id;
  if not found then raise exception using errcode = '55000', message = 'deletion_lease_not_owned'; end if;
end;
$$;

create or replace function public.fail_account_deletion(
  p_request_id uuid,
  p_worker_id uuid,
  p_error_code text
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
  if p_error_code !~ '^[A-Z0-9_:-]{1,64}$' then
    raise exception using errcode = '22023', message = 'invalid_deletion_failure';
  end if;
  update app_private.account_deletion_requests
  set state = 'failed', last_error_code = p_error_code,
      next_attempt_at = clock_timestamp() + least(interval '1 hour', make_interval(secs => 60 * greatest(1, attempt_count))),
      leased_by = null, lease_expires_at = null
  where id = p_request_id and leased_by = p_worker_id;
end;
$$;

create or replace function public.defer_account_deletion(
  p_request_id uuid,
  p_worker_id uuid,
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
  if p_retry_after_seconds not between 30 and 3600 then
    raise exception using errcode = '22023', message = 'invalid_deletion_defer';
  end if;
  update app_private.account_deletion_requests
  set state = case when tombstoned_at is null then 'pending' else 'deleting' end,
      next_attempt_at = clock_timestamp() + make_interval(secs => p_retry_after_seconds),
      leased_by = null, lease_expires_at = null
  where id = p_request_id and leased_by = p_worker_id;
  if not found then raise exception using errcode = '55000', message = 'deletion_lease_not_owned'; end if;
end;
$$;

create or replace function public.complete_account_deletion(
  p_request_id uuid,
  p_worker_id uuid
)
returns void
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  select user_id into v_user_id
  from app_private.account_deletion_requests
  where id = p_request_id and leased_by = p_worker_id
    and billing_cancelled_at is not null and tombstoned_at is not null
  for update;
  if not found then raise exception using errcode = '55000', message = 'deletion_not_ready'; end if;
  delete from auth.users where id = v_user_id;
  update app_private.account_deletion_requests
  set state = 'completed', completed_at = clock_timestamp(),
      leased_by = null, lease_expires_at = null, last_error_code = null
  where id = p_request_id;
end;
$$;

create or replace function public.replay_operations_ledger_event(
  p_object_key text,
  p_payload jsonb,
  p_payload_sha256 bytea
)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_event_id uuid;
  v_event_type text;
  v_user_id uuid;
  v_item_id uuid;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  v_event_id := (p_payload ->> 'eventId')::uuid;
  v_event_type := p_payload ->> 'eventType';
  v_user_id := (p_payload ->> 'userId')::uuid;
  if p_object_key !~ ('^(trial-grants|deletions)/v1/[0-9]{4}/[0-9]{2}/[0-9]{2}/' || v_event_id::text || '\.json$')
    or v_event_type not in ('trial_grant_created', 'gallery_item_unpublished', 'account_deleted')
    or (p_payload ->> 'schemaVersion')::integer <> 1
    or octet_length(p_payload_sha256) <> 32
  then
    raise exception using errcode = '22023', message = 'invalid_ledger_replay';
  end if;
  insert into app_private.ledger_replay_events (
    event_id, event_type, object_key, payload_sha256
  ) values (v_event_id, v_event_type, p_object_key, p_payload_sha256)
  on conflict (event_id) do nothing;
  if not found then return false; end if;
  if v_event_type = 'gallery_item_unpublished' then
    v_item_id := (p_payload ->> 'itemId')::uuid;
    delete from public.gallery_items where id = v_item_id and owner_id = v_user_id;
  elsif v_event_type = 'account_deleted' then
    delete from auth.sessions where user_id = v_user_id;
    delete from auth.users where id = v_user_id;
  elsif v_event_type = 'trial_grant_created' then
    insert into app_private.trial_grants (
      identity_hmac, original_user_id, normalization_version, hmac_key_version,
      ledger_event_id, granted_at, ends_at
    ) values (
      decode(p_payload ->> 'identityHmac', 'hex'), v_user_id,
      (p_payload ->> 'identityNormalizationVersion')::smallint,
      (p_payload ->> 'identityHmacKeyVersion')::smallint,
      v_event_id, (p_payload ->> 'occurredAt')::timestamptz,
      (p_payload ->> 'occurredAt')::timestamptz + interval '7 days'
    ) on conflict (identity_hmac) do nothing;
  end if;
  return true;
end;
$$;

-- Remove obsolete owner-callable implementations and retain only reviewed
-- public RPC contracts. SECURITY DEFINER wrappers perform their own caller checks.
revoke execute on all functions in schema app_private from public, anon, authenticated;
revoke usage on schema app_private from public, anon, authenticated;
revoke execute on function public.start_gallery_trial() from authenticated;
revoke execute on function public.publish_gallery_item(uuid, text, text, text, text, timestamptz, text, text) from authenticated;
revoke execute on function public.unpublish_gallery_item(uuid) from authenticated;
revoke execute on function public.current_request_account_active() from public, anon;

create or replace function public.create_gallery_profile(p_slug text, p_time_zone text default 'UTC')
returns public.profiles
language sql
security definer
set search_path = ''
as $$ select app_private.create_profile(p_slug, p_time_zone); $$;

create or replace function public.request_gallery_account_deletion(p_idempotency_key uuid)
returns uuid
language sql
security definer
set search_path = ''
as $$ select app_private.request_account_deletion(p_idempotency_key); $$;

revoke execute on function public.finalize_my_stripe_checkout(uuid, text) from public, anon;
revoke execute on function public.record_stripe_checkout_event(text, text, uuid, uuid, text, text, timestamptz, boolean) from public, anon, authenticated;
revoke execute on function public.record_stripe_webhook_failure(text, text, timestamptz, boolean, text) from public, anon, authenticated;
revoke execute on function public.list_stripe_reconciliation_targets(integer) from public, anon, authenticated;
revoke execute on function public.claim_account_deletions(uuid, integer, integer) from public, anon, authenticated;
revoke execute on function public.mark_account_deletion_billing_cancelled(uuid, uuid) from public, anon, authenticated;
revoke execute on function public.fail_account_deletion(uuid, uuid, text) from public, anon, authenticated;
revoke execute on function public.defer_account_deletion(uuid, uuid, integer) from public, anon, authenticated;
revoke execute on function public.complete_account_deletion(uuid, uuid) from public, anon, authenticated;
revoke execute on function public.replay_operations_ledger_event(text, jsonb, bytea) from public, anon, authenticated;

grant execute on function public.create_gallery_profile(text, text) to authenticated;
grant execute on function public.current_request_account_active() to authenticated;
grant execute on function public.request_gallery_account_deletion(uuid) to authenticated;
grant execute on function public.publish_gallery_item(uuid, text, text, text, text, text, timestamptz, text, text, text) to authenticated;
grant execute on function public.request_gallery_item_unpublish(uuid, text) to authenticated;
grant execute on function public.prepare_my_stripe_checkout(text) to authenticated;
grant execute on function public.finalize_my_stripe_checkout(uuid, text) to authenticated;

grant execute on function public.record_stripe_checkout_event(text, text, uuid, uuid, text, text, timestamptz, boolean) to service_role;
grant execute on function public.record_stripe_webhook_failure(text, text, timestamptz, boolean, text) to service_role;
grant execute on function public.list_stripe_reconciliation_targets(integer) to service_role;
grant execute on function public.claim_account_deletions(uuid, integer, integer) to service_role;
grant execute on function public.mark_account_deletion_billing_cancelled(uuid, uuid) to service_role;
grant execute on function public.fail_account_deletion(uuid, uuid, text) to service_role;
grant execute on function public.defer_account_deletion(uuid, uuid, integer) to service_role;
grant execute on function public.complete_account_deletion(uuid, uuid) to service_role;
grant execute on function public.replay_operations_ledger_event(text, jsonb, bytea) to service_role;

revoke all on app_private.idempotency_records,
  app_private.stripe_checkout_attempts,
  app_private.ledger_replay_events
from public, anon, authenticated;

comment on table app_private.idempotency_records is 'Persistent bounded owner mutation idempotency records.';
comment on table app_private.stripe_checkout_attempts is 'One pending subscription Checkout guard per user.';
comment on table app_private.ledger_replay_events is 'Deduplication and high-water evidence for verified R2 replay.';
