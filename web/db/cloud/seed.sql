-- Synthetic local-only data. Never run this seed against a hosted environment.
-- Password for all three local users: Local-only-Password-42!

insert into auth.users (
  instance_id, id, aud, role, email, encrypted_password, email_confirmed_at,
  raw_app_meta_data, raw_user_meta_data, created_at, updated_at,
  confirmation_token, email_change, email_change_token_new, recovery_token
)
values
  (
    '00000000-0000-0000-0000-000000000000',
    '10000000-0000-0000-0000-000000000001',
    'authenticated', 'authenticated', 'owner-a@example.test',
    extensions.crypt('Local-only-Password-42!', extensions.gen_salt('bf')),
    clock_timestamp(), '{"provider":"email","providers":["email"]}'::jsonb,
    '{}'::jsonb, clock_timestamp(), clock_timestamp(), '', '', '', ''
  ),
  (
    '00000000-0000-0000-0000-000000000000',
    '20000000-0000-0000-0000-000000000002',
    'authenticated', 'authenticated', 'owner-b@example.test',
    extensions.crypt('Local-only-Password-42!', extensions.gen_salt('bf')),
    clock_timestamp(), '{"provider":"email","providers":["email"]}'::jsonb,
    '{}'::jsonb, clock_timestamp(), clock_timestamp(), '', '', '', ''
  ),
  (
    '00000000-0000-0000-0000-000000000000',
    '30000000-0000-0000-0000-000000000003',
    'authenticated', 'authenticated', 'trial-user@example.test',
    extensions.crypt('Local-only-Password-42!', extensions.gen_salt('bf')),
    clock_timestamp(), '{"provider":"email","providers":["email"]}'::jsonb,
    '{}'::jsonb, clock_timestamp(), clock_timestamp(), '', '', '', ''
  )
on conflict (id) do nothing;

insert into public.profiles (user_id, slug, time_zone)
values
  ('10000000-0000-0000-0000-000000000001', 'owner-a', 'Australia/Perth'),
  ('20000000-0000-0000-0000-000000000002', 'owner-b', 'UTC'),
  ('30000000-0000-0000-0000-000000000003', 'trial-user', 'UTC')
on conflict (user_id) do update
set slug = excluded.slug, time_zone = excluded.time_zone;

insert into app_private.verified_identities (
  user_id, identity_hmac, normalization_version, hmac_key_version
)
values
  (
    '10000000-0000-0000-0000-000000000001',
    extensions.digest(convert_to('seed:owner-a@example.test', 'UTF8'), 'sha256'), 1, 1
  ),
  (
    '20000000-0000-0000-0000-000000000002',
    extensions.digest(convert_to('seed:owner-b@example.test', 'UTF8'), 'sha256'), 1, 1
  ),
  (
    '30000000-0000-0000-0000-000000000003',
    extensions.digest(convert_to('seed:trial-user@example.test', 'UTF8'), 'sha256'), 1, 1
  )
on conflict (user_id) do nothing;

insert into app_private.entitlements (
  user_id, status, stripe_customer_id, stripe_subscription_id, stripe_price_id,
  paid_through, last_stripe_event_created_at
)
values
  (
    '10000000-0000-0000-0000-000000000001', 'active', 'cus_seed_a',
    'sub_seed_a', 'price_seed_monthly', clock_timestamp() + interval '1 year', clock_timestamp()
  ),
  (
    '20000000-0000-0000-0000-000000000002', 'active', 'cus_seed_b',
    'sub_seed_b', 'price_seed_annual', clock_timestamp() + interval '1 year', clock_timestamp()
  ),
  ('30000000-0000-0000-0000-000000000003', 'none', null, null, null, null, null)
on conflict (user_id) do nothing;

update app_private.runtime_controls
set allow_trial_grants = true, allow_checkout = true, allow_publish = true
where singleton;

insert into public.gallery_items (
  id, owner_id, client_item_id, url, url_sha256, thumbnail_url, kind,
  file_name, title, captured_at, host, content_type
)
values
  (
    'a1000000-0000-0000-0000-000000000001',
    '10000000-0000-0000-0000-000000000001',
    'a1100000-0000-0000-0000-000000000001',
    'https://media.example.test/captures/screenshot-a.png',
    decode(repeat('00', 32), 'hex'),
    'https://media.example.test/thumbs/screenshot-a.png',
    'screenshot', 'screenshot-a.png', 'ignored-by-trigger',
    clock_timestamp() - interval '1 day', 'Synthetic', 'image/png'
  ),
  (
    'b2000000-0000-0000-0000-000000000002',
    '20000000-0000-0000-0000-000000000002',
    'b2200000-0000-0000-0000-000000000002',
    'https://media.example.test/captures/screencast-b.mp4',
    decode(repeat('00', 32), 'hex'), null,
    'screencast', 'screencast-b.mp4', 'ignored-by-trigger',
    clock_timestamp() - interval '2 days', 'Synthetic', 'video/mp4'
  )
on conflict (id) do nothing;
