"use client";

import { useState } from "react";
import {
  ScanFace, ShieldCheck, DoorOpen, Plus, MapPin, Lock, Unlock, AlertTriangle, Radio,
} from "lucide-react";
import {
  useApiData, api, errToast, StatusBadge, Section, EmptyState, LoadingBlock, StatCard,
} from "@/components/shared";
import { formatJalali, formatTime, toFa } from "@/lib/persian";
import type { AccessPermT, AccessEventT, ZoneT, EmployeeList } from "@/lib/api-types";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { useToast } from "@/hooks/use-toast";
import { cn } from "@/lib/utils";

const WEEKDAY_LABELS: Record<string, string> = {
  "0": "یکشنبه", "1": "دوشنبه", "2": "سه‌شنبه", "3": "چهارشنبه", "4": "پنجشنبه", "5": "جمعه", "6": "شنبه",
};

export function AccessView() {
  const perms = useApiData<{ permissions: AccessPermT[] }>("/api/access");
  const events = useApiData<{ events: AccessEventT[] }>("/api/access", 12000);
  void perms;

  return (
    <Tabs defaultValue="live" className="space-y-4" dir="rtl">
      <TabsList>
        <TabsTrigger value="live" className="gap-1.5"><Radio className="h-4 w-4" />مانیتورینگ زنده</TabsTrigger>
        <TabsTrigger value="zones" className="gap-1.5"><MapPin className="h-4 w-4" />مناطق و گیت‌ها</TabsTrigger>
        <TabsTrigger value="perms" className="gap-1.5"><ShieldCheck className="h-4 w-4" />مجوزهای دسترسی</TabsTrigger>
        <TabsTrigger value="events" className="gap-1.5"><DoorOpen className="h-4 w-4" />گزارش رویدادها</TabsTrigger>
      </TabsList>

      <TabsContent value="live"><LiveMonitor /></TabsContent>
      <TabsContent value="zones"><ZonesPanel /></TabsContent>
      <TabsContent value="perms"><PermsPanel /></TabsContent>
      <TabsContent value="events"><EventsPanel /></TabsContent>
    </Tabs>
  );
}

// ================= Live monitoring + scan simulator =================
function LiveMonitor() {
  const { data, loading, reload } = useApiData<{ events: AccessEventT[] }>("/api/access", 8000);
  const events = data?.events ?? [];
  const denied = events.filter((e) => e.result === "غیرمجاز").length;

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
        <StatCard icon={DoorOpen} label="رویدادهای اخیر" value={events.length} />
        <StatCard icon={Unlock} label="عبور مجاز" value={events.length - denied} tone="green" />
        <StatCard icon={Lock} label="تلاش غیرمجاز" value={denied} tone={denied ? "red" : "default"} />
        <StatCard icon={AlertTriangle} label="نرخ تخلف" value={`${toFa(events.length ? Math.round((denied / events.length) * 100) : 0)}٪`} tone={denied ? "amber" : "default"} />
      </div>

      <div className="grid lg:grid-cols-3 gap-4">
        <GateSimulator onDone={reload} />

        <Section
          title="فید زنده رویدادهای تردد (AC-05)"
          action={<span className="flex items-center gap-1.5 text-[11px] text-emerald-600"><span className="h-2 w-2 rounded-full bg-emerald-500 live-dot" />زنده</span>}
          className="lg:col-span-2"
          contentClass="max-h-[480px] overflow-y-auto thin-scroll space-y-1.5"
        >
          {loading ? <LoadingBlock /> : events.length === 0 ? <EmptyState text="رویدادی ثبت نشده" /> : events.map((ev) => (
            <div
              key={ev.id}
              className={cn(
                "flex items-center gap-2 rounded-lg border p-2.5 text-xs",
                ev.result === "غیرمجاز" && "border-rose-500/30 bg-rose-500/5"
              )}
            >
              <span className={`h-2.5 w-2.5 rounded-full shrink-0 ${ev.result === "مجاز" ? "bg-emerald-500" : "bg-rose-500 live-dot"}`} />
              <div className="min-w-0 flex-1">
                <span className="font-medium">
                  {ev.employee ? `${ev.employee.firstName} ${ev.employee.lastName}` : ev.visitor ? `${ev.visitor.firstName} ${ev.visitor.lastName}` : "نامشخص"}
                </span>
                <span className="text-muted-foreground"> — {ev.gate?.name} {ev.gate?.zone ? `(${ev.gate.zone.name})` : ""}</span>
                {ev.reason && <span className="text-rose-600"> — {ev.reason}</span>}
              </div>
              <Badge variant="outline" className="text-[10px] shrink-0">{ev.direction} • {ev.method}</Badge>
              <StatusBadge value={ev.result} />
              <span className="num-fa text-[10px] text-muted-foreground shrink-0 w-10">{formatTime(ev.timestamp)}</span>
            </div>
          ))}
        </Section>
      </div>
    </div>
  );
}

