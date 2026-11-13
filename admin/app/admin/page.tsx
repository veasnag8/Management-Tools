'use client';

import React, { useState, useEffect, useMemo, useCallback } from 'react';
import Link from 'next/link';
import {
  KeyRound,
  CheckCircle2,
  XCircle,
  Laptop2,
  ArrowRight,
  ShieldAlert,
  Activity,
  Zap,
  Coins,
  Copy,
  Eye,
  EyeOff,
  Flame,
  Check,
  RefreshCw
} from 'lucide-react';
import { createClient } from '@/lib/supabase/client';
import { formatDate, maskLicenseKey } from '@/lib/utils';
import { DashboardStats, License, ActivationLog } from '@/types';

interface LiveTelemetryUser {
  id: string;
  name: string;
  email: string;
  licenseKey: string;
  maskedKey: string;
  state: 'translating' | 'online' | 'offline';
  tokensToday: number;
  burnRate: number;
  currentTask: string;
  deviceId: string;
  expiresAt: string | null;
  status: string;
  lastSeenAt: string | null;
}

const HUB_CX = 410;
const HUB_CY = 235;

function getHubCoordinates(index: number, total: number) {
  const presets = [
    { x: 165, y: 112 },
    { x: 655, y: 112 },
    { x: 675, y: 318 },
    { x: 410, y: 398 },
    { x: 145, y: 318 },
  ];
  if (total <= 5 && index < presets.length) {
    return presets[index];
  }
  const angle = ((-90 + (360 / Math.max(total, 1)) * index) * Math.PI) / 180;
  return {
    x: Math.round(HUB_CX + 275 * Math.cos(angle)),
    y: Math.round(HUB_CY + 162 * Math.sin(angle)),
  };
}

