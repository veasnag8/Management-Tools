'use client';

import { useState, useEffect, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { getBrowserClient } from '@/lib/supabase/client';
import { License, Device, ActivationLog } from '@/types';
import Badge from '@/components/Badge';
import ConfirmationModal from '@/components/ConfirmationModal';
import { formatDate, formatRelativeTime } from '@/lib/utils';
import Link from 'next/link';

export default function LicenseDetailPage() {
  const { id } = useParams() as { id: string };
  const router = useRouter();
  const supabase = getBrowserClient();

  const [license, setLicense] = useState<License | null>(null);
  const [devices, setDevices] = useState<Device[]>([]);
  const [logs, setLogs] = useState<ActivationLog[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [copied, setCopied] = useState(false);

  // Modals state
  const [confirmModal, setConfirmModal] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    confirmText?: string;
    variant?: 'danger' | 'warning' | 'primary';
    onConfirm: () => Promise<void>;
  }>({
    isOpen: false,
    title: '',
    message: '',
    onConfirm: async () => {},
  });

  const [extendModalOpen, setExtendModalOpen] = useState(false);
  const [extensionDays, setExtensionDays] = useState(30);

  const fetchLicenseData = useCallback(async () => {
    try {
      const [licRes, devRes, logRes] = await Promise.all([
        supabase
          .from('licenses')
          .select('*, product:products(*)')
          .eq('id', id)
          .single(),
        supabase
          .from('devices')
          .select('*')
          .eq('license_id', id)
          .order('last_seen_at', { ascending: false }),
        supabase
          .from('activations')
          .select('*')
          .eq('license_id', id)
          .order('created_at', { ascending: false })
          .limit(20),
      ]);

      if (licRes.data) {
        setLicense(licRes.data as License);
      }
      if (devRes.data) {
        setDevices(devRes.data as Device[]);
      }
      if (logRes.data) {
        setLogs(logRes.data as ActivationLog[]);
      }
    } catch (err) {
      console.error('Error fetching license details:', err);
    } finally {
      setLoading(false);
    }
  }, [id, supabase]);

  useEffect(() => {
    fetchLicenseData();
  }, [fetchLicenseData]);

  const copyKey = () => {
    if (!license) return;
    navigator.clipboard.writeText(license.license_key);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  const handleUpdateStatus = (newStatus: 'active' | 'disabled' | 'revoked') => {
    setConfirmModal({
      isOpen: true,
      title: `Change Status to ${newStatus.toUpperCase()}`,
      message: `Are you sure you want to mark this license as "${newStatus}"? Clients on this license will be affected during their next validation cycle.`,
      variant: newStatus === 'active' ? 'primary' : 'danger',
      confirmText: `Set as ${newStatus}`,
      onConfirm: async () => {
        setActionLoading(true);
        try {
          await supabase
            .from('licenses')
            .update({ status: newStatus, updated_at: new Date().toISOString() })
            .eq('id', id);
          await fetchLicenseData();
        } finally {
          setActionLoading(false);
          setConfirmModal((prev) => ({ ...prev, isOpen: false }));
        }
      },
    });
  };

  const handleRevokeDevice = (deviceId: string, deviceName: string | null) => {
    setConfirmModal({
      isOpen: true,
      title: 'Revoke Device Slot',
      message: `Revoke hardware device "${deviceName || deviceId}"? The user will be required to re-authenticate or use another available slot.`,
      variant: 'danger',
      confirmText: 'Revoke Device',
      onConfirm: async () => {
        setActionLoading(true);
        try {
          await supabase
            .from('devices')
            .delete()
            .eq('license_id', id)
            .eq('device_id', deviceId);
          await fetchLicenseData();
        } finally {
          setActionLoading(false);
          setConfirmModal((prev) => ({ ...prev, isOpen: false }));
        }
      },
    });
  };

  const handleResetAllDevices = () => {
    setConfirmModal({
      isOpen: true,
      title: 'Reset All Registered Devices',
      message: 'This will purge all registered hardware bindings for this license. All active PCs will need to re-activate.',
      variant: 'warning',
      confirmText: 'Reset All Slots',
      onConfirm: async () => {
        setActionLoading(true);
        try {
          await supabase.from('devices').delete().eq('license_id', id);
          await fetchLicenseData();
        } finally {
          setActionLoading(false);
          setConfirmModal((prev) => ({ ...prev, isOpen: false }));
        }
      },
    });
  };

  const handleExtendExpiry = async () => {
    if (!license) return;
    setActionLoading(true);
    try {
      const currentExpiry = license.expires_at ? new Date(license.expires_at) : new Date();
      const baseDate = currentExpiry > new Date() ? currentExpiry : new Date();
      baseDate.setDate(baseDate.getDate() + extensionDays);

      await supabase
        .from('licenses')
        .update({
          expires_at: baseDate.toISOString(),
          status: license.status === 'expired' ? 'active' : license.status,
          updated_at: new Date().toISOString(),
        })
        .eq('id', id);

      setExtendModalOpen(false);
      await fetchLicenseData();
    } finally {
      setActionLoading(false);
    }
  };

  if (loading) {
    return (
      <div className="flex items-center justify-center min-h-[400px]">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-emerald-500"></div>
      </div>
    );
  }

  if (!license) {
    return (
      <div className="text-center py-12">
        <h2 className="text-xl font-semibold text-white">License not found</h2>
        <Link href="/admin/licenses" className="mt-4 inline-block text-emerald-400 hover:underline text-sm">
          Return to License List
        </Link>
      </div>
    );
  }

  const activeDeviceCount = devices.filter((d) => d.status === 'active').length;

  return (
    <div className="space-y-6">
      {/* Breadcrumbs */}
      <div className="flex items-center space-x-2 text-sm text-slate-400">
        <Link href="/admin/licenses" className="hover:text-emerald-400 transition-colors">
          Licenses
        </Link>
        <span>/</span>
        <span className="text-slate-100 font-mono text-xs">{license.license_key}</span>
      </div>

      {/* Header Bar */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl flex flex-col md:flex-row md:items-center justify-between gap-4">
        <div className="space-y-1">
          <div className="flex items-center space-x-3">
            <span className="font-mono text-xl md:text-2xl font-bold text-white tracking-wider">
              {license.license_key}
            </span>
            <button
              onClick={copyKey}
              className="p-1.5 text-slate-400 hover:text-emerald-400 bg-slate-800 hover:bg-slate-700 rounded-lg transition-colors"
              title="Copy License Key"
            >
              {copied ? (
                <span className="text-xs text-emerald-400 font-sans px-1">Copied!</span>
              ) : (
                <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 16H6a2 2 0 01-2-2V6a2 2 0 012-2h8a2 2 0 012 2v2m-6 12h8a2 2 0 002-2v-8a2 2 0 00-2-2h-8a2 2 0 00-2 2v8a2 2 0 002 2z" />
                </svg>
              )}
            </button>
            <Badge status={license.status} />
          </div>
          <p className="text-xs text-slate-400">
            Product:{' '}
            <span className="text-slate-200 font-medium">
              {license.product?.name || 'Standard Product'} ({license.product?.product_code})
            </span>{' '}
            • Created: {formatDate(license.created_at)}
          </p>
        </div>

        {/* Quick Actions */}
        <div className="flex flex-wrap items-center gap-2">
          {license.status === 'active' ? (
            <button
              onClick={() => handleUpdateStatus('disabled')}
              disabled={actionLoading}
              className="px-3 py-1.5 text-xs font-semibold text-amber-400 border border-amber-500/30 bg-amber-500/10 hover:bg-amber-500/20 rounded-lg transition-colors"
            >
              Disable License
            </button>
          ) : (
            <button
              onClick={() => handleUpdateStatus('active')}
              disabled={actionLoading}
              className="px-3 py-1.5 text-xs font-semibold text-emerald-400 border border-emerald-500/30 bg-emerald-500/10 hover:bg-emerald-500/20 rounded-lg transition-colors"
            >
              Enable License
            </button>
          )}

          <button
            onClick={() => setExtendModalOpen(true)}
            disabled={actionLoading}
            className="px-3 py-1.5 text-xs font-semibold text-cyan-400 border border-cyan-500/30 bg-cyan-500/10 hover:bg-cyan-500/20 rounded-lg transition-colors"
          >
            Extend Expiry
          </button>

          <button
            onClick={() => handleUpdateStatus('revoked')}
            disabled={actionLoading}
            className="px-3 py-1.5 text-xs font-semibold text-rose-400 border border-rose-500/30 bg-rose-500/10 hover:bg-rose-500/20 rounded-lg transition-colors"
          >
            Revoke
          </button>
        </div>
      </div>

      {/* Grid: Details & Metrics */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Customer & Policy info */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400">Customer Details</h3>
          <div className="space-y-2 text-sm">
            <div>
              <span className="text-slate-500 text-xs block">Customer Name</span>
              <span className="text-slate-200 font-medium">{license.customer_name || 'Anonymous / Unassigned'}</span>
            </div>
            <div>
              <span className="text-slate-500 text-xs block">Customer Email</span>
              <span className="text-slate-200 font-medium">{license.customer_email || 'None'}</span>
            </div>
            <div>
              <span className="text-slate-500 text-xs block">Internal Notes</span>
              <p className="text-slate-300 text-xs bg-slate-950 p-2.5 rounded-lg border border-slate-800/80 mt-1">
                {license.notes || 'No notes provided.'}
              </p>
            </div>
          </div>
        </div>

        {/* Validity & Grace */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4">
          <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400">Validity & Policy</h3>
          <div className="space-y-2 text-sm">
            <div>
              <span className="text-slate-500 text-xs block">Valid From</span>
              <span className="text-slate-200 font-medium">{formatDate(license.start_at)}</span>
            </div>
            <div>
              <span className="text-slate-500 text-xs block">Expiration</span>
              <span className="text-slate-200 font-medium">
                {license.expires_at ? formatDate(license.expires_at) : 'Lifetime (No Expiry)'}
              </span>
            </div>
            <div>
              <span className="text-slate-500 text-xs block">Offline Grace Period</span>
              <span className="text-slate-200 font-medium">{license.offline_grace_hours} Hours</span>
            </div>
          </div>
        </div>

        {/* Device Slot Meter */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-5 shadow-xl space-y-4 flex flex-col justify-between">
          <div>
            <h3 className="text-xs font-bold uppercase tracking-wider text-slate-400">Hardware Slots</h3>
            <div className="mt-3 flex items-baseline space-x-2">
              <span className="text-3xl font-black text-white">{activeDeviceCount}</span>
              <span className="text-sm font-semibold text-slate-400">/ {license.max_devices} slots used</span>
            </div>
            {/* Progress Bar */}
            <div className="w-full bg-slate-950 rounded-full h-2 mt-3 overflow-hidden border border-slate-800">
              <div
                className={`h-full transition-all ${
                  activeDeviceCount >= license.max_devices ? 'bg-amber-500' : 'bg-emerald-500'
                }`}
                style={{ width: `${Math.min(100, (activeDeviceCount / license.max_devices) * 100)}%` }}
              />
            </div>
          </div>

          <div className="pt-2">
            <button
              onClick={handleResetAllDevices}
              disabled={devices.length === 0 || actionLoading}
              className="w-full py-2 px-3 text-xs font-semibold text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 disabled:opacity-40 rounded-xl transition-colors"
            >
              Reset All Device Slots
            </button>
          </div>
        </div>
      </div>

      {/* Hardware Devices Section */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-base font-bold text-white">Registered Hardware Devices</h3>
            <p className="text-xs text-slate-400">PC Hardware fingerprints linked to this license.</p>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/50 border-y border-slate-800">
              <tr>
                <th className="py-3 px-4">Device ID (PC Fingerprint)</th>
                <th className="py-3 px-4">PC Name / OS</th>
                <th className="py-3 px-4">App Version</th>
                <th className="py-3 px-4">Last Seen</th>
                <th className="py-3 px-4">IP Address</th>
                <th className="py-3 px-4 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {devices.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-8 text-center text-slate-500">
                    No hardware devices currently registered.
                  </td>
                </tr>
              ) : (
                devices.map((d) => (
                  <tr key={d.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-3 px-4 font-mono font-medium text-emerald-400">{d.device_id}</td>
                    <td className="py-3 px-4 text-slate-300">
                      <div className="font-medium text-white">{d.device_name || 'Windows Machine'}</div>
                      <div className="text-[11px] text-slate-500">
                        {d.os_name} {d.os_version}
                      </div>
                    </td>
                    <td className="py-3 px-4 text-slate-300">{d.app_version ? `v${d.app_version}` : 'v1.0.0'}</td>
                    <td className="py-3 px-4 text-slate-400" title={formatDate(d.last_seen_at)}>
                      {formatRelativeTime(d.last_seen_at)}
                    </td>
                    <td className="py-3 px-4 font-mono text-slate-400">{d.last_ip_address || '—'}</td>
                    <td className="py-3 px-4 text-right">
                      <button
                        onClick={() => handleRevokeDevice(d.device_id, d.device_name)}
                        className="text-rose-400 hover:text-rose-300 font-semibold text-xs hover:underline"
                      >
                        Revoke Slot
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Telemetry & Activation Activity Log */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-4">
        <div>
          <h3 className="text-base font-bold text-white">Recent Telemetry & Activations</h3>
          <p className="text-xs text-slate-400">Last 20 API events recorded for this license key.</p>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/50 border-y border-slate-800">
              <tr>
                <th className="py-3 px-4">Action</th>
                <th className="py-3 px-4">Device ID</th>
                <th className="py-3 px-4">IP Address</th>
                <th className="py-3 px-4">Timestamp</th>
                <th className="py-3 px-4">User Agent</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-mono">
              {logs.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-6 text-center text-slate-500 font-sans">
                    No activity logs recorded yet.
                  </td>
                </tr>
              ) : (
                logs.map((log) => (
                  <tr key={log.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-2.5 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 rounded text-[11px] font-bold ${
                          log.action === 'activate'
                            ? 'bg-emerald-500/20 text-emerald-400'
                            : log.action === 'validate' || log.action === 'heartbeat'
                            ? 'bg-cyan-500/20 text-cyan-400'
                            : log.action === 'deactivate' || log.action === 'reset'
                            ? 'bg-amber-500/20 text-amber-400'
                            : 'bg-rose-500/20 text-rose-400'
                        }`}
                      >
                        {log.action}
                      </span>
                    </td>
                    <td className="py-2.5 px-4 text-slate-300 text-[11px]">{log.device_id || '—'}</td>
                    <td className="py-2.5 px-4 text-slate-400 text-[11px]">{log.ip_address || '—'}</td>
                    <td className="py-2.5 px-4 text-slate-400 font-sans text-[11px]">{formatDate(log.created_at)}</td>
                    <td className="py-2.5 px-4 text-slate-500 font-sans text-[11px] truncate max-w-xs">
                      {log.user_agent || 'Windows Client'}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Confirmation Modal */}
      <ConfirmationModal
        isOpen={confirmModal.isOpen}
        title={confirmModal.title}
        message={confirmModal.message}
        confirmText={confirmModal.confirmText}
        variant={confirmModal.variant}
        onConfirm={confirmModal.onConfirm}
        onCancel={() => setConfirmModal((prev) => ({ ...prev, isOpen: false }))}
      />

      {/* Extend Expiry Modal */}
      {extendModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fadeIn">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-4">
            <h3 className="text-lg font-bold text-white">Extend License Expiry</h3>
            <p className="text-xs text-slate-400">
              Select the number of days to extend the expiration date from today or the existing expiration.
            </p>

            <div className="grid grid-cols-3 gap-2 py-2">
              {[30, 90, 365].map((days) => (
                <button
                  key={days}
                  type="button"
                  onClick={() => setExtensionDays(days)}
                  className={`py-2 px-3 text-xs font-semibold rounded-xl border transition-all ${
                    extensionDays === days
                      ? 'border-cyan-500 bg-cyan-500/10 text-cyan-400'
                      : 'border-slate-800 bg-slate-950 text-slate-400 hover:text-white'
                  }`}
                >
                  +{days} Days
                </button>
              ))}
            </div>

            <div>
              <label className="block text-xs font-semibold text-slate-400 mb-1">Custom Days</label>
              <input
                type="number"
                min="1"
                max="3650"
                value={extensionDays}
                onChange={(e) => setExtensionDays(parseInt(e.target.value) || 1)}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-sm text-white"
              />
            </div>

            <div className="flex justify-end space-x-3 pt-3 border-t border-slate-800">
              <button
                type="button"
                onClick={() => setExtendModalOpen(false)}
                className="px-4 py-2 text-xs font-medium text-slate-400 hover:text-white bg-slate-800 rounded-xl"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleExtendExpiry}
                disabled={actionLoading}
                className="px-4 py-2 text-xs font-semibold text-slate-950 bg-cyan-400 hover:bg-cyan-300 rounded-xl"
              >
                {actionLoading ? 'Extending...' : 'Confirm Extension'}
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