function GateSimulator({ onDone }: { onDone: () => void }) {
  const [employeeId, setEmployeeId] = useState("");
  const [gateId, setGateId] = useState("");
  const [direction, setDirection] = useState("ورود");
  const [method, setMethod] = useState("کارت");
  const [result, setResult] = useState<{ allowed: boolean; reason: string | null; who: string } | null>(null);
  const [busy, setBusy] = useState(false);
  const { toast } = useToast();
  const { data: empData } = useApiData<{ employees: EmployeeList[] }>("/api/employees");
  const { data: zoneData } = useApiData<{ zones: ZoneT[] }>("/api/zones");

  const gates = (zoneData?.zones ?? []).flatMap((z) => z.gates.map((g) => ({ ...g, zoneName: z.name })));

  const scan = async () => {
    if (!employeeId || !gateId) return errToast(new Error("پرسنل و گیت را انتخاب کنید"));
    setBusy(true);
    setResult(null);
    try {
      const r = await api<{ allowed: boolean; reason: string | null; event: AccessEventT }>("/api/access", {
        method: "PUT",
        body: JSON.stringify({ employeeId, gateId, direction, method }),
      });
      const emp = empData?.employees.find((e) => e.id === employeeId);
      setResult({ allowed: r.allowed, reason: r.reason, who: `${emp?.firstName ?? ""} ${emp?.lastName ?? ""}` });
      onDone();
      if (!r.allowed) toast({ title: "هشدار: عبور غیرمجاز", description: r.reason ?? "", variant: "destructive" });
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Section title="شبیه‌ساز عبور از گیت" icon={ScanFace} contentClass="space-y-3">
      <div className="space-y-1.5">
        <Label>پرسنل</Label>
        <Select value={employeeId || undefined} onValueChange={setEmployeeId}>
          <SelectTrigger><SelectValue placeholder="انتخاب پرسنل..." /></SelectTrigger>
          <SelectContent className="max-h-56">
            {(empData?.employees ?? []).filter((e) => e.status === "فعال").map((e) => (
              <SelectItem key={e.id} value={e.id}>{e.firstName} {e.lastName} — {toFa(e.personnelCode)}</SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="space-y-1.5">
        <Label>گیت / نقطه تردد</Label>
        <Select value={gateId || undefined} onValueChange={setGateId}>
          <SelectTrigger><SelectValue placeholder="انتخاب گیت..." /></SelectTrigger>
          <SelectContent className="max-h-56">
            {gates.map((g) => (
              <SelectItem key={g.id} value={g.id} disabled={g.status === "خراب"}>
                {g.name} — {g.zoneName} {g.status !== "فعال" ? `(خراب)` : ""}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="grid grid-cols-2 gap-2">
        <div className="space-y-1.5">
          <Label>جهت</Label>
          <Select value={direction} onValueChange={setDirection}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="ورود">ورود</SelectItem>
              <SelectItem value="خروج">خروج</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>روش احراز</Label>
          <Select value={method} onValueChange={setMethod}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {["کارت", "اثر انگشت", "چهره", "QR", "RFID"].map((m) => <SelectItem key={m} value={m}>{m}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>
      <Button onClick={scan} disabled={busy} className="w-full gap-1.5">
        <ScanFace className="h-4 w-4" />{busy ? "در حال بررسی..." : "ثبت عبور"}
      </Button>

      {result && (
        <div className={cn(
          "rounded-xl border-2 p-3.5 text-center space-y-1.5 animate-in fade-in",
          result.allowed ? "border-emerald-500/50 bg-emerald-500/10" : "border-rose-500/50 bg-rose-500/10"
        )}>
          {result.allowed ? <Unlock className="h-8 w-8 mx-auto text-emerald-600" /> : <Lock className="h-8 w-8 mx-auto text-rose-600" />}
          <div className={cn("font-bold", result.allowed ? "text-emerald-700 dark:text-emerald-300" : "text-rose-700 dark:text-rose-300")}>
            {result.allowed ? "دسترسی مجاز" : "دسترسی غیرمجاز"}
          </div>
          <p className="text-xs text-muted-foreground">{result.who}</p>
          {result.reason && <p className="text-xs text-rose-600">{result.reason}</p>}
        </div>
      )}
    </Section>
  );
}

// ================= Zones & gates =================
function ZonesPanel() {
  const { data, loading, reload } = useApiData<{ zones: ZoneT[] }>("/api/zones");
  const [zoneOpen, setZoneOpen] = useState(false);
  const [gateOpen, setGateOpen] = useState(false);
  const { toast } = useToast();
  const zones = data?.zones ?? [];

  const setGateStatus = async (id: string, status: string) => {
    try {
      await api("/api/zones", { method: "PATCH", body: JSON.stringify({ kind: "gate", id, status }) });
      toast({ title: "وضعیت گیت تغییر کرد" });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  return (
    <div className="space-y-4">
      <div className="flex gap-2">
        <Dialog open={zoneOpen} onOpenChange={setZoneOpen}>
          <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />منطقه جدید</Button></DialogTrigger>
          <DialogContent className="max-w-md">
            <ZoneForm onDone={() => { setZoneOpen(false); reload(); toast({ title: "منطقه ایجاد شد" }); }} />
          </DialogContent>
        </Dialog>
        <Dialog open={gateOpen} onOpenChange={setGateOpen}>
          <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />گیت جدید</Button></DialogTrigger>
          <DialogContent className="max-w-md">
            <GateForm zones={zones} onDone={() => { setGateOpen(false); reload(); toast({ title: "گیت ایجاد شد" }); }} />
          </DialogContent>
        </Dialog>
      </div>

      {loading ? <LoadingBlock /> : (
        <div className="grid md:grid-cols-2 xl:grid-cols-3 gap-3">
          {zones.map((z) => (
            <Section key={z.id} title={z.name} icon={MapPin} contentClass="space-y-2.5">
              <div className="flex items-center gap-2 flex-wrap">
                <StatusBadge value={z.riskLevel} />
                <span className="num-fa text-[10px] text-muted-foreground">({toFa(z.code)})</span>
                <Badge variant="secondary" className="num-fa text-[10px] mr-auto">{toFa(z.accessPerms?.length ?? 0)} مجوز</Badge>
              </div>
              {z.description && <p className="text-xs text-muted-foreground">{z.description}</p>}
              <div className="space-y-1.5">
                {z.gates.length === 0 ? <p className="text-xs text-muted-foreground">گیت ثبت نشده</p> : z.gates.map((g) => (
                  <div key={g.id} className="rounded-lg border p-2 text-xs space-y-1.5">
                    <div className="flex items-center justify-between gap-2">
                      <span className="font-medium">{g.name}</span>
                      <StatusBadge value={g.status} />
                    </div>
                    <div className="flex items-center justify-between text-[10px] text-muted-foreground num-fa">
                      <span>{g.type} • {g.direction}</span>
                      <span>{g.device ? `${g.device.brand}` : "بدون دستگاه"}</span>
                    </div>
                    <div className="flex gap-1">
                      <Button size="sm" variant="outline" className="h-6 text-[10px] px-2" onClick={() => setGateStatus(g.id, "فعال")}>فعال</Button>
                      <Button size="sm" variant="outline" className="h-6 text-[10px] px-2" onClick={() => setGateStatus(g.id, "غیرفعال")}>غیرفعال</Button>
                      <Button size="sm" variant="outline" className="h-6 text-[10px] px-2 text-rose-700 border-rose-500/40" onClick={() => setGateStatus(g.id, "خراب")}>خراب</Button>
                    </div>
                  </div>
                ))}
              </div>
            </Section>
          ))}
        </div>
      )}
    </div>
  );
}

function ZoneForm({ onDone }: { onDone: () => void }) {
  const [form, setForm] = useState({ name: "", code: "", riskLevel: "عادی", description: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/zones", { method: "POST", body: JSON.stringify({ kind: "zone", ...form }) });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <DialogHeader><DialogTitle>تعریف منطقه (AC-01)</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <div className="space-y-1.5"><Label>نام منطقه *</Label><Input value={form.name} onChange={(e) => set("name", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد *</Label><Input dir="ltr" className="text-right" value={form.code} onChange={(e) => set("code", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>سطح حساسیت</Label>
          <Select value={form.riskLevel} onValueChange={(v) => set("riskLevel", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {["عادی", "حساس", "فوق‌حساس"].map((r) => <SelectItem key={r} value={r}>{r}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5"><Label>توضیح</Label><Input value={form.description} onChange={(e) => set("description", e.target.value)} /></div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "..." : "ایجاد"}</Button></DialogFooter>
    </div>
  );
}

function GateForm({ zones, onDone }: { zones: ZoneT[]; onDone: () => void }) {
  const [form, setForm] = useState({ name: "", code: "", zoneId: "", type: "درب برقی", direction: "دوطرفه" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/zones", { method: "POST", body: JSON.stringify({ kind: "gate", ...form }) });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <DialogHeader><DialogTitle>تعریف گیت (AC-01)</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <div className="space-y-1.5"><Label>نام گیت *</Label><Input value={form.name} onChange={(e) => set("name", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد *</Label><Input dir="ltr" className="text-right" value={form.code} onChange={(e) => set("code", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>منطقه *</Label>
          <Select value={form.zoneId || undefined} onValueChange={(v) => set("zoneId", v)}>
            <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
            <SelectContent>{zones.map((z) => <SelectItem key={z.id} value={z.id}>{z.name}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5">
            <Label>نوع</Label>
            <Select value={form.type} onValueChange={(v) => set("type", v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {["گیت میله‌ای", "راهبند", "درب برقی", "ورودی‌گردان"].map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>جهت</Label>
            <Select value={form.direction} onValueChange={(v) => set("direction", v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {["ورودی", "خروجی", "دوطرفه"].map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "..." : "ایجاد"}</Button></DialogFooter>
    </div>
  );
}

// ================= Permissions =================
function PermsPanel() {
  const { data, loading, reload } = useApiData<{ permissions: AccessPermT[] }>("/api/access");
  const [open, setOpen] = useState(false);
  const { toast } = useToast();
  const permissions = data?.permissions ?? [];

  const revoke = async (id: string) => {
    try {
      await api("/api/access", { method: "PATCH", body: JSON.stringify({ id, status: "لغوشده" }) });
      toast({ title: "مجوز لغو شد" });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  return (
    <Section
      title="مجوزهای دسترسی (AC-02)"
      action={
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />صدور مجوز</Button></DialogTrigger>
          <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto thin-scroll">
            <PermForm zones={[]} onDone={() => { setOpen(false); reload(); toast({ title: "مجوز دسترسی صادر شد" }); }} />
          </DialogContent>
        </Dialog>
      }
      contentClass="p-0"
    >
      {loading ? <div className="p-4"><LoadingBlock /></div> : permissions.length === 0 ? (
        <div className="p-4"><EmptyState text="مجوزی صادر نشده است" /></div>
      ) : (
        <div className="overflow-x-auto thin-scroll">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/40">
                <TableHead className="text-right">پرسنل</TableHead>
                <TableHead className="text-right">منطقه</TableHead>
                <TableHead className="text-right">نوع</TableHead>
                <TableHead className="text-right">اعتبار</TableHead>
                <TableHead className="text-right">روزها</TableHead>
                <TableHead className="text-right">ساعات</TableHead>
                <TableHead className="text-right">وضعیت</TableHead>
                <TableHead className="text-right">عملیات</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {permissions.map((p) => (
                <TableRow key={p.id} className="hover:bg-muted/30">
                  <TableCell className="font-medium text-xs">{p.employee ? `${p.employee.firstName} ${p.employee.lastName}` : "—"}</TableCell>
                  <TableCell className="text-xs">{p.zone?.name ?? "—"}</TableCell>
                  <TableCell><StatusBadge value={p.type} /></TableCell>
                  <TableCell className="num-fa text-[11px]">
                    {formatJalali(p.validFrom)} {p.validUntil ? `تا ${formatJalali(p.validUntil)}` : "— بدون پایان"}
                  </TableCell>
                  <TableCell className="text-[10px] max-w-40 truncate">
                    {p.days ? p.days.split(",").map((d) => WEEKDAY_LABELS[d.trim()] ?? d).join("، ") : "همه روزها"}
                  </TableCell>
                  <TableCell className="num-fa text-[11px]">{p.timeFrom ? `${toFa(p.timeFrom)} تا ${toFa(p.timeUntil ?? "")}` : "۲۴ ساعته"}</TableCell>
                  <TableCell><StatusBadge value={p.status} /></TableCell>
                  <TableCell>
                    {p.status === "فعال" ? (
                      <Button size="sm" variant="outline" className="h-7 text-[11px] text-rose-700 border-rose-500/40" onClick={() => revoke(p.id)}>لغو</Button>
                    ) : "—"}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </Section>
  );
}

function PermForm({ zones, onDone }: { zones: ZoneT[]; onDone: () => void }) {
  const { data: zoneData } = useApiData<{ zones: ZoneT[] }>("/api/zones");
  const [form, setForm] = useState({ employeeId: "", zoneId: "", type: "دائم", days: "0,1,2,3,4", timeFrom: "08:00", timeUntil: "17:00", validUntil: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));
  const zList = zones.length ? zones : zoneData?.zones ?? [];

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/access", {
        method: "POST",
        body: JSON.stringify({
          ...form,
          validUntil: form.validUntil ? new Date(form.validUntil).toISOString() : null,
          timeFrom: form.timeFrom || null,
          timeUntil: form.timeUntil || null,
        }),
      });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <DialogHeader><DialogTitle>صدور مجوز دسترسی (AC-02)</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <PermEmployeeSelect onPick={(v) => set("employeeId", v)} />
        <div className="space-y-1.5">
          <Label>منطقه *</Label>
          <Select value={form.zoneId || undefined} onValueChange={(v) => set("zoneId", v)}>
            <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
            <SelectContent>{zList.map((z) => <SelectItem key={z.id} value={z.id}>{z.name} ({z.riskLevel})</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>نوع مجوز</Label>
          <Select value={form.type} onValueChange={(v) => set("type", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="دائم">دائم</SelectItem>
              <SelectItem value="موقت">موقت</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>روزهای مجاز</Label>
          <div className="flex flex-wrap gap-1.5">
            {Object.entries(WEEKDAY_LABELS).map(([idx, name]) => {
              const active = form.days.split(",").includes(idx);
              return (
                <button
                  key={idx}
                  type="button"
                  onClick={() => {
                    const arr = form.days.split(",").filter(Boolean);
                    const next = active ? arr.filter((d) => d !== idx) : [...arr, idx];
                    set("days", next.join(","));
                  }}
                  className={cn(
                    "text-xs rounded-full border px-3 py-1 transition-colors",
                    active ? "bg-primary text-primary-foreground border-primary" : "bg-muted text-muted-foreground"
                  )}
                >
                  {name}
                </button>
              );
            })}
          </div>
        </div>
        <div className="grid grid-cols-2 gap-3">
          <div className="space-y-1.5"><Label>از ساعت</Label><Input type="time" value={form.timeFrom} onChange={(e) => set("timeFrom", e.target.value)} /></div>
          <div className="space-y-1.5"><Label>تا ساعت</Label><Input type="time" value={form.timeUntil} onChange={(e) => set("timeUntil", e.target.value)} /></div>
        </div>
        <div className="space-y-1.5"><Label>اعتبار تا (اختیاری)</Label><Input type="date" value={form.validUntil} onChange={(e) => set("validUntil", e.target.value)} /></div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving || !form.employeeId || !form.zoneId}>{saving ? "..." : "صدور مجوز"}</Button></DialogFooter>
    </div>
  );
}

function PermEmployeeSelect({ onPick }: { onPick: (v: string) => void }) {
  const { data } = useApiData<{ employees: EmployeeList[] }>("/api/employees");
  return (
    <div className="space-y-1.5">
      <Label>پرسنل *</Label>
      <Select onValueChange={onPick}>
        <SelectTrigger><SelectValue placeholder="انتخاب پرسنل..." /></SelectTrigger>
        <SelectContent className="max-h-56">
          {(data?.employees ?? []).filter((e) => e.status === "فعال").map((e) => (
            <SelectItem key={e.id} value={e.id}>{e.firstName} {e.lastName} — {toFa(e.personnelCode)}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

// ================= Events report =================
function EventsPanel() {
  const [filter, setFilter] = useState("all");
  const { data, loading } = useApiData<{ events: AccessEventT[] }>(`/api/access${filter === "all" ? "" : `?result=${encodeURIComponent(filter)}`}`);
  const events = data?.events ?? [];

  return (
    <Section
      title="گزارش رویدادهای تردد (AC-06)"
      action={
        <Select value={filter} onValueChange={setFilter}>
          <SelectTrigger className="w-36 h-8 text-xs"><SelectValue /></SelectTrigger>
          <SelectContent>
            <SelectItem value="all">همه</SelectItem>
            <SelectItem value="مجاز">مجاز</SelectItem>
            <SelectItem value="غیرمجاز">غیرمجاز</SelectItem>
          </SelectContent>
        </Select>
      }
      contentClass="p-0"
    >
      {loading ? <div className="p-4"><LoadingBlock /></div> : events.length === 0 ? (
        <div className="p-4"><EmptyState text="رویدادی ثبت نشده است" /></div>
      ) : (
        <div className="overflow-x-auto thin-scroll max-h-[520px] overflow-y-auto">
          <Table>
            <TableHeader className="sticky top-0 bg-background z-10">
              <TableRow className="bg-muted/40">
                <TableHead className="text-right">زمان</TableHead>
                <TableHead className="text-right">شخص</TableHead>
                <TableHead className="text-right">گیت</TableHead>
                <TableHead className="text-right">منطقه</TableHead>
                <TableHead className="text-right">جهت</TableHead>
                <TableHead className="text-right">روش</TableHead>
                <TableHead className="text-right">نتیجه</TableHead>
                <TableHead className="text-right">دلیل</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {events.map((ev) => (
                <TableRow key={ev.id} className={cn("hover:bg-muted/30", ev.result === "غیرمجاز" && "bg-rose-500/5")}>
                  <TableCell className="num-fa text-[11px] whitespace-nowrap">
                    {formatJalali(ev.timestamp)}
                    <span className="text-muted-foreground mr-1">{formatTime(ev.timestamp)}</span>
                  </TableCell>
                  <TableCell className="text-xs font-medium">
                    {ev.employee ? `${ev.employee.firstName} ${ev.employee.lastName}` : ev.visitor ? `${ev.visitor.firstName} ${ev.visitor.lastName}` : "—"}
                  </TableCell>
                  <TableCell className="text-xs">{ev.gate?.name ?? "—"}</TableCell>
                  <TableCell className="text-xs">{ev.gate?.zone?.name ?? "—"}</TableCell>
                  <TableCell className="text-xs">{ev.direction}</TableCell>
                  <TableCell className="text-xs">{ev.method}</TableCell>
                  <TableCell><StatusBadge value={ev.result} /></TableCell>
                  <TableCell className="text-[11px] text-rose-600">{ev.reason ?? "—"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </Section>
  );
}
