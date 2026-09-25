# License Management Admin Portal

Modern Next.js 14 / TypeScript / Tailwind CSS management console for software licensing, hardware PC activations, release management, and audit inspection.

## Key Features

- **Dashboard**: Real-time KPI cards for active licenses, activated hardware devices, expiry tracking, and revenue overview.
- **License Generator**: Single and bulk license generation (`PROD-XXXX-XXXX-XXXX-XXXX`) with customizable duration, device slots, and CSV export.
- **License Inspector**: Detailed drill-down for each license, registered hardware devices, slot resets, status changes, and extension modal.
- **Device Management**: View all registered client machines (`PC-XXXXXXXX-XXXXXXXX`), active sessions, IP telemetry, and remote revocation.
- **Product Catalog**: Manage products, versions, download links, and activation policies.
- **Release Versioning**: Release binary manager with mandatory upgrade flags, release notes, and SHA256 integrity checksums.
- **Audit Logs**: Immutable log viewer showing admin modifications and client heartbeat/activation telemetry.

## Setup & Development

```bash
# Install dependencies
npm install

# Configure environment variables (.env.local)
NEXT_PUBLIC_SUPABASE_URL=https://your-project.supabase.co
NEXT_PUBLIC_SUPABASE_ANON_KEY=your-anon-key
SUPABASE_SERVICE_ROLE_KEY=your-service-role-key

# Run local development server
npm run dev
```

Admin portal is accessible at `http://localhost:3000`.
