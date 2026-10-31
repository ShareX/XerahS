-- XIP0085 web/API contract. These wrappers keep JSON casing and route semantics
-- stable while the underlying transactional functions remain private.

create or replace function app_private.gallery_item_json(p_item public.gallery_items)
returns jsonb
language sql
immutable
security invoker
set search_path = ''
as $$
  select jsonb_build_object(
    'id', (p_item).id,
    'clientItemId', (p_item).client_item_id,
    'url', (p_item).url,
    'thumbnailUrl', (p_item).thumbnail_url,
    'kind', (p_item).kind,
    'fileName', (p_item).file_name,
    'title', (p_item).title,
    'capturedAt', (p_item).captured_at,
    'publishedAt', (p_item).published_at,
    'host', (p_item).host,
    'contentType', (p_item).content_type
  );
$$;

create or replace function app_private.opaque_item_cursor(p_item_id uuid)
returns text
language sql
immutable
strict
security invoker
set search_path = ''
as $$
  select replace(replace(rtrim(encode(convert_to(p_item_id::text, 'UTF8'), 'base64'), '='), '+', '-'), '/', '_');
$$;

create or replace function app_private.decode_item_cursor(p_cursor text)
returns uuid
language plpgsql
immutable
strict
security invoker
set search_path = ''
as $$
declare
  v_base64 text := translate(p_cursor, '-_', '+/');
begin
  v_base64 := v_base64 || repeat('=', (4 - length(v_base64) % 4) % 4);
  return convert_from(decode(v_base64, 'base64'), 'UTF8')::uuid;
exception when others then
  raise exception using errcode = '22023', message = 'invalid_gallery_cursor';
end;
$$;

create or replace function public.get_my_account_summary()
returns jsonb
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_profile public.profiles;
  v_entitlement app_private.entitlements;
  v_trial_status text;
begin
  select * into v_profile from public.profiles where user_id = v_user_id;
  if not found then
    raise exception using errcode = 'P0002', message = 'profile_not_found';
  end if;

  select * into v_entitlement from app_private.entitlements where user_id = v_user_id;
  v_trial_status := case
    when v_entitlement.trial_started_at is null then 'not_started'
    when v_entitlement.status = 'trial_pending' then 'trial_pending'
    when v_entitlement.trial_ends_at > clock_timestamp() then 'active'
    else 'expired'
  end;

  return jsonb_build_object(
    'slug', v_profile.slug::text,
    'timeZone', v_profile.time_zone,
    'strongAuth', true,
    'trialStatus', v_trial_status,
    'trialEndsAt', v_entitlement.trial_ends_at,
    'subscriptionStatus', case when v_entitlement.stripe_subscription_id is null then null else v_entitlement.status end,
    'paidThrough', v_entitlement.paid_through,
    'canPublish', app_private.can_publish(v_user_id),
    'disputeSuspended', coalesce(v_entitlement.dispute_suspended, false)
  );
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
    'status', case when coalesce(v_replicated, false) then 'active' else 'trial_pending' end,
    'endsAt', v_entitlement.trial_ends_at
  );
end;
$$;

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
begin
  if p_idempotency_key is null or length(p_idempotency_key) not between 1 and 255 then
    raise exception using errcode = '22023', message = 'invalid_idempotency_key';
  end if;
  -- The server-derived title is authoritative; the client value is checked only
  -- to detect a stale desktop/API contract and is never persisted directly.
  if p_title is distinct from app_private.derive_title(p_file_name) then
    raise exception using errcode = '22023', message = 'title_mismatch';
  end if;

  v_item_id := app_private.publish_gallery_item(
    p_client_item_id, p_url, p_thumbnail_url, p_kind, p_file_name,
    p_captured_at, p_host, p_content_type
  );
  select * into strict v_item
  from public.gallery_items
  where id = v_item_id and owner_id = v_user_id and unpublish_pending_at is null;
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
begin
  if p_idempotency_key is null or length(p_idempotency_key) not between 1 and 255 then
    raise exception using errcode = '22023', message = 'invalid_idempotency_key';
  end if;
  select id, unpublish_event_id into v_item_id, v_event_id
  from public.gallery_items
  where owner_id = v_user_id and client_item_id = p_client_item_id
  for update;

  if v_item_id is null then
    return jsonb_build_object('operationId', null, 'replicated', true);
  end if;
  if v_event_id is null then
    v_event_id := app_private.unpublish_gallery_item(v_item_id);
  end if;
  select state = 'replicated' into v_replicated
  from app_private.operations_ledger_outbox where event_id = v_event_id;
  return jsonb_build_object(
    'operationId', v_event_id,
    'replicated', coalesce(v_replicated, false)
  );
end;
$$;

create or replace function public.list_my_gallery_items(
  p_cursor text default null,
  p_limit integer default 50,
  p_kind text default null,
  p_from timestamptz default null,
  p_to timestamptz default null
)
returns jsonb
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_cursor_id uuid;
  v_cursor_at timestamptz;
  v_items jsonb;
  v_next_cursor text;
