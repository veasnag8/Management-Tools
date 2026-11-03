'use client';

import { useState, useEffect, useCallback } from 'react';
import { getBrowserClient } from '@/lib/supabase/client';
import { formatDate } from '@/lib/utils';
import Link from 'next/link';

interface LogItem {
  id: string;
  type: 'activation' | 'admin';
  action: string;
  license_key?: string;
  license_id?: string;
  device_id?: string;
  ip_address?: string;
  user_agent?: string;
  created_at: string;
  metadata?: any;
  old_value?: any;
  new_value?: any;
}

export default function LogsPage() {
  const supabase = getBrowserClient();
  const [logs, setLogs] = useState<LogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [clearing, setClearing] = useState(false);
  const [clearModalOpen, setClearModalOpen] = useState(false);
  const [clearScope, setClearScope] = useState<'all' | 'client' | 'admin'>('all');
  const [activeTab, setActiveTab] = useState<'all' | 'client' | 'admin'>('all');
  const [search, setSearch] = useState('');
  const [selectedLog, setSelectedLog] = useState<LogItem | null>(null);
  const [toastMessage, setToastMessage] = useState<string | null>(null);

  const fetchLogs = useCallback(async () => {
    try {
      const [actRes, admRes] = await Promise.all([
        supabase
          .from('activations')
          .select('*, license:licenses(license_key)')
          .order('created_at', { ascending: false })
          .limit(100),
        supabase
          .from('license_logs')
          .select('*, license:licenses(license_key)')
          .order('created_at', { ascending: false })
          .limit(100),
      ]);

      const combined: LogItem[] = [];

      if (actRes.data) {
        actRes.data.forEach((a: any) => {
          combined.push({
            id: a.id,
            type: 'activation',
            action: a.action,
            license_key: a.license?.license_key,
            license_id: a.license_id,
            device_id: a.device_id,
            ip_address: a.ip_address,
            user_agent: a.user_agent,
            created_at: a.created_at,
            metadata: a.metadata,
          });
        });
      }

      if (admRes.data) {
        admRes.data.forEach((ad: any) => {
          combined.push({
            id: ad.id,
            type: 'admin',
            action: `ADMIN: ${ad.action}`,
            license_key: ad.license?.license_key,
            license_id: ad.license_id,
            created_at: ad.created_at,
            old_value: ad.old_value,
            new_value: ad.new_value,
          });
        });
      }

      // Sort by timestamp desc
      combined.sort((a, b) => new Date(b.created_at).getTime() - new Date(a.created_at).getTime());
      setLogs(combined);
    } catch (err) {
      console.error('Error loading logs:', err);
    } finally {
      setLoading(false);
    }
  }, [supabase]);

  useEffect(() => {
    fetchLogs();
  }, [fetchLogs]);

  const handleClearLogs = async () => {
    setClearing(true);
    try {
      let apiSuccess = false;

      // 1. Call Next.js API route (service role backed)
      try {
        const res = await fetch('/api/admin/logs/clear', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify({ scope: clearScope }),
        });
        const data = await res.json();
        if (data?.success) {
          apiSuccess = true;
        }
      } catch (apiErr) {
        console.warn('API clear error, fallback to direct Supabase delete:', apiErr);
      }

      // 2. Direct client fallback if API route failed
      if (!apiSuccess) {
        if (clearScope === 'client') {
          await supabase.from('activations').delete().neq('id', '00000000-0000-0000-0000-000000000000');
        } else if (clearScope === 'admin') {
          await supabase.from('license_logs').delete().neq('id', '00000000-0000-0000-0000-000000000000');
        } else {
          await Promise.all([
            supabase.from('activations').delete().neq('id', '00000000-0000-0000-0000-000000000000'),
            supabase.from('license_logs').delete().neq('id', '00000000-0000-0000-0000-000000000000'),
          ]);
        }
      }

      setClearModalOpen(false);
      setToastMessage('Activity logs cleared successfully.');
      setTimeout(() => setToastMessage(null), 4000);
      setLoading(true);
      await fetchLogs();
    } catch (err: any) {
      console.error('Failed to clear logs:', err);
      alert('Failed to clear logs: ' + (err.message || 'Unknown error'));
    } finally {
      setClearing(false);
    }
  };

  const filteredLogs = logs.filter((log) => {
    if (activeTab === 'client' && log.type !== 'activation') return false;
    if (activeTab === 'admin' && log.type !== 'admin') return false;

    if (!search) return true;
    const term = search.toLowerCase();
    return (
      log.action.toLowerCase().includes(term) ||
      (log.license_key && log.license_key.toLowerCase().includes(term)) ||
      (log.device_id && log.device_id.toLowerCase().includes(term)) ||
      (log.ip_address && log.ip_address.toLowerCase().includes(term))
    );
  });

  return (
    <div className="space-y-6">
      {/* Toast Notification */}
      {toastMessage && (
        <div className="fixed top-6 right-6 z-50 bg-emerald-500/90 text-slate-950 font-semibold px-4 py-2.5 rounded-xl shadow-2xl backdrop-blur-md text-xs flex items-center space-x-2 animate-fadeIn">
          <svg className="w-4 h-4 text-slate-950" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2.5} d="M5 13l4 4L19 7" />
          </svg>
          <span>{toastMessage}</span>
        </div>
      )}

      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Audit & Telemetry Logs</h1>
          <p className="text-sm text-slate-400 mt-1">
            Real-time event stream of admin operations and client validation/heartbeat pings.
          </p>
        </div>

        <div className="flex items-center space-x-3 self-start sm:self-auto">
          {/* Refresh Logs Button */}
          <button
            type="button"
            onClick={() => {
              setLoading(true);
              fetchLogs();
            }}
            className="inline-flex items-center space-x-2 px-3.5 py-2 bg-slate-900 border border-slate-800 hover:bg-slate-800 text-slate-300 text-xs font-semibold rounded-xl transition-all"
          >
            <svg className="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 4v5h.582m15.356 2A8.001 8.001 0 004.582 9m0 0H9m11 11v-5h-.581m0 0a8.003 8.003 0 01-15.357-2m15.357 2H15" />
            </svg>
            <span>Refresh Logs</span>
          </button>

          {/* Clear Logs Button */}
          <button
            type="button"
            onClick={() => {
              setClearScope(activeTab === 'client' ? 'client' : activeTab === 'admin' ? 'admin' : 'all');
              setClearModalOpen(true);
            }}
            disabled={loading || clearing || logs.length === 0}
            className="inline-flex items-center space-x-2 px-3.5 py-2 bg-rose-500/10 border border-rose-500/30 hover:bg-rose-500/20 text-rose-400 hover:text-rose-300 text-xs font-semibold rounded-xl transition-all shadow-sm disabled:opacity-40 disabled:cursor-not-allowed"
            title="Clear activity and telemetry logs"
          >
            <svg className="w-4 h-4 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
              <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
            </svg>
            <span>Clear Logs</span>
          </button>
        </div>
      </div>

      {/* Filter and Tab controls */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl p-4 shadow-xl flex flex-col sm:flex-row items-center justify-between gap-4">
        <div className="flex items-center space-x-2 bg-slate-950 p-1 border border-slate-800 rounded-xl w-full sm:w-auto">
          {[
            { id: 'all', label: 'All Activity' },
            { id: 'client', label: 'Client Telemetry' },
            { id: 'admin', label: 'Admin Audit' },
          ].map((tab) => (
            <button
              key={tab.id}
              type="button"
              onClick={() => setActiveTab(tab.id as any)}
              className={`px-3 py-1.5 text-xs font-semibold rounded-lg transition-all ${
                activeTab === tab.id
                  ? 'bg-emerald-500 text-slate-950 shadow-md'
                  : 'text-slate-400 hover:text-white'
              }`}
            >
              {tab.label}
            </button>
          ))}
        </div>

        <div className="relative w-full sm:w-80">
          <svg className="w-4 h-4 absolute left-3.5 top-3 text-slate-500" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 21l-6-6m2-5a7 7 0 11-14 0 7 7 0 0114 0z" />
          </svg>
          <input
            type="text"
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            placeholder="Search action, key, PC ID, IP..."
            className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl pl-10 pr-4 py-2 text-xs text-white placeholder-slate-600"
          />
        </div>
      </div>

      {/* Logs Table */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/70 border-b border-slate-800 font-semibold">
              <tr>
                <th className="py-3.5 px-4">Type / Action</th>
                <th className="py-3.5 px-4">License Key</th>
                <th className="py-3.5 px-4">Device ID</th>
                <th className="py-3.5 px-4">IP Address</th>
                <th className="py-3.5 px-4">Timestamp</th>
                <th className="py-3.5 px-4 text-right">Details</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60 font-mono">
              {loading ? (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-slate-400 font-sans">
                    <div className="flex justify-center items-center space-x-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-emerald-500"></div>
                      <span>Streaming event logs...</span>
                    </div>
                  </td>
                </tr>
              ) : filteredLogs.length === 0 ? (
                <tr>
                  <td colSpan={6} className="py-12 text-center text-slate-500 font-sans">
                    No log events found.
                  </td>
                </tr>
              ) : (
                filteredLogs.map((log) => (
                  <tr key={log.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-3 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 rounded text-[11px] font-bold ${
                          log.type === 'admin'
                            ? 'bg-purple-500/20 text-purple-400 border border-purple-500/30'
                            : log.action === 'activate'
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
                    <td className="py-3 px-4">
                      {log.license_key ? (
                        <Link
                          href={`/admin/licenses/${log.license_id}`}
                          className="text-cyan-400 hover:underline"
                        >
                          {log.license_key}
                        </Link>
                      ) : (
                        <span className="text-slate-600">—</span>
                      )}
                    </td>
                    <td className="py-3 px-4 text-slate-300 text-[11px]">
                      {log.device_id || '—'}
                    </td>
                    <td className="py-3 px-4 text-slate-400 text-[11px]">
                      {log.ip_address || '—'}
                    </td>
                    <td className="py-3 px-4 text-slate-400 font-sans text-[11px]">
                      {formatDate(log.created_at)}
                    </td>
                    <td className="py-3 px-4 text-right font-sans">
                      {(log.metadata || log.old_value || log.new_value) && (
                        <button
                          onClick={() => setSelectedLog(log)}
                          className="text-emerald-400 hover:text-emerald-300 hover:underline text-xs font-semibold"
                        >
                          View Payload
                        </button>
                      )}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Confirmation Modal for Clear Logs */}
      {clearModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fadeIn">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-md w-full p-6 shadow-2xl space-y-5">
            <div className="flex items-start space-x-3">
              <div className="w-10 h-10 rounded-xl bg-rose-500/10 border border-rose-500/20 flex items-center justify-center shrink-0">
                <svg className="w-5 h-5 text-rose-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                  <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                </svg>
              </div>
              <div className="space-y-1">
                <h3 className="text-base font-bold text-white">Clear Activity & Audit Logs</h3>
                <p className="text-xs text-slate-400">
                  This will permanently delete the recorded event logs from the database. This action cannot be undone.
                </p>
              </div>
            </div>

            {/* Scope Selection */}
            <div className="space-y-2 bg-slate-950 p-3 rounded-xl border border-slate-800">
              <label className="text-[11px] font-semibold text-slate-400 uppercase tracking-wider block">
                Select Scope to Clear:
              </label>

              <div className="space-y-1.5 text-xs">
                <label className="flex items-center space-x-2.5 p-2 rounded-lg hover:bg-slate-900/80 cursor-pointer">
                  <input
                    type="radio"
                    name="clearScope"
                    value="all"
                    checked={clearScope === 'all'}
                    onChange={() => setClearScope('all')}
                    className="text-emerald-500 focus:ring-emerald-500 bg-slate-900 border-slate-700"
                  />
                  <div>
                    <span className="text-white font-medium block">All Logs (Recommended)</span>
                    <span className="text-slate-500 text-[11px]">Clear both client telemetry & admin audit events</span>
                  </div>
                </label>

                <label className="flex items-center space-x-2.5 p-2 rounded-lg hover:bg-slate-900/80 cursor-pointer">
                  <input
                    type="radio"
                    name="clearScope"
                    value="client"
                    checked={clearScope === 'client'}
                    onChange={() => setClearScope('client')}
                    className="text-emerald-500 focus:ring-emerald-500 bg-slate-900 border-slate-700"
                  />
                  <div>
                    <span className="text-white font-medium block">Client Telemetry Only</span>
                    <span className="text-slate-500 text-[11px]">Clear activations, validations & heartbeats</span>
                  </div>
                </label>

                <label className="flex items-center space-x-2.5 p-2 rounded-lg hover:bg-slate-900/80 cursor-pointer">
                  <input
                    type="radio"
                    name="clearScope"
                    value="admin"
                    checked={clearScope === 'admin'}
                    onChange={() => setClearScope('admin')}
                    className="text-emerald-500 focus:ring-emerald-500 bg-slate-900 border-slate-700"
                  />
                  <div>
                    <span className="text-white font-medium block">Admin Audit Only</span>
                    <span className="text-slate-500 text-[11px]">Clear admin configuration changes & actions</span>
                  </div>
                </label>
              </div>
            </div>

            <div className="flex justify-end space-x-3 pt-2">
              <button
                type="button"
                onClick={() => setClearModalOpen(false)}
                disabled={clearing}
                className="px-4 py-2 text-xs font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 rounded-xl transition-colors disabled:opacity-50"
              >
                Cancel
              </button>
              <button
                type="button"
                onClick={handleClearLogs}
                disabled={clearing}
                className="inline-flex items-center space-x-2 px-4 py-2 text-xs font-bold text-white bg-rose-600 hover:bg-rose-500 rounded-xl shadow-lg shadow-rose-600/30 transition-all disabled:opacity-50"
              >
                {clearing ? (
                  <>
                    <div className="animate-spin rounded-full h-3.5 w-3.5 border-b-2 border-white"></div>
                    <span>Clearing...</span>
                  </>
                ) : (
                  <>
                    <svg className="w-3.5 h-3.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                      <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 7l-.867 12.142A2 2 0 0116.138 21H7.862a2 2 0 01-1.995-1.858L5 7m5 4v6m4-6v6m1-10V4a1 1 0 00-1-1h-4a1 1 0 00-1 1v3M4 7h16" />
                    </svg>
                    <span>Confirm & Clear</span>
                  </>
                )}
              </button>
            </div>
          </div>
        </div>
      )}

      {/* Payload Modal */}
      {selectedLog && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fadeIn">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-xl w-full p-6 shadow-2xl space-y-4">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-bold text-white">
                Log Payload Details ({selectedLog.action})
              </h3>
              <button
                onClick={() => setSelectedLog(null)}
                className="text-slate-400 hover:text-white text-lg"
              >
                ✕
              </button>
            </div>

            <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 overflow-x-auto max-h-96">
              <pre className="text-xs font-mono text-emerald-300 whitespace-pre-wrap">
                {JSON.stringify(
                  {
                    id: selectedLog.id,
                    type: selectedLog.type,
                    action: selectedLog.action,
                    license_key: selectedLog.license_key,
                    device_id: selectedLog.device_id,
                    ip_address: selectedLog.ip_address,
                    user_agent: selectedLog.user_agent,
                    metadata: selectedLog.metadata,
                    old_value: selectedLog.old_value,
                    new_value: selectedLog.new_value,
                    timestamp: selectedLog.created_at,
                  },
                  null,
                  2
                )}
              </pre>
            </div>

            <div className="flex justify-end pt-2">
              <button
                onClick={() => setSelectedLog(null)}
                className="px-4 py-2 text-xs font-medium text-slate-300 bg-slate-800 hover:bg-slate-700 rounded-xl"
              >
                Close
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
