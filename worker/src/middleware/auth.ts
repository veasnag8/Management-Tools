import { Env, DeviceTokenPayload } from '../types';
import { verifyDeviceToken } from '../services/security-service';
import { getSupabaseClient } from '../db/supabase';

export interface AuthenticatedDeviceContext {
  deviceId: string;
  licenseId: string;
  productId?: string;
}

export interface AuthenticatedAdminContext {
  userId: string;
  email: string;
  role: 'admin' | 'manager' | 'support';
}

/**
 * Authenticate Windows Client via Bearer Device Token
 */
export async function authenticateDevice(
  request: Request,
  env: Env
): Promise<{ auth: AuthenticatedDeviceContext | null; errorResponse?: Response }> {
  const authHeader = request.headers.get('Authorization');
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return {
      auth: null,
      errorResponse: new Response(
        JSON.stringify({
          success: false,
          data: null,
          error: { code: 'INVALID_DEVICE_TOKEN', message: 'Missing or invalid Authorization header.' }
        }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      )
    };
  }

  const token = authHeader.substring(7).trim();
  const payload = await verifyDeviceToken(token, env.JWT_SECRET || 'b6e3f89a74c10294857d19e830c24f61e8947b19485d928374a5e6f1c2d3b4a5');

  if (!payload || !payload.sub || !payload.license_id) {
    return {
      auth: null,
      errorResponse: new Response(
        JSON.stringify({
          success: false,
          data: null,
          error: { code: 'INVALID_DEVICE_TOKEN', message: 'Expired or invalid device token.' }
        }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      )
    };
  }

  return {
    auth: {
      deviceId: payload.sub,
      licenseId: payload.license_id,
      productId: payload.product_id
    }
  };
}

/**
 * Authenticate Administrator via Supabase Auth Access Token
 */
export async function authenticateAdmin(
  request: Request,
  env: Env,
  allowedRoles: ('admin' | 'manager' | 'support')[] = ['admin']
): Promise<{ admin: AuthenticatedAdminContext | null; errorResponse?: Response }> {
  const authHeader = request.headers.get('Authorization');
  if (!authHeader || !authHeader.startsWith('Bearer ')) {
    return {
      admin: null,
      errorResponse: new Response(
        JSON.stringify({
          success: false,
          data: null,
          error: { code: 'UNAUTHORIZED', message: 'Admin authentication required.' }
        }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      )
    };
  }

  const token = authHeader.substring(7).trim();
  const supabase = getSupabaseClient(env);

  // Validate token with Supabase Auth
  const { data: { user }, error: userErr } = await supabase.auth.getUser(token);
  if (userErr || !user) {
    return {
      admin: null,
      errorResponse: new Response(
        JSON.stringify({
          success: false,
          data: null,
          error: { code: 'UNAUTHORIZED', message: 'Invalid admin session token.' }
        }),
        { status: 401, headers: { 'Content-Type': 'application/json' } }
      )
    };
  }

  // Fetch admin profile to verify role
  const { data: profile, error: profErr } = await supabase
    .from('profiles')
    .select('role')
    .eq('id', user.id)
    .single();

  const role = (profile?.role || 'admin') as 'admin' | 'manager' | 'support';

  if (!allowedRoles.includes(role)) {
    return {
      admin: null,
      errorResponse: new Response(
        JSON.stringify({
          success: false,
          data: null,
          error: { code: 'UNAUTHORIZED', message: 'Insufficient administrative privileges.' }
        }),
        { status: 403, headers: { 'Content-Type': 'application/json' } }
      )
    };
  }

  return {
    admin: {
      userId: user.id,
      email: user.email || '',
      role
    }
  };
}
