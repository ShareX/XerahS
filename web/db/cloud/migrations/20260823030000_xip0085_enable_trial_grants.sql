-- Launch XIP0085 cloud controls and activate the trial as soon as it is
-- granted. Kill switches remain UPDATE-able without a migration. Ledger
-- replication stays asynchronous and must not block the owner's 7-day access.

update app_private.runtime_controls
set
  allow_trial_grants = true,
  allow_checkout = true,
  allow_publish = true,
  updated_at = clock_timestamp()
where singleton;

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
  set status = 'trial_active', trial_started_at = v_started_at,
      trial_ends_at = v_started_at + interval '7 days', trial_ledger_event_id = v_event_id
  where user_id = v_user_id;

  insert into app_private.entitlement_transitions (
    user_id, prior_status, result_status, reason
  ) values (v_user_id, v_entitlement.status, 'trial_active', 'trial_requested');

  insert into app_private.audit_events (actor_user_id, action, target_id, succeeded)
  values (v_user_id, 'trial.requested', v_event_id, true);

  return v_event_id;
end;
$$;

create or replace function public.start_my_trial()
returns jsonb
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
  v_event_id uuid;
  v_entitlement app_private.entitlements;
  v_replicated boolean;
begin
  v_event_id := app_private.start_trial();
  select * into v_entitlement from app_private.entitlements where user_id = v_user_id;
  select state = 'replicated' into v_replicated
  from app_private.operations_ledger_outbox where event_id = v_event_id;
  return jsonb_build_object(
    'operationId', v_event_id,
    'replicated', coalesce(v_replicated, false),
    'status', case
      when v_entitlement.status in ('trial_active', 'active') then 'active'
      else 'trial_pending'
    end,
    'endsAt', v_entitlement.trial_ends_at
  );
end;
$$;
