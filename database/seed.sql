-- Seed Data for Development & Testing
-- Author: Software Licensing Platform Architect

INSERT INTO public.products (id, product_code, name, description, current_version, download_url, active)
VALUES 
(
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    'TEST-TOOL',
    'KN 3in1 Pro — Drama Stream Manager',
    'Professional Desktop drama episode batch downloader & streaming client.',
    '1.0.0',
    'https://releases.example.com/TestTool-1.0.0-Setup.exe',
    true
),
(
    'b1eebc99-9c0b-4ef8-bb6d-6bb9bd380a22',
    'CHINESE-REVIEW',
    'Chinese Review Tool',
    'Comprehensive language analysis, spaced repetition and study tool.',
    '1.2.0',
    'https://releases.example.com/ChineseReview-1.2.0-Setup.exe',
    true
)
ON CONFLICT (product_code) DO NOTHING;

INSERT INTO public.release_versions (id, product_id, version, download_url, release_notes, sha256, mandatory, active)
VALUES
(
    'c2eebc99-9c0b-4ef8-bb6d-6bb9bd380a33',
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    '1.0.0',
    'https://releases.example.com/TestTool-1.0.0-Setup.exe',
    'Initial production release with offline grace period and license verification.',
    'e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855',
    false,
    true
)
ON CONFLICT (product_id, version) DO NOTHING;

INSERT INTO public.licenses (
    id,
    license_key,
    customer_name,
    customer_email,
    product_id,
    status,
    start_at,
    expires_at,
    max_devices,
    offline_grace_hours,
    notes
)
VALUES
(
    'd3eebc99-9c0b-4ef8-bb6d-6bb9bd380a44',
    'TOOL-7F92KQ-XP48MD-9R2L',
    'Test Customer',
    'customer@example.com',
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    'active',
    now(),
    now() + INTERVAL '30 days',
    1,
    72,
    'Standard 30-day single device evaluation license'
),
(
    'e4eebc99-9c0b-4ef8-bb6d-6bb9bd380a55',
    'TOOL-AAAAAA-BBBBBB-CCCC',
    'Lifetime VIP Customer',
    'vip@example.com',
    'a0eebc99-9c0b-4ef8-bb6d-6bb9bd380a11',
    'active',
    now(),
    NULL,
    3,
    168,
    'Lifetime 3-device team license'
)
ON CONFLICT (license_key) DO NOTHING;
