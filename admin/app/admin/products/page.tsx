'use client';

import { useState, useEffect, useCallback } from 'react';
import { getBrowserClient } from '@/lib/supabase/client';
import { Product } from '@/types';
import { formatDate } from '@/lib/utils';

export default function ProductsPage() {
  const supabase = getBrowserClient();
  const [products, setProducts] = useState<Product[]>([]);
  const [loading, setLoading] = useState(true);
  const [modalOpen, setModalOpen] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [editingProduct, setEditingProduct] = useState<Product | null>(null);

  // Form fields
  const [productCode, setProductCode] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [currentVersion, setCurrentVersion] = useState('1.0.0');
  const [downloadUrl, setDownloadUrl] = useState('');
  const [active, setActive] = useState(true);

  const fetchProducts = useCallback(async () => {
    try {
      const { data, error } = await supabase
        .from('products')
        .select('*')
        .order('created_at', { ascending: false });
      if (!error && data) {
        setProducts(data as Product[]);
      }
    } catch (err) {
      console.error('Failed to load products:', err);
    } finally {
      setLoading(false);
    }
  }, [supabase]);

  useEffect(() => {
    fetchProducts();
  }, [fetchProducts]);

  const handleOpenModal = (prod?: Product) => {
    if (prod) {
      setEditingProduct(prod);
      setProductCode(prod.product_code);
      setName(prod.name);
      setDescription(prod.description || '');
      setCurrentVersion(prod.current_version || '1.0.0');
      setDownloadUrl(prod.download_url || '');
      setActive(prod.active);
    } else {
      setEditingProduct(null);
      setProductCode('');
      setName('');
      setDescription('');
      setCurrentVersion('1.0.0');
      setDownloadUrl('');
      setActive(true);
    }
    setModalOpen(true);
  };

  const handleSaveProduct = async (e: React.FormEvent) => {
    e.preventDefault();
    setSubmitting(true);

    try {
      if (editingProduct) {
        await supabase
          .from('products')
          .update({
            product_code: productCode.trim().toUpperCase(),
            name: name.trim(),
            description: description.trim() || null,
            current_version: currentVersion.trim(),
            download_url: downloadUrl.trim() || null,
            active,
            updated_at: new Date().toISOString(),
          })
          .eq('id', editingProduct.id);
      } else {
        await supabase.from('products').insert([
          {
            product_code: productCode.trim().toUpperCase(),
            name: name.trim(),
            description: description.trim() || null,
            current_version: currentVersion.trim(),
            download_url: downloadUrl.trim() || null,
            active,
          },
        ]);
      }
      setModalOpen(false);
      await fetchProducts();
    } catch (err) {
      console.error('Error saving product:', err);
    } finally {
      setSubmitting(false);
    }
  };

  const toggleProductStatus = async (product: Product) => {
    try {
      await supabase
        .from('products')
        .update({ active: !product.active, updated_at: new Date().toISOString() })
        .eq('id', product.id);
      await fetchProducts();
    } catch (err) {
      console.error('Error toggling product status:', err);
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold text-white tracking-tight">Software Products</h1>
          <p className="text-sm text-slate-400 mt-1">
            Manage desktop software applications, catalog codes, and public versions.
          </p>
        </div>

        <button
          type="button"
          onClick={() => handleOpenModal()}
          className="inline-flex items-center space-x-2 px-4 py-2.5 bg-emerald-500 hover:bg-emerald-400 text-slate-950 text-sm font-semibold rounded-xl shadow-lg shadow-emerald-500/20 transition-all self-start sm:self-auto"
        >
          <svg className="w-4 h-4" fill="none" stroke="currentColor" viewBox="0 0 24 24">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4v16m8-8H4" />
          </svg>
          <span>Add Product</span>
        </button>
      </div>

      {/* Product List Cards */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
        {loading ? (
          <div className="col-span-full py-16 text-center text-slate-400">
            <div className="flex justify-center items-center space-x-2">
              <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-emerald-500"></div>
              <span>Loading products...</span>
            </div>
          </div>
        ) : products.length === 0 ? (
          <div className="col-span-full py-16 text-center text-slate-500 bg-slate-900 border border-slate-800 rounded-2xl">
            No products found. Create your first product above.
          </div>
        ) : (
          products.map((p) => (
            <div
              key={p.id}
              className="bg-slate-900 border border-slate-800 hover:border-slate-700 rounded-2xl p-6 shadow-xl flex flex-col justify-between transition-all"
            >
              <div className="space-y-3">
                <div className="flex items-center justify-between">
                  <span className="font-mono text-xs font-bold px-2.5 py-1 bg-emerald-500/10 text-emerald-400 border border-emerald-500/20 rounded-lg">
                    {p.product_code}
                  </span>
                  <span
                    className={`inline-flex items-center px-2 py-0.5 rounded text-[11px] font-semibold ${
                      p.active ? 'bg-emerald-500/10 text-emerald-400' : 'bg-slate-800 text-slate-500'
                    }`}
                  >
                    {p.active ? 'Active' : 'Disabled'}
                  </span>
                </div>

                <div>
                  <h3 className="text-lg font-bold text-white">{p.name}</h3>
                  <p className="text-xs text-slate-400 mt-1 line-clamp-2">
                    {p.description || 'No description provided.'}
                  </p>
                </div>

                <div className="pt-2 border-t border-slate-800 space-y-1 text-xs">
                  <div className="flex justify-between text-slate-400">
                    <span>Current Version:</span>
                    <span className="font-mono text-white font-medium">v{p.current_version || '1.0.0'}</span>
                  </div>
                  <div className="flex justify-between text-slate-400">
                    <span>Created:</span>
                    <span className="text-slate-300">{formatDate(p.created_at)}</span>
                  </div>
                </div>
              </div>

              <div className="flex items-center justify-between pt-4 mt-4 border-t border-slate-800/80">
                <button
                  type="button"
                  onClick={() => toggleProductStatus(p)}
                  className={`text-xs font-semibold hover:underline ${
                    p.active ? 'text-amber-400' : 'text-emerald-400'
                  }`}
                >
                  {p.active ? 'Disable' : 'Enable'}
                </button>

                <button
                  type="button"
                  onClick={() => handleOpenModal(p)}
                  className="px-3 py-1.5 text-xs font-medium text-slate-300 hover:text-white bg-slate-800 hover:bg-slate-700 rounded-lg transition-colors"
                >
                  Edit Details
                </button>
              </div>
            </div>
          ))
        )}
      </div>

      {/* Create / Edit Product Modal */}
      {modalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-950/80 backdrop-blur-sm animate-fadeIn">
          <div className="bg-slate-900 border border-slate-800 rounded-2xl max-w-lg w-full p-6 shadow-2xl space-y-5">
            <h3 className="text-lg font-bold text-white">
              {editingProduct ? 'Edit Product' : 'Create New Product'}
            </h3>

            <form onSubmit={handleSaveProduct} className="space-y-4 text-xs">
              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Product Code (Key Prefix) <span className="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="e.g. KNPRO, DRAMA"
                  value={productCode}
                  onChange={(e) => setProductCode(e.target.value.toUpperCase())}
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white font-mono uppercase"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Product Display Name <span className="text-rose-400">*</span>
                </label>
                <input
                  type="text"
                  required
                  placeholder="e.g. KN 3in1 Pro Drama Downloader"
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white"
                />
              </div>

              <div>
                <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                  Description
                </label>
                <textarea
                  rows={2}
                  value={description}
                  onChange={(e) => setDescription(e.target.value)}
                  placeholder="Short description of this tool or tier..."
                  className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2 text-sm text-white"
                />
              </div>

              <div className="grid grid-cols-2 gap-4">
                <div>
                  <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                    Current Version
                  </label>
                  <input
                    type="text"
                    required
                    placeholder="1.0.0"
                    value={currentVersion}
                    onChange={(e) => setCurrentVersion(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white font-mono"
                  />
                </div>
                <div>
                  <label className="block font-semibold text-slate-300 mb-1.5 uppercase tracking-wider">
                    Download Installer URL
                  </label>
                  <input
                    type="url"
                    placeholder="https://..."
                    value={downloadUrl}
                    onChange={(e) => setDownloadUrl(e.target.value)}
                    className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2.5 text-sm text-white"
                  />
                </div>
              </div>

              <div className="flex items-center space-x-2 pt-2">
                <input
                  type="checkbox"
                  id="activeCheck"
                  checked={active}
                  onChange={(e) => setActive(e.target.checked)}
                  className="rounded border-slate-800 bg-slate-950 text-emerald-500 focus:ring-emerald-500 h-4 w-4"
                />
                <label htmlFor="activeCheck" className="text-sm text-slate-300 font-medium">
                  Product Active & Open for Activations
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
                  {submitting ? 'Saving...' : 'Save Product'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
