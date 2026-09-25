import React from 'react';
import { cn } from '@/lib/utils';
import { LicenseStatus, DeviceStatus, UserRole } from '@/types';

interface BadgeProps {
  status: LicenseStatus | DeviceStatus | UserRole | string;
  className?: string;
}

export function StatusBadge({ status, className }: BadgeProps) {
  const s = status ? status.toLowerCase() : '';

  let style = 'bg-slate-100 text-slate-700 border-slate-200';

  if (s === 'active') style = 'bg-emerald-50 text-emerald-700 border-emerald-200';
  else if (s === 'expired') style = 'bg-amber-50 text-amber-700 border-amber-200';
  else if (s === 'disabled') style = 'bg-rose-50 text-rose-700 border-rose-200';
  else if (s === 'revoked') style = 'bg-purple-50 text-purple-700 border-purple-200';
  else if (s === 'pending') style = 'bg-blue-50 text-blue-700 border-blue-200';
  else if (s === 'admin') style = 'bg-indigo-50 text-indigo-700 border-indigo-200';
  else if (s === 'manager') style = 'bg-sky-50 text-sky-700 border-sky-200';
  else if (s === 'support') style = 'bg-teal-50 text-teal-700 border-teal-200';

  return (
    <span className={cn('inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold uppercase tracking-wider border', style, className)}>
      <span className={cn('w-1.5 h-1.5 rounded-full mr-1.5', {
        'bg-emerald-500': s === 'active',
        'bg-amber-500': s === 'expired',
        'bg-rose-500': s === 'disabled',
        'bg-purple-500': s === 'revoked',
        'bg-blue-500': s === 'pending',
        'bg-indigo-500': s === 'admin',
        'bg-sky-500': s === 'manager',
        'bg-teal-500': s === 'support',
      })} />
      {status}
    </span>
  );
}

export const Badge = StatusBadge;
export default StatusBadge;
