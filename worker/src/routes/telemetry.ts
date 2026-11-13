import { Env } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { jsonResponse, errorResponse } from '../middleware/validation';
import { authenticateDevice } from '../middleware/auth';
import { checkRateLimit, getClientIp } from '../middleware/rate-limit';

export interface TelemetryPayload {
  tokens?: number;
  prompt_tokens?: number;
  completion_tokens?: number;
  total_tokens?: number;
  model_name?: string;
  task_type?: string;
  task_description?: string;
  active_keys_count?: number;
  license_key?: string;
  device_id?: string;
}

export async function handleTelemetry(request: Request, env: Env): Promise<Response> {
  const ip = getClientIp(request);

  // Rate Limit: 120 telemetry pings per minute per IP
  const rateLimit = checkRateLimit(`telemetry:ip:${ip}`, 120, 60);
  if (!rateLimit.allowed) {
    return errorResponse('RATE_LIMITED', `Telemetry rate limit exceeded. Retry in ${rateLimit.resetIn}s.`, 429);
  }

  let body: TelemetryPayload = {};
  try {
    const text = await request.text();
    if (text) {
      body = JSON.parse(text);
    }
  } catch {
    return errorResponse('INVALID_REQUEST', 'Invalid JSON payload.', 400);
  }

  const supabase = getSupabaseClient(env);
  let licenseId: string | null = null;
  let deviceId: string | null = null;

  // 1. Try Device Token authentication
  const { auth } = await authenticateDevice(request, env);
  if (auth) {
    licenseId = auth.licenseId;
    deviceId = auth.deviceId;
  }

  // 2. Fallback to license_key and device_id
  if (!licenseId && body.license_key) {
    const { data: lic } = await supabase
      .from('licenses')
      .select('id, status')
      .eq('license_key', body.license_key.trim())
      .single();

    if (lic && lic.status === 'active') {
      licenseId = lic.id;
      deviceId = body.device_id || 'unknown_device';
    }
  }

  if (!licenseId || !deviceId) {
    return errorResponse('UNAUTHORIZED', 'Valid device token or active license_key is required.', 401);
  }

  const totalTokens = body.total_tokens || body.tokens || 0;
  const promptTokens = body.prompt_tokens || 0;
  const completionTokens = body.completion_tokens || 0;
  const modelName = body.model_name || 'gemini-2.5-flash';
  const taskType = body.task_type || 'translate';
  const taskDesc = body.task_description || (body.active_keys_count ? `Gemini Translate (${body.active_keys_count} Keys Pool)` : 'Gemini AI Stream');

  // Insert token usage telemetry record
  const { error: insertErr } = await supabase.from('token_usages').insert({
    license_id: licenseId,
    device_id: deviceId,
    model_name: modelName,
    prompt_tokens: promptTokens,
    completion_tokens: completionTokens,
    total_tokens: totalTokens,
    task_type: taskType,
    task_description: taskDesc,
  });

  // Also touch last_seen_at on device so it is immediately detected active
  await supabase
    .from('devices')
    .update({
      last_seen_at: new Date().toISOString(),
      last_ip_address: ip || null
    })
    .eq('license_id', licenseId)
    .eq('device_id', deviceId);

  if (insertErr) {
    console.error('Failed to insert token_usages telemetry:', insertErr);
    return errorResponse('SERVER_ERROR', 'Failed to record token usage.', 500);
  }

  return jsonResponse({
    success: true,
    recorded_tokens: totalTokens,
    timestamp: new Date().toISOString()
  });
}
