export type UserRole = 'admin' | 'manager' | 'support';

export interface UserProfile {
  id: string;
  full_name: string;
  role: UserRole;
  created_at: string;
  updated_at: string;
}

export interface Product {
  id: string;
  product_code: string;
  name: string;
  description: string | null;
  current_version: string | null;
  download_url: string | null;
  active: boolean;
  created_at: string;
  updated_at: string;
}

export type LicenseStatus = 'pending' | 'active' | 'disabled' | 'expired' | 'revoked';

export interface License {
  id: string;
  license_key: string;
  customer_name: string | null;
  customer_email: string | null;
  product_id: string;
  status: LicenseStatus;
  start_at: string;
  expires_at: string | null;
  max_devices: number;
  offline_grace_hours: number;
  notes: string | null;
  created_by: string | null;
  created_at: string;
  updated_at: string;
  products?: Product;
  product?: Product;
  devices?: Device[];
  device_count?: number;
}

export type DeviceStatus = 'active' | 'revoked';

export interface Device {
  id: string;
  license_id: string;
  device_id: string;
  device_name: string | null;
  os_name: string | null;
  os_version: string | null;
  app_version: string | null;
  ip_address: string | null;
  last_ip_address?: string | null;
  status: DeviceStatus;
  first_seen_at: string;
  last_seen_at: string;
  revoked_at: string | null;
  revocation_reason: string | null;
}

export interface LicenseLog {
  id: string;
  license_id: string;
  admin_id: string | null;
  action: string;
  old_value: any;
  new_value: any;
  created_at: string;
}

export interface ReleaseVersion {
  id: string;
  product_id: string;
  version: string;
  download_url: string | null;
  release_notes: string | null;
  sha256: string | null;
  mandatory: boolean;
  active: boolean;
  created_at: string;
}

export interface ApiResponse<T = any> {
  success: boolean;
  data?: T;
  error?: {
    code: string;
    message: string;
  };
}

export interface DashboardStats {
  totalLicenses: number;
  activeLicenses: number;
  expiredLicenses: number;
  disabledLicenses: number;
  activeDevices: number;
  expiring7Days: number;
  expiring30Days: number;
  recentActivationsCount?: number;
}


export interface TokenUsage {
  id: string;
  license_id: string;
  device_id: string;
  model_name: string;
  prompt_tokens: number;
  completion_tokens: number;
  total_tokens: number;
  task_type: string;
  task_description: string | null;
  created_at: string;
}

export interface ActivationLog {
  id: string;
  license_id: string;
  device_id: string | null;
  action: string;
  ip_address: string | null;
  user_agent: string | null;
  metadata?: any;
  created_at: string;
  licenses?: {
    license_key?: string;
    customer_name?: string | null;
  };
}
