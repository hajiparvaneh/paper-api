import Link from "next/link";
import type { Metadata } from "next";
import { Manrope, Space_Grotesk } from "next/font/google";

import "./globals.css";

const manrope = Manrope({
  subsets: ["latin"],
  variable: "--font-body",
  display: "swap",
});

const spaceGrotesk = Space_Grotesk({
  subsets: ["latin"],
  variable: "--font-display",
  display: "swap",
});

export const metadata: Metadata = {
  title: {
    default: "PaperAPI Self-Hosted Dashboard",
    template: "%s | PaperAPI",
  },
  description:
    "Self-hosted control plane for PaperAPI: manage keys, watch usage, and monitor PDF jobs on your infrastructure.",
  icons: {
    icon: "/favicon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body
        className={`${manrope.variable} ${spaceGrotesk.variable} antialiased`}
      >
        <div className="relative min-h-screen overflow-hidden">
          <div className="pointer-events-none absolute left-[-10%] top-[-20%] h-[520px] w-[520px] rounded-full bg-cyan-300/15 blur-[160px]" />
          <div className="pointer-events-none absolute right-[-10%] top-[5%] h-[460px] w-[460px] rounded-full bg-teal-400/20 blur-[160px]" />
          <div className="pointer-events-none absolute bottom-[-20%] left-[20%] h-[420px] w-[420px] rounded-full bg-sky-500/10 blur-[150px]" />
          <div className="relative z-10 flex min-h-screen flex-col">
            <div className="flex-1">{children}</div>
            <footer className="border-t border-white/10 bg-slate-950/70">
              <div className="mx-auto flex max-w-6xl flex-wrap items-center justify-between gap-4 px-6 py-6 text-xs text-slate-400">
                <span className="uppercase tracking-[0.3em] text-slate-300">
                  PaperAPI self-hosted
                </span>
                <div className="flex flex-wrap items-center gap-3">
                  <Link
                    href="/"
                    className="rounded-full border border-white/10 bg-white/5 px-4 py-2 uppercase tracking-[0.2em] text-slate-200 transition hover:border-cyan-200/60"
                  >
                    Dashboard
                  </Link>
                  <Link
                    href="/sdk"
                    className="rounded-full border border-white/10 bg-white/5 px-4 py-2 uppercase tracking-[0.2em] text-slate-200 transition hover:border-cyan-200/60"
                  >
                    SDKs
                  </Link>
                </div>
              </div>
            </footer>
          </div>
        </div>
      </body>
    </html>
  );
}
