'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Sidebar } from '@/components/Sidebar';
import { Header } from '@/components/Header';
import { createClient } from '@/lib/supabase/client';

export default function AdminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const router = useRouter();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [isCheckingAuth, setIsCheckingAuth] = useState(true);

  useEffect(() => {
    const supabase = createClient();
    async function checkAuth() {
      const { data: { session } } = await supabase.auth.getSession();
      if (!session) {
        router.push('/login');
      } else {
        setIsCheckingAuth(false);
      }
    }
    checkAuth();
  }, [router]);

  if (isCheckingAuth) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-[#060A16]">
        <div className="flex flex-col items-center space-y-3">
          <div className="w-9 h-9 border-4 border-emerald-400 border-t-transparent rounded-full animate-spin" />
          <span className="text-xs font-semibold text-slate-400 tracking-wider">CONNECTING TO SNA GOD CORE...</span>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-[#060A16] flex relative z-10 text-slate-100">
      <Sidebar isOpen={sidebarOpen} onClose={() => setSidebarOpen(false)} />
      <div className="flex-1 flex flex-col min-w-0 lg:pl-64">
        <Header onMenuClick={() => setSidebarOpen(true)} />
        <main className="flex-1 p-4 sm:p-6 lg:p-8 max-w-[1520px] w-full mx-auto">
          {children}
        </main>
      </div>
    </div>
  );
}
