import { Env } from '../types';
import { getSupabaseClient } from '../db/supabase';
import { UpdateService } from '../services/update-service';
import { jsonResponse, errorResponse } from '../middleware/validation';

export async function handleVersion(request: Request, env: Env): Promise<Response> {
  const url = new URL(request.url);
  const product = url.searchParams.get('product');
  const currentVersion = url.searchParams.get('current_version') || '1.0.0';

  if (!product) {
    return errorResponse('INVALID_REQUEST', 'Query parameter "product" is required.', 400);
  }

  const supabase = getSupabaseClient(env);
  const updateService = new UpdateService(supabase, env);

  const result = await updateService.checkVersion(product, currentVersion);

  if (!result.success || !result.data) {
    return errorResponse(
      result.error?.code || 'INVALID_REQUEST',
      result.error?.message || 'Version check failed.',
      result.statusCode || 400
    );
  }

  return jsonResponse(result.data, { status: 200 });
}
