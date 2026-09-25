export interface Env {
  SUPABASE_URL: string;
  SUPABASE_SERVICE_ROLE_KEY: string;
  JWT_SECRET: string;
  LICENSE_SIGNING_KEY: string; // HMAC or Ed25519 seed used for server-signed cache
  ENVIRONMENT?: string;
  DEFAULT_OFFLINE_GRACE_HOURS?: string;
}

export type ApiErrorCode =
  | 'SUCCESS'
  | 'INVALID_REQUEST'
  | 'INVALID_LICENSE'
  | 'LICENSE_EXPIRED'
  | 'LICENSE_DISABLED'
  | 'LICENSE_REVOKED'
  | 'DEVICE_LIMIT_REACHED'
  | 'DEVICE_REVOKED'
  | 'INVALID_DEVICE_TOKEN'
  | 'PRODUCT_DISABLED'
  | 'NOT_STARTED'
  | 'RATE_LIMITED'
  | 'SERVER_ERROR'
  | 'OFFLINE_NOT_ALLOWED'
  | 'UNAUTHORIZED';

export interface ApiResponse<T = any> {
  success: boolean;
  data: T | null;
  error: {
    code: ApiErrorCode;
    message: string;
  } | null;
}

export interface ActivateRequest {
  license_key: string;
  device_id: string;
  device_name?: string;
  os_name?: string;
  os_version?: string;
  app_version?: string;
}

export interface ActivateResponseData {
  status: string;
  expires_at: string | null;
  device_token: string;
  server_time: string;
  offline_grace_hours: number;
  product_code: string;
  signed_license_cache: string; // Cryptographically signed payload for offline verification
}

export interface ValidateRequest {
  device_id?: string;
  app_version?: string;
}

export interface ValidateResponseData {
  valid: boolean;
  status: string;
  expires_at: string | null;
  server_time: string;
  offline_grace_hours: number;
  signed_license_cache: string;
}

export interface HeartbeatRequest {
  app_version?: string;
  device_name?: string;
}

export interface HeartbeatResponseData {
  acknowledged: boolean;
  server_time: string;
  status: string;
  expires_at: string | null;
}

export interface DeactivateRequest {
  reason?: string;
}

export interface ResetDeviceRequest {
  license_id: string;
  device_id: string;
  reason?: string;
}

export interface VersionResponseData {
  product_code: string;
  current_version: string;
  latest_version: string;
  download_url: string | null;
  release_notes: string | null;
  sha256: string | null;
  mandatory: boolean;
  has_update: boolean;
}

export interface DeviceTokenPayload {
  sub: string; // device_id
  license_id: string;
  product_id?: string;
  iat: number;
  exp: number;
}

export interface SignedLicensePayload {
  license_id: string;
  device_id: string;
  product_code: string;
  status: string;
  expires_at: string | null;
  offline_grace_hours: number;
  issued_at: string;
}
