"use client";

import {
  Users, UserCheck, ShieldAlert, DoorOpen, UserX, CalendarX2, Timer, Server, History, FileWarning, LogIn,
} from "lucide-react";
import { AreaChart, Area, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell, Legend, BarChart, Bar, CartesianGrid } from "recharts";
import { useApiData, StatCard, Section, StatusBadge, LoadingBlock, EmptyState, ErrorBlock } from "@/components/shared";
import { formatJalaliLong, formatTime, toFa, formatNumber } from "@/lib/persian";
import type { DashboardData } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { api } from "@/components/shared";
import { useToast } from "@/hooks/use-toast";

const PIE_COLORS = ["#0f766e", "#10b981", "#f59e0b", "#8b5cf6", "#64748b"];

export function DashboardView() {
  const { data, loading, error, reload } = useApiData<DashboardData>("/api/dashboard", 20000);
  const { toast } = useToast();

  if (loading) return <LoadingBlock rows={6} />;
  if (error || !data) return <ErrorBlock msg={error ?? "خطا"} onRetry={reload} />;

  const k = data.kpis;
  const trend = data.attendanceTrend.map((t) => ({ ...t, label: toFa(t.day) }));

  const resolveAlert = async (id: string) => {
    try {
      await api("/api/alerts", { method: "PATCH", body: JSON.stringify({ id, resolved: true }) });
      toast({ title: "هشدار رسیدگی شد" });
      reload();
    } catch (e) {
      toast({ title: e instanceof Error ? e.message : "خطا", variant: "destructive" });
    }
  };

  return (
    <div className="space-y-5">
      {/* KPI row 1 */}
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <StatCard icon={Users} label="پرسنل فعال" value={k.activeEmployees} sub={`از مجموع ${toFa(k.totalEmployees)} نفر`} tone="default" />
        <StatCard icon={UserCheck} label="حاضران امروز" value={k.presentToday} sub={`${toFa(k.lateToday)} نفر تأخیر`} tone="green" />
        <StatCard icon={DoorOpen} label="مراجعین داخل سازمان" value={k.visitorsInside} sub={`${toFa(k.pendingVisitorApprovals)} در انتظار تأیید`} tone="violet" />
        <StatCard icon={ShieldAlert} label="هشدارهای باز حراست" value={k.unresolvedAlerts} tone={k.unresolvedAlerts > 5 ? "red" : "amber"} />
      </div>

      {/* KPI row 2 */}
      <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
        <StatCard icon={UserX} label="غایب امروز" value={k.absentToday} tone={k.absentToday ? "red" : "default"} />
        <StatCard icon={CalendarX2} label="مرخصی امروز" value={k.onLeaveToday} tone="violet" />
        <StatCard icon={Timer} label="درخواست‌های در انتظار" value={k.pendingRequests} tone="amber" />
        <StatCard icon={Server} label="دستگاه‌های آنلاین" value={`${k.devicesOnline}/${k.devicesTotal}`} tone={k.devicesOnline === k.devicesTotal ? "green" : "amber"} />
        <StatCard icon={History} label="گیت‌های سالم" value={`${k.gatesOk}/${k.gatesTotal}`} tone={k.gatesOk === k.gatesTotal ? "green" : "amber"} />
        <StatCard icon={LogIn} label="رویدادهای اخیر" value={data.recentEvents.length} />
      </div>

      {/* Charts */}
      <div className="grid lg:grid-cols-3 gap-4">
        <Section title="روند حضور و غیاب (۷ روز اخیر)" className="lg:col-span-2" contentClass="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <AreaChart data={trend} margin={{ top: 5, right: 5, left: 5, bottom: 0 }}>
              <defs>
                <linearGradient id="gP" x1="0" y1="0" x2="0" y2="1">
                  <stop offset="0%" stopColor="#0f766e" stopOpacity={0.35} />
                  <stop offset="100%" stopColor="#0f766e" stopOpacity={0.02} />
                </linearGradient>
              </defs>
              <CartesianGrid strokeDasharray="3 3" stroke="currentColor" opacity={0.08} />
              <XAxis dataKey="label" tick={{ fontSize: 11 }} reversed />
              <YAxis tick={{ fontSize: 11 }} width={28} allowDecimals={false} orientation="right" />
              <Tooltip
                contentStyle={{ borderRadius: 10, direction: "rtl", fontFamily: "Vazirmatn", fontSize: 12 }}
                formatter={(v: number, n) => [toFa(v), n === "present" ? "حاضر" : n === "late" ? "تأخیر" : n === "leave" ? "مرخصی" : "غایب"]}
              />
              <Area type="monotone" dataKey="present" stroke="#0f766e" fill="url(#gP)" strokeWidth={2} />
              <Area type="monotone" dataKey="late" stroke="#f59e0b" fill="transparent" strokeWidth={1.5} />
              <Area type="monotone" dataKey="leave" stroke="#8b5cf6" fill="transparent" strokeWidth={1.5} />
              <Area type="monotone" dataKey="absent" stroke="#f43f5e" fill="transparent" strokeWidth={1.5} />
            </AreaChart>
          </ResponsiveContainer>
        </Section>

        <Section title="ترکیب نوع استخدام" contentClass="h-64">
          <ResponsiveContainer width="100%" height="100%">
            <PieChart>
              <Pie
                data={data.employmentTypes}
                dataKey="value"
                nameKey="name"
                innerRadius="52%"
                outerRadius="80%"
                paddingAngle={3}
                label={(p: { name?: string; value?: number }) => (p.name ? `${p.name} ${toFa(p.value ?? "")}` : "")}
                labelLine={false}
                fontSize={11}
              >
                {data.employmentTypes.map((_, i) => (
                  <Cell key={i} fill={PIE_COLORS[i % PIE_COLORS.length]} />
                ))}
              </Pie>
              <Tooltip contentStyle={{ borderRadius: 10, direction: "rtl", fontFamily: "Vazirmatn", fontSize: 12 }} formatter={(v: number) => toFa(v)} />
            </PieChart>
          </ResponsiveContainer>
        </Section>
      </div>

      <div className="grid lg:grid-cols-2 gap-4">
        <Section title="پرسنل به تفکیک واحد سازمانی" contentClass="h-60">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={data.departmentStats} layout="vertical" margin={{ top: 0, right: 10, left: 10, bottom: 0 }}>
              <CartesianGrid strokeDasharray="3 3" stroke="currentColor" opacity={0.08} horizontal={false} />
              <XAxis type="number" tick={{ fontSize: 11 }} allowDecimals={false} />
              <YAxis type="category" dataKey="name" tick={{ fontSize: 10.5 }} width={130} orientation="right" />
              <Tooltip contentStyle={{ borderRadius: 10, direction: "rtl", fontFamily: "Vazirmatn", fontSize: 12 }} formatter={(v: number) => [toFa(v) + " نفر", "پرسنل"]} />
              <Bar dataKey="value" fill="#0f766e" radius={[0, 6, 6, 0]} barSize={14} />
            </BarChart>
          </ResponsiveContainer>
        </Section>

        {/* Alerts */}
        <Section
          title="هشدارهای حراست"
          icon={FileWarning}
          contentClass="max-h-60 overflow-y-auto thin-scroll space-y-2"
        >
          {data.alerts.length === 0 ? (
            <EmptyState text="هشدار بازی وجود ندارد" />
          ) : (
            data.alerts.map((a) => (
              <div key={a.id} className="flex items-start gap-2 rounded-lg border p-2.5 bg-card">
                <StatusBadge value={a.severity} />
                <div className="flex-1 min-w-0">
                  <p className="text-xs leading-relaxed">{a.message}</p>
                  <p className="text-[10px] text-muted-foreground mt-1 num-fa">
                    {a.type} • {a.source} • {formatTime(a.createdAt)}
                  </p>
                </div>
                <Button size="sm" variant="outline" className="h-7 text-[11px] shrink-0" onClick={() => resolveAlert(a.id)}>
                  رسیدگی
                </Button>
              </div>
            ))
          )}
        </Section>
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        {/* Live gate feed */}
        <Section title="آخرین رویدادهای تردد" className="lg:col-span-2" contentClass="max-h-72 overflow-y-auto thin-scroll">
          <div className="space-y-1.5">
            {data.recentEvents.map((ev) => (
              <div key={ev.id} className="flex items-center gap-2 rounded-lg border p-2 text-xs">
                <span className={`h-2 w-2 rounded-full shrink-0 ${ev.result === "مجاز" ? "bg-emerald-500" : "bg-rose-500 live-dot"}`} />
                <span className="font-medium shrink-0">
                  {ev.employee ? `${ev.employee.firstName} ${ev.employee.lastName}` : ev.visitor ? `${ev.visitor.firstName} ${ev.visitor.lastName}` : "نامشخص"}
                </span>
                <span className="text-muted-foreground truncate">{ev.gate?.name} — {ev.direction} ({ev.method})</span>
                <span className="mr-auto flex items-center gap-2 shrink-0">
                  <StatusBadge value={ev.result} />
                  <span className="text-[10px] text-muted-foreground num-fa">{formatTime(ev.timestamp)}</span>
                </span>
              </div>
            ))}
          </div>
        </Section>

        {/* Inside visitors */}
        <Section title="مهمانان حاضر در سازمان" contentClass="max-h-72 overflow-y-auto thin-scroll space-y-2">
          {data.insideVisitors.length === 0 ? (
            <EmptyState text="مهمانی داخل سازمان نیست" />
          ) : (
            data.insideVisitors.map((v) => (
              <div key={v.id} className="rounded-lg border p-2.5 text-xs space-y-1">
                <div className="flex items-center justify-between gap-2">
                  <span className="font-medium">{v.firstName} {v.lastName}</span>
                  <StatusBadge value={v.card?.returned ? "خارج شده" : "داخل سازمان"} />
                </div>
                <p className="text-muted-foreground">میزبان: {v.hostName} — {v.purpose}</p>
                {v.card && !v.card.returned && (
                  <p className="num-fa text-[10px] text-muted-foreground">
                    کارت موقت: {toFa(v.card.cardNo)} ({v.card.type})
                  </p>
                )}
              </div>
            ))
          )}
        </Section>
      </div>

      {/* Expiring contracts */}
      <Section title="قراردادهای در حال انقضا (۳۰ روز آینده)" contentClass="p-0">
        {data.contractExpiring.length === 0 ? (
          <div className="p-4"><EmptyState text="قراردادی در حال انقضا نیست" /></div>
        ) : (
          <div className="overflow-x-auto thin-scroll">
            <table className="w-full text-xs">
              <thead>
                <tr className="text-muted-foreground border-b bg-muted/40">
                  <th className="p-2.5 text-right font-medium">پرسنل</th>
                  <th className="p-2.5 text-right font-medium">شماره قرارداد</th>
                  <th className="p-2.5 text-right font-medium">نوع</th>
                  <th className="p-2.5 text-right font-medium">تاریخ پایان</th>
                  <th className="p-2.5 text-right font-medium">حقوق ماهانه</th>
                </tr>
              </thead>
              <tbody>
                {data.contractExpiring.map((c) => {
                  const days = c.endDate ? Math.ceil((new Date(c.endDate).getTime() - Date.now()) / 86400000) : null;
                  return (
                    <tr key={c.id} className="border-b last:border-0 hover:bg-muted/30">
                      <td className="p-2.5 font-medium">{c.employee?.firstName} {c.employee?.lastName}</td>
                      <td className="p-2.5 num-fa">{toFa(c.contractNo)}</td>
                      <td className="p-2.5"><StatusBadge value={c.type} /></td>
                      <td className="p-2.5 num-fa">
                        {formatJalaliLong(c.endDate)}
                        {days !== null && <span className={`mr-2 text-[10px] ${days <= 15 ? "text-rose-600 font-bold" : "text-amber-600"}`}>({toFa(days)} روز مانده)</span>}
                      </td>
                      <td className="p-2.5 num-fa">{formatNumber(c.salary)}</td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
      </Section>
    </div>
  );
}
