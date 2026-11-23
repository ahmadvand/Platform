"use client";

import { useCallback, useEffect, useState } from "react";
import {
  MonitorCog, Wifi, WifiOff, RefreshCw, LockOpen, Timer, RotateCcw, UserPlus, UserMinus,
  KeyRound, Send, Radio, ListChecks, Loader2, Upload, X,
} from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { api, errToast, useApiData, StatusBadge, Section, LoadingBlock, EmptyState } from "@/components/shared";
import { toast } from "@/hooks/use-toast";
import { toFa, formatJalali, formatTime } from "@/lib/persian";

interface DeviceRow {
  id: string;
  name: string;
  brand: string;
  serial: string;
  ip: string | null;
  location: string | null;
  purpose: string;
  mode: string;
  active: boolean;
  pollingInterval: number;
  lastSeenAt: string | null;
  lastPulledAt: string | null;
  firmware: string | null;
  userCount: number | null;
  online: boolean;
  pendingCommands: number;
}

interface CommandRow {
  id: string;
  deviceId: string;
  type: string;
  status: string;
  createdAt: string;
  ackedAt: string | null;
  result: string | null;
  createdBy: string | null;
  payload: string | null;
  device?: { name: string };
}

const CMD_LABEL: Record<string, string> = {
  unlock: "بازکردن درب", sync_time: "همگام‌سازی ساعت", restart: "ری‌استارت",
  push_user: "ارسال کاربر", delete_user: "حذف کاربر", get_user: "دریافت کاربر",
  refresh_data: "به‌روزرسانی داده", clear_data: "پاک‌سازی داده",
  enable_device: "فعال‌سازی", disable_device: "غیرفعال‌سازی",
};

const STATUS_FA: Record<string, string> = {
  pending: "در انتظار", delivered: "تحویل‌شده", acked: "اجرا شده", failed: "ناموفق",
};

