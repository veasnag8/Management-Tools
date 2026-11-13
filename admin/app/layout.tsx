import type { Metadata } from 'next';
import './globals.css';

export const metadata: Metadata = {
  title: 'SNA GOD • License Management Console',
  description: 'Dubber Tool - ធូលបកប្រែរឿង • SNA GOD AI Token & License Console',
};

export default function RootLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <html lang="en" className="dark">
      <head>
        <link rel="preconnect" href="https://fonts.googleapis.com" />
        <link rel="preconnect" href="https://fonts.gstatic.com" crossOrigin="anonymous" />
        <link
          href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600;700&family=Kantumruy+Pro:wght@400;500;600;700&display=swap"
          rel="stylesheet"
        />
      </head>
      <body className="bg-[#060A16] text-[#f1f5f9] min-h-screen antialiased selection:bg-emerald-500/30 selection:text-emerald-200">
        <div className="cosmic-nebula"></div>
        {children}
      </body>
    </html>
  );
}
