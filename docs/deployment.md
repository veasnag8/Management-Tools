# Deployment Guide

## 1. Database (Supabase)
Execute migrations `001_profiles.sql` through `008_atomic_functions.sql` and `seed.sql`.

## 2. Cloudflare Worker API
```bash
cd worker
npm install
wrangler secret put SUPABASE_URL
wrangler secret put SUPABASE_SERVICE_ROLE_KEY
wrangler secret put JWT_SECRET
wrangler secret put LICENSE_SIGNING_KEY
npm run deploy
```

## 3. Admin Dashboard (Vercel)
Set `NEXT_PUBLIC_SUPABASE_URL` and `NEXT_PUBLIC_SUPABASE_ANON_KEY` in Vercel project settings and deploy `admin/`.

## 4. Windows Desktop App
```bash
cd windows-client
dotnet publish Tool/Tool.csproj -c Release -r win-x64 --self-contained false -o bin/Publish
```
