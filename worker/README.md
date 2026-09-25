# Cloudflare Worker Licensing API

This service provides the authoritative server API for license activation, validation, heartbeats, version checking, and admin device resets.

## Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `GET` | `/health` | None | API health status and UTC server time |
| `POST` | `/v1/activate` | None (Rate Limited) | Activate a license on a PC device |
| `POST` | `/v1/validate` | Device Token (`Bearer`) | Validate active license & retrieve signed offline cache |
| `POST` | `/v1/heartbeat` | Device Token (`Bearer`) | Telemetry ping & status update |
| `POST` | `/v1/deactivate`| Device Token (`Bearer`) | Release PC device registration |
| `POST` | `/v1/reset-device` | Supabase Admin JWT | Admin revocation of a device slot |
| `GET` | `/v1/version` | None | Check latest release & mandatory update requirement |

## Secrets Configuration

Set the following secrets using `wrangler secret put`:

```bash
wrangler secret put SUPABASE_URL
wrangler secret put SUPABASE_SERVICE_ROLE_KEY
wrangler secret put JWT_SECRET
wrangler secret put LICENSE_SIGNING_KEY
```

## Local Development

```bash
npm install
npm run dev
```

## Deployment

```bash
npm run deploy
```
