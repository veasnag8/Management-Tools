import { Env, DeactivateRequest } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { DeviceService } from '../services/device-service';
import { jsonResponse, errorResponse } from '../middleware/validation';
import { authenticateDevice } from '../middleware/auth';
import { getClientIp } from '../middleware/rate-limit';

export async function handleDeactivate(request: Request, env: Env): Promise<Response> {
  const ip = getClientIp(request);
  const userAgent = request.headers.get('User-Agent') || 'WindowsClient';

  // Authenticate Device Token
  const { auth, errorResponse: authErr } = await authenticateDevice(request, env);
  if (authErr || !auth) {
    return authErr || errorResponse('INVALID_DEVICE_TOKEN', 'Unauthorized device token.', 401);
  }

  let body: DeactivateRequest = {};
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

  const result = await deviceService.deactivate(
    auth.licenseId,
    auth.deviceId,
    body.reason || 'Client initiated deactivation',
    ip,
    userAgent
  );

  if (!result.success || !result.data) {
    return errorResponse(
      result.error?.code || 'INVALID_REQUEST',
      result.error?.message || 'Deactivation failed.',
      result.statusCode || 400
    );
  }

  return jsonResponse(result.data, { status: 200 });
}
