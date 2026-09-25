'use client';

import { useState, useEffect } from 'react';
import { getBrowserClient } from '@/lib/supabase/client';
import { useRouter } from 'next/navigation';

export default function SettingsPage() {
  const router = useRouter();
  const supabase = getBrowserClient();

  const [userEmail, setUserEmail] = useState<string>('');
  const [userId, setUserId] = useState<string>('');
  const [role, setRole] = useState<string>('admin');
  const [fullName, setFullName] = useState<string>('');
  const [saving, setSaving] = useState(false);
  const [successMsg, setSuccessMsg] = useState<string | null>(null);

  // Health check state
  const [apiTesting, setApiTesting] = useState(false);
  const [apiStatus, setApiStatus] = useState<{
    ok: boolean;
    latency: number;
    message: string;
  } | null>(null);

  useEffect(() => {
    async function loadUser() {
      const {
        data: { user },
      } = await supabase.auth.getUser();

      if (user) {
        setUserEmail(user.email || '');
        setUserId(user.id);

        const { data: profile } = await supabase
          .from('profiles')
          .select('*')
          .eq('id', user.id)
          .single();

        if (profile) {
          setFullName(profile.full_name || '');
          setRole(profile.role || 'admin');
        }
      }
    }
    loadUser();
  }, [supabase]);

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true);
    setSuccessMsg(null);

    try {
      const { error } = await supabase
        .from('profiles')
        .upsert({
          id: userId,
          full_name: fullName,
          updated_at: new Date().toISOString(),
        });

      if (!error) {
        setSuccessMsg('Profile updated successfully.');
        setTimeout(() => setSuccessMsg(null), 3000);
      }
    } catch (err) {
      console.error('Error saving profile:', err);
    } finally {
      setSaving(false);
    }
  };

  const handleSignOut = async () => {
    await supabase.auth.signOut();
    router.push('/login');
  };

  const testApiHealth = async () => {
    setApiTesting(true);
    setApiStatus(null);
    const start = performance.now();

    try {
      // Direct health test to worker or Supabase query
      const { error } = await supabase.from('products').select('id').limit(1);
      const latency = Math.round(performance.now() - start);

      if (!error) {
        setApiStatus({
          ok: true,
          latency,
          message: `Supabase & Database connection operational (${latency}ms)`,
        });
      } else {
        setApiStatus({
          ok: false,
          latency,
          message: `Database connection error: ${error.message}`,
        });
      }
    } catch (err: any) {
      setApiStatus({
        ok: false,
        latency: 0,
        message: err.message || 'Failed to ping API.',
      });
    } finally {
      setApiTesting(false);
    }
  };

  return (
    <div className="max-w-4xl mx-auto space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-bold text-white tracking-tight">System & Account Settings</h1>
        <p className="text-sm text-slate-400 mt-1">
          Configure administrator credentials, inspect infrastructure, and test API endpoints.
        </p>
      </div>

      {successMsg && (
        <div className="p-4 bg-emerald-500/10 border border-emerald-500/30 rounded-xl text-sm text-emerald-400">
          {successMsg}
        </div>
      )}

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        {/* Profile Card */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-5">
          <h2 className="text-base font-bold text-white">Administrator Profile</h2>

          <form onSubmit={handleUpdateProfile} className="space-y-4 text-xs">
            <div>
              <label className="block font-semibold text-slate-400 mb-1">Email Address</label>
              <input
                type="text"
                disabled
                value={userEmail}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-sm text-slate-500 cursor-not-allowed"
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-400 mb-1">Admin Role</label>
              <input
                type="text"
                disabled
                value={role.toUpperCase()}
                className="w-full bg-slate-950 border border-slate-800 rounded-xl px-4 py-2 text-sm font-semibold text-emerald-400 cursor-not-allowed"
              />
            </div>

            <div>
              <label className="block font-semibold text-slate-300 mb-1">Full Display Name</label>
              <input
                type="text"
                value={fullName}
                onChange={(e) => setFullName(e.target.value)}
                placeholder="Admin Name"
                className="w-full bg-slate-950 border border-slate-800 focus:border-emerald-500 rounded-xl px-4 py-2 text-sm text-white"
              />
            </div>

            <div className="flex justify-between items-center pt-4 border-t border-slate-800">
              <button
                type="button"
                onClick={handleSignOut}
                className="px-4 py-2 text-xs font-semibold text-rose-400 hover:text-rose-300 bg-rose-500/10 hover:bg-rose-500/20 border border-rose-500/20 rounded-xl transition-colors"
              >
                Sign Out
              </button>

              <button
                type="submit"
                disabled={saving}
                className="px-5 py-2 text-xs font-semibold text-slate-950 bg-emerald-500 hover:bg-emerald-400 disabled:opacity-50 rounded-xl shadow-lg shadow-emerald-500/20 transition-all"
              >
                {saving ? 'Saving...' : 'Save Profile'}
              </button>
            </div>
          </form>
        </div>

        {/* Infrastructure & Diagnostics */}
        <div className="bg-slate-900 border border-slate-800 rounded-2xl p-6 shadow-xl space-y-5 flex flex-col justify-between">
          <div className="space-y-4">
            <h2 className="text-base font-bold text-white">System Diagnostics & API</h2>

            <div className="space-y-2 text-xs">
              <div className="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
                <span className="text-slate-500 block text-[11px]">Supabase Project URL</span>
                <span className="font-mono text-white text-xs block truncate">
                  {process.env.NEXT_PUBLIC_SUPABASE_URL || 'https://kscfelnxuavwkcyqwvsk.supabase.co'}
                </span>
              </div>

              <div className="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
                <span className="text-slate-500 block text-[11px]">License Cryptographic Engine</span>
                <span className="text-emerald-400 font-mono text-xs block">
                  HMAC SHA-256 (Anti-Clock Tamper Verified)
                </span>
              </div>

              <div className="p-3 bg-slate-950 rounded-xl border border-slate-800 space-y-1">
                <span className="text-slate-500 block text-[11px]">Security Isolation</span>
                <span className="text-cyan-400 text-xs block">
                  Row Level Security (RLS) + Edge Worker Auth Gate
                </span>
              </div>
            </div>

            {apiStatus && (
              <div
                className={`p-3 rounded-xl border text-xs font-mono ${
                  apiStatus.ok
                    ? 'bg-emerald-500/10 border-emerald-500/30 text-emerald-400'
                    : 'bg-rose-500/10 border-rose-500/30 text-rose-400'
                }`}
              >
                {apiStatus.message}
              </div>
            )}
          </div>

          <div className="pt-4 border-t border-slate-800">
            <button
              type="button"
              onClick={testApiHealth}
              disabled={apiTesting}
              className="w-full py-2.5 px-4 text-xs font-semibold text-white bg-slate-800 hover:bg-slate-700 disabled:opacity-50 rounded-xl border border-slate-700 transition-colors flex items-center justify-center space-x-2"
            >
              {apiTesting ? (
                <>
                  <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-emerald-500"></div>
                  <span>Testing Connection...</span>
                </>
              ) : (
                <>
                  <svg className="w-4 h-4 text-emerald-400" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 10V3L4 14h7v7l9-11h-7z" />
                  </svg>
                  <span>Test Database & API Connectivity</span>
                </>
              )}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
