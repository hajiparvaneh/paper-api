"use client";

import { useEffect, useMemo, useRef, useState } from "react";
const API_KEYS_STORAGE_KEY = "paperapi.selfhosted.apiKeys";
const KEY_VAULT_STORAGE_KEY = "paperapi.selfhosted.keyVault";
const LOGS_STORAGE_KEY = "paperapi.selfhosted.logs";

interface ApiKeyRecord {
  id: string;
  name: string;
  value: string | null;
  prefix: string;
  createdAt: string;
  lastUsed: string | null;
  status: "active" | "revoked";
}

interface ApiKeyApiResponse {
  id: string;
  name: string;
  prefix: string;
  isActive: boolean;
  createdAt: string;
  lastUsedAt: string | null;
}

interface CreateKeyResponse {
  key: ApiKeyApiResponse;
  plaintextKey: string;
}

type KeyVault = Record<string, string>;

interface SelfHostedStatusResponse {
  isConfigured: boolean;
  username?: string | null;
}

interface SelfHostedMeResponse {
  username: string;
}

interface LogRecord {
  id: string;
  timestamp: string;
  event: string;
  detail: string;
  status: "ok" | "warn" | "error";
}

interface TestResultMeta {
  status: number;
  statusText: string;
  size: number;
  contentType: string | null;
  receivedAt: string;
}

function readStorage<T>(key: string, fallback: T): T {
  if (typeof window === "undefined") {
    return fallback;
  }
  const raw = window.localStorage.getItem(key);
  if (!raw) {
    return fallback;
  }
  try {
    return JSON.parse(raw) as T;
  } catch {
    return fallback;
  }
}

function writeStorage<T>(key: string, value: T) {
  if (typeof window === "undefined") {
    return;
  }
  window.localStorage.setItem(key, JSON.stringify(value));
}

function readKeyVault(): KeyVault {
  return readStorage<KeyVault>(KEY_VAULT_STORAGE_KEY, {});
}

function writeKeyVault(vault: KeyVault) {
  writeStorage(KEY_VAULT_STORAGE_KEY, vault);
}

async function readErrorMessage(response: Response) {
  const text = await response.text();
  if (!text) {
    return `Request failed (${response.status}).`;
  }
  try {
    const data = JSON.parse(text) as {
      message?: string;
      error?: string;
      details?: string;
    };
    if (data.message) {
      return data.message;
    }
    if (data.details) {
      return data.details;
    }
    if (data.error) {
      return data.error;
    }
  } catch {
    // Fall back to raw text.
  }
  return text;
}

function formatDate(value: string) {
  return new Date(value).toLocaleString();
}

function maskKey(value: string | null, prefix: string) {
  const source = value || prefix;
  if (!source) {
    return "missing";
  }
  if (source.length <= 12) {
    return source;
  }
  return `${source.slice(0, 6)}...${source.slice(-4)}`;
}

function formatBytes(value: number) {
  if (!Number.isFinite(value)) {
    return "0 B";
  }
  if (value < 1024) {
    return `${value} B`;
  }
  const kb = value / 1024;
  if (kb < 1024) {
    return `${kb.toFixed(1)} KB`;
  }
  const mb = kb / 1024;
  return `${mb.toFixed(1)} MB`;
}

const defaultTestHtml = `<html>
  <head>
    <style>
      body { font-family: Arial, sans-serif; padding: 32px; }
      h1 { color: #0f172a; }
      p { color: #334155; }
      .badge { display: inline-block; padding: 6px 12px; border-radius: 999px; background: #cffafe; }
    </style>
  </head>
  <body>
    <h1>Hello from PaperAPI</h1>
    <p>This PDF was rendered by your self-hosted instance.</p>
    <span class="badge">Self-hosted</span>
  </body>
</html>`;

const defaultLogs: LogRecord[] = [
  {
    id: "log-1",
    timestamp: new Date().toISOString(),
    event: "PDF render",
    detail: "Rendered invoice batch (12 pages)",
    status: "ok",
  },
  {
    id: "log-2",
    timestamp: new Date(Date.now() - 1000 * 60 * 45).toISOString(),
    event: "Queue health",
    detail: "Workers warmed and ready",
    status: "ok",
  },
  {
    id: "log-3",
    timestamp: new Date(Date.now() - 1000 * 60 * 90).toISOString(),
    event: "API key audit",
    detail: "Rotated key for staging",
    status: "warn",
  },
];

