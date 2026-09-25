import React from 'react';
import { cn } from '@/lib/utils';
import { LicenseStatus, DeviceStatus, UserRole } from '@/types';

export interface BadgeProps {
  status: LicenseStatus | DeviceStatus | UserRole | string;
  className?: string;
}

export function StatusBadge({ status, className }: BadgeProps) {
  const s = status ? status.toLowerCase() : '';

  let style = 'bg-slate-800 text-slate-400 border-slate-700';

  if (s === 'active') style = 'bg-emerald-500/10 text-emerald-400 border-emerald-500/30';
  else if (s === 'expired') style = 'bg-amber-500/10 text-amber-400 border-amber-500/30';
  else if (s === 'disabled') style = 'bg-rose-500/10 text-rose-400 border-rose-500/30';
  else if (s === 'revoked') style = 'bg-purple-500/10 text-purple-400 border-purple-500/30';
  else if (s === 'pending') style = 'bg-blue-500/10 text-blue-400 border-blue-500/30';
  else if (s === 'admin') style = 'bg-indigo-500/10 text-indigo-400 border-indigo-500/30';
  else if (s === 'manager') style = 'bg-sky-500/10 text-sky-400 border-sky-500/30';
  else if (s === 'support') style = 'bg-teal-500/10 text-teal-400 border-teal-500/30';

  return (
    <span
      className={cn(
        'inline-flex items-center px-2.5 py-0.5 rounded-full text-xs font-semibold uppercase tracking-wider border font-sans',
        style,
        className
      )}
    >
      <span
        className={cn('w-1.5 h-1.5 rounded-full mr-1.5', {
          'bg-emerald-400': s === 'active',
          'bg-amber-400': s === 'expired',
          'bg-rose-400': s === 'disabled',
          'bg-purple-400': s === 'revoked',
          'bg-blue-400': s === 'pending',
          'bg-indigo-400': s === 'admin',
          'bg-sky-400': s === 'manager',
          'bg-teal-400': s === 'support',
        })}
      />
      {status}
    </span>
  );
}

export const Badge = StatusBadge;
export default StatusBadge;
