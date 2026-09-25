'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import {
  KeyRound,
  CheckCircle2,
  AlertTriangle,
  XCircle,
  Laptop2,
  Plus,
  ArrowRight,
  ShieldAlert,
  Activity
} from 'lucide-react';
import { StatCard } from '@/components/StatCard';
import { StatusBadge } from '@/components/Badge';
import { createClient } from '@/lib/supabase/client';
import { formatDate, maskLicenseKey } from '@/lib/utils';
import { DashboardStats, License, ActivationLog } from '@/types';

export default function AdminDashboardPage() {
  const [stats, setStats] = useState<DashboardStats>({
    totalLicenses: 0,
    activeLicenses: 0,
    expiredLicenses: 0,
    disabledLicenses: 0,
    activeDevices: 0,
    expiring7Days: 0,
    expiring30Days: 0,
  });

  const [recentLicenses, setRecentLicenses] = useState<License[]>([]);
  const [recentActivations, setRecentActivations] = useState<ActivationLog[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  useEffect(() => {
    async function fetchDashboardData() {
      setIsLoading(true);
      const supabase = createClient();

      try {
        const { data: licenses } = await supabase
          .from('licenses')
          .select('*, products(name, product_code)')
          .order('created_at', { ascending: false });

        const { count: activeDevicesCount } = await supabase
          .from('devices')
          .select('*', { count: 'exact', head: true })
          .eq('status', 'active');

        const { data: activations } = await supabase
          .from('activations')
          .select('*, licenses(license_key, customer_name)')
          .order('created_at', { ascending: false })
          .limit(6);

        if (licenses) {
          const now = new Date();
          const in7Days = new Date(now.getTime() + 7 * 24 * 3600 * 1000);
          const in30Days = new Date(now.getTime() + 30 * 24 * 3600 * 1000);

          let activeCount = 0;
          let expiredCount = 0;
          let disabledCount = 0;
          let exp7 = 0;
          let exp30 = 0;

          licenses.forEach((lic: License) => {
            if (lic.status === 'active') activeCount++;
            if (lic.status === 'expired') expiredCount++;
            if (lic.status === 'disabled' || lic.status === 'revoked') disabledCount++;

            if (lic.status === 'active' && lic.expires_at) {
              const exp = new Date(lic.expires_at);
              if (exp > now && exp <= in7Days) exp7++;
              else if (exp > now && exp <= in30Days) exp30++;
            }
          });

          setStats({
            totalLicenses: licenses.length,
            activeLicenses: activeCount,
            expiredLicenses: expiredCount,
            disabledLicenses: disabledCount,
            activeDevices: activeDevicesCount || 0,
            expiring7Days: exp7,
            expiring30Days: exp30,
          });

          setRecentLicenses(licenses.slice(0, 5));
        }

        if (activations) {
          setRecentActivations(activations);
        }
      } catch (err) {
        console.error('Failed to load dashboard metrics:', err);
      } finally {
        setIsLoading(false);
      }
    }

    fetchDashboardData();
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">System Overview</h1>
          <p className="text-sm text-slate-500 mt-1">
            Real-time status of software licenses, PC activations, and product releases.
          </p>
        </div>
        <div className="flex items-center space-x-3">
          <Link
            href="/admin/licenses/new"
            className="inline-flex items-center space-x-2 px-4 py-2.5 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-sm font-medium transition-colors shadow-sm"
          >
            <Plus className="w-4 h-4" />
            <span>Generate License</span>
          </Link>
        </div>
      </div>

      {stats.expiring7Days > 0 && (
        <div className="p-4 rounded-xl bg-amber-50 border border-amber-200 flex items-start space-x-3 text-amber-800">
          <ShieldAlert className="w-5 h-5 flex-shrink-0 text-amber-600 mt-0.5" />
          <div className="flex-1 text-sm">
            <span className="font-semibold">Action Required:</span> {stats.expiring7Days} active license(s) expiring within the next 7 days.
          </div>
          <Link href="/admin/licenses" className="text-xs font-semibold text-amber-900 hover:underline flex items-center space-x-1">
            <span>View All</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>
      )}

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
        <StatCard title="Total Licenses" value={stats.totalLicenses} icon={KeyRound} color="indigo" subtitle="All keys" />
        <StatCard title="Active Licenses" value={stats.activeLicenses} icon={CheckCircle2} color="emerald" subtitle="Authorized" />
        <StatCard title="Active Devices" value={stats.activeDevices} icon={Laptop2} color="indigo" subtitle="Connected PCs" />
        <StatCard title="Expired" value={stats.expiredLicenses} icon={AlertTriangle} color="amber" subtitle="Pending renewal" />
        <StatCard title="Disabled / Revoked" value={stats.disabledLicenses} icon={XCircle} color="rose" subtitle="Access blocked" />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2 bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
          <div className="p-5 border-b border-slate-100 flex items-center justify-between">
            <div className="flex items-center space-x-2">
              <KeyRound className="w-4 h-4 text-slate-500" />
              <h3 className="font-semibold text-slate-900 text-sm">Recently Created Licenses</h3>
            </div>
            <Link href="/admin/licenses" className="text-xs font-semibold text-indigo-600 hover:text-indigo-700 flex items-center space-x-1">
              <span>View All</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </Link>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left text-xs">
              <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-100">
                <tr>
                  <th className="px-5 py-3">License Key</th>
                  <th className="px-5 py-3">Customer</th>
                  <th className="px-5 py-3">Product</th>
                  <th className="px-5 py-3">Status</th>
                  <th className="px-5 py-3">Expires</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-100 text-slate-700 font-medium">
                {recentLicenses.map((lic) => (
                  <tr key={lic.id} className="hover:bg-slate-50/80 transition-colors">
                    <td className="px-5 py-3.5 font-mono text-indigo-600 font-semibold">
                      <Link href={`/admin/licenses/${lic.id}`} className="hover:underline">
                        {maskLicenseKey(lic.license_key)}
                      </Link>
                    </td>
                    <td className="px-5 py-3.5">
                      <div>{lic.customer_name || 'Anonymous'}</div>
                      <div className="text-[11px] text-slate-400 font-normal">{lic.customer_email || 'No email'}</div>
                    </td>
                    <td className="px-5 py-3.5 text-slate-900 font-medium">{lic.products?.name || 'Standard Product'}</td>
                    <td className="px-5 py-3.5"><StatusBadge status={lic.status} /></td>
                    <td className="px-5 py-3.5 text-slate-500">{formatDate(lic.expires_at)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden flex flex-col">
          <div className="p-5 border-b border-slate-100 flex items-center justify-between">
            <div className="flex items-center space-x-2">
              <Activity className="w-4 h-4 text-slate-500" />
              <h3 className="font-semibold text-slate-900 text-sm">Live Client Activity</h3>
            </div>
            <Link href="/admin/logs" className="text-xs font-semibold text-indigo-600 hover:text-indigo-700">Logs</Link>
          </div>
          <div className="p-5 space-y-4 flex-1 overflow-y-auto">
            {recentActivations.map((act) => (
              <div key={act.id} className="flex items-start space-x-3 text-xs">
                <div className="p-1.5 rounded-lg bg-slate-100 text-slate-600 mt-0.5"><Laptop2 className="w-3.5 h-3.5" /></div>
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between">
                    <span className="font-semibold text-slate-800 capitalize">{act.action}</span>
                    <span className="text-[10px] text-slate-400">{formatDate(act.created_at)}</span>
                  </div>
                  <p className="text-slate-500 truncate mt-0.5 font-mono">{act.device_id || 'PC Device'}</p>
                </div>
              </div>
            ))}
          </div>
        </div>
      </div>
    </div>
  );
}
