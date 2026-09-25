import { Env } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { LicenseService } from '../services/license-service';
import { jsonResponse, errorResponse } from '../middleware/validation';
import { authenticateDevice } from '../middleware/auth';
import { checkRateLimit, getClientIp } from '../middleware/rate-limit';

export async function handleValidate(request: Request, env: Env): Promise<Response> {
  const ip = getClientIp(request);

  // Rate Limit: 60 validation checks per minute per IP
  const rateLimit = checkRateLimit(`validate:ip:${ip}`, 60, 60);
  if (!rateLimit.allowed) {
    return errorResponse('RATE_LIMITED', `Rate limit exceeded. Retry in ${rateLimit.resetIn}s.`, 429);
  }

  // Authenticate Device Token
  const { auth, errorResponse: authErr } = await authenticateDevice(request, env);
  if (authErr || !auth) {
    return authErr || errorResponse('INVALID_DEVICE_TOKEN', 'Unauthorized device token.', 401);
  }

  const supabase = getSupabaseClient(env);
  const licenseService = new LicenseService(supabase, env);
  const userAgent = request.headers.get('User-Agent') || 'WindowsClient';

  const result = await licenseService.validate(auth.licenseId, auth.deviceId, ip, userAgent);

  if (!result.success || !result.data) {
    return errorResponse(
      result.error?.code || 'INVALID_REQUEST',
      result.error?.message || 'License validation failed.',
      result.statusCode || 403
    );
  }

  return jsonResponse(result.data, { status: 200 });
}
