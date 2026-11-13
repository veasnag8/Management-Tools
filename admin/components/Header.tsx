'use client';

import React, { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Menu, LogOut, User } from 'lucide-react';
import { createClient } from '@/lib/supabase/client';
import { StatusBadge } from './Badge';

interface HeaderProps {
  onMenuClick: () => void;
}

export function Header({ onMenuClick }: HeaderProps) {
  const router = useRouter();
  const [userEmail, setUserEmail] = useState<string>('admin@example.com');
  const [userRole, setUserRole] = useState<string>('admin');
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const supabase = createClient();
    async function fetchUser() {
      const { data: { user } } = await supabase.auth.getUser();
      if (user) {
        setUserEmail(user.email || '');
        const { data: profile } = await supabase
          .from('profiles')
          .select('role')
          .eq('id', user.id)
          .single();
        if (profile?.role) {
          setUserRole(profile.role);
        }
      }
    }
    fetchUser();
  }, []);

  const handleSignOut = async () => {
    setIsLoading(true);
    const supabase = createClient();
    await supabase.auth.signOut();
    router.push('/login');
  };

  return (
    <header className="sticky top-0 z-30 flex items-center justify-between h-16 px-4 sm:px-6 bg-[#060A16]/85 backdrop-blur-xl border-b border-slate-800/80">
      <div className="flex items-center space-x-3">
        <button
          onClick={onMenuClick}
          className="p-2 text-slate-400 rounded-lg lg:hidden hover:bg-slate-800 hover:text-white focus:outline-none"
        >
          <Menu className="w-5 h-5" />
        </button>
        <div>
          <div className="flex items-center gap-2 flex-wrap">
            <h1 className="text-sm sm:text-base font-extrabold text-white tracking-tight">
              <span className="bg-gradient-to-r from-amber-300 via-amber-400 to-yellow-500 bg-clip-text text-transparent">
                SNA GOD
              </span>
              <span className="text-slate-600 mx-1.5">•</span>
              <span>License Management Console</span>
            </h1>
            <span className="hidden sm:inline-flex items-center gap-1.5 px-2.5 py-0.5 rounded-full text-[11px] font-semibold bg-emerald-500/15 text-emerald-300 border border-emerald-400/30">
              <span className="relative flex h-2 w-2">
                <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75"></span>
                <span className="relative inline-flex rounded-full h-2 w-2 bg-emerald-400"></span>
              </span>
              Core Online • Live Token Telemetry
            </span>
          </div>
          <p className="text-[11px] text-slate-400 font-normal">
            Dubber Tool - ធូលបកប្រែរឿង • តាមដានចរន្ត AI និងការប្រើប្រាស់ Token ប្រចាំថ្ងៃ
          </p>
        </div>
      </div>

      <div className="flex items-center space-x-3">
        <div className="flex items-center space-x-2.5 pl-3 pr-2 py-1.5 rounded-xl bg-[#0D1428] border border-slate-800">
          <div className="flex flex-col items-end hidden md:flex text-right leading-tight">
            <span className="text-xs font-semibold text-slate-200">{userEmail}</span>
            <div className="mt-0.5">
              <span className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full text-[10px] font-bold bg-indigo-500/20 text-indigo-300 border border-indigo-400/30">
                <span className="w-1.5 h-1.5 rounded-full bg-indigo-400"></span>
                {userRole.toUpperCase()}
              </span>
            </div>
          </div>
          <div className="w-8 h-8 rounded-full bg-slate-800 border border-slate-700 flex items-center justify-center text-slate-300">
            <User className="w-4 h-4" />
          </div>
        </div>

        <button
          onClick={handleSignOut}
          disabled={isLoading}
          title="Sign Out"
          className="p-2 text-slate-400 hover:text-rose-400 hover:bg-rose-500/10 rounded-lg transition-colors"
        >
          <LogOut className="w-4 h-4" />
        </button>
      </div>
    </header>
  );
}
