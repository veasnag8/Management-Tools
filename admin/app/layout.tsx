import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'License Platform - Admin Console',
  description: 'Enterprise Windows Software Licensing Management Platform',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en">
      <body className="bg-slate-50 min-h-screen antialiased text-slate-900">{children}</body>
    </html>
  );
}
