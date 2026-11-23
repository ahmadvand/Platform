"use client";

import { useState } from "react";
import { ScrollText, ShieldAlert, Search } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import {
  useApiData, Section, LoadingBlock, EmptyState,
} from "@/components/shared";
import { toFa, formatJalali, formatTime } from "@/lib/persian";

interface LogRow {
  id: string;
  actorName: string | null;
  action: string;
  entity: string;
  entityId: string | null;
  detail: string | null;
  ip: string | null;
  at: string;
}

const ACTION_FA: Record<string, string> = {
  login: "ورود موفق", login_failed: "ورود ناموفق", account_locked: "قفل حساب",
  logout: "خروج", create: "ایجاد", update: "ویرایش", delete: "حذف",
  approve: "تأیید", reject: "رد", reset_password: "بازنشانی رمز",
  unlock: "رفع قفل", set_roles: "تغییر نقش‌ها", set_permissions: "تغییر دسترسی‌ها",
  revoke_sessions: "ابطال نشست‌ها", device_command: "دستور دستگاه",
  transfer_user: "انتقال کاربر", rotate_secret: "چرخش توکن", finalize: "نهایی‌سازی",
  denied: "دسترسی ردشده",
};

const DANGER = new Set(["login_failed", "account_locked", "denied", "delete", "reset_password"]);

export function AuditView() {
  const [q, setQ] = useState("");
  const [action, setAction] = useState("all");
  const { data, loading } = useApiData<{ logs: LogRow[] }>(
    `/api/audit?${q ? `q=${encodeURIComponent(q)}&` : ""}${action !== "all" ? `action=${action}` : ""}`
  );

  const logs = data?.logs ?? [];
  const failed = logs.filter((l) => DANGER.has(l.action)).length;

  return (
    <div className="space-y-4 min-w-0">
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative flex-1 min-w-52">
          <Search className="absolute right-3 top-2.5 h-4 w-4 text-muted-foreground" />
          <Input
            className="pr-9"
            placeholder="جستجوی کاربر، موجودیت..."
            value={q}
            onChange={(e) => setQ(e.target.value)}
          />
        </div>
        <Select value={action} onValueChange={setAction}>
          <SelectTrigger className="w-44"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">همه عملیات</SelectItem>
            {Object.entries(ACTION_FA).map(([k, v]) => (
              <SelectItem key={k} value={k}>{v}</SelectItem>
            ))}
          </SelectContent>
        </Select>
        <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
          <ShieldAlert className="h-4 w-4 text-rose-500" />
          <span className="num-fa">{toFa(failed)} رخداد حساس در این صفحه</span>
        </div>
      </div>

      <Section title="رخدادهای امنیتی و عملیات" icon={ScrollText} contentClass="max-h-[65vh] overflow-y-auto thin-scroll">
        {loading ? <LoadingBlock rows={6} /> : logs.length === 0 ? (
          <EmptyState text="رخدادی یافت نشد" />
        ) : (
          <div className="space-y-1.5">
            {logs.map((l) => (
              <div key={l.id} className="flex flex-wrap items-center gap-2 text-xs border rounded-lg px-3 py-2">
                <span
                  className={`inline-block h-2 w-2 rounded-full shrink-0 ${DANGER.has(l.action) ? "bg-rose-500" : "bg-emerald-500"}`}
                />
                <b className={DANGER.has(l.action) ? "text-rose-700" : ""}>{ACTION_FA[l.action] ?? l.action}</b>
                <span className="text-muted-foreground">{l.actorName ?? "سیستم"}</span>
                <Badge2>{l.entity}{l.entityId ? `: ${l.entityId}` : ""}</Badge2>
                {l.detail && (
                  <span className="text-[10px] text-muted-foreground truncate max-w-64" title={l.detail}>{l.detail}</span>
                )}
                <span className="flex-1" />
                <span dir="ltr" className="text-[10px] font-mono text-muted-foreground">{l.ip ?? "—"}</span>
                <span className="num-fa text-[10px] text-muted-foreground shrink-0">
                  {formatJalali(l.at)} {formatTime(l.at)}
                </span>
              </div>
            ))}
          </div>
        )}
      </Section>
    </div>
  );
}

function Badge2({ children }: { children: React.ReactNode }) {
  return (
    <span className="rounded bg-muted px-1.5 py-0.5 text-[10px] font-mono" dir="ltr">
      {children}
    </span>
  );
}
