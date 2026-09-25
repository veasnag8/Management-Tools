interface RateLimitEntry {
  count: number;
  resetAt: number;
}

// In-memory worker rate limit cache
const memoryStore = new Map<string, RateLimitEntry>();

// Periodic cleanup of expired entries (max 10,000 entries)
function cleanupMemoryStore() {
  if (memoryStore.size > 5000) {
    const now = Date.now();
    for (const [key, entry] of memoryStore.entries()) {
      if (entry.resetAt < now) {
        memoryStore.delete(key);
      }
    }
  }
}

/**
 * Check rate limit for a key (e.g. IP address or License key hash)
 * @param key unique identifier (e.g. `ip:192.168.1.1` or `lic:TOOL-7F92KQ`)
 * @param maxRequests maximum allowed requests within window
 * @param windowSeconds window duration in seconds
 */
export function checkRateLimit(
  key: string,
  maxRequests: number = 30,
  windowSeconds: number = 60
): { allowed: boolean; remaining: number; resetIn: number } {
  cleanupMemoryStore();

  const now = Date.now();
  const entry = memoryStore.get(key);

  if (!entry || entry.resetAt < now) {
    // New window
    memoryStore.set(key, {
      count: 1,
      resetAt: now + windowSeconds * 1000
    });
    return { allowed: true, remaining: maxRequests - 1, resetIn: windowSeconds };
  }

  if (entry.count >= maxRequests) {
    const resetIn = Math.ceil((entry.resetAt - now) / 1000);
    return { allowed: false, remaining: 0, resetIn };
  }

  entry.count += 1;
  const resetIn = Math.ceil((entry.resetAt - now) / 1000);
  return { allowed: true, remaining: maxRequests - entry.count, resetIn };
}

/**
 * Helper to get client IP from Cloudflare Request
 */
export function getClientIp(request: Request): string {
  return (
    request.headers.get('CF-Connecting-IP') ||
    request.headers.get('X-Forwarded-For')?.split(',')[0].trim() ||
    '127.0.0.1'
  );
}