begin
  if p_limit not between 1 and 50 or (p_kind is not null and p_kind not in ('screenshot', 'screencast'))
    or (p_from is not null and p_to is not null and p_from > p_to)
  then
    raise exception using errcode = '22023', message = 'invalid_gallery_query';
  end if;

  if p_cursor is not null then
    v_cursor_id := app_private.decode_item_cursor(p_cursor);
    select captured_at into v_cursor_at
    from public.gallery_items
    where id = v_cursor_id and owner_id = v_user_id and unpublish_pending_at is null;
    if v_cursor_at is null then
      raise exception using errcode = '22023', message = 'invalid_gallery_cursor';
    end if;
  end if;

  with page as (
    select item as gallery_item
    from public.gallery_items as item
    where item.owner_id = v_user_id
      and item.unpublish_pending_at is null
      and (p_kind is null or item.kind = p_kind)
      and (p_from is null or item.captured_at >= p_from)
      and (p_to is null or item.captured_at <= p_to)
      and (v_cursor_at is null or (item.captured_at, item.id) < (v_cursor_at, v_cursor_id))
    order by item.captured_at desc, item.id desc
    limit p_limit + 1
  ), numbered as (
    select page.gallery_item,
      row_number() over (order by (page.gallery_item).captured_at desc, (page.gallery_item).id desc) as row_number
    from page
  )
  select
    coalesce(jsonb_agg(app_private.gallery_item_json(numbered.gallery_item)
      order by (numbered.gallery_item).captured_at desc, (numbered.gallery_item).id desc)
      filter (where numbered.row_number <= p_limit), '[]'::jsonb),
    case when count(*) > p_limit then app_private.opaque_item_cursor(
      (array_agg((numbered.gallery_item).id
        order by (numbered.gallery_item).captured_at desc, (numbered.gallery_item).id desc))[p_limit]
    ) else null end
  into v_items, v_next_cursor
  from numbered;

  return jsonb_build_object('items', v_items, 'nextCursor', v_next_cursor);
end;
$$;

create or replace function public.get_my_gallery_calendar(p_month text)
returns table(day text, count bigint)
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(false);
  v_zone text;
  v_local_start timestamp;
  v_utc_start timestamptz;
  v_utc_end timestamptz;
begin
  if p_month !~ '^[0-9]{4}-(0[1-9]|1[0-2])$' then
    raise exception using errcode = '22023', message = 'invalid_calendar_month';
  end if;
  select time_zone into strict v_zone from public.profiles where user_id = v_user_id;
  v_local_start := to_date(p_month || '-01', 'YYYY-MM-DD')::timestamp;
  v_utc_start := v_local_start at time zone v_zone;
  v_utc_end := (v_local_start + interval '1 month') at time zone v_zone;

  return query
  select to_char(item.captured_at at time zone v_zone, 'YYYY-MM-DD'), count(*)
  from public.gallery_items as item
  where item.owner_id = v_user_id
    and item.unpublish_pending_at is null
    and item.captured_at >= v_utc_start
    and item.captured_at < v_utc_end
  group by 1
  order by 1;
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
  v_customer_id text;
begin
  if p_plan not in ('monthly', 'annual') then
    raise exception using errcode = '22023', message = 'invalid_billing_plan';
  end if;
  if not (select allow_checkout from app_private.runtime_controls where singleton) then
    raise exception using errcode = '55000', message = 'checkout_disabled';
  end if;
  insert into app_private.entitlements (user_id) values (v_user_id)
  on conflict (user_id) do nothing;
  select stripe_customer_id into v_customer_id
  from app_private.entitlements where user_id = v_user_id for update;

  return jsonb_build_object(
    'customerId', v_customer_id,
    'customerIdempotencyKey', 'xerahs-customer-' || v_user_id::text,
    'checkoutIdempotencyKey', 'xerahs-checkout-' || v_user_id::text || '-' || p_plan || '-' || to_char(clock_timestamp(), 'YYYYMMDDHH24')
  );
end;
$$;

create or replace function public.attach_my_stripe_customer(p_customer_id text)
returns text
language plpgsql
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
  v_existing text;
begin
  if p_customer_id !~ '^cus_[A-Za-z0-9]{8,255}$' then
    raise exception using errcode = '22023', message = 'invalid_stripe_customer';
  end if;
  select stripe_customer_id into v_existing
  from app_private.entitlements where user_id = v_user_id for update;
  if v_existing is not null and v_existing <> p_customer_id then
    raise exception using errcode = '23505', message = 'stripe_customer_already_attached';
  end if;
  update app_private.entitlements
  set stripe_customer_id = p_customer_id
  where user_id = v_user_id;
  return p_customer_id;
end;
$$;

create or replace function public.get_my_stripe_customer_id()
returns text
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_user_id uuid := app_private.current_user_aal2(true);
  v_customer_id text;
begin
  select stripe_customer_id into v_customer_id
  from app_private.entitlements where user_id = v_user_id;
  return v_customer_id;
end;
$$;

