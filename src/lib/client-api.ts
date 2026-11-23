"use client";

// کلاینت API با refresh خودکار (PLAN P0.6):
// روی 401 یک‌بار /api/auth/refresh را امتحان می‌کند، سپس درخواست را تکرار می‌کند.

let refreshing: Promise<boolean> | null = null;

async function tryRefresh(): Promise<boolean> {
  if (!refreshing) {
    refreshing = (async () => {
      try {
        const res = await fetch("/api/auth/refresh", {
          method: "POST",
          cache: "no-store",
        });
        return res.ok;
      } catch {
        return false;
      } finally {
        setTimeout(() => {
          refreshing = null;
        }, 100);
      }
    })();
  }
  return refreshing;
}

export async function apiAuth<T = unknown>(url: string, opts?: RequestInit, retry = true): Promise<T> {
  const res = await fetch(url, {
    headers: { "Content-Type": "application/json" },
    cache: "no-store",
    ...opts,
  });
  if (res.status === 401 && retry && !url.includes("/api/auth/")) {
    const ok = await tryRefresh();
    if (ok) return apiAuth<T>(url, opts, false);
    // نشست واقعاً منقضی — رویداد جهانی برای نمایش صفحه ورود
    window.dispatchEvent(new CustomEvent("hr-session-expired"));
    throw new Error("نشست منقضی شده — دوباره وارد شوید");
  }
  const j = await res.json().catch(() => ({}));
  if (!res.ok) {
    const msg = (j as { error?: string }).error || "خطای پردازش در سرور";
    if (res.status === 403)
      throw new Error(msg || "دسترسی غیرمجاز");
    throw new Error(msg);
  }
  return j as T;
}
