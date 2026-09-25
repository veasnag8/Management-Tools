import { Env, ActivateRequest } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { LicenseService } from '../services/license-service';
import { jsonResponse, errorResponse, validateActivatePayload } from '../middleware/validation';
import { checkRateLimit, getClientIp } from '../middleware/rate-limit';

export async function handleActivate(request: Request, env: Env): Promise<Response> {
  const ip = getClientIp(request);

  // Rate Limit: 10 activate attempts per minute per IP
  const rateLimit = checkRateLimit(`activate:ip:${ip}`, 10, 60);
  if (!rateLimit.allowed) {
    return errorResponse('RATE_LIMITED', `Too many activation attempts. Please retry in ${rateLimit.resetIn}s.`, 429);
  }

  let body: ActivateRequest;
  try {
    body = await request.json();
  } catch {
    return errorResponse('INVALID_REQUEST', 'Malformed JSON payload in request body.', 400);
  }

  const validation = validateActivatePayload(body);
  if (!validation.valid) {
    return errorResponse('INVALID_REQUEST', validation.error || 'Invalid activation payload.', 400);
  }

  // Rate Limit: 5 attempts per minute per license key
  const licKey = body.license_key.trim().toUpperCase();
  const licRateLimit = checkRateLimit(`activate:lic:${licKey}`, 5, 60);
  if (!licRateLimit.allowed) {
    return errorResponse('RATE_LIMITED', 'Too many attempts for this license key.', 429);
  }

  const supabase = getSupabaseClient(env);
  const licenseService = new LicenseService(supabase, env);

  const userAgent = request.headers.get('User-Agent') || 'WindowsClient';
  const result = await licenseService.activate(body, ip, userAgent);

  if (!result.success || !result.data) {
    return errorResponse(
      result.error?.code || 'INVALID_REQUEST',
      result.error?.message || 'Activation failed.',
      result.statusCode || 400
    );
  }

  return jsonResponse(result.data, { status: 200 });
}