create or replace function public.resolve_stripe_webhook_owner(
  p_customer_id text,
  p_metadata_user_id uuid default null
)
returns uuid
language plpgsql
stable
security definer
set search_path = ''
as $$
declare
  v_user_id uuid;
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  if p_customer_id is not null then
    select user_id into v_user_id
    from app_private.entitlements where stripe_customer_id = p_customer_id;
  end if;
  if v_user_id is null then
    v_user_id := p_metadata_user_id;
  elsif p_metadata_user_id is not null and v_user_id <> p_metadata_user_id then
    raise exception using errcode = '23505', message = 'stripe_webhook_owner_mismatch';
  end if;
  if v_user_id is null or not exists (select 1 from auth.users where id = v_user_id) then
    raise exception using errcode = 'P0002', message = 'stripe_webhook_owner_not_found';
  end if;
  return v_user_id;
end;
$$;

create or replace function public.record_stripe_webhook_event(
  p_event_id text,
  p_event_type text,
  p_created_at timestamptz,
  p_livemode boolean
)
returns boolean
language plpgsql
security definer
set search_path = ''
as $$
begin
  if auth.role() <> 'service_role' then
    raise exception using errcode = '42501', message = 'service_role_required';
  end if;
  insert into app_private.stripe_webhook_events (
    event_id, event_type, stripe_created_at, livemode, state, attempt_count, processed_at
  ) values (
    p_event_id, p_event_type, p_created_at, p_livemode, 'processed', 1, clock_timestamp()
  ) on conflict (event_id) do nothing;
  return found;
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
  on conflict (event_id) do nothing;
  if not found then
    return false;
  end if;

  select status into v_prior_status from app_private.entitlements
  where user_id = v_user_id for update;
  select case
      when p_suspended then 'dispute_suspended'
      when paid_through > clock_timestamp() then 'active'
      else 'past_due'
    end
  into v_result_status
  from app_private.entitlements where user_id = v_user_id;

  update app_private.entitlements
  set dispute_suspended = p_suspended, status = v_result_status,
      last_stripe_event_created_at = greatest(
        coalesce(last_stripe_event_created_at, '-infinity'::timestamptz), p_created_at
      )
  where user_id = v_user_id;
  insert into app_private.entitlement_transitions (
    user_id, prior_status, result_status, reason, stripe_event_id, stripe_customer_id
  ) values (
    v_user_id, v_prior_status, v_result_status,
    case when p_suspended then 'stripe_dispute_opened' else 'stripe_dispute_closed' end,
    p_event_id, p_customer_id
  );
  update app_private.stripe_webhook_events
  set state = 'processed', processed_at = clock_timestamp()
  where event_id = p_event_id;
  return true;
end;
$$;

revoke execute on function app_private.gallery_item_json(public.gallery_items) from public, anon, authenticated;
revoke execute on function app_private.opaque_item_cursor(uuid) from public, anon, authenticated;
revoke execute on function app_private.decode_item_cursor(text) from public, anon, authenticated;

revoke execute on function public.get_my_account_summary() from public, anon;
revoke execute on function public.start_my_trial() from public, anon;
revoke execute on function public.publish_gallery_item(uuid, text, text, text, text, text, timestamptz, text, text, text) from public, anon;
revoke execute on function public.request_gallery_item_unpublish(uuid, text) from public, anon;
revoke execute on function public.list_my_gallery_items(text, integer, text, timestamptz, timestamptz) from public, anon;
revoke execute on function public.get_my_gallery_calendar(text) from public, anon;
revoke execute on function public.prepare_my_stripe_checkout(text) from public, anon;
revoke execute on function public.attach_my_stripe_customer(text) from public, anon;
revoke execute on function public.get_my_stripe_customer_id() from public, anon;
revoke execute on function public.resolve_stripe_webhook_owner(text, uuid) from public, anon, authenticated;
revoke execute on function public.record_stripe_webhook_event(text, text, timestamptz, boolean) from public, anon, authenticated;
revoke execute on function public.apply_stripe_dispute(text, text, timestamptz, boolean, text, boolean) from public, anon, authenticated;

grant execute on function public.get_my_account_summary() to authenticated;
grant execute on function public.start_my_trial() to authenticated;
grant execute on function public.publish_gallery_item(uuid, text, text, text, text, text, timestamptz, text, text, text) to authenticated;
grant execute on function public.request_gallery_item_unpublish(uuid, text) to authenticated;
grant execute on function public.list_my_gallery_items(text, integer, text, timestamptz, timestamptz) to authenticated;
grant execute on function public.get_my_gallery_calendar(text) to authenticated;
grant execute on function public.prepare_my_stripe_checkout(text) to authenticated;
grant execute on function public.attach_my_stripe_customer(text) to authenticated;
grant execute on function public.get_my_stripe_customer_id() to authenticated;
grant execute on function public.resolve_stripe_webhook_owner(text, uuid) to service_role;
grant execute on function public.record_stripe_webhook_event(text, text, timestamptz, boolean) to service_role;
grant execute on function public.apply_stripe_dispute(text, text, timestamptz, boolean, text, boolean) to service_role;
