'use client';

import { useState, useEffect, useCallback } from 'react';
import { getBrowserClient } from '@/lib/supabase/client';
import { Device } from '@/types';
import ConfirmationModal from '@/components/ConfirmationModal';
import { formatDate, formatRelativeTime } from '@/lib/utils';
import Link from 'next/link';

interface DeviceWithLicense extends Omit<Device, 'license' | 'licenses'> {
  license?: {
    id: string;
    license_key: string;
    customer_name: string | null;
    status: string;
  };
}

export default function DevicesPage() {
  const supabase = getBrowserClient();
  const [devices, setDevices] = useState<DeviceWithLicense[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<'all' | 'active' | 'revoked'>('all');

  const [confirmModal, setConfirmModal] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    onConfirm: () => Promise<void>;
  }>({
    isOpen: false,
    title: '',
    message: '',
    onConfirm: async () => {},
  });

  const fetchDevices = useCallback(async () => {
    try {
      let query = supabase
        .from('devices')
        .select('*, license:licenses(id, license_key, customer_name, status)')
        .order('last_seen_at', { ascending: false });

      if (statusFilter !== 'all') {
        query = query.eq('status', statusFilter);
      }

      const { data, error } = await query;
      if (!error && data) {
        setDevices(data as DeviceWithLicense[]);
      }
    } catch (err) {
      console.error('Error loading devices:', err);
    } finally {
      setLoading(false);
    }
  }, [statusFilter, supabase]);

  useEffect(() => {
    fetchDevices();
  }, [fetchDevices]);

  const handleRevokeDevice = (device: DeviceWithLicense) => {
    setConfirmModal({
      isOpen: true,
      title: 'Revoke Device Binding',
      message: `Revoke device ${device.device_id} (${device.device_name || 'PC'}) from license ${device.license?.license_key}? This machine will no longer pass online or offline validation until re-activated.`,
      onConfirm: async () => {
        try {
          await supabase.from('devices').delete().eq('id', device.id);
          await fetchDevices();
        } finally {
          setConfirmModal((prev) => ({ ...prev, isOpen: false }));
        }
      },
    });
  };

  const filteredDevices = devices.filter((d) => {
    const term = search.toLowerCase();
    return (
      d.device_id.toLowerCase().includes(term) ||
      (d.device_name && d.device_name.toLowerCase().includes(term)) ||
      (d.last_ip_address && d.last_ip_address.toLowerCase().includes(term)) ||
      (d.license?.license_key && d.license.license_key.toLowerCase().includes(term)) ||
      (d.license?.customer_name && d.license.customer_name.toLowerCase().includes(term))
    );
  });

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Registered Devices</h1>
          <p className="text-sm text-slate-400 mt-1">
            Hardware fingerprints and active PC seats bound to licenses.
          </p>
        </div>
      </div>

      {/* Filter / Search Bar */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl flex flex-col sm:flex-row items-center gap-4">
        <div className="relative flex-1 w-full">
          <svg className="w-4 h-4 absolute left-3.5 top-3.5 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search by Device ID, License Key, PC Name, or IP..."
            className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl pl-10 pr-4 py-2 text-sm text-white placeholder-slate-600 transition-colors"
          />
        </div>

        <div className="flex items-center space-x-2 w-full sm:w-auto">
          {(['all', 'active', 'revoked'] as const).map((s) => (
            <button
              key={s}
              type="button"
              onClick={() => setStatusFilter(s)}
              className={`px-3 py-1.5 text-xs font-semibold rounded-xl border transition-all capitalize ${
                statusFilter === s
                  ? 'border-emerald-500 bg-emerald-500/10 text-emerald-400'
                  : 'border-slate-800 bg-slate-950 text-slate-400 hover:text-white'
              }`}
            >
              {s}
            </button>
          ))}
        </div>
      </div>

      {/* Devices Table */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/70 border-b border-slate-800">
              <tr>
                <th className="py-3.5 px-4 font-semibold">Device Fingerprint</th>
                <th className="py-3.5 px-4 font-semibold">Attached License</th>
                <th className="py-3.5 px-4 font-semibold">Computer & OS</th>
                <th className="py-3.5 px-4 font-semibold">App Version</th>
                <th className="py-3.5 px-4 font-semibold">Last Seen</th>
                <th className="py-3.5 px-4 font-semibold">IP Address</th>
                <th className="py-3.5 px-4 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {loading ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-slate-400">
                    <div className="flex justify-center items-center space-x-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-emerald-500"></div>
                      <span>Loading hardware devices...</span>
                    </div>
                  </td>
                </tr>
              ) : filteredDevices.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-slate-500">
                    No registered devices matching criteria.
                  </td>
                </tr>
              ) : (
                filteredDevices.map((d) => (
                  <tr key={d.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-3.5 px-4 font-mono font-medium text-emerald-400">
                      {d.device_id}
                    </td>
                    <td className="py-3.5 px-4">
                      {d.license ? (
                        <div>
                          <Link
                            href={`/admin/licenses/${d.license.id}`}
                            className="font-mono font-medium text-cyan-400 hover:underline"
                          >
                            {d.license.license_key}
                          </Link>
                          {d.license.customer_name && (
                            <span className="block text-[11px] text-slate-500">{d.license.customer_name}</span>
                          )}
                        </div>
                      ) : (
                        <span className="text-slate-500 font-mono">Orphaned</span>
                      )}
                    </td>
                    <td className="py-3.5 px-4 text-slate-300">
                      <div className="font-medium text-white">{d.device_name || 'Windows Machine'}</div>
                      <div className="text-[11px] text-slate-500">
                        {d.os_name} {d.os_version}
                      </div>
                    </td>
                    <td className="py-3.5 px-4 text-slate-300">
                      <span className="bg-slate-950 px-2 py-0.5 rounded border border-slate-800 font-mono">
                        {d.app_version ? `v${d.app_version}` : 'v1.0.0'}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-slate-400" title={formatDate(d.last_seen_at)}>
                      {formatRelativeTime(d.last_seen_at)}
                    </td>
                    <td className="py-3.5 px-4 font-mono text-slate-400">
                      {d.last_ip_address || '—'}
                    </td>
                    <td className="py-3.5 px-4 text-right">
                      <button
                        onClick={() => handleRevokeDevice(d)}
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

      <ConfirmationModal
        isOpen={confirmModal.isOpen}
        title={confirmModal.title}
        message={confirmModal.message}
        confirmText="Revoke Device"
        variant="danger"
        onConfirm={confirmModal.onConfirm}
        onCancel={() => setConfirmModal((prev) => ({ ...prev, isOpen: false }))}
      />
    </div>
  );
}