export function DevicesView() {
  const { data, loading, reload } = useApiData<{ devices: DeviceRow[] }>("/api/devices", 15000);
  const commands = useApiData<{ commands: CommandRow[] }>("/api/devices/commands?take=50", 10000).data;
  const [busy, setBusy] = useState<string | null>(null);
  const [transferOpen, setTransferOpen] = useState(false);
  const employees = useApiData<{ employees: { id: string; firstName: string; lastName: string; personnelCode: string }[] }>("/api/employees").data;

  const doCommand = useCallback(async (device: DeviceRow, path: string, label: string, body?: unknown) => {
    setBusy(device.id + path);
    try {
      await api(`/api/devices/${device.id}/${path}`, { method: "POST", body: body ? JSON.stringify(body) : undefined });
      toast({ title: "دستور ارسال شد", description: `${label} برای «${device.name}» در صف اجرا قرار گرفت` });
      reload();
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(null);
    }
  }, [reload]);

  const devices = data?.devices ?? [];
  const online = devices.filter((d) => d.online).length;

  return (
    <div className="space-y-4 min-w-0">
      {/* خلاصه وضعیت */}
      <div className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        <Card className="shadow-sm"><CardContent className="p-4 flex items-center gap-3">
          <div className="rounded-lg p-2.5 bg-primary/10 text-primary"><MonitorCog className="h-5 w-5" /></div>
          <div><div className="text-xl font-bold num-fa">{toFa(devices.length)}</div><div className="text-xs text-muted-foreground">کل دستگاه‌ها</div></div>
        </CardContent></Card>
        <Card className="shadow-sm"><CardContent className="p-4 flex items-center gap-3">
          <div className="rounded-lg p-2.5 bg-emerald-500/10 text-emerald-600"><Wifi className="h-5 w-5" /></div>
          <div><div className="text-xl font-bold num-fa">{toFa(online)}</div><div className="text-xs text-muted-foreground">آنلاین</div></div>
        </CardContent></Card>
        <Card className="shadow-sm"><CardContent className="p-4 flex items-center gap-3">
          <div className="rounded-lg p-2.5 bg-rose-500/10 text-rose-600"><WifiOff className="h-5 w-5" /></div>
          <div><div className="text-xl font-bold num-fa">{toFa(devices.length - online)}</div><div className="text-xs text-muted-foreground">آفلاین</div></div>
        </CardContent></Card>
        <Card className="shadow-sm"><CardContent className="p-4 flex items-center gap-3">
          <div className="rounded-lg p-2.5 bg-amber-500/10 text-amber-600"><ListChecks className="h-5 w-5" /></div>
          <div><div className="text-xl font-bold num-fa">{toFa(devices.reduce((s, d) => s + d.pendingCommands, 0))}</div><div className="text-xs text-muted-foreground">دستور در صف</div></div>
        </CardContent></Card>
      </div>

      {/* کارت دستگاه‌ها */}
      {loading ? <LoadingBlock rows={4} /> : (
        <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-4">
          {devices.map((d) => (
            <Card key={d.id} className="shadow-sm overflow-hidden min-w-0">
              <CardContent className="p-4 space-y-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <div className="font-semibold text-sm truncate">{d.name}</div>
                    <div dir="ltr" className="text-[10px] text-muted-foreground font-mono text-right">{d.serial}</div>
                  </div>
                  {d.online ? (
                    <Badge className="bg-emerald-500/15 text-emerald-700 border-emerald-500/30 shrink-0"><Wifi className="h-3 w-3 mr-1" />آنلاین</Badge>
                  ) : (
                    <Badge className="bg-rose-500/15 text-rose-700 border-rose-500/30 shrink-0"><WifiOff className="h-3 w-3 mr-1" />آفلاین</Badge>
                  )}
                </div>
                <div className="grid grid-cols-2 gap-1.5 text-[11px] text-muted-foreground">
                  <span>برند: <b className="text-foreground">{d.brand}</b></span>
                  <span dir="ltr" className="text-left font-mono">{d.ip ?? "—"}</span>
                  <span>مکان: {d.location ?? "—"}</span>
                  <span className="num-fa">آخرین اتصال: {d.lastSeenAt ? formatTime(d.lastSeenAt) : "—"}</span>
                  <span className="num-fa">کاربران: {toFa(d.userCount ?? "—")}</span>
                  <span className="num-fa">صف: {toFa(d.pendingCommands)} دستور</span>
                </div>
                {!d.active && <Badge variant="outline" className="text-[10px]">غیرفعال</Badge>}

                {/* دکمه‌های عملیات (PLAN P2.4) */}
                <div className="grid grid-cols-4 gap-1.5 pt-1">
                  {[
                    { path: "unlock", icon: LockOpen, label: "بازکردن درب", title: "بازکردن درب" },
                    { path: "sync-time", icon: Timer, label: "تنظیم ساعت", title: "همگام‌سازی ساعت" },
                    { path: "refresh-data", icon: RefreshCw, label: "به‌روزرسانی", title: "دریافت داده‌ها از دستگاه" },
                    { path: "restart", icon: RotateCcw, label: "ری‌استارت", title: "ری‌استارت دستگاه" },
                  ].map((b) => (
                    <Button
                      key={b.path}
                      variant="outline"
                      size="sm"
                      title={b.title}
                      disabled={busy === d.id + b.path || !d.active}
                      onClick={() => doCommand(d, b.path, b.label)}
                      className="h-8 flex-col gap-0.5 text-[9px] px-1"
                    >
                      {busy === d.id + b.path ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <b.icon className="h-3.5 w-3.5" />}
                      {b.label}
                    </Button>
                  ))}
                </div>
                <div className="grid grid-cols-2 gap-1.5">
                  <Button variant="outline" size="sm" className="h-8 text-[11px]" onClick={() => setTransferOpen(true)}>
                    <UserPlus className="h-3.5 w-3.5" /> انتقال کاربر
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-8 text-[11px]"
                    title="چرخش توکن امنیتی دستگاه"
                    onClick={() => doCommand(d, "rotate-secret", "چرخش توکن")}
                  >
                    <KeyRound className="h-3.5 w-3.5" /> چرخش توکن
                  </Button>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {/* گزارش دستورات اخیر */}
      <Section title="گزارش دستورات دستگاه‌ها" icon={Radio} contentClass="max-h-96 overflow-y-auto thin-scroll">
        {commands?.commands?.length ? (
          <div className="space-y-1.5">
            {commands.commands.map((c) => (
              <div key={c.id} className="flex items-center gap-2 text-xs border rounded-lg px-3 py-2">
                <StatusBadge value={STATUS_FA[c.status] ?? c.status} />
                <span className="font-medium">{CMD_LABEL[c.type] ?? c.type}</span>
                <span className="text-muted-foreground num-fa">{c.device?.name ?? ""}</span>
                <span className="flex-1" />
                {c.result && <span className="text-[10px] text-muted-foreground truncate max-w-48">{c.result}</span>}
                <span className="num-fa text-[10px] text-muted-foreground">{formatJalali(c.createdAt)} {formatTime(c.createdAt)}</span>
              </div>
            ))}
          </div>
        ) : (
          <EmptyState text="هنوز دستوری صادر نشده است" />
        )}
      </Section>

      <TransferDialog
        open={transferOpen}
        onOpenChange={setTransferOpen}
        devices={devices}
        employees={employees?.employees ?? []}
        onDone={() => { reload(); }}
      />
    </div>
  );
}

// ---------- دیالوگ انتقال کاربر بین دستگاه‌ها ----------
function TransferDialog({
  open, onOpenChange, devices, employees, onDone,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  devices: DeviceRow[];
  employees: { id: string; firstName: string; lastName: string; personnelCode: string }[];
  onDone: () => void;
}) {
  const [employeeId, setEmployeeId] = useState<string>("");
  const [targets, setTargets] = useState<string[]>([]);
  const [allTargets, setAllTargets] = useState(false);
  const [busy, setBusy] = useState(false);

  const toggle = (id: string) =>
    setTargets((t) => (t.includes(id) ? t.filter((x) => x !== id) : [...t, id]));

  const submit = async () => {
    if (!employeeId) return;
    setBusy(true);
    try {
      const r = await api<{ queued: number; detail: { pushTo: string[] } }>("/api/devices/transfer-user", {
        method: "POST",
        body: JSON.stringify({ employeeId, targetDeviceIds: targets, allTargets }),
      });
      toast({
        title: "انتقال کاربر در صف قرار گرفت",
        description: `${toFa(r.queued)} دستور برای ${r.detail.pushTo.length} دستگاه ثبت شد — دستگاه‌ها در اولین اتصال اجرا می‌کنند`,
      });
      onOpenChange(false);
      setEmployeeId("");
      setTargets([]);
      setAllTargets(false);
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg max-h-[85vh] overflow-y-auto thin-scroll">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2 text-base">
            <Send className="h-4 w-4 text-primary" /> انتقال کاربر بین دستگاه‌های تردد
          </DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-xs font-medium">انتخاب پرسنل</label>
            <Select value={employeeId} onValueChange={setEmployeeId}>
              <SelectTrigger><SelectValue placeholder="پرسنل مورد نظر..." /></SelectTrigger>
              <SelectContent className="max-h-60">
                {employees.map((e) => (
                  <SelectItem key={e.id} value={e.id}>
                    {e.firstName} {e.lastName} ({e.personnelCode})
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <label className="text-xs font-medium">دستگاه‌های مقصد</label>
              <Button variant="ghost" size="sm" className="h-7 text-[11px]" onClick={() => setAllTargets((v) => !v)}>
                {allTargets ? "انتخاب دستی" : "همه دستگاه‌ها"}
              </Button>
            </div>
            {!allTargets && (
              <div className="grid grid-cols-2 gap-1.5 max-h-40 overflow-y-auto thin-scroll border rounded-lg p-2">
                {devices.map((d) => (
                  <button
                    key={d.id}
                    type="button"
                    onClick={() => toggle(d.id)}
                    className={`text-[11px] text-right rounded-md border px-2 py-1.5 transition-colors ${
                      targets.includes(d.id) ? "bg-primary/10 border-primary/50 font-medium" : "hover:bg-muted"
                    }`}
                  >
                    {d.name}
                  </button>
                ))}
              </div>
            )}
            {allTargets && (
              <p className="text-[11px] text-muted-foreground border rounded-lg px-3 py-2 bg-muted/50">
                کاربر به همه {toFa(devices.length)} دستگاه فعال ارسال می‌شود
              </p>
            )}
          </div>

          <div className="flex gap-2 justify-end">
            <Button variant="ghost" size="sm" onClick={() => onOpenChange(false)}><X className="h-4 w-4" /> انصراف</Button>
            <Button size="sm" onClick={submit} disabled={!employeeId || busy || (!allTargets && targets.length === 0)}>
              {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Upload className="h-4 w-4" />}
              ارسال به دستگاه‌ها
            </Button>
          </div>
          <p className="text-[10px] text-muted-foreground">
            دستورات push_user در صف قرار می‌گیرند و سرویس پل دستگاه‌ها هنگام اتصال دستگاه آن‌ها را اجرا می‌کند.
          </p>
        </div>
      </DialogContent>
    </Dialog>
  );
}
