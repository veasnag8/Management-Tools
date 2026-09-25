# REST API Specification

Base URL: `https://<your-worker>.workers.dev`

All requests and responses use `application/json`.

## Endpoints

- `GET /health` - Health check & server UTC time
- `POST /v1/activate` - Activate license key with device fingerprint
- `POST /v1/validate` - Online validation with device token & signed offline cache issuance
- `POST /v1/heartbeat` - Background telemetry ping
- `POST /v1/deactivate` - Unregister PC device
- `POST /v1/reset-device` - Admin-only device slot reset
- `GET /v1/version` - Check application version & mandatory updates
