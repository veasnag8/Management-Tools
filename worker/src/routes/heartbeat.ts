import { Env, HeartbeatRequest } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { DeviceService } from '../services/device-service';
import { jsonResponse, errorResponse } from '../middleware/validation';
import { authenticateDevice } from '../middleware/auth';
import { checkRateLimit, getClientIp } from '../middleware/rate-limit';

export async function handleHeartbeat(request: Request, env: Env): Promise<Response> {
  const ip = getClientIp(request);

  // Rate Limit: 30 heartbeats per minute per IP
  const rateLimit = checkRateLimit(`heartbeat:ip:${ip}`, 30, 60);
  if (!rateLimit.allowed) {
    return errorResponse('RATE_LIMITED', `Heartbeat rate limit exceeded. Retry in ${rateLimit.resetIn}s.`, 429);
  }

  // Authenticate Device Token
  const { auth, errorResponse: authErr } = await authenticateDevice(request, env);
  if (authErr || !auth) {
    return authErr || errorResponse('INVALID_DEVICE_TOKEN', 'Unauthorized device token.', 401);
  }

  let body: HeartbeatRequest = {};
  try {
    const text = await request.text();
    if (text) {
      body = JSON.parse(text);
    }
  } catch {
    // optional body
  }

  const supabase = getSupabaseClient(env);
  const deviceService = new DeviceService(supabase, env);

  const result = await deviceService.heartbeat(auth.licenseId, auth.deviceId, body, ip);

  if (!result.success || !result.data) {
    return errorResponse(
      result.error?.code || 'INVALID_REQUEST',
      result.error?.message || 'Heartbeat failed.',
      result.statusCode || 400
    );
  }

  return jsonResponse(result.data, { status: 200 });
}
