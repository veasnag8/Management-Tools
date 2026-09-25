import { Env, ResetDeviceRequest } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { DeviceService } from '../services/device-service';
import { jsonResponse, errorResponse } from '../middleware/validation';
import { authenticateAdmin } from '../middleware/auth';

export async function handleResetDevice(request: Request, env: Env): Promise<Response> {
  // 1. Authenticate Admin
  const { admin, errorResponse: authErr } = await authenticateAdmin(request, env, ['admin', 'manager']);
  if (authErr || !admin) {
    return authErr || errorResponse('UNAUTHORIZED', 'Admin authorization required.', 401);
  }

  let body: ResetDeviceRequest;
  try {
    body = await request.json();
  } catch {
    return errorResponse('INVALID_REQUEST', 'Malformed JSON payload.', 400);
  }

  if (!body.license_id || !body.device_id) {
    return errorResponse('INVALID_REQUEST', 'Both license_id and device_id are required.', 400);
  }

  const supabase = getSupabaseClient(env);
  const deviceService = new DeviceService(supabase, env);

  const result = await deviceService.resetDeviceByAdmin(
    body.license_id,
    body.device_id,
    admin.userId,
    body.reason || 'Admin reset device slot'
  );

  if (!result.success || !result.data) {
    return errorResponse(
      result.error?.code || 'INVALID_REQUEST',
      result.error?.message || 'Failed to reset device.',
      result.statusCode || 400
    );
  }

  return jsonResponse(result.data, { status: 200 });
}
