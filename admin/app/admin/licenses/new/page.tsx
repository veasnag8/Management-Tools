'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { getBrowserClient } from '@/lib/supabase/client';
import { generateLicenseKey } from '@/lib/key-generator';
import { Product } from '@/types';
import Link from 'next/link';

export default function NewLicensePage() {
  const router = useRouter();
  const supabase = getBrowserClient();

  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Form fields
  const [isBulk, setIsBulk] = useState(false);
  const [bulkCount, setBulkCount] = useState(5);
  const [productId, setProductId] = useState('');
  const [customerName, setCustomerName] = useState('');
  const [customerEmail, setCustomerEmail] = useState('');
  const [expiryPreset, setExpiryPreset] = useState<'lifetime' | '30d' | '90d' | '365d' | 'custom'>('365d');
  const [customExpiryDate, setCustomExpiryDate] = useState('');
  const [maxDevices, setMaxDevices] = useState(1);
  const [offlineGraceHours, setOfflineGraceHours] = useState(72);
  const [notes, setNotes] = useState('');

  // Generated keys state
  const [generatedKeys, setGeneratedKeys] = useState<string[]>([]);

  useEffect(() => {
    async function fetchProducts() {
      const { data, error } = await supabase
        .from('products')
        .select('*')
        .eq('active', true)
        .order('name');
      if (data && data.length > 0) {
        setProducts(data as Product[]);
        setProductId(data[0].id);
      }
    }
    fetchProducts();
  }, [supabase]);

  const calculateExpiry = (): string | null => {
    if (expiryPreset === 'lifetime') return null;
    const now = new Date();
    if (expiryPreset === '30d') {
      now.setDate(now.getDate() + 30);
      return now.toISOString();
    }
    if (expiryPreset === '90d') {
      now.setDate(now.getDate() + 90);
      return now.toISOString();
    }
    if (expiryPreset === '365d') {
      now.setDate(now.getDate() + 365);
      return now.toISOString();
    }
    if (expiryPreset === 'custom' && customExpiryDate) {
      return new Date(customExpiryDate).toISOString();
    }
    return null;
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!productId) {
      setError('Please select a valid product.');
      return;
    }

    setLoading(true);
    setError(null);

    const selectedProduct = products.find((p) => p.id === productId);
    const prefix = selectedProduct?.product_code || 'PROD';
    const expiresAt = calculateExpiry();
    const count = isBulk ? Math.min(Math.max(1, bulkCount), 100) : 1;

    try {
      const keysToInsert: {
        license_key: string;
        product_id: string;
        customer_name: string | null;
        customer_email: string | null;
        status: string;
        start_at: string;
        expires_at: string | null;
        max_devices: number;
        offline_grace_hours: number;
        notes: string | null;
      }[] = [];

      for (let i = 0; i < count; i++) {
        keysToInsert.push({
          license_key: generateLicenseKey(prefix),
          product_id: productId,
          customer_name: isBulk ? null : customerName || null,
          customer_email: isBulk ? null : customerEmail || null,
          status: 'active',
          start_at: new Date().toISOString(),
          expires_at: expiresAt,
          max_devices: maxDevices,
          offline_grace_hours: offlineGraceHours,
          notes: notes || null,
        });
      }

      const { data, error: insertError } = await supabase
        .from('licenses')
        .insert(keysToInsert)
        .select();

      if (insertError) {
        throw insertError;
      }

      const createdKeys = keysToInsert.map((k) => k.license_key);
      setGeneratedKeys(createdKeys);

      if (!isBulk && data && data[0]) {
        router.push(`/admin/licenses/${data[0].id}`);
      }
    } catch (err: any) {
      setError(err.message || 'Failed to create license(s).');
    } finally {
      setLoading(false);
    }
  };

  const exportCSV = () => {
    if (generatedKeys.length === 0) return;
    const headers = 'License Key,Product ID,Max Devices,Offline Grace Hours,Expires At\n';
    const rows = generatedKeys
      .map((key) => `"${key}","${productId}","${maxDevices}","${offlineGraceHours}","${calculateExpiry() || 'Lifetime'}"`)
      .join('\n');
    const blob = new Blob([headers + rows], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', `licenses_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Header Breadcrumbs */}
      <div className="flex items-center space-x-2 text-sm text-slate-400">
        <Link href="/admin/licenses" className="hover:text-emerald-400 transition-colors">
          Licenses
        </Link>
        <span>/</span>
        <span className="text-slate-100">Create License</span>
      </div>

      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Generate New License</h1>
          <p className="text-sm text-slate-400 mt-1">
            Create single personalized license or generate bulk activation keys.
          </p>
        </div>

        {/* Mode Toggle */}
        <div className="flex items-center bg-slate-900 border border-slate-800 rounded-lg p-1">
          <button
            type="button"
            onClick={() => setIsBulk(false)}
            className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-all ${
              !isBulk ? 'bg-emerald-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white'
            }`}
          >
            Single License
          </button>
          <button
            type="button"
            onClick={() => setIsBulk(true)}
            className={`px-3 py-1.5 text-xs font-semibold rounded-md transition-all ${
              isBulk ? 'bg-emerald-500 text-slate-950 shadow-md' : 'text-slate-400 hover:text-white'
            }`}
          >
            Bulk Generation
          </button>
        </div>
      </div>

      {error && (
        <div className="p-4 bg-rose-500/10 border border-rose-500/20 rounded-xl text-sm text-rose-400">
          {error}
        </div>
      )}

      {/* Generation Results (Bulk) */}
      {generatedKeys.length > 0 && (
        <div className="bg-slate-900 border border-emerald-500/30 rounded-2xl p-6 shadow-xl space-y-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center space-x-3">
              <span className="flex h-3 w-3 relative">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-3 w-3 bg-emerald-500"></span>
              </span>
              <h3 className="text-base font-bold text-white">
                Successfully Generated {generatedKeys.length} License Keys
              </h3>
            </div>
            <button
              onClick={exportCSV}
              className="px-3 py-1.5 text-xs font-medium text-emerald-400 border border-emerald-500/30 bg-emerald-500/10 hover:bg-emerald-500/20 rounded-lg transition-colors flex items-center space-x-1.5"
            >
              <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
              <span>Export CSV</span>
            </button>
          </div>

          <div className="bg-slate-950 border border-slate-800 rounded-xl p-4 max-h-56 overflow-y-auto space-y-2 font-mono text-xs text-emerald-300">
            {generatedKeys.map((k, i) => (
              <div key={i} className="flex justify-between items-center py-1 border-b border-slate-900 last:border-0">
                <span>{k}</span>
                <button
                  type="button"
                  onClick={() => navigator.clipboard.writeText(k)}
                  className="text-slate-400 hover:text-white text-[11px] underline"
                >
                  Copy
                </button>
              </div>
            ))}
          </div>

          <div className="flex justify-end">
            <Link
              href="/admin/licenses"
              className="px-4 py-2 text-xs font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 rounded-lg transition-colors"
            >
              Done & Return to Licenses
            </Link>
          </div>
        </div>
      )}

      <form onSubmit={handleSubmit} className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-6">
        {/* Product selection */}
        <div>
          <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
            Target Product <span className="text-rose-400">*</span>
          </label>
          <select
            value={productId}
            onChange={(e) => setProductId(e.target.value)}
            required
            className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white transition-colors"
          >
            {products.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name} ({p.product_code}) {p.current_version ? `- v${p.current_version}` : ''}
              </option>
            ))}
          </select>
        </div>

        {/* Customer fields (Single mode only) */}
        {!isBulk ? (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
                Customer Name
              </label>
              <input
                type="text"
                value={customerName}
                onChange={(e) => setCustomerName(e.target.value)}
                placeholder="e.g. John Doe"
                className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-600 transition-colors"
              />
            </div>
            <div>
              <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
                Customer Email
              </label>
              <input
                type="email"
                value={customerEmail}
                onChange={(e) => setCustomerEmail(e.target.value)}
                placeholder="e.g. john@example.com"
                className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-600 transition-colors"
              />
            </div>
          </div>
        ) : (
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
              Batch Quantity (1 - 100) <span className="text-rose-400">*</span>
            </label>
            <input
              type="number"
              min="1"
              max="100"
              value={bulkCount}
              onChange={(e) => setBulkCount(parseInt(e.target.value) || 1)}
              className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white transition-colors"
            />
          </div>
        )}

        {/* Expiration Settings */}
        <div>
          <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
            License Validity / Expiry
          </label>
          <div className="grid grid-cols-2 sm:grid-cols-5 gap-2.5">
            {[
              { id: 'lifetime', label: 'Lifetime' },
              { id: '30d', label: '30 Days' },
              { id: '90d', label: '90 Days' },
              { id: '365d', label: '1 Year' },
              { id: 'custom', label: 'Custom Date' },
            ].map((option) => (
              <button
                key={option.id}
                type="button"
                onClick={() => setExpiryPreset(option.id as any)}
                className={`py-2 px-3 text-xs font-medium rounded-xl border transition-all ${
                  expiryPreset === option.id
                    ? 'border-emerald-500 bg-emerald-500/10 text-emerald-400 font-semibold'
                    : 'border-slate-800 bg-slate-950 text-slate-400 hover:text-white hover:border-slate-700'
                }`}
              >
                {option.label}
              </button>
            ))}
          </div>

          {expiryPreset === 'custom' && (
            <div className="mt-3">
              <input
                type="date"
                value={customExpiryDate}
                onChange={(e) => setCustomExpiryDate(e.target.value)}
                min={new Date().toISOString().split('T')[0]}
                required
                className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white transition-colors"
              />
            </div>
          )}
        </div>

        {/* Device & Security limits */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
              Max Hardware Devices
            </label>
            <input
              type="number"
              min="1"
              max="100"
              value={maxDevices}
              onChange={(e) => setMaxDevices(parseInt(e.target.value) || 1)}
              required
              className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white transition-colors"
            />
            <p className="text-[11px] text-slate-500 mt-1">
              Number of unique PC hardware fingerprints allowed concurrently.
            </p>
          </div>
          <div>
            <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
              Offline Grace Period (Hours)
            </label>
            <input
              type="number"
              min="0"
              max="720"
              value={offlineGraceHours}
              onChange={(e) => setOfflineGraceHours(parseInt(e.target.value) || 0)}
              required
              className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white transition-colors"
            />
            <p className="text-[11px] text-slate-500 mt-1">
              Hours the app can run without reaching the Cloudflare Worker API.
            </p>
          </div>
        </div>

        {/* Notes */}
        <div>
          <label className="block text-xs font-semibold text-slate-300 mb-2 uppercase tracking-wider">
            Internal Notes (Optional)
          </label>
          <textarea
            rows={2}
            value={notes}
            onChange={(e) => setNotes(e.target.value)}
            placeholder="Special terms, promotional code, or order ID..."
            className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 focus:ring-1 focus:ring-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white placeholder-slate-600 transition-colors"
          />
        </div>

        {/* Form Actions */}
        <div className="flex items-center justify-end space-x-3 pt-4 border-t border-slate-800">
          <Link
            href="/admin/licenses"
            className="px-4 py-2.5 text-sm font-medium text-slate-400 hover:text-white bg-slate-800 hover:bg-slate-700 rounded-xl transition-colors"
          >
            Cancel
          </Link>
          <button
            type="submit"
            disabled={loading}
            className="px-6 py-2.5 text-sm font-semibold text-slate-950 bg-emerald-500 hover:bg-emerald-400 disabled:opacity-50 rounded-xl transition-all shadow-lg shadow-emerald-500/20"
          >
            {loading ? 'Generating...' : isBulk ? `Generate ${bulkCount} Keys` : 'Create License'}
          </button>
        </div>
      </form>
    </div>
  );
}