export default function HomePage() {
  const [isReady, setIsReady] = useState(false);
  const [isConfigured, setIsConfigured] = useState(false);
  const [adminUsername, setAdminUsername] = useState<string | null>(null);
  const [sessionActive, setSessionActive] = useState(false);
  const [authBusy, setAuthBusy] = useState(false);
  const [setupError, setSetupError] = useState<string | null>(null);
  const [loginError, setLoginError] = useState<string | null>(null);
  const [setupForm, setSetupForm] = useState({
    username: "",
    password: "",
    confirm: "",
  });
  const [loginForm, setLoginForm] = useState({ username: "", password: "" });
  const [apiKeys, setApiKeys] = useState<ApiKeyRecord[]>([]);
  const [keyVault, setKeyVault] = useState<KeyVault>({});
  const [keyError, setKeyError] = useState<string | null>(null);
  const [keyBusy, setKeyBusy] = useState(false);
  const [logs, setLogs] = useState<LogRecord[]>([]);
  const [newKeyName, setNewKeyName] = useState("");
  const [copiedKey, setCopiedKey] = useState<string | null>(null);
  const [selectedKeyId, setSelectedKeyId] = useState<string | null>(null);
  const [testHtml, setTestHtml] = useState(defaultTestHtml);
  const [testOptions, setTestOptions] = useState(`{\n  "pageSize": "A4"\n}`);
  const [testBusy, setTestBusy] = useState(false);
  const [testError, setTestError] = useState<string | null>(null);
  const [testResultUrl, setTestResultUrl] = useState<string | null>(null);
  const [testResultMeta, setTestResultMeta] = useState<TestResultMeta | null>(
    null,
  );
  const testPanelRef = useRef<HTMLDivElement | null>(null);

  const pdfApiBaseUrl =
    process.env.NEXT_PUBLIC_PDF_API_BASE_URL || "http://localhost:8087";

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      setIsReady(false);
      const cachedVault = readKeyVault();
      setKeyVault(cachedVault);

      const cachedKeys = readStorage<ApiKeyRecord[]>(API_KEYS_STORAGE_KEY, [])
        .filter((key) => key && key.id && key.name)
        .map((key) => ({
          ...key,
          value: key.value ?? cachedVault[key.id] ?? null,
          prefix: key.prefix ?? (key.value ? key.value.slice(0, 12) : ""),
        }));
      if (cachedKeys.length > 0) {
        setApiKeys(cachedKeys);
      }

      try {
        const statusResponse = await fetch(
          `${pdfApiBaseUrl}/self-hosted/status`,
        );
        if (statusResponse.ok) {
          const status =
            (await statusResponse.json()) as SelfHostedStatusResponse;
          if (!cancelled) {
            setIsConfigured(status.isConfigured);
            setAdminUsername(status.username ?? null);
          }
        }
      } catch {
        if (!cancelled) {
          setIsConfigured(false);
        }
      }

      try {
        const meResponse = await fetch(`${pdfApiBaseUrl}/self-hosted/me`, {
          credentials: "include",
        });
        if (meResponse.ok) {
          const me = (await meResponse.json()) as SelfHostedMeResponse;
          if (!cancelled) {
            setSessionActive(true);
            setAdminUsername(me.username);
          }
        } else if (!cancelled) {
          setSessionActive(false);
        }
      } catch {
        if (!cancelled) {
          setSessionActive(false);
        }
      }

      const storedLogs = readStorage<LogRecord[]>(LOGS_STORAGE_KEY, []);
      if (storedLogs.length === 0) {
        writeStorage(LOGS_STORAGE_KEY, defaultLogs);
        setLogs(defaultLogs);
      } else {
        setLogs(storedLogs);
      }

      if (!cancelled) {
        setIsReady(true);
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [pdfApiBaseUrl]);

  useEffect(() => {
    writeStorage(API_KEYS_STORAGE_KEY, apiKeys);
  }, [apiKeys]);

  useEffect(() => {
    writeKeyVault(keyVault);
  }, [keyVault]);

  useEffect(() => {
    if (!sessionActive) {
      setApiKeys([]);
      setSelectedKeyId(null);
    }
  }, [sessionActive]);

  useEffect(() => {
    if (!sessionActive) {
      return;
    }
    let cancelled = false;

    const loadKeys = async () => {
      setKeyError(null);
      try {
        const response = await fetch(`${pdfApiBaseUrl}/self-hosted/api-keys`, {
          method: "GET",
          credentials: "include",
        });
        if (!response.ok) {
          const message = await readErrorMessage(response);
          throw new Error(message);
        }
        const data = (await response.json()) as ApiKeyApiResponse[];
        const hydrated: ApiKeyRecord[] = data.map((key) => {
          const status: ApiKeyRecord["status"] = key.isActive
            ? "active"
            : "revoked";
          return {
            id: key.id,
            name: key.name,
            prefix: key.prefix,
            value: keyVault[key.id] ?? null,
            createdAt: key.createdAt,
            lastUsed: key.lastUsedAt,
            status,
          };
        });
        if (!cancelled) {
          setApiKeys(hydrated);
        }
      } catch (error) {
        if (!cancelled) {
          setKeyError(
            error instanceof Error
              ? error.message
              : "Unable to load API keys from the server.",
          );
        }
      }
    };

    void loadKeys();

    return () => {
      cancelled = true;
    };
  }, [pdfApiBaseUrl, sessionActive, keyVault]);

  useEffect(() => {
    writeStorage(LOGS_STORAGE_KEY, logs);
  }, [logs]);

  useEffect(() => {
    if (
      apiKeys.length > 0 &&
      !apiKeys.some((key) => key.id === selectedKeyId)
    ) {
      setSelectedKeyId(apiKeys[0].id);
    }
  }, [apiKeys, selectedKeyId]);

  useEffect(() => {
    return () => {
      if (testResultUrl) {
        URL.revokeObjectURL(testResultUrl);
      }
    };
  }, [testResultUrl]);

  const selectedKey = selectedKeyId
    ? apiKeys.find((key) => key.id === selectedKeyId)
    : undefined;
  const curlSnippet = selectedKey?.value
    ? `curl -X POST ${pdfApiBaseUrl}/v1/generate \\
  -H "Authorization: Bearer ${selectedKey.value}" \\
  -H "Content-Type: application/json" \\
  -d '{"html":"<html>...</html>"}' \\
  --output document.pdf`
    : "Create an API key (and keep the plaintext value) to generate a test request.";

  const usageStats = useMemo(() => {
    const renders = logs.filter((log) => log.event === "PDF render").length;
    return {
      renders,
      alerts: logs.filter((log) => log.status !== "ok").length,
      keys: apiKeys.length,
    };
  }, [logs, apiKeys]);

  if (!isReady) {
    return (
      <div className="mx-auto flex min-h-screen max-w-5xl items-center justify-center px-6 text-slate-200">
        <div className="rounded-3xl border border-white/10 bg-slate-950/40 px-10 py-12">
          Loading self-hosted console...
        </div>
      </div>
    );
  }

  if (!isConfigured) {
    return (
      <main className="mx-auto max-w-6xl px-6 py-16">
        <Header />
        <div className="mt-10 grid gap-10 lg:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-6">
            <p className="text-xs uppercase tracking-[0.4em] text-cyan-200/70">
              First run setup
            </p>
            <h1 className="text-4xl font-semibold text-white">
              Create the single admin account for your self-hosted dashboard.
            </h1>
            <p className="text-slate-300">
              Credentials are stored in the database for this instance. Once it
              is created, the setup page is disabled and you will be logged in
              automatically.
            </p>
            <div className="rounded-3xl border border-white/10 bg-slate-900/40 p-6">
              <p className="text-sm text-slate-300">
                To reset the admin account, remove the database volume and
                restart the stack.
              </p>
            </div>
          </div>
          <form
            onSubmit={async (event) => {
              event.preventDefault();
              setSetupError(null);
              if (authBusy) {
                return;
              }
              if (!setupForm.username || !setupForm.password) {
                setSetupError("Username and password are required.");
                return;
              }
              if (setupForm.password.length < 8) {
                setSetupError("Use at least 8 characters.");
                return;
              }
              if (setupForm.password !== setupForm.confirm) {
                setSetupError("Passwords do not match.");
                return;
              }
              setAuthBusy(true);
              try {
                const response = await fetch(
                  `${pdfApiBaseUrl}/self-hosted/setup`,
                  {
                    method: "POST",
                    headers: {
                      "Content-Type": "application/json",
                    },
                    credentials: "include",
                    body: JSON.stringify({
                      username: setupForm.username.trim(),
                      password: setupForm.password,
                    }),
                  },
                );

                if (!response.ok) {
                  const message = await readErrorMessage(response);
                  throw new Error(message);
                }

                const data = (await response.json()) as SelfHostedMeResponse;
                setIsConfigured(true);
                setSessionActive(true);
                setAdminUsername(data.username);
                setSetupForm({ username: "", password: "", confirm: "" });
              } catch (error) {
                setSetupError(
                  error instanceof Error
                    ? error.message
                    : "Unable to create the admin account.",
                );
              } finally {
                setAuthBusy(false);
              }
            }}
            className="space-y-6 rounded-3xl border border-white/10 bg-slate-950/70 p-8 shadow-lg"
          >
            <div>
              <h2 className="text-xl font-semibold text-white">
                Admin credentials
              </h2>
              <p className="text-sm text-slate-400">
                These details are stored in your self-hosted database.
              </p>
            </div>
            <Field
              label="Username"
              value={setupForm.username}
              onChange={(value) =>
                setSetupForm((prev) => ({ ...prev, username: value }))
              }
              placeholder="admin"
            />
            <Field
              label="Password"
              type="password"
              value={setupForm.password}
              onChange={(value) =>
                setSetupForm((prev) => ({ ...prev, password: value }))
              }
              placeholder="Create a strong password"
            />
            <Field
              label="Confirm password"
              type="password"
              value={setupForm.confirm}
              onChange={(value) =>
                setSetupForm((prev) => ({ ...prev, confirm: value }))
              }
              placeholder="Repeat password"
            />
            {setupError ? (
              <div className="rounded-2xl border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
                {setupError}
              </div>
            ) : null}
            <button
              type="submit"
              disabled={authBusy}
              className="w-full rounded-2xl bg-cyan-300/90 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {authBusy ? "Saving..." : "Save admin credentials"}
            </button>
          </form>
        </div>
      </main>
    );
  }

  if (!sessionActive) {
    return (
      <main className="mx-auto max-w-6xl px-6 py-16">
        <Header />
        <div className="mt-10 grid gap-10 lg:grid-cols-[1.1fr_0.9fr]">
          <div className="space-y-6">
            <p className="text-xs uppercase tracking-[0.4em] text-cyan-200/70">
              Welcome back
            </p>
            <h1 className="text-4xl font-semibold text-white">
              Log in to manage your self-hosted PaperAPI instance.
            </h1>
            <p className="text-slate-300">
              This console never leaves your network. It keeps API keys, usage
              snapshots, and rendering activity in sync with your self-hosted
              instance.
            </p>
            <div className="rounded-3xl border border-white/10 bg-slate-900/40 p-6">
              <p className="text-sm text-slate-300">
                Admin account: {adminUsername ?? "configured"}
              </p>
            </div>
          </div>
          <form
            onSubmit={async (event) => {
              event.preventDefault();
              setLoginError(null);
              if (authBusy) {
                return;
              }
              if (!loginForm.username || !loginForm.password) {
                setLoginError("Enter the admin username and password.");
                return;
              }
              setAuthBusy(true);
              try {
                const response = await fetch(
                  `${pdfApiBaseUrl}/self-hosted/login`,
                  {
                    method: "POST",
                    headers: {
                      "Content-Type": "application/json",
                    },
                    credentials: "include",
                    body: JSON.stringify({
                      username: loginForm.username.trim(),
                      password: loginForm.password,
                    }),
                  },
                );

                if (!response.ok) {
                  const message = await readErrorMessage(response);
                  throw new Error(message);
                }

                const data = (await response.json()) as SelfHostedMeResponse;
                setIsConfigured(true);
                setSessionActive(true);
                setAdminUsername(data.username);
                setLoginForm({ username: "", password: "" });
              } catch (error) {
                setLoginError(
                  error instanceof Error
                    ? error.message
                    : "Unable to log in with those credentials.",
                );
              } finally {
                setAuthBusy(false);
              }
            }}
            className="space-y-6 rounded-3xl border border-white/10 bg-slate-950/70 p-8 shadow-lg"
          >
            <div>
              <h2 className="text-xl font-semibold text-white">Admin login</h2>
              <p className="text-sm text-slate-400">
                No additional users are supported yet.
              </p>
            </div>
            <Field
              label="Username"
              value={loginForm.username}
              onChange={(value) =>
                setLoginForm((prev) => ({ ...prev, username: value }))
              }
              placeholder="admin"
            />
            <Field
              label="Password"
              type="password"
              value={loginForm.password}
              onChange={(value) =>
                setLoginForm((prev) => ({ ...prev, password: value }))
              }
              placeholder="Your password"
            />
            {loginError ? (
              <div className="rounded-2xl border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
                {loginError}
              </div>
            ) : null}
            <button
              type="submit"
              disabled={authBusy}
              className="w-full rounded-2xl bg-cyan-300/90 px-5 py-3 text-sm font-semibold text-slate-950 transition hover:bg-cyan-200 disabled:cursor-not-allowed disabled:opacity-60"
            >
              {authBusy ? "Signing in..." : "Log in"}
            </button>
          </form>
        </div>
      </main>
    );
  }

  return (
    <main className="mx-auto max-w-6xl px-6 pb-16 pt-12">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-xs uppercase tracking-[0.4em] text-cyan-200/70">
            Self-hosted dashboard
          </p>
          <h1 className="text-3xl font-semibold text-white">
            PaperAPI Control Room
          </h1>
          <p className="text-sm text-slate-400">
            Your self-hosted instance. No plans, no billing, just your
            infrastructure.
          </p>
        </div>
        <button
          type="button"
          onClick={async () => {
            try {
              await fetch(`${pdfApiBaseUrl}/self-hosted/logout`, {
                method: "POST",
                credentials: "include",
              });
            } finally {
              setSessionActive(false);
            }
          }}
          className="rounded-full border border-white/10 bg-white/5 px-4 py-2 text-xs uppercase tracking-[0.2em] text-slate-200 transition hover:border-cyan-200/60"
        >
          Log out
        </button>
      </div>

      <section className="mt-10 grid gap-6 md:grid-cols-3">
        <StatCard
          title="PDF renders"
          value={usageStats.renders.toString()}
          helper="Last 24h activity"
        />
        <StatCard
          title="Active API keys"
          value={usageStats.keys.toString()}
          helper="Manage below"
        />
        <StatCard
          title="Alerts"
          value={usageStats.alerts.toString()}
          helper="Warnings or failures"
        />
      </section>

      <section className="mt-10 grid gap-6 lg:grid-cols-[1.35fr_0.65fr]">
        <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <h2 className="text-xl font-semibold text-white">API keys</h2>
              <p className="text-sm text-slate-400">
                Keys used by your PDF API and workers.
              </p>
            </div>
            <form
              onSubmit={async (event) => {
                event.preventDefault();
                if (!newKeyName.trim() || keyBusy) {
                  return;
                }
                setKeyError(null);
                setKeyBusy(true);
                try {
                  const response = await fetch(
                    `${pdfApiBaseUrl}/self-hosted/api-keys`,
                    {
                      method: "POST",
                      headers: {
                        "Content-Type": "application/json",
                      },
                      credentials: "include",
                      body: JSON.stringify({
                        name: newKeyName.trim(),
                      }),
                    },
                  );

                  if (!response.ok) {
                    const message = await readErrorMessage(response);
                    throw new Error(message);
                  }

                  const data = (await response.json()) as CreateKeyResponse;
                  const status: ApiKeyRecord["status"] = data.key.isActive
                    ? "active"
                    : "revoked";
                  const record: ApiKeyRecord = {
                    id: data.key.id,
                    name: data.key.name,
                    prefix: data.key.prefix,
                    value: data.plaintextKey,
                    createdAt: data.key.createdAt,
                    lastUsed: data.key.lastUsedAt,
                    status,
                  };

                  setKeyVault((prev) => ({
                    ...prev,
                    [record.id]: data.plaintextKey,
                  }));
                  setApiKeys((prev) => [record, ...prev]);
                  setNewKeyName("");
                } catch (error) {
                  setKeyError(
                    error instanceof Error
                      ? error.message
                      : "Unable to create an API key.",
                  );
                } finally {
                  setKeyBusy(false);
                }
              }}
              className="flex flex-wrap items-center gap-3"
            >
              <input
                className="min-w-[180px] flex-1 rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-2 text-sm text-white placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-200/40"
                placeholder="Key name"
                value={newKeyName}
                onChange={(event) => setNewKeyName(event.target.value)}
              />
              <button
                type="submit"
                disabled={keyBusy}
                className="rounded-2xl bg-cyan-300/90 px-4 py-2 text-xs font-semibold text-slate-950 transition hover:bg-cyan-200 disabled:cursor-not-allowed disabled:opacity-60"
              >
                {keyBusy ? "Creating..." : "Create key"}
              </button>
            </form>
            {keyError ? (
              <div className="mt-4 rounded-2xl border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
                {keyError}
              </div>
            ) : null}
          </div>
          <div className="mt-6 space-y-4">
            {apiKeys.length === 0 ? (
              <div className="rounded-2xl border border-dashed border-white/10 bg-slate-900/40 px-4 py-6 text-sm text-slate-400">
                No keys yet. Create one to authenticate PDF requests.
              </div>
            ) : (
              apiKeys.map((key) => (
                <div
                  key={key.id}
                  className="flex flex-wrap items-center justify-between gap-4 rounded-2xl border border-white/10 bg-slate-900/50 px-4 py-3"
                >
                  <div>
                    <p className="text-sm font-semibold text-white">
                      {key.name}
                    </p>
                    <p className="text-xs text-slate-400">
                      Created {formatDate(key.createdAt)}
                    </p>
                  </div>
                  <div className="flex flex-wrap items-center gap-3">
                    <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs text-slate-200">
                      {maskKey(key.value, key.prefix)}
                    </span>
                    <button
                      type="button"
                      onClick={() => {
                        setSelectedKeyId(key.id);
                        testPanelRef.current?.scrollIntoView({
                          behavior: "smooth",
                          block: "start",
                        });
                      }}
                      className="rounded-full border border-white/10 px-3 py-1 text-xs text-slate-200 transition hover:border-cyan-200/60"
                    >
                      Test
                    </button>
                    <button
                      type="button"
                      disabled={!key.value}
                      onClick={async () => {
                        if (!key.value) {
                          return;
                        }
                        try {
                          await navigator.clipboard.writeText(key.value);
                          setCopiedKey(key.id);
                          setTimeout(() => setCopiedKey(null), 1500);
                        } catch {
                          setCopiedKey(null);
                        }
                      }}
                      className="rounded-full border border-white/10 px-3 py-1 text-xs text-slate-200 transition hover:border-cyan-200/60 disabled:cursor-not-allowed disabled:opacity-50"
                    >
                      {key.value
                        ? copiedKey === key.id
                          ? "Copied"
                          : "Copy"
                        : "Missing"}
                    </button>
                    <button
                      type="button"
                      disabled={keyBusy}
                      onClick={async () => {
                        if (keyBusy) {
                          return;
                        }
                        setKeyError(null);
                        setKeyBusy(true);
                        const nextStatus =
                          key.status === "active" ? "revoked" : "active";
                        const action =
                          nextStatus === "revoked" ? "revoke" : "restore";
                        try {
                          const response = await fetch(
                            `${pdfApiBaseUrl}/self-hosted/api-keys/${key.id}/${action}`,
                            { method: "POST", credentials: "include" },
                          );
                          if (!response.ok) {
                            const message = await readErrorMessage(response);
                            throw new Error(message);
                          }
                          setApiKeys((prev) =>
                            prev.map((item) =>
                              item.id === key.id
                                ? { ...item, status: nextStatus }
                                : item,
                            ),
                          );
                        } catch (error) {
                          setKeyError(
                            error instanceof Error
                              ? error.message
                              : "Unable to update API key.",
                          );
                        } finally {
                          setKeyBusy(false);
                        }
                      }}
                      className={`rounded-full px-3 py-1 text-xs transition disabled:cursor-not-allowed disabled:opacity-60 ${
                        key.status === "active"
                          ? "border border-white/10 text-slate-200 hover:border-rose-300/60"
                          : "border border-rose-300/60 text-rose-200 hover:border-cyan-200/60"
                      }`}
                    >
                      {key.status === "active" ? "Revoke" : "Restore"}
                    </button>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        <div className="space-y-6">
          <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
            <h2 className="text-lg font-semibold text-white">Quick actions</h2>
            <p className="mt-2 text-sm text-slate-400">
              Set the PDF API base URL for internal services.
            </p>
            <div className="mt-4 rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3 text-xs text-slate-200">
              {process.env.NEXT_PUBLIC_PDF_API_BASE_URL ||
                "http://localhost:8086"}
            </div>
            <div className="mt-4 space-y-2 text-sm text-slate-400">
              <p>- Database is local-only. Keep your backups in sync.</p>
              <p>- No subscriptions: all usage is treated as free-tier.</p>
              <p>
                - Plaintext keys are stored locally; the server stores only
                hashes.
              </p>
            </div>
          </div>

          <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
            <h2 className="text-lg font-semibold text-white">
              Instance health
            </h2>
            <div className="mt-4 space-y-3 text-sm">
              <HealthRow label="PDF API" status="Healthy" />
              <HealthRow label="Worker queue" status="Warmed" />
              <HealthRow label="Database" status="Connected" />
            </div>
          </div>
        </div>
      </section>

      <section
        ref={testPanelRef}
        className="mt-10 grid gap-6 lg:grid-cols-[1.2fr_0.8fr]"
      >
        <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div>
              <h2 className="text-xl font-semibold text-white">Test a key</h2>
              <p className="text-sm text-slate-400">
                Send a live request and download the generated PDF.
              </p>
              <p className="text-xs text-slate-500">
                Plaintext keys are stored in your browser for copy; the server
                stores only hashes.
              </p>
            </div>
            <span className="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs text-slate-200">
              {pdfApiBaseUrl}
            </span>
          </div>

          <div className="mt-6 grid gap-4">
            <label className="space-y-2 text-sm text-slate-300">
              <span>API key</span>
              <select
                value={selectedKey?.id ?? ""}
                onChange={(event) => setSelectedKeyId(event.target.value)}
                className="w-full rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3 text-sm text-white focus:outline-none focus:ring-2 focus:ring-cyan-200/40"
              >
                {apiKeys.length === 0 ? (
                  <option value="">Create a key first</option>
                ) : (
                  apiKeys.map((key) => (
                    <option key={key.id} value={key.id}>
                      {key.name} {key.status === "revoked" ? "(revoked)" : ""}
                    </option>
                  ))
                )}
              </select>
            </label>

            <label className="space-y-2 text-sm text-slate-300">
              <span>HTML payload</span>
              <textarea
                value={testHtml}
                onChange={(event) => setTestHtml(event.target.value)}
                rows={10}
                className="w-full resize-none rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3 text-sm text-white placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-200/40"
              />
            </label>

            <label className="space-y-2 text-sm text-slate-300">
              <span>Options JSON (optional)</span>
              <textarea
                value={testOptions}
                onChange={(event) => setTestOptions(event.target.value)}
                rows={5}
                className="w-full resize-none rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3 text-sm text-white placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-200/40"
              />
            </label>
          </div>

          {testError ? (
            <div className="mt-4 rounded-2xl border border-rose-500/40 bg-rose-500/10 px-4 py-3 text-sm text-rose-200">
              {testError}
            </div>
          ) : null}

          <div className="mt-5 flex flex-wrap items-center gap-3">
            <button
              type="button"
              disabled={!selectedKey || testBusy}
              onClick={async () => {
                if (!selectedKey) {
                  setTestError("Select a key before sending a request.");
                  return;
                }
                if (!selectedKey.value) {
                  setTestError(
                    "This key does not have a stored plaintext value. Create a new key to test.",
                  );
                  return;
                }
                setTestError(null);
                setTestBusy(true);
                setTestResultMeta(null);
                if (testResultUrl) {
                  URL.revokeObjectURL(testResultUrl);
                  setTestResultUrl(null);
                }

                let parsedOptions: Record<string, unknown> | undefined;
                const trimmedOptions = testOptions.trim();
                if (trimmedOptions) {
                  try {
                    parsedOptions = JSON.parse(trimmedOptions) as Record<
                      string,
                      unknown
                    >;
                  } catch (error) {
                    setTestBusy(false);
                    setTestError(
                      error instanceof Error
                        ? error.message
                        : "Options JSON is invalid.",
                    );
                    return;
                  }
                }

                try {
                  const response = await fetch(`${pdfApiBaseUrl}/v1/generate`, {
                    method: "POST",
                    headers: {
                      Authorization: `Bearer ${selectedKey.value}`,
                      "Content-Type": "application/json",
                    },
                    body: JSON.stringify({
                      html: testHtml,
                      options: parsedOptions,
                    }),
                  });

                  if (!response.ok) {
                    const text = await response.text();
                    let message = text;
                    try {
                      const json = JSON.parse(text) as {
                        message?: string;
                        error?: string;
                      };
                      if (json.message) {
                        message = json.message;
                      } else if (json.error) {
                        message = json.error;
                      }
                    } catch {
                      // Keep raw text
                    }
                    setTestError(
                      `Request failed (${response.status}). ${message}`,
                    );
                    setTestResultMeta({
                      status: response.status,
                      statusText: response.statusText,
                      size: 0,
                      contentType: response.headers.get("content-type"),
                      receivedAt: new Date().toISOString(),
                    });
                    return;
                  }

                  const blob = await response.blob();
                  const url = URL.createObjectURL(blob);
                  setTestResultUrl(url);
                  setTestResultMeta({
                    status: response.status,
                    statusText: response.statusText,
                    size: blob.size,
                    contentType:
                      blob.type || response.headers.get("content-type"),
                    receivedAt: new Date().toISOString(),
                  });
                } catch (error) {
                  setTestError(
                    error instanceof Error ? error.message : "Request failed.",
                  );
                } finally {
                  setTestBusy(false);
                }
              }}
              className="rounded-2xl bg-cyan-300/90 px-5 py-3 text-xs font-semibold text-slate-950 transition hover:bg-cyan-200 disabled:cursor-not-allowed disabled:opacity-50"
            >
              {testBusy ? "Sending..." : "Send test request"}
            </button>
            {testResultUrl ? (
              <a
                href={testResultUrl}
                download="paperapi-test.pdf"
                className="rounded-2xl border border-white/10 px-4 py-3 text-xs font-semibold text-slate-200 transition hover:border-cyan-200/60"
              >
                Download PDF
              </a>
            ) : null}
          </div>
        </div>

        <div className="space-y-6">
          <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
            <h3 className="text-lg font-semibold text-white">
              Request preview
            </h3>
            <p className="mt-2 text-sm text-slate-400">
              Use this to reproduce the request in a terminal.
            </p>
            <pre className="mt-4 whitespace-pre-wrap rounded-2xl border border-white/10 bg-slate-900/70 p-4 text-xs text-slate-200">
              {curlSnippet}
            </pre>
          </div>
          <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
            <h3 className="text-lg font-semibold text-white">Response</h3>
            {testResultMeta ? (
              <div className="mt-3 space-y-2 text-sm text-slate-300">
                <p>Status: {testResultMeta.status}</p>
                <p>
                  Content-Type:{" "}
                  {testResultMeta.contentType || "application/pdf"}
                </p>
                <p>Size: {formatBytes(testResultMeta.size)}</p>
                <p>Received: {formatDate(testResultMeta.receivedAt)}</p>
              </div>
            ) : (
              <p className="mt-3 text-sm text-slate-400">
                Send a request to see status and payload size.
              </p>
            )}
            {testResultUrl ? (
              <div className="mt-4 overflow-hidden rounded-2xl border border-white/10">
                <iframe
                  title="Generated PDF preview"
                  src={testResultUrl}
                  className="h-[360px] w-full"
                />
              </div>
            ) : null}
          </div>
        </div>
      </section>

      <section className="mt-10 grid gap-6 lg:grid-cols-2">
        <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
          <h2 className="text-xl font-semibold text-white">Recent logs</h2>
          <div className="mt-4 space-y-3">
            {logs.map((log) => (
              <div
                key={log.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3"
              >
                <div>
                  <p className="text-sm font-semibold text-white">
                    {log.event}
                  </p>
                  <p className="text-xs text-slate-400">{log.detail}</p>
                </div>
                <div className="text-right text-xs text-slate-400">
                  <p>{formatDate(log.timestamp)}</p>
                  <StatusBadge status={log.status} />
                </div>
              </div>
            ))}
          </div>
        </div>
        <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
          <h2 className="text-xl font-semibold text-white">Rendering notes</h2>
          <div className="mt-4 space-y-4 text-sm text-slate-300">
            <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-4">
              <p className="text-sm font-semibold text-white">wkhtmltopdf</p>
              <p className="mt-2 text-sm text-slate-400">
                Confirm binary availability and font packs after upgrades. Use
                the PDF API health endpoint to validate.
              </p>
            </div>
            <div className="rounded-2xl border border-white/10 bg-slate-900/60 p-4">
              <p className="text-sm font-semibold text-white">Audit trail</p>
              <p className="mt-2 text-sm text-slate-400">
                All requests are logged on your instance. Export logs regularly
                if you need long-term retention.
              </p>
            </div>
          </div>
        </div>
      </section>
    </main>
  );
}

function Header() {
  return (
    <header className="flex flex-wrap items-center justify-between gap-4">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-2xl border border-white/10 bg-white/5 text-sm font-semibold text-cyan-100">
          P
        </div>
        <div>
          <p className="text-sm uppercase tracking-[0.35em] text-cyan-200/70">
            PaperAPI
          </p>
          <p className="text-lg font-semibold text-white">
            Self-hosted dashboard
          </p>
        </div>
      </div>
      <div className="flex flex-wrap items-center gap-3">
        <a
          href="/sdk"
          className="rounded-full border border-white/10 bg-white/5 px-4 py-2 text-xs uppercase tracking-[0.2em] text-slate-200 transition hover:border-cyan-200/60"
        >
          SDKs
        </a>
        <span className="rounded-full border border-white/10 bg-white/5 px-4 py-2 text-xs uppercase tracking-[0.2em] text-slate-200">
          Self-Hosted
        </span>
      </div>
    </header>
  );
}

function Field({
  label,
  value,
  onChange,
  placeholder,
  type = "text",
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  type?: string;
}) {
  return (
    <label className="space-y-2 text-sm text-slate-300">
      <span>{label}</span>
      <input
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        placeholder={placeholder}
        className="w-full rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3 text-sm text-white placeholder:text-slate-500 focus:outline-none focus:ring-2 focus:ring-cyan-200/40"
      />
    </label>
  );
}

function StatCard({
  title,
  value,
  helper,
}: {
  title: string;
  value: string;
  helper: string;
}) {
  return (
    <div className="rounded-3xl border border-white/10 bg-slate-950/70 p-6">
      <p className="text-xs uppercase tracking-[0.3em] text-cyan-200/70">
        {title}
      </p>
      <p className="mt-3 text-3xl font-semibold text-white">{value}</p>
      <p className="mt-2 text-sm text-slate-400">{helper}</p>
    </div>
  );
}

function StatusBadge({ status }: { status: LogRecord["status"] }) {
  const styles = {
    ok: "border-emerald-300/60 text-emerald-200",
    warn: "border-amber-300/60 text-amber-200",
    error: "border-rose-300/60 text-rose-200",
  };

  return (
    <span
      className={`mt-1 inline-flex rounded-full border px-2 py-0.5 text-[10px] uppercase tracking-[0.2em] ${
        styles[status]
      }`}
    >
      {status}
    </span>
  );
}

function HealthRow({ label, status }: { label: string; status: string }) {
  return (
    <div className="flex items-center justify-between rounded-2xl border border-white/10 bg-slate-900/60 px-4 py-3 text-sm">
      <span className="text-slate-300">{label}</span>
      <span className="text-cyan-200">{status}</span>
    </div>
  );
}
