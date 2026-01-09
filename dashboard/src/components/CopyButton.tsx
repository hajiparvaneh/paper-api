"use client";

import { useEffect, useRef, useState } from "react";

interface CopyButtonProps {
  text: string;
  ariaLabel?: string;
}

export function CopyButton({ text, ariaLabel = "Copy to clipboard" }: CopyButtonProps) {
  const [copied, setCopied] = useState(false);
  const timeoutRef = useRef<NodeJS.Timeout | null>(null);

  useEffect(() => {
    return () => {
      if (timeoutRef.current) {
        clearTimeout(timeoutRef.current);
      }
    };
  }, []);

  async function handleCopy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      if (timeoutRef.current) {
        clearTimeout(timeoutRef.current);
      }
      timeoutRef.current = setTimeout(() => setCopied(false), 1800);
    } catch (error) {
      console.error("Failed to copy text", error);
    }
  }

  return (
    <button
      type="button"
      onClick={handleCopy}
      className="inline-flex items-center gap-2 rounded-full border border-white/30 px-3 py-1 text-xs font-medium text-white/70 transition hover:border-white hover:text-white"
      aria-label={ariaLabel}
    >
      <svg
        xmlns="http://www.w3.org/2000/svg"
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.5"
        className="h-4 w-4"
      >
        {copied ? (
          <path strokeLinecap="round" strokeLinejoin="round" d="m5 13 4 4L19 7" />
        ) : (
          <>
            <rect width="10" height="12" x="9" y="7" rx="2" />
            <path d="M5 16V6a2 2 0 0 1 2-2h8" />
          </>
        )}
      </svg>
      {copied ? "Copied" : "Copy"}
    </button>
  );
}
