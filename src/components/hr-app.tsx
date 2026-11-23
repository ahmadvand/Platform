"use client";

import { useCallback, useEffect, useState } from "react";
import {
  LayoutDashboard, Users, Network, Fingerprint, CalendarCheck2, ScanFace, ShieldCheck, Menu, X, Bell,
  MonitorCog, FilePenLine, Banknote, CalendarDays, UserCog, ScrollText, LogOut, Loader2,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { toFa, formatJalaliLong } from "@/lib/persian";
import { api } from "@/components/shared";
import { DashboardView } from "@/components/views/dashboard-view";
import { EmployeesView } from "@/components/views/employees-view";
import { OrgView } from "@/components/views/org-view";
import { AttendanceView } from "@/components/views/attendance-view";
import { RequestsView } from "@/components/views/requests-view";
import { AccessView } from "@/components/views/access-view";
import { VisitorsView } from "@/components/views/visitors-view";
import { DevicesView } from "@/components/views/devices-view";
import { CorrectionsView } from "@/components/views/corrections-view";
import { PayrollView } from "@/components/views/payroll-view";
import { HolidaysView } from "@/components/views/holidays-view";
import { UsersView } from "@/components/views/users-view";
import { AuditView } from "@/components/views/audit-view";
import { LoginView } from "@/components/views/login-view";

interface NavDef {
  id: string;
  label: string;
  icon: typeof LayoutDashboard;
  hint: string;
  perm: string;
  group: string;
}

const NAV: NavDef[] = [
  { id: "dashboard", label: "داشبورد", icon: LayoutDashboard, hint: "نمای کلی و شاخص‌ها", perm: "dashboard.view", group: "عملیات" },
  { id: "hr", label: "منابع انسانی", icon: Users, hint: "پرسنل، قرارداد، احکام", perm: "employees.view", group: "عملیات" },
  { id: "org", label: "ساختار سازمانی", icon: Network, hint: "چارت و پست‌ها", perm: "org.view", group: "عملیات" },
  { id: "att", label: "حضور و غیاب", icon: Fingerprint, hint: "کارکرد، شیفت، دستگاه", perm: "attendance.view", group: "عملیات" },
  { id: "req", label: "درخواست‌ها", icon: CalendarCheck2, hint: "مرخصی، مأموریت، مجوز", perm: "attendance.view", group: "عملیات" },
  { id: "corr", label: "اصلاح تردد", icon: FilePenLine, hint: "درخواست و تأیید اصلاح", perm: "corrections.view", group: "عملیات" },
  { id: "acs", label: "کنترل تردد", icon: ScanFace, hint: "مناطق، گیت، مجوز", perm: "access.view", group: "عملیات" },
  { id: "vis", label: "ارباب رجوع و حراست", icon: ShieldCheck, hint: "مهمان، کارت، هشدار", perm: "visitors.view", group: "عملیات" },
  { id: "dev", label: "مانیتور دستگاه‌ها", icon: MonitorCog, hint: "عملیات، انتقال کاربر، صف دستور", perm: "devices.view", group: "دستگاه‌ها و گزارش" },
  { id: "pay", label: "حقوق و دستمزد", icon: Banknote, hint: "لیست حقوق و فیش‌ها", perm: "payroll.view", group: "دستگاه‌ها و گزارش" },
  { id: "cal", label: "تقویم تعطیلات", icon: CalendarDays, hint: "تعطیلات رسمی شمسی", perm: "holidays.view", group: "دستگاه‌ها و گزارش" },
  { id: "users", label: "کاربران و نقش‌ها", icon: UserCog, hint: "حساب‌ها، دسترسی‌ها، امنیت", perm: "users.view", group: "مدیریت سیستم" },
  { id: "audit", label: "گزارش حسابرسی", icon: ScrollText, hint: "رخدادهای امنیتی سامانه", perm: "audit.view", group: "مدیریت سیستم" },
];

const TITLES: Record<string, { title: string; sub: string }> = {
  dashboard: { title: "داشبورد مدیریتی", sub: "نمای کلی سیستم جامع — تصمیم‌گیری مبتنی بر داده" },
  hr: { title: "مدیریت منابع انسانی", sub: "پرسنل، قراردادها و احکام کارگزینی" },
  org: { title: "ساختار سازمانی", sub: "چارت، پست‌ها و تخصیص پرسنل" },
  att: { title: "حضور و غیاب", sub: "محاسبه کارکرد، شیفت‌ها و دستگاه‌ها" },
  req: { title: "درخواست‌ها و گردش تأیید", sub: "مرخصی، مأموریت و مجوزها" },
  corr: { title: "اصلاح تردد", sub: "درخواست اصلاح ورود/خروج و گردش تأیید" },
  acs: { title: "کنترل تردد و دسترسی", sub: "مناطق حساس، گیت‌ها و مانیتورینگ" },
  vis: { title: "ارباب رجوع و حراست", sub: "مهمانان، کارت موقت و هشدارها" },
  dev: { title: "مانیتور و عملیات دستگاه‌ها", sub: "کنترل از راه دور، انتقال کاربر، صف دستورات" },
  pay: { title: "حقوق و دستمزد", sub: "لیست‌های حقوق ماهانه و فیش‌های پرسنلی" },
  cal: { title: "تقویم تعطیلات", sub: "تعطیلات رسمی سال جلالی و مدیریت تقویم" },
  users: { title: "کاربران و نقش‌ها", sub: "حساب‌های کاربری، نقش‌ها، بازنشانی رمز و امنیت" },
  audit: { title: "گزارش حسابرسی", sub: "ثبت رخدادهای امنیتی و عملیات کاربران" },
};

interface SessionInfo {
  id: number;
  username: string;
  fullName: string;
  isSuperadmin: boolean;
}

export function HrApp() {
  const [session, setSession] = useState<SessionInfo | null>(null);
  const [perms, setPerms] = useState<string[]>([]);
  const [authLoading, setAuthLoading] = useState(true);
  const [active, setActive] = useState("dashboard");
  const [drawer, setDrawer] = useState(false);
  const [badges, setBadges] = useState<{ pendingRequests: number; unresolvedAlerts: number } | null>(null);
  const [clock, setClock] = useState<string>("");

  const loadSession = useCallback(async () => {
    try {
      const d = await api<{
        user: SessionInfo;
        permissions: string[];
      }>("/api/auth/me");
      setSession(d.user);
      setPerms(d.permissions ?? []);
    } catch {
      setSession(null);
      setPerms([]);
    } finally {
      setAuthLoading(false);
    }
  }, []);

  useEffect(() => {
    loadSession();
    const onExpired = () => {
      setSession(null);
      setPerms([]);
    };
    window.addEventListener("hr-session-expired", onExpired);
    return () => window.removeEventListener("hr-session-expired", onExpired);
  }, [loadSession]);

  const has = useCallback((p: string) => perms.includes(p), [perms]);

  // poll بدنه — فقط با نشست فعال
  useEffect(() => {
    if (!session) return;
    let stop = false;
    const load = async () => {
      try {
        const d = await api<{ kpis: { pendingRequests: number; unresolvedAlerts: number } }>("/api/dashboard");
        if (!stop) setBadges({ pendingRequests: d.kpis.pendingRequests, unresolvedAlerts: d.kpis.unresolvedAlerts });
      } catch {
        /* ignore */
      }
    };
    load();
    const t = setInterval(load, 20000);
    return () => {
      stop = true;
      clearInterval(t);
    };
  }, [session]);

  // ساعت زنده
  useEffect(() => {
    const tick = () => {
      const d = new Date();
      const p2 = (n: number) => String(n).padStart(2, "0");
      setClock(`${p2(d.getHours())}:${p2(d.getMinutes())}:${p2(d.getSeconds())}`);
    };
    tick();
    const t = setInterval(tick, 1000);
    return () => clearInterval(t);
  }, []);

  const logout = async () => {
    try {
      await fetch("/api/auth/logout", { method: "POST", cache: "no-store" });
    } catch {
      /* ignore */
    }
    setSession(null);
    setPerms([]);
  };

  if (authLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-background" dir="rtl">
        <div className="flex flex-col items-center gap-3 text-muted-foreground">
          <Loader2 className="h-8 w-8 animate-spin text-primary" />
          <p className="text-sm">در حال بررسی نشست...</p>
        </div>
      </div>
    );
  }

  if (!session) {
    return <LoginView onLogin={loadSession} />;
  }

  const visibleNav = NAV.filter((n) => has(n.perm));
  const currentNav = visibleNav.find((n) => n.id === active) ?? visibleNav[0];
  const activeId = currentNav?.id ?? "dashboard";

  const badgeFor = (id: string): number => {
    if (!badges) return 0;
    if (id === "req") return badges.pendingRequests;
    if (id === "vis") return badges.unresolvedAlerts;
    return 0;
  };

  const goTo = (id: string) => {
    setActive(id);
    setDrawer(false);
  };

  const groups = [...new Set(visibleNav.map((n) => n.group))];

  const sidebar = (
    <div className="flex flex-col h-full">
      {/* brand */}
      <div className="flex items-center gap-3 px-5 h-16 border-b border-sidebar-border shrink-0">
        <div className="rounded-xl bg-sidebar-primary/20 p-2">
          <ShieldCheck className="h-6 w-6 text-sidebar-primary" />
        </div>
        <div className="min-w-0">
          <div className="font-bold text-sm text-sidebar-foreground">سامانه جامع یکپارچه</div>
          <div className="text-[10px] text-sidebar-foreground/60">منابع انسانی • تردد • حراست</div>
        </div>
      </div>

      {/* nav — فیلترشده بر اساس دسترسی (PLAN P1.7) */}
      <nav className="flex-1 overflow-y-auto thin-scroll p-3 space-y-3" aria-label="ناوبری اصلی">
        {groups.map((g) => (
          <div key={g} className="space-y-1">
            {groups.length > 1 && (
              <div className="text-[9px] font-semibold text-sidebar-foreground/40 px-3 pt-1">{g}</div>
            )}
            {visibleNav.filter((n) => n.group === g).map((n) => {
              const b = badgeFor(n.id);
              const Icon = n.icon;
              const activeCls = activeId === n.id
                ? "bg-sidebar-accent text-sidebar-accent-foreground font-semibold"
                : "text-sidebar-foreground/75 hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground";
              return (
                <button
                  key={n.id}
                  onClick={() => goTo(n.id)}
                  className={cn("w-full flex items-center gap-3 rounded-lg px-3 py-2.5 text-sm transition-colors text-right", activeCls)}
                >
                  <Icon className="h-4.5 w-4.5 shrink-0" />
                  <span className="flex-1">{n.label}</span>
                  {b > 0 && (
                    <span className="num-fa inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-500/90 text-white text-[10px] font-bold px-1">
                      {toFa(b)}
                    </span>
                  )}
                </button>
              );
            })}
          </div>
        ))}
      </nav>

      {/* session footer */}
      <div className="p-3 border-t border-sidebar-border space-y-2">
        <div className="flex items-center gap-2.5 px-2">
          <div className="h-8 w-8 rounded-full bg-sidebar-accent flex items-center justify-center text-xs font-bold text-sidebar-accent-foreground">
            {session.fullName.slice(0, 1)}
          </div>
          <div className="min-w-0 flex-1">
            <div className="text-xs font-medium text-sidebar-foreground truncate">{session.fullName}</div>
            <div dir="ltr" className="text-[10px] text-sidebar-foreground/50 text-left">{session.username}</div>
          </div>
          <button
            onClick={logout}
            title="خروج از حساب"
            className="p-2 rounded-lg text-sidebar-foreground/60 hover:text-rose-400 hover:bg-sidebar-accent/60 transition-colors"
          >
            <LogOut className="h-4 w-4" />
          </button>
        </div>
        <p className="text-[9px] text-sidebar-foreground/40 px-2">نسخه {toFa("2.0")} — پوشش کامل رودمپ</p>
      </div>
    </div>
  );

  const titleInfo = TITLES[activeId] ?? TITLES.dashboard;

  return (
    <div className="min-h-screen flex flex-col bg-background" dir="rtl">
      {/* Mobile top bar */}
      <header className="lg:hidden sticky top-0 z-40 flex items-center justify-between h-14 px-4 bg-sidebar text-sidebar-foreground border-b border-sidebar-border">
        <button onClick={() => setDrawer(true)} aria-label="باز کردن منو" className="p-2">
          <Menu className="h-5 w-5" />
        </button>
        <span className="font-bold text-sm">سامانه جامع یکپارچه</span>
        <span className="num-fa text-xs text-sidebar-foreground/70">{clock}</span>
      </header>

      {/* Mobile drawer */}
      {drawer && (
        <div className="lg:hidden fixed inset-0 z-50 flex">
          <div className="absolute inset-0 bg-black/50" onClick={() => setDrawer(false)} />
          <aside className="relative w-72 max-w-[85%] bg-sidebar text-sidebar-foreground shadow-xl">
            <button onClick={() => setDrawer(false)} aria-label="بستن منو" className="absolute top-4 left-3 p-1.5 z-10">
              <X className="h-5 w-5" />
            </button>
            {sidebar}
          </aside>
        </div>
      )}

      <div className="flex flex-1">
        {/* Desktop sidebar */}
        <aside className="hidden lg:flex w-64 shrink-0 bg-sidebar text-sidebar-foreground sticky top-0 h-screen">
          {sidebar}
        </aside>

        {/* Main */}
        <div className="flex-1 flex flex-col min-w-0">
          {/* Desktop header */}
          <header className="hidden lg:flex sticky top-0 z-30 items-center justify-between h-16 px-6 bg-card/80 backdrop-blur border-b">
            <div>
              <h1 className="font-bold">{titleInfo.title}</h1>
              <p className="text-[11px] text-muted-foreground">{titleInfo.sub}</p>
            </div>
            <div className="flex items-center gap-4">
              {clock && (
                <div className="text-left">
                  <div className="num-fa text-lg font-bold leading-none" dir="ltr">{toFa(clock)}</div>
                  <div className="num-fa text-[11px] text-muted-foreground">{formatJalaliLong(new Date())}</div>
                </div>
              )}
              <div className="h-8 w-px bg-border" />
              <div className="relative" title="اعلان‌ها">
                <Bell className="h-5 w-5 text-muted-foreground" />
                {badges && badges.pendingRequests + badges.unresolvedAlerts > 0 && (
                  <span className="num-fa absolute -top-1.5 -left-1.5 inline-flex h-4.5 min-w-4.5 items-center justify-center rounded-full bg-rose-500 text-white text-[9px] font-bold px-1">
                    {toFa(badges.pendingRequests + badges.unresolvedAlerts)}
                  </span>
                )}
              </div>
            </div>
          </header>

          {/* Content */}
          <main className="flex-1 p-4 md:p-6 max-w-[1500px] w-full mx-auto">
            {activeId === "dashboard" && <DashboardView />}
            {activeId === "hr" && <EmployeesView />}
            {activeId === "org" && <OrgView />}
            {activeId === "att" && <AttendanceView />}
            {activeId === "req" && <RequestsView />}
            {activeId === "corr" && <CorrectionsView />}
            {activeId === "acs" && <AccessView />}
            {activeId === "vis" && <VisitorsView />}
            {activeId === "dev" && <DevicesView />}
            {activeId === "pay" && <PayrollView />}
            {activeId === "cal" && <HolidaysView />}
            {activeId === "users" && <UsersView />}
            {activeId === "audit" && <AuditView />}
          </main>

          {/* Sticky footer */}
          <footer className="mt-auto border-t bg-card">
            <div className="max-w-[1500px] mx-auto px-6 py-3 flex flex-wrap items-center justify-between gap-2 text-[11px] text-muted-foreground num-fa">
              <span>سامانه جامع منابع انسانی، حضور و غیاب، کنترل تردد و ارباب رجوع</span>
              <span>Core HR First • Configuration over Customization • Single Source of Truth • API-First</span>
            </div>
          </footer>
        </div>
      </div>
    </div>
  );
}
