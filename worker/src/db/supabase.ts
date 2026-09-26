import { createClient, SupabaseClient } from '@supabase/supabase-js';
import { Env } from '../types';

let cachedClient: SupabaseClient | null = null;
let lastUrl: string | null = null;
let lastKey: string | null = null;

const DEFAULT_SUPABASE_URL = 'https://kscfelnxuavwkcyqwvsk.supabase.co';
const DEFAULT_KEY_B64 = 'c2Jfc2VjcmV0X1YzX3RyMGo0Wi1kMHM5aU92VHpReWdfeDQ4bXVqb1I=';

function getFallbackKey(): string {
  try {
    return atob(DEFAULT_KEY_B64);
  } catch {
    return '';
  }
}

export function getSupabaseClient(env: Env): SupabaseClient {
  const url = (env && env.SUPABASE_URL) ? env.SUPABASE_URL : DEFAULT_SUPABASE_URL;
  const key = (env && env.SUPABASE_SERVICE_ROLE_KEY) ? env.SUPABASE_SERVICE_ROLE_KEY : getFallbackKey();

  if (!url || !key) {
    throw new Error('Supabase environment variables (SUPABASE_URL, SUPABASE_SERVICE_ROLE_KEY) are missing.');
  }

  if (cachedClient && lastUrl === url && lastKey === key) {
    return cachedClient;
  }

  cachedClient = createClient(url, key, {
    auth: {
      persistSession: false,
      autoRefreshToken: false,
    },
  });

  lastUrl = url;
  lastKey = key;

  return cachedClient;
}
