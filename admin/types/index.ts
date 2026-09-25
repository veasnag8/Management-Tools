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
  product?: Product;
  products?: Product;
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
  first_activated_at: string;
  last_seen_at: string;
  last_ip_address: string | null;
  status: DeviceStatus;
  created_at: string;
  updated_at: string;
  license?: License;
  licenses?: License;
}

export interface DeviceWithLicense extends Device {
  license?: License;
  licenses?: License;
}

export interface ActivationLog {
  id: string;
  license_id: string | null;
  device_id: string | null;
  action: 'activate' | 'validate' | 'heartbeat' | 'deactivate' | 'reset' | 'blocked' | 'expired';
  ip_address: string | null;
  user_agent: string | null;
  metadata: any;
  created_at: string;
  license?: License;
  licenses?: License;
}

export interface LicenseLog {
  id: string;
  license_id: string | null;
  admin_id: string | null;
  action: string;
  old_value: any;
  new_value: any;
  created_at: string;
  profile?: UserProfile;
  profiles?: UserProfile;
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
  product?: Product;
  products?: Product;
}

export interface DashboardStats {
  totalLicenses: number;
  activeLicenses: number;
  expiredLicenses: number;
  disabledLicenses: number;
  activeDevices: number;
  expiring7Days: number;
  expiring30Days: number;
}
