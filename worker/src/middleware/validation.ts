import { ApiResponse, ApiErrorCode } from '../types';

export function jsonResponse<T>(
  data: T | null,
  options: {
    status?: number;
    error?: { code: ApiErrorCode; message: string } | null;
    headers?: Record<string, string>;
  } = {}
): Response {
  const status = options.status || (options.error ? 400 : 200);
  const body: ApiResponse<T> = {
    success: !options.error,
    data: data,
    error: options.error || null
  };

  const responseHeaders = new Headers({
    'Content-Type': 'application/json; charset=utf-8',
    'Cache-Control': 'no-store, no-cache, must-revalidate',
    ...options.headers
  });

  return new Response(JSON.stringify(body), {
    status,
    headers: responseHeaders
  });
}

export function errorResponse(code: ApiErrorCode, message: string, status = 400): Response {
  return jsonResponse(null, {
    status,
    error: { code, message }
  });
}

/**
 * Validates Activate Request Body
 */
export function validateActivatePayload(body: any): { valid: boolean; error?: string } {
  if (!body || typeof body !== 'object') {
    return { valid: false, error: 'Request body must be a valid JSON object.' };
  }

  if (!body.license_key || typeof body.license_key !== 'string' || body.license_key.trim().length < 5) {
    return { valid: false, error: 'A valid license_key is required.' };
  }

  if (!body.device_id || typeof body.device_id !== 'string' || body.device_id.trim().length < 4) {
    return { valid: false, error: 'A valid device_id is required.' };
  }

  return { valid: true };
}