function buildLightningSubPath(x1: number, y1: number, x2: number, y2: number) {
  const dx = x2 - x1;
  const dy = y2 - y1;
  const len = Math.hypot(dx, dy) || 1;
  const nx = -dy / len;
  const ny = dx / len;
  const p1x = x1 + dx * 0.28 + nx * 6;
  const p1y = y1 + dy * 0.28 + ny * 6;
  const p2x = x1 + dx * 0.52 - nx * 7;
  const p2y = y1 + dy * 0.52 - ny * 7;
  const p3x = x1 + dx * 0.76 + nx * 5;
  const p3y = y1 + dy * 0.76 + ny * 5;
  return `M ${x1} ${y1} L ${p1x.toFixed(1)} ${p1y.toFixed(1)} L ${p2x.toFixed(1)} ${p2y.toFixed(1)} L ${p3x.toFixed(1)} ${p3y.toFixed(1)} L ${x2} ${y2}`;
}

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
  const [liveUsers, setLiveUsers] = useState<LiveTelemetryUser[]>([]);
  const [revealedKeys, setRevealedKeys] = useState<Record<string, boolean>>({});
  const [selectedUser, setSelectedUser] = useState<LiveTelemetryUser | null>(null);
  const [sidebarFilter, setSidebarFilter] = useState<'ALL' | 'translating' | 'online' | 'offline'>('ALL');
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isRefreshing, setIsRefreshing] = useState(false);
  const [lastSyncedTime, setLastSyncedTime] = useState<string>('');

  // Real-time Database Fetcher
  const fetchRealData = useCallback(async (showIndicator = false) => {
    if (showIndicator) setIsRefreshing(true);
    const supabase = createClient();

    try {
      // 1. Fetch real licenses from Supabase
      const { data: licenses } = await supabase
        .from('licenses')
        .select('*, products(name, product_code), devices(*)')
        .order('created_at', { ascending: false });

      // 2. Fetch real active devices count
      const { count: activeDevicesCount } = await supabase
        .from('devices')
        .select('*', { count: 'exact', head: true })
        .eq('status', 'active');

      // 3. Fetch real activation logs
      const { data: activations } = await supabase
        .from('activations')
        .select('*, licenses(license_key, customer_name)')
        .order('created_at', { ascending: false })
        .limit(10);

      // 4. Fetch real Gemini token usages for today
      const todayStart = new Date();
      todayStart.setHours(0, 0, 0, 0);

      const { data: tokenUsages } = await supabase
        .from('token_usages')
        .select('*')
        .gte('created_at', todayStart.toISOString());

      // Aggregate real tokens per license
      const tokenMapByLicense: Record<string, { total: number; latestTask: string; latestAt: string }> = {};
      if (tokenUsages && Array.isArray(tokenUsages)) {
        tokenUsages.forEach((t: any) => {
          if (!tokenMapByLicense[t.license_id]) {
            tokenMapByLicense[t.license_id] = { total: 0, latestTask: '', latestAt: '' };
          }
          tokenMapByLicense[t.license_id].total += t.total_tokens || 0;
          if (!tokenMapByLicense[t.license_id].latestAt || t.created_at > tokenMapByLicense[t.license_id].latestAt) {
            tokenMapByLicense[t.license_id].latestAt = t.created_at;
            tokenMapByLicense[t.license_id].latestTask = t.task_description || t.task_type || 'Gemini AI Call';
          }
        });
      }

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

        // Map real licenses directly to real-time telemetry nodes
        const mappedUsers: LiveTelemetryUser[] = licenses.map((lic: any) => {
          const dev = lic.devices?.find((d: any) => d.status === 'active') || lic.devices?.[0];
          const tokenData = tokenMapByLicense[lic.id];
          const tokensToday = tokenData?.total || 0;

          // Determine live state strictly from real database heartbeat & device activity
          let state: 'translating' | 'online' | 'offline' = 'offline';
          let burnRate = 0;
          let currentTask = 'Not Used Yet';

          if (lic.status === 'active' && dev && dev.status === 'active') {
            const lastSeen = dev.last_seen_at ? new Date(dev.last_seen_at).getTime() : 0;
            const diffSeconds = (now.getTime() - lastSeen) / 1000;

            // Heartbeat sent within last 60s means app is online
            if (diffSeconds <= 60) {
              const latestTokenTime = tokenData?.latestAt ? new Date(tokenData.latestAt).getTime() : 0;
              const tokenDiffSeconds = (now.getTime() - latestTokenTime) / 1000;

              // If Gemini tokens consumed within last 3 minutes, client is translating
              if (tokenDiffSeconds <= 180 && tokensToday > 0) {
                state = 'translating';
                burnRate = Math.min(300, Math.max(90, Math.round(tokensToday / 120)));
                currentTask = tokenData?.latestTask || 'Active Gemini AI Stream';
              } else {
                state = 'online';
                currentTask = 'Dubber App Standby (Ready)';
              }
            }
          }

          return {
            id: lic.id,
            name: lic.customer_name || 'Unnamed Client',
            email: lic.customer_email || 'No email registered',
            licenseKey: lic.license_key,
            maskedKey: maskLicenseKey(lic.license_key),
            state,
            tokensToday,
            burnRate,
            currentTask,
            deviceId: dev?.device_id || 'Unbound',
            expiresAt: lic.expires_at,
            status: lic.status,
            lastSeenAt: dev?.last_seen_at || null,
          };
        });

        setLiveUsers(mappedUsers);
      }

      if (activations) {
        setRecentActivations(activations);
      }

      setLastSyncedTime(new Date().toLocaleTimeString());
    } catch (err) {
      console.error('Failed to load real database metrics:', err);
    } finally {
      setIsLoading(false);
      setIsRefreshing(false);
    }
  }, []);

  // Initial load
  useEffect(() => {
    fetchRealData();
  }, [fetchRealData]);

  // Real-time Supabase Postgres Changes Subscription
  useEffect(() => {
    const supabase = createClient();

    // Listen to changes in devices (heartbeats/activations)
    const deviceSub = supabase
      .channel('realtime:devices')
      .on('postgres_changes', { event: '*', schema: 'public', table: 'devices' }, () => {
        fetchRealData();
      })
      .subscribe();

    // Listen to changes in licenses
    const licenseSub = supabase
      .channel('realtime:licenses')
      .on('postgres_changes', { event: '*', schema: 'public', table: 'licenses' }, () => {
        fetchRealData();
      })
      .subscribe();

    // Listen to changes in token_usages
    const tokenSub = supabase
      .channel('realtime:token_usages')
      .on('postgres_changes', { event: 'INSERT', schema: 'public', table: 'token_usages' }, () => {
        fetchRealData();
      })
      .subscribe();

    // Polling interval every 8 seconds to ensure state stays in sync
    const pollInterval = setInterval(() => {
      fetchRealData();
    }, 8000);

    return () => {
      supabase.removeChannel(deviceSub);
      supabase.removeChannel(licenseSub);
      supabase.removeChannel(tokenSub);
      clearInterval(pollInterval);
    };
  }, [fetchRealData]);

  // Dynamic calculations based exclusively on real fetched data
  const totalTokensToday = useMemo(
    () => liveUsers.reduce((sum, u) => sum + u.tokensToday, 0),
    [liveUsers]
  );
  const activeStreamsCount = useMemo(
    () => liveUsers.filter((u) => u.state === 'translating').length,
    [liveUsers]
  );
  const totalBurnRate = useMemo(
    () => liveUsers.filter((u) => u.state === 'translating').reduce((sum, u) => sum + u.burnRate, 0),
    [liveUsers]
  );
  const onlineUsersCount = useMemo(
    () => liveUsers.filter((u) => u.state === 'online' || u.state === 'translating').length,
    [liveUsers]
  );

  const filteredLeaderboard = useMemo(() => {
    const sorted = [...liveUsers].sort((a, b) => b.tokensToday - a.tokensToday);
    if (sidebarFilter === 'ALL') return sorted;
    return sorted.filter((u) => u.state === sidebarFilter);
  }, [liveUsers, sidebarFilter]);

  const maxTokens = useMemo(
    () => Math.max(...liveUsers.map((u) => u.tokensToday), 1),
    [liveUsers]
  );

  const toggleRevealKey = (id: string) => {
    setRevealedKeys((prev) => ({ ...prev, [id]: !prev[id] }));
  };

  const copyKeyToClipboard = (key: string) => {
    navigator.clipboard.writeText(key);
    setCopiedKey(key);
    setTimeout(() => setCopiedKey(null), 2000);
  };

  return (
    <div className="space-y-6">
      {/* ACTION REQUIRED BANNER */}
      {stats.expiring7Days > 0 && (
        <div className="p-4 rounded-2xl bg-amber-500/10 border border-amber-500/30 flex items-start space-x-3 text-amber-200">
          <ShieldAlert className="w-5 h-5 flex-shrink-0 text-amber-400 mt-0.5" />
          <div className="flex-1 text-sm font-medium">
            <span className="font-bold text-amber-300">Action Required:</span> {stats.expiring7Days} active license(s) expiring within the next 7 days.
          </div>
          <Link href="/admin/licenses" className="text-xs font-bold text-amber-300 hover:underline flex items-center space-x-1">
            <span>View All</span>
            <ArrowRight className="w-3.5 h-3.5" />
          </Link>
        </div>
      )}

      {/* TOP ROW: SNA GOD ENERGY HUB (8 COLS) + USERS & DAILY TOKEN USAGE (4 COLS) */}
      <div className="grid grid-cols-1 xl:grid-cols-12 gap-5 items-stretch">
        {/* LEFT 8 COLS: SNA GOD ORBITAL & LIVE ELECTRIC CIRCUIT CANVAS */}
        <div className="xl:col-span-8 glass-panel rounded-2xl p-4 sm:p-5 relative overflow-hidden flex flex-col justify-between">
          <div className="relative z-20 flex flex-wrap items-center justify-between gap-2 pb-2">
            <div className="flex items-center gap-2 px-3 py-1.5 rounded-xl bg-slate-950/85 border border-amber-400/30">
              <span className="w-2 h-2 rounded-full bg-amber-400 shadow-[0_0_8px_#fbbf24]"></span>
              <span className="text-xs font-bold text-amber-200">SNA GOD AI Energy Conduit</span>
              <span className="text-[11px] font-mono text-slate-400">• {totalBurnRate} tok/s</span>
              {lastSyncedTime && (
                <span className="text-[10px] text-slate-500 ml-1">Synced {lastSyncedTime}</span>
              )}
            </div>

            <div className="flex items-center gap-2">
              <button
                onClick={() => fetchRealData(true)}
                disabled={isRefreshing}
                className="p-1.5 rounded-lg bg-slate-900 border border-slate-700 text-slate-400 hover:text-white transition disabled:opacity-50"
                title="Refresh Real Database"
              >
                <RefreshCw className={`w-3.5 h-3.5 ${isRefreshing ? 'animate-spin' : ''}`} />
              </button>

              <div className="flex flex-wrap items-center gap-3 text-[11px] bg-slate-950/85 px-3 py-1.5 rounded-xl border border-slate-800">
                <span className="flex items-center gap-1.5 text-emerald-300 font-semibold">
                  <span className="w-2.5 h-2.5 rounded-full bg-emerald-400 shadow-[0_0_10px_#10b981] animate-pulse"></span>
                  <span>⚡ កំពុង Translate AI (មានចរន្ត)</span>
                </span>
                <span className="flex items-center gap-1.5 text-emerald-400 font-medium">
                  <span className="w-2 h-2 rounded-full bg-emerald-500"></span>
                  <span>🟢 កំពុងប្រើ (App Open)</span>
                </span>
                <span className="flex items-center gap-1.5 text-slate-400 font-medium">
                  <span className="w-2 h-2 rounded-full bg-slate-500"></span>
                  <span>⚫ មិនទាន់ប្រើ</span>
                </span>
              </div>
            </div>
          </div>

          {/* EXACT 820 x 470 LOGICAL CANVAS */}
          <div className="relative w-full overflow-x-auto py-1">
            <div className="relative w-full min-w-[620px] aspect-[820/470] mx-auto select-none">
              {/* Concentric Decorative Cosmic Rings */}
              <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-[74%] h-[82%] rounded-[50%] border border-slate-700/35 pointer-events-none"></div>
              <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 w-[54%] h-[62%] rounded-[50%] border border-emerald-500/15 pointer-events-none"></div>
              <div className="absolute left-1/2 top-1/2 w-[31%] h-[54%] rounded-full border border-dashed border-amber-400/25 orbit-ring-cw pointer-events-none">
                <span className="absolute -top-1 left-1/2 w-2 h-2 rounded-full bg-amber-400 shadow-[0_0_10px_#fbbf24]"></span>
              </div>
              <div className="absolute left-1/2 top-1/2 w-[24%] h-[42%] rounded-full border border-dotted border-emerald-400/30 orbit-ring-ccw pointer-events-none">
                <span className="absolute -bottom-1 left-1/2 w-2 h-2 rounded-full bg-emerald-400 shadow-[0_0_10px_#10b981]"></span>
              </div>

              {/* SVG ELECTRIC WIRES */}
              <svg viewBox="0 0 820 470" preserveAspectRatio="none" className="absolute inset-0 w-full h-full z-10 pointer-events-none">
                <defs>
                  <filter id="emeraldPlasmaGlow" x="-30%" y="-30%" width="160%" height="160%">
                    <feGaussianBlur stdDeviation="4.5" result="blur1" />
                    <feGaussianBlur stdDeviation="1.8" result="blur2" />
                    <feMerge>
                      <feMergeNode in="blur1" />
                      <feMergeNode in="blur2" />
                      <feMergeNode in="SourceGraphic" />
                    </feMerge>
                  </filter>
                  <filter id="goldSparkGlow" x="-50%" y="-50%" width="200%" height="200%">
                    <feGaussianBlur stdDeviation="2.5" result="sparkBlur" />
                    <feMerge>
                      <feMergeNode in="sparkBlur" />
                      <feMergeNode in="SourceGraphic" />
                    </feMerge>
                  </filter>
                </defs>
                <g>
                  {liveUsers.map((user, idx) => {
                    const { x: ux, y: uy } = getHubCoordinates(idx, liveUsers.length);
                    const pathId = `conduit-path-${user.id}`;

                    if (user.state === 'translating') {
                      return (
                        <g key={user.id}>
                          <path id={pathId} d={`M ${HUB_CX} ${HUB_CY} L ${ux} ${uy}`} stroke="rgba(16, 185, 129, 0.25)" strokeWidth="5" fill="none" />
                          <path d={`M ${HUB_CX} ${HUB_CY} L ${ux} ${uy}`} stroke="#10B981" strokeWidth="3.5" strokeOpacity="0.75" filter="url(#emeraldPlasmaGlow)" fill="none" />
                          <path d={`M ${HUB_CX} ${HUB_CY} L ${ux} ${uy}`} stroke="#34D399" strokeWidth="2.6" strokeLinecap="round" className="electric-stream-fast" fill="none" />
                          <path d={buildLightningSubPath(HUB_CX, HUB_CY, ux, uy)} stroke="#FDE047" strokeWidth="1.4" strokeLinecap="round" className="electric-arc-flicker" fill="none" />
                          {[0, 0.45, 0.9].map((delaySec, pIdx) => (
                            <circle key={pIdx} r={pIdx === 0 ? 4.5 : 3.6} fill={pIdx === 0 ? '#FDE047' : '#34D399'} filter="url(#goldSparkGlow)">
                              <animateMotion dur="1.35s" begin={`${delaySec}s`} repeatCount="indefinite">
                                <mpath href={`#${pathId}`} />
                              </animateMotion>
                            </circle>
                          ))}
                        </g>
                      );
                    }

                    if (user.state === 'online') {
                      return (
                        <path
                          key={user.id}
                          d={`M ${HUB_CX} ${HUB_CY} L ${ux} ${uy}`}
                          stroke="#10B981"
                          strokeWidth="2.2"
                          filter="url(#emeraldPlasmaGlow)"
                          className="online-steady-wire"
                          fill="none"
                        />
                      );
                    }

                    return (
                      <path
                        key={user.id}
                        d={`M ${HUB_CX} ${HUB_CY} L ${ux} ${uy}`}
                        stroke="rgba(148, 163, 184, 0.22)"
                        strokeWidth="1.4"
                        strokeDasharray="5 6"
                        fill="none"
                      />
                    );
                  })}
                </g>
              </svg>

              {/* CENTRAL GOLDEN-AMBER SNA GOD CORE */}
              <div className="absolute left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-20 w-32 h-32 sm:w-36 sm:h-36 rounded-full sna-god-core flex flex-col items-center justify-center text-center border-2 border-amber-100/90 px-2 shadow-2xl">
                <Flame className="w-8 h-8 sm:w-9 sm:h-9 text-white drop-shadow-[0_2px_6px_rgba(120,53,15,0.9)]" />
                <span className="font-extrabold text-sm sm:text-base tracking-wider text-white drop-shadow-[0_2px_8px_rgba(0,0,0,0.85)] leading-none mt-0.5">
                  SNA GOD
                </span>
                <div className="mt-1 px-2 py-0.5 rounded-full bg-slate-950/65 border border-amber-200/40 text-[9px] font-mono font-bold text-amber-100 leading-tight">
                  {activeStreamsCount} AI Stream{activeStreamsCount === 1 ? '' : 's'}
                </div>
                <div className="text-[9px] font-mono font-extrabold text-white drop-shadow mt-0.5">
                  {totalTokensToday.toLocaleString()} tok
                </div>
              </div>

              {/* REAL USER NODES LAYER */}
              <div className="absolute inset-0 z-20">
                {liveUsers.map((user, idx) => {
                  const { x: ux, y: uy } = getHubCoordinates(idx, liveUsers.length);
                  const leftPct = (ux / 820) * 100;
                  const topPct = (uy / 470) * 100;
                  const initials = user.name.slice(0, 2).toUpperCase();

                  let ringStyle = 'border-slate-600/80 bg-slate-900/90 text-slate-300';
                  if (user.state === 'translating') {
                    ringStyle = 'border-emerald-400 bg-emerald-950/95 text-emerald-100 node-translating-glow';
                  } else if (user.state === 'online') {
                    ringStyle = 'border-emerald-500 bg-emerald-950/90 text-emerald-100 node-online-glow';
                  }

                  return (
                    <button
                      key={user.id}
                      type="button"
                      onClick={() => setSelectedUser(user)}
                      style={{ left: `${leftPct}%`, top: `${topPct}%` }}
                      className="hub-user-node absolute -translate-x-1/2 -translate-y-1/2 flex flex-col items-center cursor-pointer focus:outline-none"
                    >
                      <div className={`relative w-11 h-11 sm:w-12 sm:h-12 rounded-full border-2 ${ringStyle} flex items-center justify-center font-extrabold text-xs sm:text-sm backdrop-blur-md`}>
                        <span>{initials}</span>
                        <span className={`absolute -bottom-0.5 -right-0.5 w-3 h-3 rounded-full border-2 border-[#060A16] ${user.state === 'offline' ? 'bg-slate-500' : 'bg-emerald-400'}`}></span>
                      </div>
                      <div className="mt-1 px-2.5 py-1 rounded-xl bg-[#090F20]/95 border border-slate-800 text-center shadow-xl whitespace-nowrap">
                        <div className="text-[11px] font-bold text-white leading-tight">{user.name}</div>
                        <div className="my-0.5">
                          {user.state === 'translating' ? (
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/25 text-emerald-300 border border-emerald-400/50 shadow-[0_0_12px_rgba(16,185,129,0.4)]">
                              ⚡ Translate AI
                            </span>
                          ) : user.state === 'online' ? (
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-500/40">
                              🟢 កំពុងប្រើ
                            </span>
                          ) : (
                            <span className="px-2 py-0.5 rounded-full text-[10px] font-medium bg-slate-800/95 text-slate-400 border border-slate-700">
                              មិនទាន់ប្រើ
                            </span>
                          )}
                        </div>
                        <div className={`text-[10px] font-mono font-bold ${user.state === 'translating' ? 'text-amber-300' : user.state === 'online' ? 'text-emerald-300' : 'text-slate-400'}`}>
                          {user.tokensToday.toLocaleString()} tok
                        </div>
                      </div>
                    </button>
                  );
                })}
              </div>

              {/* USER DETAILS POPOVER */}
              {selectedUser && (
                <div className="absolute z-40 top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-80 glass-panel rounded-2xl p-4 border border-emerald-400/50 shadow-2xl">
                  <div className="flex items-start justify-between gap-2 mb-2.5">
                    <div className="flex items-center gap-2.5">
                      <div className="w-9 h-9 rounded-full flex items-center justify-center font-bold text-xs text-white bg-slate-900 border border-emerald-400/40">
                        {selectedUser.name.slice(0, 2).toUpperCase()}
                      </div>
                      <div>
                        <h4 className="text-sm font-bold text-white leading-tight">{selectedUser.name}</h4>
                        <p className="text-[11px] text-slate-400">{selectedUser.email}</p>
                      </div>
                    </div>
                    <button
                      onClick={() => setSelectedUser(null)}
                      className="text-slate-400 hover:text-white p-1 rounded-lg"
                    >
                      ✕
                    </button>
                  </div>

                  <div className="p-2.5 rounded-xl bg-slate-950/90 border border-slate-800 space-y-1.5 text-[11px] mb-3">
                    <div className="flex items-center justify-between gap-2">
                      <span className="text-slate-400">License Key:</span>
                      <span className="font-mono font-bold text-indigo-300 truncate">{selectedUser.maskedKey}</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-400">ស្ថានភាព (Live):</span>
                      <span className={`font-bold ${selectedUser.state === 'translating' ? 'text-emerald-300' : selectedUser.state === 'online' ? 'text-emerald-400' : 'text-slate-400'}`}>
                        {selectedUser.state === 'translating' ? '⚡ Translate AI' : selectedUser.state === 'online' ? '🟢 កំពុងប្រើ' : '⚫ មិនទាន់ប្រើ'}
                      </span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-400">Gemini Token ថ្ងៃនេះ:</span>
                      <span className="font-mono font-extrabold text-amber-300">{selectedUser.tokensToday.toLocaleString()} Tokens</span>
                    </div>
                    <div className="flex items-center justify-between">
                      <span className="text-slate-400">Device ID:</span>
                      <span className="font-mono text-slate-300 truncate max-w-[165px]">{selectedUser.deviceId}</span>
                    </div>
                    {selectedUser.lastSeenAt && (
                      <div className="flex items-center justify-between text-[10px] text-slate-500">
                        <span>Last Heartbeat:</span>
                        <span>{formatDate(selectedUser.lastSeenAt)}</span>
                      </div>
                    )}
                  </div>

                  <div className="space-y-1.5">
                    <button
                      onClick={() => copyKeyToClipboard(selectedUser.licenseKey)}
                      className="w-full py-2 rounded-xl text-xs font-semibold bg-indigo-500/20 text-indigo-300 border border-indigo-400/30 hover:bg-indigo-500/30 transition flex items-center justify-center gap-1.5"
                    >
                      {copiedKey === selectedUser.licenseKey ? <Check className="w-3.5 h-3.5" /> : <Copy className="w-3.5 h-3.5" />}
                      <span>{copiedKey === selectedUser.licenseKey ? 'Copied Full Key!' : 'Copy Full License Key'}</span>
                    </button>
                    <Link
                      href={`/admin/licenses/${selectedUser.id}`}
                      className="w-full py-2 rounded-xl text-xs font-semibold bg-slate-800 text-slate-200 border border-slate-700 hover:bg-slate-700 transition flex items-center justify-center"
                    >
                      Manage License Details →
                    </Link>
                  </div>
                </div>
              )}
            </div>
          </div>
        </div>

        {/* RIGHT 4 COLS: USERS & DAILY TOKEN USAGE PANEL */}
        <div className="xl:col-span-4 glass-panel rounded-2xl p-4 sm:p-5 flex flex-col justify-between">
          <div>
            <div className="flex items-center justify-between pb-3 mb-3 border-b border-slate-800">
              <div>
                <h2 className="font-bold text-sm sm:text-base text-white leading-tight">Users &amp; Daily Token Usage</h2>
                <p className="text-[11px] text-slate-400">ការប្រើប្រាស់ Gemini Token ថ្ងៃនេះពី Database ផ្ទាល់</p>
              </div>
              <span className="px-2.5 py-1 rounded-lg text-[11px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-400/35">
                {onlineUsersCount} / {liveUsers.length} កំពុងប្រើ
              </span>
            </div>

            {/* Filter Pills */}
            <div className="flex flex-wrap items-center gap-1.5 mb-3">
              <button
                onClick={() => setSidebarFilter('ALL')}
                className={`px-2.5 py-1 rounded-lg text-[11px] font-semibold transition ${sidebarFilter === 'ALL' ? 'bg-indigo-600 text-white' : 'bg-slate-900 text-slate-300 border border-slate-700 hover:bg-slate-800'}`}
              >
                All ({liveUsers.length})
              </button>
              <button
                onClick={() => setSidebarFilter('translating')}
                className={`px-2.5 py-1 rounded-lg text-[11px] font-medium transition ${sidebarFilter === 'translating' ? 'bg-indigo-600 text-white' : 'bg-slate-900 text-emerald-300 border border-emerald-500/30 hover:bg-slate-800'}`}
              >
                ⚡ Translate AI ({liveUsers.filter((u) => u.state === 'translating').length})
              </button>
              <button
                onClick={() => setSidebarFilter('online')}
                className={`px-2.5 py-1 rounded-lg text-[11px] font-medium transition ${sidebarFilter === 'online' ? 'bg-indigo-600 text-white' : 'bg-slate-900 text-emerald-200 border border-slate-700 hover:bg-slate-800'}`}
              >
                🟢 កំពុងប្រើ ({liveUsers.filter((u) => u.state === 'online').length})
              </button>
              <button
                onClick={() => setSidebarFilter('offline')}
                className={`px-2.5 py-1 rounded-lg text-[11px] font-medium transition ${sidebarFilter === 'offline' ? 'bg-indigo-600 text-white' : 'bg-slate-900 text-slate-400 border border-slate-700 hover:bg-slate-800'}`}
              >
                ⚫ មិនទាន់ប្រើ ({liveUsers.filter((u) => u.state === 'offline').length})
              </button>
            </div>

            {/* User List */}
            <div className="space-y-2.5 max-h-[360px] overflow-y-auto pr-1">
              {filteredLeaderboard.length > 0 ? (
                filteredLeaderboard.map((user) => {
                  const pct = Math.min(100, Math.round((user.tokensToday / maxTokens) * 100));
                  const isHeavy = user.tokensToday >= 200000;

                  return (
                    <div
                      key={user.id}
                      className={`p-3 rounded-xl bg-slate-950/75 hover:bg-slate-900/95 border transition space-y-2 ${user.state === 'translating' ? 'border-emerald-500/35' : 'border-slate-800'}`}
                    >
                      <div className="flex items-center justify-between gap-2">
                        <div className="flex items-center gap-2.5 min-w-0">
                          <div className={`w-8 h-8 rounded-full flex items-center justify-center font-bold text-xs shrink-0 ${user.state === 'offline' ? 'bg-slate-800 text-slate-400' : 'bg-emerald-950 text-emerald-300 border border-emerald-400/40'}`}>
                            {user.name.slice(0, 2).toUpperCase()}
                          </div>
                          <div className="min-w-0">
                            <div className="text-xs font-bold text-white truncate">{user.name}</div>
                            <div className="text-[10px] font-mono text-indigo-300 truncate">{user.maskedKey}</div>
                          </div>
                        </div>
                        {user.state === 'translating' ? (
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-400/45 animate-pulse">
                            ⚡ Translate AI
                          </span>
                        ) : user.state === 'online' ? (
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-bold bg-emerald-500/15 text-emerald-400 border border-emerald-500/35">
                            🟢 កំពុងប្រើ
                          </span>
                        ) : (
                          <span className="px-2 py-0.5 rounded-full text-[10px] font-medium bg-slate-800 text-slate-400 border border-slate-700">
                            ⚫ មិនទាន់ប្រើ
                          </span>
                        )}
                      </div>

                      <div className="space-y-1">
                        <div className="flex items-center justify-between text-[11px]">
                          <span className="text-slate-400">Gemini Token ថ្ងៃនេះ:</span>
                          <div className="flex items-center gap-1.5">
                            {user.state === 'translating' && (
                              <span className="text-[10px] font-mono font-bold text-emerald-400">+{user.burnRate} tok/s</span>
                            )}
                            <span className={`font-mono font-extrabold ${isHeavy ? 'text-amber-300' : 'text-slate-200'}`}>
                              {user.tokensToday.toLocaleString()} tokens
                            </span>
                          </div>
                        </div>
                        <div className="w-full h-1.5 rounded-full bg-slate-800 overflow-hidden">
                          <div
                            className={`h-full rounded-full transition-all duration-500 ${user.state === 'translating' ? 'bg-gradient-to-r from-emerald-500 to-amber-400' : isHeavy ? 'bg-amber-400' : user.state === 'online' ? 'bg-emerald-500' : 'bg-slate-700'}`}
                            style={{ width: `${pct}%` }}
                          />
                        </div>
                      </div>

                      <div className="flex items-center justify-between gap-1 pt-1 border-t border-slate-800/80">
                        <span className="text-[10px] text-slate-500 truncate max-w-[170px]">{user.currentTask}</span>
                        <button
                          onClick={() => copyKeyToClipboard(user.licenseKey)}
                          className="px-2 py-0.5 rounded text-[10px] font-semibold bg-indigo-500/15 text-indigo-300 hover:bg-indigo-500/25 transition"
                        >
                          {copiedKey === user.licenseKey ? 'Copied' : 'Copy Key'}
                        </button>
                      </div>
                    </div>
                  );
                })
              ) : (
                <div className="text-center py-8 text-xs text-slate-500">
                  គ្មាន User ក្នុង Database ត្រូវនឹង Filter នេះទេ។
                </div>
              )}
            </div>
          </div>

          <div className="mt-3 pt-2.5 px-3 py-2 rounded-xl bg-slate-950/90 border border-slate-800 flex items-center justify-between text-[11px]">
            <span className="text-slate-400">សរុប Gemini Token ថ្ងៃនេះ:</span>
            <span className="font-mono font-extrabold text-amber-300">{totalTokensToday.toLocaleString()} tokens</span>
          </div>
        </div>
      </div>

      {/* SYSTEM OVERVIEW 5 KPI STRIP */}
      <div className="space-y-3.5">
        <div>
          <h2 className="text-lg sm:text-xl font-extrabold text-white tracking-tight">System Overview</h2>
          <p className="text-xs text-slate-400">
            Real-time status of software licenses, green online users, AI current streams, and daily token burn directly from Supabase.
          </p>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
          <div className="glass-panel glass-panel-hover rounded-2xl p-4 sm:p-5 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-slate-400">Total Licenses</p>
              <p className="text-2xl sm:text-3xl font-extrabold text-white my-1">{stats.totalLicenses}</p>
              <p className="text-xs text-slate-400">Issued keys in DB</p>
            </div>
            <div className="w-11 h-11 rounded-xl bg-indigo-500/20 border border-indigo-400/30 flex items-center justify-center text-indigo-400">
              <KeyRound className="w-5 h-5" />
            </div>
          </div>

          <div className="glass-panel glass-panel-hover rounded-2xl p-4 sm:p-5 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-slate-400">Active / Online Users</p>
              <p className="text-2xl sm:text-3xl font-extrabold text-emerald-400 my-1">{onlineUsersCount}</p>
              <p className="text-xs text-emerald-300 font-semibold">កំពុងប្រើ (Heartbeat OK)</p>
            </div>
            <div className="w-11 h-11 rounded-xl bg-emerald-500/20 border border-emerald-400/35 flex items-center justify-center text-emerald-400">
              <CheckCircle2 className="w-5 h-5" />
            </div>
          </div>

          <div className="glass-panel glass-panel-hover rounded-2xl p-4 sm:p-5 flex items-center justify-between border-emerald-500/25">
            <div>
              <p className="text-xs font-medium text-slate-400">Active AI Streams</p>
              <p className="text-2xl sm:text-3xl font-extrabold text-white my-1">{activeStreamsCount}</p>
              <p className="text-xs font-mono text-emerald-300 font-semibold">⚡ +{totalBurnRate} tok/s</p>
            </div>
            <div className="w-11 h-11 rounded-xl bg-emerald-500/20 border border-emerald-400/40 flex items-center justify-center text-emerald-300">
              <Zap className="w-5 h-5" />
            </div>
          </div>

          <div className="glass-panel glass-panel-hover rounded-2xl p-4 sm:p-5 flex items-center justify-between border-amber-500/25">
            <div>
              <p className="text-xs font-medium text-slate-400">Today&apos;s Total Tokens</p>
              <p className="font-mono text-xl sm:text-2xl font-extrabold text-amber-300 my-1.5">{totalTokensToday.toLocaleString()}</p>
              <p className="text-xs text-amber-200/80">Gemini API Consumption</p>
            </div>
            <div className="w-11 h-11 rounded-xl bg-amber-500/20 border border-amber-400/35 flex items-center justify-center text-amber-400">
              <Coins className="w-5 h-5" />
            </div>
          </div>

          <div className="glass-panel glass-panel-hover rounded-2xl p-4 sm:p-5 flex items-center justify-between">
            <div>
              <p className="text-xs font-medium text-slate-400">Expired / Disabled</p>
              <p className="text-2xl sm:text-3xl font-extrabold text-white my-1">{stats.disabledLicenses + stats.expiredLicenses}</p>
              <p className="text-xs text-rose-400/90">Access blocked</p>
            </div>
            <div className="w-11 h-11 rounded-xl bg-rose-500/20 border border-rose-400/30 flex items-center justify-center text-rose-400">
              <XCircle className="w-5 h-5" />
            </div>
          </div>
        </div>
      </div>

      {/* BOTTOM ROW: RECENTLY CREATED LICENSES (8 COLS) + LIVE CLIENT ACTIVITY (4 COLS) */}
      <div className="grid grid-cols-1 lg:grid-cols-12 gap-5 items-start">
        {/* Real Licenses Table */}
        <div className="lg:col-span-8 glass-panel rounded-2xl p-4 sm:p-5">
          <div className="flex items-center justify-between pb-3.5 mb-3 border-b border-slate-800">
            <div className="flex items-center gap-2">
              <KeyRound className="w-4 h-4 text-indigo-400" />
              <h3 className="font-bold text-base text-white">Recently Created Licenses</h3>
            </div>
            <Link href="/admin/licenses" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300 flex items-center gap-1">
              <span>View All</span>
              <ArrowRight className="w-3.5 h-3.5" />
            </Link>
          </div>

          <div className="overflow-x-auto">
            <table className="w-full text-left border-collapse text-xs">
              <thead>
                <tr className="border-b border-slate-800 text-[11px] font-bold uppercase tracking-wider text-slate-400">
                  <th className="py-2.5 px-3">License Key</th>
                  <th className="py-2.5 px-3">Customer</th>
                  <th className="py-2.5 px-3">Today&apos;s Tokens</th>
                  <th className="py-2.5 px-3">Live Status</th>
                  <th className="py-2.5 px-3">Expires</th>
                  <th className="py-2.5 px-3 text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-slate-800/70">
                {recentLicenses.length > 0 ? (
                  recentLicenses.map((lic) => {
                    const userMatch = liveUsers.find((u) => u.id === lic.id);
                    const tokens = userMatch?.tokensToday || 0;
                    const state = userMatch?.state || (lic.status === 'active' ? 'online' : 'offline');
                    const isRevealed = revealedKeys[lic.id];

                    return (
                      <tr key={lic.id} className="hover:bg-slate-800/35 transition">
                        <td className="py-3 px-3">
                          <div className="flex items-center gap-1.5">
                            <span className="font-mono font-bold text-indigo-300">
                              {isRevealed ? lic.license_key : maskLicenseKey(lic.license_key)}
                            </span>
                            <button
                              onClick={() => toggleRevealKey(lic.id)}
                              className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white"
                              title="Show/Hide Key"
                            >
                              {isRevealed ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
                            </button>
                            <button
                              onClick={() => copyKeyToClipboard(lic.license_key)}
                              className="p-1 rounded hover:bg-slate-800 text-slate-400 hover:text-white"
                              title="Copy Key"
                            >
                              {copiedKey === lic.license_key ? <Check className="w-3.5 h-3.5 text-emerald-400" /> : <Copy className="w-3.5 h-3.5" />}
                            </button>
                          </div>
                        </td>
                        <td className="py-3 px-3">
                          <div className="font-bold text-white">{lic.customer_name || 'Anonymous'}</div>
                          <div className="text-[11px] text-slate-400">{lic.customer_email || 'No email'}</div>
                        </td>
                        <td className="py-3 px-3 min-w-[130px]">
                          <div className="font-mono font-bold text-amber-300">{tokens.toLocaleString()} tok</div>
                          <div className="w-full h-1.5 rounded-full bg-slate-800 overflow-hidden mt-1">
                            <div
                              className={`h-full rounded-full ${state === 'translating' ? 'bg-emerald-400' : 'bg-amber-400'}`}
                              style={{ width: `${Math.min(100, (tokens / maxTokens) * 100)}%` }}
                            />
                          </div>
                        </td>
                        <td className="py-3 px-3">
                          {state === 'translating' ? (
                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-500/20 text-emerald-300 border border-emerald-400/45">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-ping"></span>
                              ⚡ Translate AI
                            </span>
                          ) : state === 'online' ? (
                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-bold bg-emerald-500/15 text-emerald-400 border border-emerald-500/35">
                              <span className="w-1.5 h-1.5 rounded-full bg-emerald-400"></span>
                              🟢 កំពុងប្រើ
                            </span>
                          ) : (
                            <span className="inline-flex items-center gap-1 px-2.5 py-0.5 rounded-full text-[11px] font-medium bg-slate-800 text-slate-400 border border-slate-700">
                              ⚫ មិនទាន់ប្រើ
                            </span>
                          )}
                        </td>
                        <td className="py-3 px-3 text-slate-300">
                          {lic.expires_at ? formatDate(lic.expires_at) : 'Lifetime'}
                        </td>
                        <td className="py-3 px-3 text-right">
                          <Link
                            href={`/admin/licenses/${lic.id}`}
                            className="px-2.5 py-1 rounded-lg text-xs font-semibold bg-indigo-500/15 text-indigo-300 border border-indigo-400/30 hover:bg-indigo-500/25 transition"
                          >
                            Details
                          </Link>
                        </td>
                      </tr>
                    );
                  })
                ) : (
                  <tr>
                    <td colSpan={6} className="py-8 text-center text-slate-400 font-khmer">
                      មិនមានទិន្នន័យ License ក្នុង Database ទេ។
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          </div>
        </div>

        {/* Live Client Activity Feed */}
        <div className="lg:col-span-4 glass-panel rounded-2xl p-4 sm:p-5">
          <div className="flex items-center justify-between pb-3.5 mb-3 border-b border-slate-800">
            <div className="flex items-center gap-2">
              <Activity className="w-4 h-4 text-emerald-400" />
              <h3 className="font-bold text-base text-white">Live Client Activity</h3>
            </div>
            <Link href="/admin/logs" className="text-xs font-semibold text-indigo-400 hover:text-indigo-300">
              Logs
            </Link>
          </div>

          <div className="space-y-2.5 max-h-[360px] overflow-y-auto pr-1">
            {recentActivations.length > 0 ? (
              recentActivations.map((act) => {
                const isHeartbeat = act.action === 'heartbeat';
                const isActivate = act.action === 'activate';
                return (
                  <div
                    key={act.id}
                    className="p-2.5 rounded-xl bg-slate-950/75 border border-slate-800/90 flex items-start justify-between gap-2.5"
                  >
                    <div className="flex items-start gap-2.5 min-w-0">
                      <div
                        className={`w-8 h-8 rounded-lg border flex items-center justify-center shrink-0 mt-0.5 ${
                          isHeartbeat
                            ? 'bg-emerald-500/20 text-emerald-300 border-emerald-400/40'
                            : isActivate
                            ? 'bg-sky-500/15 text-sky-300 border-sky-400/30'
                            : 'bg-rose-500/15 text-rose-300 border-rose-400/30'
                        }`}
                      >
                        {isHeartbeat ? <Zap className="w-4 h-4" /> : <Laptop2 className="w-4 h-4" />}
                      </div>
                      <div className="min-w-0">
                        <div className="flex items-center gap-1.5">
                          <span className="text-xs font-bold text-white capitalize">{act.action}</span>
                          <span className="text-[10px] font-semibold text-emerald-400">
                            • {act.licenses?.customer_name || 'Verified'}
                          </span>
                        </div>
                        <div className="text-[11px] text-slate-300 truncate font-mono">{act.device_id || 'PC Device'}</div>
                        <div className="text-[10px] text-slate-500">{formatDate(act.created_at)}</div>
                      </div>
                    </div>
                  </div>
                );
              })
            ) : (
              <div className="text-center py-8 text-xs text-slate-500">
                មិនទាន់មានសកម្មភាព Client Activity ក្នុង Database ទេ។
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
