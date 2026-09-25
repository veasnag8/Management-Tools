# Security Architecture & Rules

1. **Zero Secret Leakage:** Supabase service-role keys are strictly on the server.
2. **DPAPI Encrypted Local Cache:** Session tokens and offline authorization caches are encrypted with Windows DPAPI.
3. **Cryptographic Offline Verification:** Server signs cache with HMAC-SHA256 for verified offline grace periods.
4. **Clock Rollback Tamper Detection:** Detects manual backward clock shifts.
