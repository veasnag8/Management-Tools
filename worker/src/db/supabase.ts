import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { Env } from '../types';

let cachedClient: SupabaseClient | null = null;
let lastUrl: string | null = null;
let lastKey: string | null = null;

export function getSupabaseClient(env: Env): SupabaseClient {
  if (!env.SUPABASE_URL || !env.SUPABASE_SERVICE_ROLE_KEY) {
    throw new Error('Supabase environment variables (SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY) are missing.');
  }

  if (cachedClient && lastUrl === env.SUPABASE_URL && lastKey === env.SUPABASE_SERVICE_ROLE_KEY) {
    return cachedClient;
  }

  cachedClient = createClient(env.SUPABASE_URL, env.SUPABASE_SERVICE_ROLE_KEY, {
    auth: {
      persistSession: false,
      autoRefreshToken: false,
    },
  });

  lastUrl = env.SUPABASE_URL;
  lastKey = env.SUPABASE_SERVICE_ROLE_KEY;

  return cachedClient;
}
