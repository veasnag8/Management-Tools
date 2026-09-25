# System Architecture & Trust Boundaries

The system is structured as an untrusted desktop client communicating with a trusted serverless API Gateway and a protected database backend.

## Trust Boundaries

1. **Windows Client (EXE):** Untrusted. No direct database connection. Uses DPAPI encrypted local cache.
2. **Admin Dashboard (Next.js):** Privileged. Uses Supabase Auth & RLS policies.
3. **API Gateway (Cloudflare Worker):** Authoritative server. Enforces rate limits and signs offline cache tokens.
4. **Database (Supabase PostgreSQL):** Authoritative source of truth with atomic stored procedures.
