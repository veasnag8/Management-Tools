import { SupabaseClient } from '@supabase/supabase-js';
import { Env, ActivateRequest, ActivateResponseData, ValidateResponseData, ApiErrorCode } from '../types';
import { generateDeviceToken, generateSignedLicenseCache } from './security-service';

export interface ServiceResult<T> {
  success: boolean;
  data?: T;
  error?: {
    code: ApiErrorCode;
    message: string;
  };
  statusCode?: number;
}

export class LicenseService {
  constructor(
    private supabase: SupabaseClient,
    private env: Env
  ) {}

  /**
   * Activate a device for a license key atomically
   */
  async activate(
    req: ActivateRequest,
    ipAddress: string,
    userAgent: string
  ): Promise<ServiceResult<ActivateResponseData>> {
    const { license_key, device_id, device_name, os_name, os_version, app_version } = req;

    // Call atomic Postgres function
    const { data: result, error } = await this.supabase.rpc('activate_device_atomic', {
      p_license_key: license_key.trim().toUpperCase(),
      p_device_id: device_id.trim(),
      p_device_name: device_name || 'Unknown Device',
      p_os_name: os_name || 'Windows',
      p_os_version: os_version || '10.0',
      p_app_version: app_version || '1.0.0',
      p_ip_address: ipAddress || '127.0.0.1',
      p_user_agent: userAgent || 'WindowsClient/1.0'
    });

    if (error) {
      console.error('RPC activate_device_atomic error:', error);
      return {
        success: false,
        error: {
          code: 'SERVER_ERROR',
          message: 'An error occurred during license activation.'
        },
        statusCode: 500
      };
    }

    if (!result || !result.success) {
      const code = (result?.code || 'INVALID_REQUEST') as ApiErrorCode;
      return {
        success: false,
        error: {
          code,
          message: result?.message || 'Activation failed.'
        },
        statusCode: code === 'DEVICE_LIMIT_REACHED' ? 409 : 400
      };
    }

    // Generate JWT device token
    const deviceToken = await generateDeviceToken(
      {
        sub: device_id,
        license_id: result.license_id,
        product_id: result.product_id
      },
      this.env.JWT_SECRET
    );

    // Generate server-signed license cache for offline verification
    const signedCache = await generateSignedLicenseCache(
      {
        license_id: result.license_id,
        device_id: device_id,
        product_code: result.product_code,
        status: 'active',
        expires_at: result.expires_at,
        offline_grace_hours: result.offline_grace_hours || 72,
        issued_at: new Date().toISOString()
      },
      this.env.LICENSE_SIGNING_KEY || this.env.JWT_SECRET
    );

    return {
      success: true,
      data: {
        status: 'active',
        expires_at: result.expires_at,
        device_token: deviceToken,
        server_time: new Date().toISOString(),
        offline_grace_hours: result.offline_grace_hours || 72,
        product_code: result.product_code,
        signed_license_cache: signedCache
      }
    };
  }

  /**
   * Validate a device license session
   */
  async validate(
    licenseId: string,
    deviceId: string,
    ipAddress: string,
    userAgent: string
  ): Promise<ServiceResult<ValidateResponseData>> {
    // Call atomic validate procedure
    const { data: result, error } = await this.supabase.rpc('validate_device_atomic', {
      p_license_id: licenseId,
      p_device_id: deviceId,
      p_ip_address: ipAddress || '127.0.0.1',
      p_user_agent: userAgent || 'WindowsClient/1.0'
    });

    if (error) {
      console.error('RPC validate_device_atomic error:', error);
      return {
        success: false,
        error: {
          code: 'SERVER_ERROR',
          message: 'Error validating license.'
        },
        statusCode: 500
      };
    }

    if (!result || !result.success) {
      const code = (result?.code || 'INVALID_REQUEST') as ApiErrorCode;
      return {
        success: false,
        error: {
          code,
          message: result?.message || 'License is invalid or expired.'
        },
        statusCode: 403
      };
    }

    // Generate fresh signed license cache
    const signedCache = await generateSignedLicenseCache(
      {
        license_id: result.license_id,
        device_id: result.device_id,
        product_code: result.product_code,
        status: 'active',
        expires_at: result.expires_at,
        offline_grace_hours: result.offline_grace_hours || 72,
        issued_at: new Date().toISOString()
      },
      this.env.LICENSE_SIGNING_KEY || this.env.JWT_SECRET
    );

    return {
      success: true,
      data: {
        valid: true,
        status: 'active',
        expires_at: result.expires_at,
        server_time: new Date().toISOString(),
        offline_grace_hours: result.offline_grace_hours || 72,
        signed_license_cache: signedCache
      }
    };
  }
}
