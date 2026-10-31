begin;

create extension if not exists pgtap with schema extensions;
set local search_path = public, extensions;

select plan(36);

select ok(to_regclass('public.profiles') is not null, 'profiles exists');
select ok(to_regclass('public.gallery_items') is not null, 'gallery_items exists');
select ok(to_regclass('app_private.entitlements') is not null, 'private entitlements exist');
select ok(to_regclass('app_private.operations_ledger_outbox') is not null, 'ledger outbox exists');

select is(
  (select relrowsecurity from pg_catalog.pg_class where oid = 'public.profiles'::regclass),
  true,
  'profiles has RLS enabled'
);
select is(
  (select relrowsecurity from pg_catalog.pg_class where oid = 'public.gallery_items'::regclass),
  true,
  'gallery_items has RLS enabled'
);
select is(has_table_privilege('anon', 'public.profiles', 'SELECT'), false, 'anon cannot read profiles');
select is(has_table_privilege('anon', 'public.gallery_items', 'SELECT'), false, 'anon cannot read gallery items');
select is(has_table_privilege('authenticated', 'public.gallery_items', 'INSERT'), false, 'direct gallery inserts are denied');
select is(has_table_privilege('authenticated', 'public.gallery_items', 'UPDATE'), false, 'direct gallery updates are denied');
select is(has_table_privilege('authenticated', 'public.gallery_items', 'DELETE'), false, 'direct gallery deletes are denied');
select is(has_schema_privilege('anon', 'app_private', 'USAGE'), false, 'anon cannot use private schema');

select ok(to_regprocedure('public.get_my_account_summary()') is not null, 'account summary RPC exists');
select ok(to_regprocedure('public.start_my_trial()') is not null, 'trial RPC exists');
select ok(
  to_regprocedure('public.publish_gallery_item(uuid,text,text,text,text,text,timestamp with time zone,text,text,text)') is not null,
  'publish API RPC exists'
);
select ok(
  to_regprocedure('public.request_gallery_item_unpublish(uuid,text)') is not null,
  'unpublish API RPC exists'
);
select is(
  has_function_privilege('anon', 'public.get_my_account_summary()', 'EXECUTE'),
  false,
  'anon cannot execute account summary'
);
select is(
  (select allow_trial_grants and allow_checkout and allow_publish from app_private.runtime_controls where singleton),
  true,
  'local seed enables staged feature controls'
);

select ok(to_regclass('app_private.idempotency_records') is not null, 'persistent idempotency records exist');
select ok(to_regclass('app_private.stripe_checkout_attempts') is not null, 'checkout attempts exist');
select ok(to_regclass('app_private.ledger_replay_events') is not null, 'ledger replay deduplication exists');
select ok(
  exists (
    select 1 from information_schema.columns
    where table_schema = 'app_private' and table_name = 'entitlements' and column_name = 'billing_status'
  ),
  'entitlements separate billing state from effective state'
);
select ok(
  exists (
    select 1 from information_schema.columns
    where table_schema = 'app_private' and table_name = 'entitlements' and column_name = 'account_state'
  ),
  'entitlements track deletion account state'
);
select is(has_schema_privilege('authenticated', 'app_private', 'USAGE'), false, 'authenticated cannot use private schema');
select is(has_function_privilege('authenticated', 'public.start_gallery_trial()', 'EXECUTE'), false, 'legacy trial RPC is revoked');
select is(
  has_function_privilege('authenticated', 'public.publish_gallery_item(uuid,text,text,text,text,timestamp with time zone,text,text)', 'EXECUTE'),
  false,
  'legacy publish RPC is revoked'
);
select is(has_function_privilege('authenticated', 'public.unpublish_gallery_item(uuid)', 'EXECUTE'), false, 'legacy unpublish RPC is revoked');
select ok(to_regprocedure('public.finalize_my_stripe_checkout(uuid,text)') is not null, 'checkout finalization RPC exists');
select ok(to_regprocedure('public.record_stripe_webhook_failure(text,text,timestamp with time zone,boolean,text)') is not null, 'webhook failure RPC exists');
select ok(to_regprocedure('public.list_stripe_reconciliation_targets(integer)') is not null, 'Stripe reconciliation RPC exists');
select ok(to_regprocedure('public.claim_account_deletions(uuid,integer,integer)') is not null, 'deletion lease RPC exists');
select ok(to_regprocedure('public.defer_account_deletion(uuid,uuid,integer)') is not null, 'deletion defer RPC exists');
select ok(to_regprocedure('public.replay_operations_ledger_event(text,jsonb,bytea)') is not null, 'verified ledger replay RPC exists');
select is(
  has_function_privilege('authenticated', 'public.list_stripe_reconciliation_targets(integer)', 'EXECUTE'),
  false,
  'authenticated cannot reconcile Stripe'
);
select is(
  has_function_privilege('service_role', 'public.list_stripe_reconciliation_targets(integer)', 'EXECUTE'),
  true,
  'service role can reconcile Stripe'
);
select is(
  has_function_privilege('authenticated', 'public.current_request_account_active()', 'EXECUTE'),
  true,
  'RLS can enforce live-session and account-state validation'
);

select * from finish();
rollback;
