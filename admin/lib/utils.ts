import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatDate(dateString: string | null | undefined): string {
  if (!dateString) return 'Never / Lifetime';
  const d = new Date(dateString);
  if (isNaN(d.getTime())) return 'Invalid date';
  return d.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatShortDate(dateString: string | null | undefined): string {
  if (!dateString) return 'Lifetime';
  const d = new Date(dateString);
  if (isNaN(d.getTime())) return 'Invalid date';
  return d.toLocaleDateString(undefined, {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

export function formatRelativeTime(dateString: string | null | undefined): string {
  if (!dateString) return 'Never';
  const d = new Date(dateString);
  if (isNaN(d.getTime())) return 'Invalid date';
  const now = new Date();
  const diffSeconds = Math.floor((now.getTime() - d.getTime()) / 1000);

  if (diffSeconds < 60) return 'Just now';
  const diffMinutes = Math.floor(diffSeconds / 60);
  if (diffMinutes < 60) return `${diffMinutes}m ago`;
  const diffHours = Math.floor(diffMinutes / 60);
  if (diffHours < 24) return `${diffHours}h ago`;
  const diffDays = Math.floor(diffHours / 24);
  if (diffDays < 30) return `${diffDays}d ago`;
  return formatDate(dateString);
}

export function maskLicenseKey(key: string): string {
  if (!key || key.length < 8) return '****';
  const parts = key.split('-');
  if (parts.length >= 4) {
    return `${parts[0]}-${parts[1]}****-${parts[parts.length - 1]}`;
  }
  return `${key.slice(0, 4)}****${key.slice(-4)}`;
}

export function getDaysRemaining(expiresAt: string | null): number | null {
  if (!expiresAt) return null;
  const exp = new Date(expiresAt).getTime();
  const now = Date.now();
  const diff = exp - now;
  return Math.ceil(diff / (1000 * 60 * 60 * 24));
}

export function exportToCsv(filename: string, rows: Record<string, any>[]) {
  if (!rows || !rows.length) return;
  const headers = Object.keys(rows[0]);
  const csvContent = [
    headers.join(','),
    ...rows.map(row =>
      headers
        .map(h => {
          let val = row[h] === null || row[h] === undefined ? '' : String(row[h]);
          val = val.replace(/"/g, '""');
          return `"${val}"`;
        })
        .join(',')
    )
  ].join('\r\n');

  const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.setAttribute('href', url);
  link.setAttribute('download', `${filename}.csv`);
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
}
