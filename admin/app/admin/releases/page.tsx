'use client';

import { useState, useEffect, useCallback } from 'react';
import { getBrowserClient } from '@/lib/supabase/client';
import { ReleaseVersion, Product } from '@/types';
import { formatDate } from '@/lib/utils';
import ConfirmationModal from '@/components/ConfirmationModal';

interface ReleaseWithProduct extends ReleaseVersion {
  product?: Product;
}

export default function ReleasesPage() {
  const supabase = getBrowserClient();
  const [releases, setReleases] = useState<ReleaseWithProduct[]>([]);
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);

  // Form states
  const [productId, setProductId] = useState('');
  const [version, setVersion] = useState('');
  const [downloadUrl, setDownloadUrl] = useState('');
  const [releaseNotes, setReleaseNotes] = useState('');
  const [sha256, setSha256] = useState('');
  const [mandatory, setMandatory] = useState(false);
  const [active, setActive] = useState(true);

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

  const fetchData = useCallback(async () => {
    try {
      const [relRes, prodRes] = await Promise.all([
        supabase
          .from('release_versions')
          .select('*, product:products(*)')
          .order('created_at', { ascending: false }),
        supabase.from('products').select('*').order('name'),
      ]);

      if (relRes.data) {
        setReleases(relRes.data as ReleaseWithProduct[]);
      }
      if (prodRes.data && prodRes.data.length > 0) {
        setProducts(prodRes.data as Product[]);
        setProductId(prodRes.data[0].id);
      }
    } catch (err) {
      console.error('Error loading releases:', err);
    } finally {
      setLoading(false);
    }
  }, [supabase]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleCreateRelease = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!productId) return;
    setSubmitting(true);

    try {
      await supabase.from('release_versions').insert([
        {
          product_id: productId,
          version: version.trim(),
          download_url: downloadUrl.trim() || null,
          release_notes: releaseNotes.trim() || null,
          sha256: sha256.trim() || null,
          mandatory,
          active,
        },
      ]);

      // Update current version in products table
      await supabase
        .from('products')
        .update({
          current_version: version.trim(),
          download_url: downloadUrl.trim() || null,
          updated_at: new Date().toISOString(),
        })
        .eq('id', productId);

      setModalOpen(false);
      setVersion('');
      setDownloadUrl('');
      setReleaseNotes('');
      setSha256('');
      setMandatory(false);
      await fetchData();
    } catch (err) {
      console.error('Error creating release:', err);
    } finally {
      setSubmitting(false);
    }
  };

  const handleDeleteRelease = (rel: ReleaseWithProduct) => {
    setConfirmModal({
      isOpen: true,
      title: 'Delete Release Version',
      message: `Are you sure you want to delete release v${rel.version} for ${rel.product?.name}?`,
      onConfirm: async () => {
        try {
          await supabase.from('release_versions').delete().eq('id', rel.id);
          await fetchData();
        } finally {
          setConfirmModal((prev) => ({ ...prev, isOpen: false }));
        }
      },
    });
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">App Versions & Releases</h1>
          <p className="text-sm text-slate-400 mt-1">
            Publish client updates, mandatory patches, and verify SHA256 checksums.
          </p>
        </div>

        <button
          type="button"
          onClick={() => setModalOpen(true)}
          className="inline-flex items-center space-x-2 px-4 py-2.5 bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-sm font-semibold rounded-xl shadow-lg shadow-emerald-500/20 transition-all self-start sm:self-auto"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
          </svg>
          <span>Publish New Version</span>
        </button>
      </div>

      {/* Releases Table */}
      <div className="bg-slate-900 border border-slate-800 rounded-2xl shadow-xl overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-left text-xs">
            <thead className="text-[11px] text-slate-400 uppercase tracking-wider bg-slate-950/70 border-b border-slate-800">
              <tr>
                <th className="py-3.5 px-4 font-semibold">Product</th>
                <th className="py-3.5 px-4 font-semibold">Version</th>
                <th className="py-3.5 px-4 font-semibold">Mandatory</th>
                <th className="py-3.5 px-4 font-semibold">SHA-256 Checksum</th>
                <th className="py-3.5 px-4 font-semibold">Release Date</th>
                <th className="py-3.5 px-4 font-semibold">Status</th>
                <th className="py-3.5 px-4 font-semibold text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-800/60">
              {loading ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-slate-400">
                    <div className="flex justify-center items-center space-x-2">
                      <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-emerald-500"></div>
                      <span>Loading releases...</span>
                    </div>
                  </td>
                </tr>
              ) : releases.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-slate-500">
                    No release versions published yet.
                  </td>
                </tr>
              ) : (
                releases.map((r) => (
                  <tr key={r.id} className="hover:bg-slate-800/30 transition-colors">
                    <td className="py-3.5 px-4 font-medium text-white">
                      {r.product?.name || 'Standard Product'}
                    </td>
                    <td className="py-3.5 px-4 font-mono font-bold text-emerald-400">
                      v{r.version}
                    </td>
                    <td className="py-3.5 px-4">
                      {r.mandatory ? (
                        <span className="px-2 py-0.5 rounded text-[10px] font-bold bg-rose-500/20 text-rose-400 border border-rose-500/30">
                          MANDATORY
                        </span>
                      ) : (
                        <span className="text-slate-500">Optional</span>
                      )}
                    </td>
                    <td className="py-3.5 px-4 font-mono text-[11px] text-slate-400 max-w-[200px] truncate" title={r.sha256 || 'None'}>
                      {r.sha256 || '—'}
                    </td>
                    <td className="py-3.5 px-4 text-slate-400">
                      {formatDate(r.created_at)}
                    </td>
                    <td className="py-3.5 px-4">
                      <span
                        className={`inline-block px-2 py-0.5 rounded text-[11px] font-semibold ${
                          r.active ? 'bg-emerald-500/10 text-emerald-400' : 'bg-slate-800 text-slate-500'
                        }`}
                      >
                        {r.active ? 'Active' : 'Archived'}
                      </span>
                    </td>
                    <td className="py-3.5 px-4 text-right space-x-3">
                      {r.download_url && (
                        <a
                          href={r.download_url}
                          target="_blank"
                          rel="noreferrer"
                          className="text-cyan-400 hover:underline"
                        >
                          Download
                        </a>
                      )}
                      <button
                        onClick={() => handleDeleteRelease(r)}
                        className="text-rose-400 hover:text-rose-300 hover:underline"
                      >
                        Delete
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Publish Release Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fadeIn">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-5">
            <h3 className="text-lg font-bold text-white">Publish Application Release</h3>

            <form onSubmit={handleCreateRelease} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Target Product <span className="text-rose-400">*</span>
                </label>
                <select
                  value={productId}
                  onChange={(e) => setProductId(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white"
                >
                  {products.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} ({p.product_code})
                    </option>
                  ))}
                </select>
              </div>

              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Version Number (SemVer) <span className="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="e.g. 1.2.0"
                  value={version}
                  onChange={(e) => setVersion(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white font-mono"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Download Installer URL <span className="text-rose-400">*</span>
                </label>
                <input
                  type="url"
                  required
                  placeholder="https://r2.yourdomain.com/downloads/KN_3in1_Pro_Setup_v1.2.0.exe"
                  value={downloadUrl}
                  onChange={(e) => setDownloadUrl(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  SHA-256 Checksum (Optional for tampering protection)
                </label>
                <input
                  type="text"
                  placeholder="e.g. e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
                  value={sha256}
                  onChange={(e) => setSha256(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white font-mono"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Release Notes / Changelog
                </label>
                <textarea
                  rows={3}
                  value={releaseNotes}
                  onChange={(e) => setReleaseNotes(e.target.value)}
                  placeholder="- Fixed video parsing&#10;- Improved download speed&#10;- Added new UI theme"
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2 text-sm text-white font-sans"
                />
              </div>

              <div className="flex items-center space-x-6 pt-2">
                <label className="flex items-center space-x-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={mandatory}
                    onChange={(e) => setMandatory(e.target.checked)}
                    className="rounded border-slate-800 bg-slate-950 text-rose-500 focus:ring-rose-500 h-4 w-4"
                  />
                  <span className="text-xs text-rose-400 font-semibold">Mandatory Force Upgrade</span>
                </label>

                <label className="flex items-center space-x-2 cursor-pointer">
                  <input
                    type="checkbox"
                    checked={active}
                    onChange={(e) => setActive(e.target.checked)}
                    className="rounded border-slate-800 bg-slate-950 text-emerald-500 focus:ring-emerald-500 h-4 w-4"
                  />
                  <span className="text-xs text-slate-300 font-medium">Active Release</span>
                </label>
              </div>

              <div className="flex justify-end space-x-3 pt-4 border-t border-slate-800">
                <button
                  type="button"
                  onClick={() => setModalOpen(false)}
                  className="px-4 py-2 text-xs font-medium text-slate-400 hover:text-white bg-slate-800 rounded-xl"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  disabled={submitting}
                  className="px-5 py-2 text-xs font-semibold text-slate-950 bg-emerald-500 hover:bg-emerald-400 disabled:opacity-50 rounded-xl shadow-lg shadow-emerald-500/20"
                >
                  {submitting ? 'Publishing...' : 'Publish Release'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}

      <ConfirmationModal
        isOpen={confirmModal.isOpen}
        title={confirmModal.title}
        message={confirmModal.message}
        confirmText="Delete"
        variant="danger"
        onConfirm={confirmModal.onConfirm}
        onCancel={() => setConfirmModal((prev) => ({ ...prev, isOpen: false }))}
      />
    </div>
  );
}
