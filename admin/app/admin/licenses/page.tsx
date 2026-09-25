'use client';

import React, { useState, useEffect } from 'react';
import Link from 'next/link';
import {
  Search,
  Plus,
  Download,
  ExternalLink,
  ChevronLeft,
  ChevronRight
} from 'lucide-react';
import { StatusBadge } from '@/components/Badge';
import { ConfirmationModal } from '@/components/ConfirmationModal';
import { createClient } from '@/lib/supabase/client';
import { formatShortDate, maskLicenseKey, exportToCsv } from '@/lib/utils';
import { License, Product, LicenseStatus } from '@/types';

export default function LicenseListPage() {
  const [licenses, setLicenses] = useState<License[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  const [searchQuery, setSearchQuery] = useState('');
  const [selectedStatus, setSelectedStatus] = useState<string>('all');
  const [selectedProduct, setSelectedProduct] = useState<string>('all');
  const [selectedExpiryFilter, setSelectedExpiryFilter] = useState<string>('all');

  const [currentPage, setCurrentPage] = useState(1);
  const pageSize = 10;

  const [modalConfig, setModalConfig] = useState<{
    isOpen: boolean;
    title: string;
    message: string;
    confirmText?: string;
    confirmVariant?: 'danger' | 'warning' | 'primary';
    action?: () => Promise<void>;
  }>({
    isOpen: false,
    title: '',
    message: '',
  });

  const fetchLicenses = async () => {
    setIsLoading(true);
    const supabase = createClient();

    try {
      const { data: prods } = await supabase.from('products').select('*');
      if (prods) setProducts(prods);

      let query = supabase
        .from('licenses')
        .select('*, products(name, product_code), devices(id, status)', { count: 'exact' })
        .order('created_at', { ascending: false });

      const { data: licData } = await query;
      if (licData) {
        const enhanced = licData.map((l: any) => ({
          ...l,
          device_count: l.devices ? l.devices.filter((d: any) => d.status === 'active').length : 0,
        }));
        setLicenses(enhanced);
      }
    } catch (err) {
      console.error('Failed to fetch licenses:', err);
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchLicenses();
  }, []);

  const filteredLicenses = licenses.filter((lic) => {
    if (searchQuery.trim()) {
      const q = searchQuery.toLowerCase();
      const matchKey = lic.license_key.toLowerCase().includes(q);
      const matchName = lic.customer_name?.toLowerCase().includes(q) || false;
      const matchEmail = lic.customer_email?.toLowerCase().includes(q) || false;
      if (!matchKey && !matchName && !matchEmail) return false;
    }

    if (selectedStatus !== 'all' && lic.status !== selectedStatus) return false;
    if (selectedProduct !== 'all' && lic.product_id !== selectedProduct) return false;

    if (selectedExpiryFilter !== 'all') {
      const now = new Date();
      if (selectedExpiryFilter === 'lifetime') {
        if (lic.expires_at !== null) return false;
      } else if (selectedExpiryFilter === 'expired') {
        if (!lic.expires_at || new Date(lic.expires_at) > now) return false;
      } else if (selectedExpiryFilter === 'expiring7') {
        if (!lic.expires_at) return false;
        const exp = new Date(lic.expires_at);
        const in7 = new Date(now.getTime() + 7 * 24 * 3600 * 1000);
        if (exp <= now || exp > in7) return false;
      }
    }

    return true;
  });

  const totalPages = Math.ceil(filteredLicenses.length / pageSize) || 1;
  const paginatedLicenses = filteredLicenses.slice(
    (currentPage - 1) * pageSize,
    currentPage * pageSize
  );

  const handleUpdateStatus = async (licenseId: string, newStatus: LicenseStatus) => {
    const supabase = createClient();
    const { data: { user } } = await supabase.auth.getUser();
    const lic = licenses.find((l) => l.id === licenseId);
    if (!lic) return;

    await supabase.from('licenses').update({ status: newStatus }).eq('id', licenseId);

    await supabase.from('license_logs').insert({
      license_id: licenseId,
      admin_id: user?.id || null,
      action: `${newStatus.toUpperCase()}_LICENSE`,
      old_value: { status: lic.status },
      new_value: { status: newStatus },
    });

    setModalConfig({ ...modalConfig, isOpen: false });
    fetchLicenses();
  };

  const handleExport = () => {
    const exportRows = filteredLicenses.map((l) => ({
      'License Key': l.license_key,
      'Customer Name': l.customer_name || '',
      'Customer Email': l.customer_email || '',
      Product: l.products?.name || '',
      Status: l.status,
      'Start Date': l.start_at,
      'Expires At': l.expires_at || 'Lifetime',
      'Max Devices': l.max_devices,
      'Active Devices': l.device_count || 0,
      'Grace Hours': l.offline_grace_hours,
      'Created At': l.created_at,
    }));
    exportToCsv('licenses_export', exportRows);
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 tracking-tight">License Management</h1>
          <p className="text-sm text-slate-500 mt-1">
            Search, filter, manage, and extend client license entitlements.
          </p>
        </div>
        <div className="flex items-center space-x-3">
          <button
            onClick={handleExport}
            className="inline-flex items-center space-x-2 px-3.5 py-2 bg-white border border-slate-300 text-slate-700 hover:bg-slate-50 rounded-xl text-sm font-medium transition-colors shadow-sm"
          >
            <Download className="w-4 h-4 text-slate-500" />
            <span>Export CSV</span>
          </button>
          <Link
            href="/admin/licenses/new"
            className="inline-flex items-center space-x-2 px-4 py-2 bg-indigo-600 hover:bg-indigo-700 text-white rounded-xl text-sm font-medium transition-colors shadow-sm"
          >
            <Plus className="w-4 h-4" />
            <span>New License</span>
          </Link>
        </div>
      </div>

      <div className="bg-white p-4 rounded-xl border border-slate-200 shadow-sm grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-3">
        <div className="relative">
          <div className="absolute inset-y-0 left-0 pl-3.5 flex items-center pointer-events-none text-slate-400">
            <Search className="w-4 h-4" />
          </div>
          <input
            type="text"
            value={searchQuery}
            onChange={(e) => { setSearchQuery(e.target.value); setCurrentPage(1); }}
            placeholder="Search key, customer, email..."
            className="w-full pl-10 pr-4 py-2 bg-slate-50 border border-slate-300 rounded-lg text-xs font-medium text-slate-900 placeholder-slate-400 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          />
        </div>

        <div>
          <select
            value={selectedStatus}
            onChange={(e) => { setSelectedStatus(e.target.value); setCurrentPage(1); }}
            className="w-full px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-xs font-medium text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="all">All Statuses</option>
            <option value="active">Active</option>
            <option value="expired">Expired</option>
            <option value="disabled">Disabled</option>
            <option value="revoked">Revoked</option>
          </select>
        </div>

        <div>
          <select
            value={selectedProduct}
            onChange={(e) => { setSelectedProduct(e.target.value); setCurrentPage(1); }}
            className="w-full px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-xs font-medium text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="all">All Products</option>
            {products.map((p) => (
              <option key={p.id} value={p.id}>{p.name} ({p.product_code})</option>
            ))}
          </select>
        </div>

        <div>
          <select
            value={selectedExpiryFilter}
            onChange={(e) => { setSelectedExpiryFilter(e.target.value); setCurrentPage(1); }}
            className="w-full px-3 py-2 bg-slate-50 border border-slate-300 rounded-lg text-xs font-medium text-slate-700 focus:outline-none focus:ring-2 focus:ring-indigo-500"
          >
            <option value="all">Any Expiration</option>
            <option value="lifetime">Lifetime Licenses</option>
            <option value="expiring7">Expiring in 7 Days</option>
            <option value="expired">Expired</option>
          </select>
        </div>
      </div>

      <div className="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="bg-slate-50 text-slate-500 uppercase tracking-wider font-semibold border-b border-slate-200">
              <tr>
                <th className="px-5 py-3.5">License Key</th>
                <th className="px-5 py-3.5">Customer</th>
                <th className="px-5 py-3.5">Product</th>
                <th className="px-5 py-3.5">Status</th>
                <th className="px-5 py-3.5">Expires</th>
                <th className="px-5 py-3.5">Devices</th>
                <th className="px-5 py-3.5">Created</th>
                <th className="px-5 py-3.5 text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 text-slate-700 font-medium">
              {paginatedLicenses.map((lic) => (
                <tr key={lic.id} className="hover:bg-slate-50/80 transition-colors">
                  <td className="px-5 py-4 font-mono text-indigo-600 font-semibold">
                    <Link href={`/admin/licenses/${lic.id}`} className="hover:underline flex items-center space-x-1">
                      <span>{maskLicenseKey(lic.license_key)}</span>
                      <ExternalLink className="w-3 h-3 text-slate-400" />
                    </Link>
                  </td>
                  <td className="px-5 py-4">
                    <div className="font-semibold text-slate-900">{lic.customer_name || 'Anonymous'}</div>
                    <div className="text-[11px] text-slate-400 font-normal">{lic.customer_email || 'No email'}</div>
                  </td>
                  <td className="px-5 py-4 text-slate-800">{lic.products?.name || 'Standard Product'}</td>
                  <td className="px-5 py-4"><StatusBadge status={lic.status} /></td>
                  <td className="px-5 py-4 text-slate-600">{formatShortDate(lic.expires_at)}</td>
                  <td className="px-5 py-4">
                    <span className="font-semibold text-slate-900">{lic.device_count || 0}</span>
                    <span className="text-slate-400"> / {lic.max_devices}</span>
                  </td>
                  <td className="px-5 py-4 text-slate-500">{formatShortDate(lic.created_at)}</td>
                  <td className="px-5 py-4 text-right">
                    <div className="flex items-center justify-end space-x-2">
                      <Link href={`/admin/licenses/${lic.id}`} className="px-2.5 py-1 text-xs font-medium text-indigo-600 hover:bg-indigo-50 rounded-lg">
                        Manage
                      </Link>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="px-5 py-3.5 bg-slate-50 border-t border-slate-200 flex items-center justify-between text-xs text-slate-600">
          <div>
            Showing <span className="font-semibold">{(currentPage - 1) * pageSize + 1}</span> to{' '}
            <span className="font-semibold">{Math.min(currentPage * pageSize, filteredLicenses.length)}</span> of <span className="font-semibold">{filteredLicenses.length}</span>
          </div>
          <div className="flex items-center space-x-2">
            <button
              onClick={() => setCurrentPage((p) => Math.max(1, p - 1))}
              disabled={currentPage === 1}
              className="p-1.5 border border-slate-300 rounded-lg bg-white disabled:opacity-40"
            >
              <ChevronLeft className="w-4 h-4" />
            </button>
            <span>Page {currentPage} of {totalPages}</span>
            <button
              onClick={() => setCurrentPage((p) => Math.min(totalPages, p + 1))}
              disabled={currentPage === totalPages}
              className="p-1.5 border border-slate-300 rounded-lg bg-white disabled:opacity-40"
            >
              <ChevronRight className="w-4 h-4" />
            </button>
          </div>
        </div>
      </div>

      <ConfirmationModal
        isOpen={modalConfig.isOpen}
        title={modalConfig.title}
        message={modalConfig.message}
        confirmText={modalConfig.confirmText}
        confirmVariant={modalConfig.confirmVariant}
        onConfirm={modalConfig.action || (() => {})}
        onCancel={() => setModalConfig({ ...modalConfig, isOpen: false })}
      />
    </div>
  );
}
