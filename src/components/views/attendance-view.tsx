"use client";

import { useState } from "react";
import {
  Fingerprint, LogIn, LogOut, Clock, Plus, RefreshCw, Power, CalendarDays, HardDriveDownload,
} from "lucide-react";
import {
  useApiData, api, errToast, StatusBadge, Section, EmptyState, LoadingBlock, StatCard,
} from "@/components/shared";
import { formatJalaliLong, formatTime, toFa, formatMinutes, formatNumber, dateKey } from "@/lib/persian";
import type { AttendanceT, ShiftT, DeviceT, EmployeeList } from "@/lib/api-types";
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

const SHIFT_TYPES = ["عادی", "نوبت‌کاری", "چرخشی", "۱۲ساعته", "اقماری"];

export function AttendanceView() {
  const [date, setDate] = useState(dateKey());
  const { data, loading, reload } = useApiData<{
    records: AttendanceT[];
    stats: { present: number; late: number; absent: number; leave: number; mission: number; totalOvertime: number; totalWork: number };
    jalali: string;
  }>(`/api/attendance?date=${date}`, 30000);

  const records = data?.records ?? [];
  const stats = data?.stats;

  return (
    <Tabs defaultValue="daily" className="space-y-4" dir="rtl">
      <TabsList>
        <TabsTrigger value="daily" className="gap-1.5"><CalendarDays className="h-4 w-4" />کارکرد روزانه</TabsTrigger>
        <TabsTrigger value="shifts" className="gap-1.5"><Clock className="h-4 w-4" />شیفت‌ها</TabsTrigger>
        <TabsTrigger value="devices" className="gap-1.5"><Fingerprint className="h-4 w-4" />دستگاه‌ها</TabsTrigger>
      </TabsList>

      {/* -------- Daily work -------- */}
      <TabsContent value="daily" className="space-y-4">
        <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-6 gap-3">
          <StatCard icon={LogIn} label="حاضر" value={stats?.present ?? 0} tone="green" />
          <StatCard icon={Clock} label="تأخیر" value={stats?.late ?? 0} tone="amber" />
          <StatCard icon={LogOut} label="غایب" value={stats?.absent ?? 0} tone={stats?.absent ? "red" : "default"} />
          <StatCard icon={CalendarDays} label="مرخصی" value={stats?.leave ?? 0} tone="violet" />
          <StatCard icon={Fingerprint} label="مأموریت" value={stats?.mission ?? 0} />
          <StatCard icon={Clock} label="مجموع اضافه‌کاری" value={formatMinutes(stats?.totalOvertime ?? 0)} tone="green" />
        </div>

        <Section
          title="کارکرد روزانه — موتور محاسبه کارکرد (ATT-03)"
          action={
            <div className="flex items-center gap-2">
              <Input
                type="date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
                className="w-40 text-xs"
              />
              <PunchDialog onDone={reload} />
            </div>
          }
          contentClass="p-0"
        >
          <div className="px-4 py-2 text-xs text-muted-foreground num-fa border-b">
            {formatJalaliLong(date + "T12:00:00")} — {toFa(records.length)} رکورد
          </div>
          {loading ? <div className="p-4"><LoadingBlock /></div> : records.length === 0 ? (
            <div className="p-4"><EmptyState text="برای این تاریخ رکوردی ثبت نشده است" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">پرسنل</TableHead>
                    <TableHead className="text-right">واحد</TableHead>
                    <TableHead className="text-right">شیفت</TableHead>
                    <TableHead className="text-right">ورود</TableHead>
                    <TableHead className="text-right">خروج</TableHead>
                    <TableHead className="text-right">کارکرد</TableHead>
                    <TableHead className="text-right">تأخیر</TableHead>
                    <TableHead className="text-right">اضافه‌کاری</TableHead>
                    <TableHead className="text-right">دستگاه</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {records.map((r) => (
                    <TableRow key={r.id} className="hover:bg-muted/30">
                      <TableCell>
                        <div className="font-medium text-xs">{r.employee?.firstName} {r.employee?.lastName}</div>
                        <div className="num-fa text-[10px] text-muted-foreground">{toFa(r.employee?.personnelCode ?? "")}</div>
                      </TableCell>
                      <TableCell className="text-xs">{r.employee?.department?.name ?? "—"}</TableCell>
                      <TableCell className="text-xs">{r.employee?.shift?.name ?? "—"}</TableCell>
                      <TableCell className="num-fa text-xs">{formatTime(r.checkIn)}</TableCell>
                      <TableCell className="num-fa text-xs">{formatTime(r.checkOut)}</TableCell>
                      <TableCell className="num-fa text-xs">{formatMinutes(r.workMinutes)}</TableCell>
                      <TableCell className={cn("num-fa text-xs", r.lateMinutes > 0 && "text-amber-600 font-semibold")}>
                        {r.lateMinutes > 0 ? `${toFa(r.lateMinutes)}′` : "—"}
                      </TableCell>
                      <TableCell className={cn("num-fa text-xs", r.overtimeMinutes > 0 && "text-emerald-600 font-semibold")}>
                        {r.overtimeMinutes > 0 ? `${toFa(r.overtimeMinutes)}′` : "—"}
                      </TableCell>
                      <TableCell className="text-[11px] text-muted-foreground">{r.device?.name ?? "—"}</TableCell>
                      <TableCell><StatusBadge value={r.status} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* -------- Shifts -------- */}
      <TabsContent value="shifts">
        <ShiftsPanel />
      </TabsContent>

      {/* -------- Devices -------- */}
      <TabsContent value="devices">
        <DevicesPanel />
      </TabsContent>
    </Tabs>
  );
}

// ================= Punch simulator =================
function PunchDialog({ onDone }: { onDone: () => void }) {
  const [open, setOpen] = useState(false);
  const [employeeId, setEmployeeId] = useState("");
  const [kind, setKind] = useState("in");
  const [saving, setSaving] = useState(false);
  const { toast } = useToast();
  const { data } = useApiData<{ employees: EmployeeList[] }>("/api/employees");

  const submit = async () => {
    if (!employeeId) return errToast(new Error("انتخاب پرسنل الزامی است"));
    setSaving(true);
    try {
      await api("/api/attendance", { method: "POST", body: JSON.stringify({ employeeId, kind }) });
      toast({
        title: kind === "in" ? "ثبت ورود انجام شد" : "ثبت خروج انجام شد",
        description: "محاسبه تأخیر/اضافه‌کاری بر اساس شیفت پرسنل انجام شد",
      });
      setOpen(false);
      setEmployeeId("");
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button size="sm" className="gap-1.5"><Fingerprint className="h-4 w-4" />شبیه‌سازی ثبت تردد</Button>
      </DialogTrigger>
      <DialogContent className="max-w-md">
        <DialogHeader><DialogTitle>ثبت تردد از دستگاه (ATT-01)</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>پرسنل</Label>
            <Select value={employeeId || undefined} onValueChange={setEmployeeId}>
              <SelectTrigger><SelectValue placeholder="انتخاب پرسنل..." /></SelectTrigger>
              <SelectContent className="max-h-64">
                {(data?.employees ?? []).filter((e) => e.status === "فعال").map((e) => (
                  <SelectItem key={e.id} value={e.id}>{e.firstName} {e.lastName} — {toFa(e.personnelCode)}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="space-y-1.5">
            <Label>نوع ثبت</Label>
            <Select value={kind} onValueChange={setKind}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="in">ورود</SelectItem>
                <SelectItem value="out">خروج</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
        <DialogFooter>
          <Button onClick={submit} disabled={saving} className="gap-1.5">
            <Fingerprint className="h-4 w-4" />{saving ? "در حال ثبت..." : "ثبت تردد"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ================= Shifts =================
function ShiftsPanel() {
  const { data, reload } = useApiData<{ shifts: ShiftT[] }>("/api/shifts");
  const [addOpen, setAddOpen] = useState(false);
  const { toast } = useToast();
  const shifts = data?.shifts ?? [];

  const assign = async (employeeId: string, shiftId: string) => {
    try {
      await api("/api/shifts", { method: "PATCH", body: JSON.stringify({ action: "assign", employeeId, shiftId }) });
      toast({ title: "تخصیص شیفت انجام شد" });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  return (
    <Section
      title="شیفت و گروه کاری (ATT-02)"
      action={
        <Dialog open={addOpen} onOpenChange={setAddOpen}>
          <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />شیفت جدید</Button></DialogTrigger>
          <DialogContent className="max-w-md">
            <ShiftForm onDone={() => { setAddOpen(false); reload(); toast({ title: "شیفت ایجاد شد" }); }} />
          </DialogContent>
        </Dialog>
      }
      contentClass="space-y-3"
    >
      {shifts.length === 0 ? <EmptyState text="شیفت ثبت نشده" /> : (
        <div className="grid sm:grid-cols-2 xl:grid-cols-3 gap-3">
          {shifts.map((s) => (
            <div key={s.id} className="rounded-xl border p-3.5 space-y-2.5 bg-card">
              <div className="flex items-center justify-between gap-2">
                <span className="font-semibold text-sm">{s.name}</span>
                <Badge variant="outline" className="text-[10px]">{s.type}</Badge>
              </div>
              <div className="text-xs num-fa text-muted-foreground flex flex-wrap gap-x-3 gap-y-1">
                <span>{toFa(s.startTime)} تا {toFa(s.endTime)}</span>
                <span>استراحت: {toFa(s.breakMinutes)}′</span>
                <span>رواداری: {toFa(s.toleranceLate)}′</span>
                {s.cyclePattern && <span className="text-primary font-medium">چرخه {toFa(s.cyclePattern)}</span>}
              </div>
              <div className="flex items-center justify-between">
                <Badge variant="secondary" className="num-fa text-[10px]">{toFa(s.employees.length)} نفر تخصیص</Badge>
              </div>
              {s.employees.length > 0 && (
                <div className="flex flex-wrap gap-1">
                  {s.employees.slice(0, 5).map((e) => (
                    <span key={e.id} className="text-[10px] rounded-full bg-muted px-2 py-0.5">{e.firstName} {e.lastName}</span>
                  ))}
                  {s.employees.length > 5 && <span className="text-[10px] text-muted-foreground">+{toFa(s.employees.length - 5)}</span>}
                </div>
              )}
            </div>
          ))}
        </div>
      )}

      <div className="rounded-xl border p-3.5 space-y-3">
        <h4 className="text-sm font-semibold">تخصیص سریع شیفت به پرسنل</h4>
        <AssignShift shifts={shifts} onAssign={assign} />
      </div>
    </Section>
  );
}

function AssignShift({ shifts, onAssign }: { shifts: ShiftT[]; onAssign: (empId: string, shiftId: string) => void }) {
  const { data } = useApiData<{ employees: EmployeeList[] }>("/api/employees");
  const [empId, setEmpId] = useState("");
  const [shiftId, setShiftId] = useState("");

  return (
    <div className="flex flex-wrap gap-2 items-center">
      <Select value={empId || undefined} onValueChange={setEmpId}>
        <SelectTrigger className="w-56"><SelectValue placeholder="انتخاب پرسنل..." /></SelectTrigger>
        <SelectContent className="max-h-64">
          {(data?.employees ?? []).filter((e) => e.status === "فعال").map((e) => (
            <SelectItem key={e.id} value={e.id}>{e.firstName} {e.lastName} {e.shift ? `— شیفت فعلی: ${e.shift.name}` : ""}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={shiftId || undefined} onValueChange={setShiftId}>
        <SelectTrigger className="w-48"><SelectValue placeholder="شیفت مقصد..." /></SelectTrigger>
        <SelectContent>
          {shifts.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}
        </SelectContent>
      </Select>
      <Button size="sm" disabled={!empId || !shiftId} onClick={() => { onAssign(empId, shiftId); setEmpId(""); setShiftId(""); }}>
        تخصیص
      </Button>
    </div>
  );
}

function ShiftForm({ onDone }: { onDone: () => void }) {
  const [form, setForm] = useState({ name: "", type: "عادی", startTime: "08:00", endTime: "16:00", breakMinutes: "60", toleranceLate: "10", cyclePattern: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    if (!form.name) return errToast(new Error("نام شیفت الزامی است"));
    setSaving(true);
    try {
      await api("/api/shifts", {
        method: "POST",
        body: JSON.stringify({
          ...form,
          breakMinutes: Number(form.breakMinutes),
          toleranceLate: Number(form.toleranceLate),
          cyclePattern: form.cyclePattern || null,
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
      <DialogHeader><DialogTitle>تعریف شیفت پارامتریک (ATT-02)</DialogTitle></DialogHeader>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5 col-span-2"><Label>نام شیفت *</Label><Input value={form.name} onChange={(e) => set("name", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>نوع</Label>
          <Select value={form.type} onValueChange={(v) => set("type", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{SHIFT_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5"><Label>الگوی چرخه (اقماری)</Label><Input dir="ltr" className="text-right" placeholder="14/14" value={form.cyclePattern} onChange={(e) => set("cyclePattern", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>ساعت شروع</Label><Input type="time" value={form.startTime} onChange={(e) => set("startTime", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>ساعت پایان</Label><Input type="time" value={form.endTime} onChange={(e) => set("endTime", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>استراحت (دقیقه)</Label><Input dir="ltr" className="text-right num-fa" value={form.breakMinutes} onChange={(e) => set("breakMinutes", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>رواداری تأخیر (دقیقه)</Label><Input dir="ltr" className="text-right num-fa" value={form.toleranceLate} onChange={(e) => set("toleranceLate", e.target.value)} /></div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "در حال ثبت..." : "ایجاد شیفت"}</Button></DialogFooter>
    </div>
  );
}

// ================= Devices =================
function DevicesPanel() {
  const { data, reload } = useApiData<{ devices: DeviceT[] }>("/api/devices", 25000);
  const [addOpen, setAddOpen] = useState(false);
  const { toast } = useToast();
  const devices = data?.devices ?? [];

  const sync = async (id: string) => {
    try {
      await api("/api/devices", { method: "PATCH", body: JSON.stringify({ action: "sync", id }) });
      toast({ title: "تخلیه اطلاعات دستگاه انجام شد", description: "رکوردهای جدید دریافت و محاسبه شد" });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  const toggle = async (id: string) => {
    try {
      await api("/api/devices", { method: "PATCH", body: JSON.stringify({ action: "toggle", id }) });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  return (
    <Section
      title="مدیریت دستگاه‌های حضور و غیاب (ATT-01)"
      action={
        <Dialog open={addOpen} onOpenChange={setAddOpen}>
          <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />دستگاه جدید</Button></DialogTrigger>
          <DialogContent className="max-w-md">
            <DeviceForm onDone={() => { setAddOpen(false); reload(); toast({ title: "دستگاه ثبت شد" }); }} />
          </DialogContent>
        </Dialog>
      }
      contentClass="space-y-3"
    >
      <div className="grid sm:grid-cols-2 xl:grid-cols-3 gap-3">
        {devices.map((d) => (
          <div key={d.id} className="rounded-xl border p-3.5 space-y-2.5 bg-card">
            <div className="flex items-start justify-between gap-2">
              <div>
                <div className="font-semibold text-sm">{d.name}</div>
                <div className="text-[11px] text-muted-foreground num-fa">{toFa(d.serial)} • {d.brand}</div>
              </div>
              <StatusBadge value={d.mode} />
            </div>
            <div className="text-xs text-muted-foreground space-y-1">
              <div className="flex justify-between"><span>کاربرد:</span><span>{d.purpose}</span></div>
              <div className="flex justify-between num-fa"><span>محل نصب:</span><span>{d.location ?? "—"}</span></div>
              <div className="flex justify-between num-fa"><span>IP:</span><span dir="ltr">{d.ip ?? "—"}</span></div>
              <div className="flex justify-between num-fa">
                <span>آخرین تخلیه:</span>
                <span>{d.lastSync ? formatTime(d.lastSync) : "—"}</span>
              </div>
              {d.gates && d.gates.length > 0 && (
                <div className="flex justify-between"><span>گیت‌ها:</span><span className="num-fa">{toFa(d.gates.length)}</span></div>
              )}
            </div>
            <div className="flex gap-2">
              <Button size="sm" variant="outline" className="h-8 gap-1 text-xs flex-1" onClick={() => sync(d.id)}>
                <RefreshCw className="h-3.5 w-3.5" />تخلیه
              </Button>
              <Button size="sm" variant="outline" className="h-8 gap-1 text-xs flex-1" onClick={() => toggle(d.id)}>
                <Power className="h-3.5 w-3.5" />{d.active ? "غیرفعال" : "فعال‌سازی"}
              </Button>
            </div>
          </div>
        ))}
      </div>
      <p className="text-[11px] text-muted-foreground num-fa">
        پشتیبانی از برندهای ZKTeco، Suprema، Anviz، Virdi و Timmy — اتصال آنلاین/آفلاین با SDK و تخلیه خودکار ({toFa(devices.length)} دستگاه فعال)
      </p>
    </Section>
  );
}

function DeviceForm({ onDone }: { onDone: () => void }) {
  const [form, setForm] = useState({ name: "", brand: "ZKTeco", serial: "", ip: "", location: "", purpose: "حضور و غیاب" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));
  const brands = ["ZKTeco", "Suprema", "Anviz", "Virdi", "Timmy"];

  const submit = async () => {
    if (!form.name || !form.serial) return errToast(new Error("نام و شماره سریال الزامی است"));
    setSaving(true);
    try {
      await api("/api/devices", { method: "POST", body: JSON.stringify(form) });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <DialogHeader><DialogTitle>افزودن دستگاه (ATT-01)</DialogTitle></DialogHeader>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5 col-span-2"><Label>نام دستگاه *</Label><Input value={form.name} onChange={(e) => set("name", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>برند</Label>
          <Select value={form.brand} onValueChange={(v) => set("brand", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{brands.map((b) => <SelectItem key={b} value={b}>{b}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5"><Label>شماره سریال *</Label><Input dir="ltr" className="text-right" value={form.serial} onChange={(e) => set("serial", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>آدرس IP</Label><Input dir="ltr" className="text-right" value={form.ip} onChange={(e) => set("ip", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>کاربرد</Label>
          <Select value={form.purpose} onValueChange={(v) => set("purpose", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              <SelectItem value="حضور و غیاب">حضور و غیاب</SelectItem>
              <SelectItem value="کنترل تردد">کنترل تردد</SelectItem>
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5 col-span-2"><Label>محل نصب</Label><Input value={form.location} onChange={(e) => set("location", e.target.value)} /></div>
      </div>
      <DialogFooter>
        <Button onClick={submit} disabled={saving} className="gap-1.5"><HardDriveDownload className="h-4 w-4" />{saving ? "در حال ثبت..." : "ثبت دستگاه"}</Button>
      </DialogFooter>
    </div>
  );
}

void formatNumber;
