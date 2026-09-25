import { SupabaseClient } from '@supabase/supabase-js';
import { Env, HeartbeatRequest, HeartbeatResponseData, ServiceResult } from '../types';

export class DeviceService {
  constructor(
    private supabase: SupabaseClient,
    private env: Env
  ) {}

  /**
   * Update heartbeat telemetry for an active device
   */
  async heartbeat(
    licenseId: string,
    deviceId: string,
    req: HeartbeatRequest,
    ipAddress: string
  ): Promise<ServiceResult<HeartbeatResponseData>> {
    // 1. Verify license & device status
    const { data: license, error: licErr } = await this.supabase
      .from('licenses')
      .select('id, status, expires_at')
      .eq('id', licenseId)
      .single();

    if (licErr || !license) {
      return {
        success: false,
        error: { code: 'INVALID_LICENSE', message: 'License not found.' },
        statusCode: 404
      };
    }

    if (license.status !== 'active') {
      return {
        success: false,
        error: { code: 'LICENSE_DISABLED', message: `License status is ${license.status}.` },
        statusCode: 403
      };
    }

    if (license.expires_at && new Date(license.expires_at) <= new Date()) {
      return {
        success: false,
        error: { code: 'LICENSE_EXPIRED', message: 'License has expired.' },
        statusCode: 403
      };
    }

    // 2. Update device last seen
    const { data: device, error: devErr } = await this.supabase
      .from('devices')
      .update({
        last_seen_at: new Date().toISOString(),
        last_ip_address: ipAddress || null,
        app_version: req.app_version || undefined,
        device_name: req.device_name || undefined
      })
      .eq('license_id', licenseId)
      .eq('device_id', deviceId)
      .eq('status', 'active')
      .select()
      .single();

    if (devErr || !device) {
      return {
        success: false,
        error: { code: 'DEVICE_REVOKED', message: 'Device is not registered or has been revoked.' },
        statusCode: 403
      };
    }

    return {
      success: true,
      data: {
        acknowledged: true,
        server_time: new Date().toISOString(),
        status: license.status,
        expires_at: license.expires_at
      }
    };
  }

  /**
   * Deactivate a device from client side
   */
  async deactivate(
    licenseId: string,
    deviceId: string,
    reason: string,
    ipAddress: string,
    userAgent: string
  ): Promise<ServiceResult<{ deactivated: boolean }>> {
    // Update device status to revoked
    const { error: devErr } = await this.supabase
      .from('devices')
      .update({
        status: 'revoked',
        updated_at: new Date().toISOString()
      })
      .eq('license_id', licenseId)
      .eq('device_id', deviceId);

    if (devErr) {
      return {
        success: false,
        error: { code: 'SERVER_ERROR', message: 'Failed to deactivate device.' },
        statusCode: 500
      };
    }

    // Log deactivation
    await this.supabase.from('activations').insert({
      license_id: licenseId,
      device_id: deviceId,
      action: 'deactivate',
      ip_address: ipAddress || null,
      user_agent: userAgent || null,
      metadata: { reason: reason || 'Client initiated deactivation' }
    });

    return {
      success: true,
      data: { deactivated: true }
    };
  }

  /**
   * Admin-only Device Reset
   */
  async resetDeviceByAdmin(
    licenseId: string,
    deviceId: string,
    adminId: string,
    reason: string
  ): Promise<ServiceResult<{ reset: boolean }>> {
    // 1. Fetch current device
    const { data: device, error: devFetchErr } = await this.supabase
      .from('devices')
      .select('*')
      .eq('license_id', licenseId)
      .eq('device_id', deviceId)
      .single();

    if (devFetchErr || !device) {
      return {
        success: false,
        error: { code: 'INVALID_REQUEST', message: 'Device not found on this license.' },
        statusCode: 404
      };
    }

    // 2. Mark device revoked or delete to free up slot
    const { error: delErr } = await this.supabase
      .from('devices')
      .update({
        status: 'revoked',
        updated_at: new Date().toISOString()
      })
      .eq('id', device.id);

    if (delErr) {
      return {
        success: false,
        error: { code: 'SERVER_ERROR', message: 'Failed to reset device.' },
        statusCode: 500
      };
    }

    // 3. Log activation audit
    await this.supabase.from('activations').insert({
      license_id: licenseId,
      device_id: deviceId,
      action: 'reset',
      metadata: {
        admin_id: adminId,
        reason: reason || 'Admin device reset for transfer'
      }
    });

    // 4. Log admin audit
    await this.supabase.from('license_logs').insert({
      license_id: licenseId,
      admin_id: adminId,
      action: 'RESET_DEVICE',
      old_value: { device_id: deviceId, status: device.status },
      new_value: { device_id: deviceId, status: 'revoked', reason }
    });

    return {
      success: true,
      data: { reset: true }
    };
  }
}
