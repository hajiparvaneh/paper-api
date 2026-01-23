import Link from "next/link";

import { CopyButton } from "@/components/CopyButton";

type Sdk = {
  id: string;
  name: string;
  ecosystem: string;
  status: "available" | "soon";
  description: string;
  command?: string;
  link?: string;
  linkText?: string;
  iconLabel: string;
  iconAccent: string;
};

const pdfApiBaseUrl =
  process.env.NEXT_PUBLIC_PDF_API_BASE_URL || "http://localhost:8087";

const sdks: Sdk[] = [
  {
    id: "nuget",
    name: "NuGet SDK",
    ecosystem: ".NET / C#",
    status: "available",
    description:
      "Official typed client with request/response helpers, retries, and tracing hooks for ASP.NET and background workers.",
    command: "dotnet add package PaperApi",
    link: "https://www.nuget.org/packages/PaperApi/",
    linkText: "NuGet",
    iconLabel: "Nu",
    iconAccent: "border-emerald-400/30 bg-emerald-500/10 text-emerald-200",
  },
  {
    id: "npm",
    name: "npm SDK",
    ecosystem: "Node.js / TypeScript",
    status: "available",
    description:
      "Fetch-first TypeScript client with runtime validation, retries, and typed responses.",
    command: "npm install @paperapi/sdk",
    link: "https://www.npmjs.com/package/@paperapi/sdk",
    linkText: "npm",
    iconLabel: "JS",
    iconAccent: "border-sky-400/30 bg-sky-500/10 text-sky-200",
  },
  {
    id: "python",
    name: "pip SDK",
    ecosystem: "Python",
    status: "soon",
    description:
      "Async-friendly client for FastAPI, Django, and serverless workloads.",
    iconLabel: "Py",
    iconAccent: "border-amber-400/30 bg-amber-400/10 text-amber-200",
  },
  {
    id: "php",
    name: "Composer SDK",
    ecosystem: "PHP / Laravel",
    status: "soon",
    description:
      "PSR-compliant wrapper with request signing and queue helpers.",
    iconLabel: "Ph",
    iconAccent: "border-purple-400/30 bg-purple-500/10 text-purple-200",
  },
];

export default function SdkPage() {
  return (
    <section className="mx-auto max-w-6xl space-y-12 px-6 py-16 text-white">
      <div className="space-y-6">
        <p className="text-sm uppercase tracking-[0.4em] text-emerald-300/80">
          SDKs
        </p>
        <h1 className="text-4xl font-semibold">
          Installed in seconds. Clean in production.
        </h1>
        <p className="max-w-3xl text-lg text-white/70">
          Skip wiring raw HTTP calls and jump straight to typed methods,
          resilient retries, and observability. Each PaperAPI SDK keeps your PDF
          code small, predictable, and security reviewed.
        </p>
        <div className="flex flex-wrap items-center gap-4 rounded-3xl border border-white/10 bg-white/5 px-6 py-4 text-sm text-white/70">
          <div className="flex flex-wrap items-center gap-3">
            <span>Self-hosted base URL:</span>
            <span className="rounded-full border border-white/10 bg-slate-950/70 px-3 py-1 font-mono text-xs text-white/90">
              {pdfApiBaseUrl}
            </span>
          </div>
          <span className="rounded-full border border-emerald-300/30 bg-emerald-400/10 px-3 py-1 text-xs font-semibold uppercase tracking-wide text-emerald-100">
            Self-Hosted Only
          </span>
        </div>
      </div>

      <div className="grid gap-6 md:grid-cols-2">
        {sdks.map((sdk) => {
          const isAvailable = sdk.status === "available";
          const badgeClasses = isAvailable
            ? "border-emerald-400/30 bg-emerald-500/10 text-emerald-100"
            : "border-white/15 bg-white/5 text-white/60";

          return (
            <div
              key={sdk.id}
              className="flex h-full flex-col justify-between rounded-3xl border border-white/10 bg-white/5 p-6"
            >
              <div className="space-y-4">
                <div className="flex items-center gap-4">
                  <div
                    className={`flex h-14 w-14 items-center justify-center rounded-2xl border text-base font-semibold ${sdk.iconAccent}`}
                  >
                    {sdk.iconLabel}
                  </div>
                  <div>
                    <p className="text-xs uppercase tracking-[0.3em] text-white/50">
                      {sdk.ecosystem}
                    </p>
                    <h2 className="text-xl font-semibold">{sdk.name}</h2>
                  </div>
                  <span
                    className={`ml-auto rounded-full border px-3 py-1 text-xs font-semibold uppercase tracking-wide ${badgeClasses}`}
                  >
                    {isAvailable ? "Available" : "Coming soon"}
                  </span>
                </div>
                <p className="text-sm text-white/70">{sdk.description}</p>
              </div>

              {isAvailable ? (
                <div className="mt-6 space-y-3">
                  <p className="text-xs uppercase tracking-[0.3em] text-white/50">
                    Install
                  </p>
                  {sdk.command ? (
                    <div className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/10 bg-slate-950/70 px-4 py-3 text-sm text-white/90">
                      <span className="font-mono">{sdk.command}</span>
                      <CopyButton text={sdk.command} />
                    </div>
                  ) : null}
                  {sdk.link ? (
                    <Link
                      href={sdk.link}
                      target="_blank"
                      rel="noreferrer"
                      className="inline-flex items-center gap-2 text-sm text-emerald-300 transition hover:text-emerald-200"
                    >
                      View on {sdk.linkText || "GitHub"}
                      <svg
                        xmlns="http://www.w3.org/2000/svg"
                        viewBox="0 0 24 24"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.5"
                        className="h-4 w-4"
                      >
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M17 7 7 17"
                        />
                        <path
                          strokeLinecap="round"
                          strokeLinejoin="round"
                          d="M8 7h9v9"
                        />
                      </svg>
                    </Link>
                  ) : null}
                </div>
              ) : (
                <div className="mt-6 rounded-2xl border border-dashed border-white/15 bg-white/5 px-4 py-3 text-sm text-white/60">
                  Packaging is finalizing. Drop us a line at{" "}
                  <a
                    className="text-white/80 hover:text-white"
                    href="mailto:founders@paperapi.de"
                  >
                    founders@paperapi.de
                  </a>{" "}
                  if you want early access.
                </div>
              )}
            </div>
          );
        })}
      </div>

      <div className="rounded-3xl border border-white/10 bg-gradient-to-r from-emerald-500/10 via-emerald-400/5 to-transparent p-6 text-white">
        <h2 className="text-2xl font-semibold">
          Need another runtime supported?
        </h2>
        <p className="mt-3 text-white/70">
          Tell us about your stack and we will prioritize the next SDK drop. We
          are actively working on Python, PHP, and Go ports with the same clean
          abstractions.
        </p>
        <div className="mt-5 flex flex-wrap gap-3 text-sm">
          <a
            href="mailto:founders@paperapi.de"
            className="rounded-full bg-white px-5 py-2 font-medium text-slate-900 transition hover:bg-slate-100"
          >
            Email the team
          </a>
          <Link
            href="https://github.com/hajiparvaneh/PaperAPI"
            target="_blank"
            rel="noreferrer"
            className="rounded-full border border-white/40 px-5 py-2 text-white/80 transition hover:border-white hover:text-white"
          >
            Follow progress on GitHub
          </Link>
        </div>
      </div>
    </section>
  );
}
