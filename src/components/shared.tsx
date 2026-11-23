"use client";

import { useCallback, useEffect, useState } from "react";
import { toast } from "@/hooks/use-toast";
import { cn } from "@/lib/utils";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import type { LucideIcon } from "lucide-react";
import { toFa } from "@/lib/persian";

// ---------- API helper (با refresh خودکار — PLAN P0.6) ----------
import { apiAuth } from "@/lib/client-api";

export async function api<T = unknown>(url: string, opts?: RequestInit): Promise<T> {
  return apiAuth<T>(url, opts);
}

export function useApiData<T>(url: string, intervalMs?: number) {
  const [data, setData] = useState<T | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    try {
      const d = await api<T>(url);
      setData(d);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "خطا");
    } finally {
      setLoading(false);
    }
  }, [url]);

  useEffect(() => {
    load();
    if (intervalMs) {
      const t = setInterval(load, intervalMs);
      return () => clearInterval(t);
    }
  }, [load, intervalMs]);

  return { data, loading, error, reload: load };
}

export function errToast(e: unknown) {
  toast({
    title: "خطا",
    description: e instanceof Error ? e.message : "عملیات ناموفق بود",
    variant: "destructive",
  });
}

// ---------- Status → badge color mapping ----------
const STATUS_MAP: Record<string, string> = {
  "فعال": "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  "حاضر": "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  "مجاز": "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  "تأیید شده": "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  "داخل سازمان": "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  "آنلاین": "bg-emerald-500/15 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",

  "تأخیر": "bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30",
  "در انتظار تأیید": "bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30",
  "موقت": "bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30",
  "حساس": "bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30",
  "متوسط": "bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30",
  "بالا": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",
  "تأخیر خروج": "bg-amber-500/15 text-amber-700 dark:text-amber-300 border-amber-500/30",

  "غایب": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",
  "رد شده": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",
  "غیرمجاز": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",
  "خراب": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",
  "آفلاین": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",
  "بحرانی": "bg-rose-600/20 text-rose-700 dark:text-rose-300 border-rose-600/40",
  "فوق‌حساس": "bg-rose-600/20 text-rose-700 dark:text-rose-300 border-rose-600/40",
  "ورود غیرمجاز": "bg-rose-600/20 text-rose-700 dark:text-rose-300 border-rose-600/40",
  "منقضی": "bg-rose-500/15 text-rose-700 dark:text-rose-300 border-rose-500/30",

  "مرخصی": "bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/30",
  "استحقاقی": "bg-violet-500/15 text-violet-700 dark:text-violet-300 border-violet-500/30",
  "مأموریت": "bg-teal-500/15 text-teal-700 dark:text-teal-300 border-teal-500/30",

  "عادی": "bg-muted text-muted-foreground border-border",
  "دائم": "bg-muted text-muted-foreground border-border",
  "غیرفعال": "bg-muted text-muted-foreground border-border",
  "تسویه‌شده": "bg-muted text-muted-foreground border-border",
  "خارج شده": "bg-muted text-muted-foreground border-border",
};

export function StatusBadge({ value, className }: { value: string; className?: string }) {
  const cls = STATUS_MAP[value] ?? "bg-muted text-muted-foreground border-border";
  return <Badge variant="outline" className={cn(cls, "font-medium", className)}>{value}</Badge>;
}

// ---------- Stat card ----------
export function StatCard({
  icon: Icon,
  label,
  value,
  sub,
  tone = "default",
}: {
  icon: LucideIcon;
  label: string;
  value: string | number;
  sub?: string;
  tone?: "default" | "green" | "amber" | "red" | "violet";
}) {
  const tones = {
    default: "text-foreground bg-card",
    green: "text-emerald-700 dark:text-emerald-300 bg-card",
    amber: "text-amber-700 dark:text-amber-300 bg-card",
    red: "text-rose-700 dark:text-rose-300 bg-card",
    violet: "text-violet-700 dark:text-violet-300 bg-card",
  };
  const iconBg = {
    default: "bg-primary/10 text-primary",
    green: "bg-emerald-500/10 text-emerald-600",
    amber: "bg-amber-500/10 text-amber-600",
    red: "bg-rose-500/10 text-rose-600",
    violet: "bg-violet-500/10 text-violet-600",
  };
  return (
    <Card className={cn("border shadow-sm", tones[tone])}>
      <CardContent className="p-4 flex items-center gap-3">
        <div className={cn("rounded-lg p-2.5 flex items-center justify-center shrink-0", iconBg[tone])}>
          <Icon className="h-5 w-5" />
        </div>
        <div className="min-w-0">
          <div className="text-xl font-bold num-fa leading-tight">
            {typeof value === "number" ? toFa(value) : toFa(String(value))}
          </div>
          <div className="text-xs text-muted-foreground truncate">{label}</div>
          {sub && <div className="text-[11px] text-muted-foreground/80 num-fa mt-0.5">{sub}</div>}
        </div>
      </CardContent>
    </Card>
  );
}

// ---------- Section card ----------
export function Section({
  title,
  icon: Icon,
  action,
  children,
  className,
  contentClass,
}: {
  title: string;
  icon?: LucideIcon;
  action?: React.ReactNode;
  children: React.ReactNode;
  className?: string;
  contentClass?: string;
}) {
  return (
    <Card className={cn("shadow-sm overflow-hidden min-w-0", className)}>
      <CardHeader className="pb-3 flex flex-row items-center justify-between space-y-0">
        <CardTitle className="text-base font-semibold flex items-center gap-2">
          {Icon && <Icon className="h-4.5 w-4.5 text-primary" />}
          {title}
        </CardTitle>
        {action}
      </CardHeader>
      <CardContent className={contentClass}>{children}</CardContent>
    </Card>
  );
}

// ---------- Loading / empty ----------
export function LoadingBlock({ rows = 3 }: { rows?: number }) {
  return (
    <div className="space-y-2">
      {Array.from({ length: rows }).map((_, i) => (
        <Skeleton key={i} className="h-10 w-full" />
      ))}
    </div>
  );
}

export function EmptyState({ text }: { text: string }) {
  return (
    <div className="text-center text-sm text-muted-foreground py-8 border border-dashed rounded-lg">
      {text}
    </div>
  );
}

// ---------- Error block ----------
export function ErrorBlock({ msg, onRetry }: { msg: string; onRetry?: () => void }) {
  return (
    <div className="text-center py-8 border border-dashed border-destructive/40 rounded-lg space-y-2">
      <p className="text-sm text-destructive">{msg}</p>
      {onRetry && (
        <button onClick={onRetry} className="text-xs underline text-muted-foreground hover:text-foreground">
          تلاش مجدد
        </button>
      )}
    </div>
  );
}
