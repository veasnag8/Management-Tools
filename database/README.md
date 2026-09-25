# Database Migrations & Schemas

This directory contains PostgreSQL migrations and schemas designed for Supabase.

## Migration Sequence

1. `001_profiles.sql` - Administrator profiles, `user_role` enum (`admin`, `manager`, `support`), automatic signup trigger, and RLS policies.
2. `002_products.sql` - Software products catalog, versioning, and download metadata.
3. `003_licenses.sql` - Core licenses table with status constraints (`pending`, `active`, `disabled`, `expired`, `revoked`), device limits, lifetime support (`expires_at IS NULL`), and grace period settings.
4. `004_devices.sql` - Registered devices/hardware identifiers bound to licenses, tracking telemetry (`os_name`, `app_version`, `last_ip_address`, `last_seen_at`).
5. `005_activations.sql` - High-fidelity audit trail for client lifecycle events (`activate`, `validate`, `heartbeat`, `deactivate`, `reset`, `blocked`, `expired`).
6. `006_license_logs.sql` - Admin-facing audit logs recording previous and updated JSON snapshots of modified licenses.
7. `007_release_versions.sql` - Version control for desktop client updates with SHA256 integrity hashes and mandatory update enforcement.
8. `008_atomic_functions.sql` - Race-condition-safe stored procedures (`activate_device_atomic`, `validate_device_atomic`) with row-level locks.

## Running Migrations in Supabase

Execute files in sequential order `001_profiles.sql` through `008_atomic_functions.sql`, followed by `seed.sql`.
