# Database Documentation & Schemas

## Tables
- `profiles`: Administrator credentials and roles (`admin`, `manager`, `support`).
- `products`: Software product catalog and version metadata.
- `licenses`: Generated license keys, statuses, and device limits.
- `devices`: Bound PC hardware fingerprints and telemetry.
- `activations`: Client event audit log.
- `license_logs`: Admin change log with before/after JSON diffs.
- `release_versions`: Version updates with SHA-256 integrity hashes.
