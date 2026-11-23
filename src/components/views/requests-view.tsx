"use client";

import { useState } from "react";
import { CalendarCheck2, Plane, BriefcaseBusiness, Plus, Check, X, TimerReset } from "lucide-react";
import {
  useApiData, api, errToast, StatusBadge, Section, EmptyState, LoadingBlock, StatCard,
} from "@/components/shared";
import { formatJalali, dateKey, toFa } from "@/lib/persian";
import type { LeaveT, MissionT, PermitT, EmployeeList } from "@/lib/api-types";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { useToast } from "@/hooks/use-toast";
import { cn } from "@/lib/utils";

export function RequestsView() {
  const { toast } = useToast();
  const leaves = useApiData<{ leaves: LeaveT[] }>("/api/leaves", 25000);
  const missions = useApiData<{ missions: MissionT[] }>("/api/missions", 25000);
  const permits = useApiData<{ permits: PermitT[] }>("/api/permits", 25000);

  const act = async (url: string, id: string, action: "approve" | "reject") => {
    try {
      await api(url, { method: "PATCH", body: JSON.stringify({ id, action }) });
      toast({ title: action === "approve" ? "درخواست تأیید شد" : "درخواست رد شد" });
      leaves.reload();
      missions.reload();
      permits.reload();
    } catch (e) {
      errToast(e);
    }
  };

  const L = leaves.data?.leaves ?? [];
  const M = missions.data?.missions ?? [];
  const P = permits.data?.permits ?? [];

  const pendingL = L.filter((x) => x.status === "در انتظار تأیید").length;
  const pendingM = M.filter((x) => x.status === "در انتظار تأیید").length;
  const pendingP = P.filter((x) => x.status === "در انتظار تأیید").length;

  return (
    <Tabs defaultValue="leave" className="space-y-4" dir="rtl">
      <TabsList>
        <TabsTrigger value="leave" className="gap-1.5">
          <CalendarCheck2 className="h-4 w-4" />مرخصی
          {pendingL > 0 && <BadgeLike n={pendingL} />}
        </TabsTrigger>
        <TabsTrigger value="mission" className="gap-1.5">
          <Plane className="h-4 w-4" />مأموریت
          {pendingM > 0 && <BadgeLike n={pendingM} />}
        </TabsTrigger>
        <TabsTrigger value="permit" className="gap-1.5">
          <TimerReset className="h-4 w-4" />مجوز اضافه‌کاری
          {pendingP > 0 && <BadgeLike n={pendingP} />}
        </TabsTrigger>
      </TabsList>

      {/* -------- Leaves -------- */}
      <TabsContent value="leave" className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          <StatCard icon={CalendarCheck2} label="در انتظار تأیید" value={pendingL} tone="amber" />
          <StatCard icon={Check} label="تأیید شده" value={L.filter((x) => x.status === "تأیید شده").length} tone="green" />
          <StatCard icon={X} label="رد شده" value={L.filter((x) => x.status === "رد شده").length} tone="red" />
        </div>
        <Section
          title="درخواست‌های مرخصی (ATT-04)"
          action={<NewLeaveDialog onDone={() => leaves.reload()} />}
          contentClass="p-0"
        >
          {leaves.loading ? <div className="p-4"><LoadingBlock /></div> : L.length === 0 ? (
            <div className="p-4"><EmptyState text="درخواستی ثبت نشده است" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">پرسنل</TableHead>
                    <TableHead className="text-right">نوع</TableHead>
                    <TableHead className="text-right">بازه</TableHead>
                    <TableHead className="text-right">مدت</TableHead>
                    <TableHead className="text-right">دلیل</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                    <TableHead className="text-right">عملیات</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {L.map((l) => (
                    <TableRow key={l.id} className="hover:bg-muted/30">
                      <TableCell>
                        <div className="font-medium text-xs">{l.employee?.firstName} {l.employee?.lastName}</div>
                        <div className="text-[10px] text-muted-foreground">{l.employee?.department?.name}</div>
                      </TableCell>
                      <TableCell><StatusBadge value={l.type} /></TableCell>
                      <TableCell className="num-fa text-xs">{formatJalali(l.fromDate + "T12:00:00")} {l.toDate !== l.fromDate ? `تا ${formatJalali(l.toDate + "T12:00:00")}` : ""}</TableCell>
                      <TableCell className="num-fa text-xs">{l.hours ? `${toFa(l.hours)} ساعت` : `${toFa(l.days)} روز`}</TableCell>
                      <TableCell className="text-xs max-w-48 truncate">{l.reason ?? "—"}</TableCell>
                      <TableCell><StatusBadge value={l.status} /></TableCell>
                      <TableCell>
                        {l.status === "در انتظار تأیید" ? (
                          <div className="flex gap-1">
                            <Button size="sm" variant="outline" className="h-7 text-[11px] gap-1 text-emerald-700 border-emerald-500/40" onClick={() => act("/api/leaves", l.id, "approve")}>
                              <Check className="h-3 w-3" />تأیید
                            </Button>
                            <Button size="sm" variant="outline" className="h-7 text-[11px] gap-1 text-rose-700 border-rose-500/40" onClick={() => act("/api/leaves", l.id, "reject")}>
                              <X className="h-3 w-3" />رد
                            </Button>
                          </div>
                        ) : (
                          <span className="text-[11px] text-muted-foreground">{l.approver ?? "—"}</span>
                        )}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* -------- Missions -------- */}
      <TabsContent value="mission" className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          <StatCard icon={Plane} label="در انتظار تأیید" value={pendingM} tone="amber" />
          <StatCard icon={Check} label="تأیید شده" value={M.filter((x) => x.status === "تأیید شده").length} tone="green" />
          <StatCard icon={BriefcaseBusiness} label="داخلی / خارجی" value={`${toFa(M.filter((x) => x.type === "داخلی").length)} / ${toFa(M.filter((x) => x.type === "خارجی").length)}`} />
        </div>
        <Section
          title="مأموریت‌ها (ATT-05)"
          action={<NewMissionDialog onDone={() => missions.reload()} />}
          contentClass="p-0"
        >
          {missions.loading ? <div className="p-4"><LoadingBlock /></div> : M.length === 0 ? (
            <div className="p-4"><EmptyState text="مأموریتی ثبت نشده است" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">پرسنل</TableHead>
                    <TableHead className="text-right">نوع</TableHead>
                    <TableHead className="text-right">مقصد</TableHead>
                    <TableHead className="text-right">بازه</TableHead>
                    <TableHead className="text-right">هدف</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                    <TableHead className="text-right">عملیات</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {M.map((m) => (
                    <TableRow key={m.id} className="hover:bg-muted/30">
                      <TableCell className="font-medium text-xs">{m.employee?.firstName} {m.employee?.lastName}</TableCell>
                      <TableCell><StatusBadge value={m.type} /></TableCell>
                      <TableCell className="text-xs max-w-48 truncate">{m.destination}</TableCell>
                      <TableCell className="num-fa text-xs">{formatJalali(m.fromDate + "T12:00:00")} تا {formatJalali(m.toDate + "T12:00:00")}</TableCell>
                      <TableCell className="text-xs max-w-40 truncate">{m.purpose ?? "—"}</TableCell>
                      <TableCell><StatusBadge value={m.status} /></TableCell>
                      <TableCell>
                        {m.status === "در انتظار تأیید" ? (
                          <div className="flex gap-1">
                            <Button size="sm" variant="outline" className="h-7 text-[11px] gap-1 text-emerald-700 border-emerald-500/40" onClick={() => act("/api/missions", m.id, "approve")}>
                              <Check className="h-3 w-3" />تأیید
                            </Button>
                            <Button size="sm" variant="outline" className="h-7 text-[11px] gap-1 text-rose-700 border-rose-500/40" onClick={() => act("/api/missions", m.id, "reject")}>
                              <X className="h-3 w-3" />رد
                            </Button>
                          </div>
                        ) : <span className="text-[11px] text-muted-foreground">{m.approver ?? "—"}</span>}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* -------- Permits -------- */}
      <TabsContent value="permit" className="space-y-4">
        <div className="grid grid-cols-3 gap-3">
          <StatCard icon={TimerReset} label="در انتظار تأیید" value={pendingP} tone="amber" />
          <StatCard icon={Check} label="تأیید شده" value={P.filter((x) => x.status === "تأیید شده").length} tone="green" />
          <StatCard icon={X} label="رد شده" value={P.filter((x) => x.status === "رد شده").length} tone="red" />
        </div>
        <Section
          title="مجوزهای اضافه‌کاری و کار در تعطیل (ATT-06)"
          action={<NewPermitDialog onDone={() => permits.reload()} />}
          contentClass="p-0"
        >
          {permits.loading ? <div className="p-4"><LoadingBlock /></div> : P.length === 0 ? (
            <div className="p-4"><EmptyState text="مجوزی ثبت نشده است" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">پرسنل</TableHead>
                    <TableHead className="text-right">نوع</TableHead>
                    <TableHead className="text-right">تاریخ</TableHead>
                    <TableHead className="text-right">بازه</TableHead>
                    <TableHead className="text-right">دلیل</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                    <TableHead className="text-right">عملیات</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {P.map((p) => (
                    <TableRow key={p.id} className="hover:bg-muted/30">
                      <TableCell className="font-medium text-xs">{p.employee?.firstName} {p.employee?.lastName}</TableCell>
                      <TableCell><StatusBadge value={p.type} /></TableCell>
                      <TableCell className="num-fa text-xs">{formatJalali(p.date + "T12:00:00")}</TableCell>
                      <TableCell className="num-fa text-xs">{toFa(p.fromTime)} تا {toFa(p.toTime)}</TableCell>
                      <TableCell className="text-xs max-w-48 truncate">{p.reason ?? "—"}</TableCell>
                      <TableCell><StatusBadge value={p.status} /></TableCell>
                      <TableCell>
                        {p.status === "در انتظار تأیید" ? (
                          <div className="flex gap-1">
                            <Button size="sm" variant="outline" className="h-7 text-[11px] gap-1 text-emerald-700 border-emerald-500/40" onClick={() => act("/api/permits", p.id, "approve")}>
                              <Check className="h-3 w-3" />تأیید
                            </Button>
                            <Button size="sm" variant="outline" className="h-7 text-[11px] gap-1 text-rose-700 border-rose-500/40" onClick={() => act("/api/permits", p.id, "reject")}>
                              <X className="h-3 w-3" />رد
                            </Button>
                          </div>
                        ) : <span className="text-[11px] text-muted-foreground">{p.approver ?? "—"}</span>}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>
    </Tabs>
  );
}

function BadgeLike({ n }: { n: number }) {
  return (
    <span className="num-fa mr-1 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-amber-500/20 text-amber-700 dark:text-amber-300 text-[10px] font-bold px-1">
      {toFa(n)}
    </span>
  );
}

// ================= New request dialogs =================
function EmployeeSelect({ value, onChange }: { value: string; onChange: (v: string) => void }) {
  const { data } = useApiData<{ employees: EmployeeList[] }>("/api/employees");
  return (
    <Select value={value || undefined} onValueChange={onChange}>
      <SelectTrigger><SelectValue placeholder="انتخاب پرسنل..." /></SelectTrigger>
      <SelectContent className="max-h-64">
        {(data?.employees ?? []).filter((e) => e.status === "فعال").map((e) => (
          <SelectItem key={e.id} value={e.id}>{e.firstName} {e.lastName} — {toFa(e.personnelCode)}</SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function NewLeaveDialog({ onDone }: { onDone: () => void }) {
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ employeeId: "", type: "استحقاقی", fromDate: dateKey(), toDate: dateKey(), days: "1", hours: "", reason: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/leaves", {
        method: "POST",
        body: JSON.stringify({
          ...form,
          days: Number(form.days),
          hours: form.hours ? Number(form.hours) : null,
        }),
      });
      onDone();
      setOpen(false);
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />ثبت درخواست</Button></DialogTrigger>
      <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto thin-scroll">
        <DialogHeader><DialogTitle>درخواست مرخصی (ATT-04)</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5"><Label>پرسنل *</Label><EmployeeSelect value={form.employeeId} onChange={(v) => set("employeeId", v)} /></div>
          <div className="space-y-1.5">
            <Label>نوع مرخصی</Label>
            <Select value={form.type} onValueChange={(v) => set("type", v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {["استحقاقی", "استعلاجی", "بدون‌حقوق", "ساعتی", "زایمان"].map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5"><Label>از تاریخ</Label><Input type="date" value={form.fromDate} onChange={(e) => set("fromDate", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>تا تاریخ</Label><Input type="date" value={form.toDate} onChange={(e) => set("toDate", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>تعداد روز</Label><Input dir="ltr" className="text-right num-fa" value={form.days} onChange={(e) => set("days", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>ساعت (برای ساعتی)</Label><Input dir="ltr" className="text-right num-fa" value={form.hours} onChange={(e) => set("hours", e.target.value)} placeholder="4" /></div>
          </div>
          <div className="space-y-1.5"><Label>دلیل</Label><Textarea rows={2} value={form.reason} onChange={(e) => set("reason", e.target.value)} /></div>
        </div>
        <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "..." : "ثبت درخواست"}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function NewMissionDialog({ onDone }: { onDone: () => void }) {
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ employeeId: "", type: "داخلی", destination: "", fromDate: dateKey(), toDate: dateKey(), purpose: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/missions", { method: "POST", body: JSON.stringify(form) });
      onDone();
      setOpen(false);
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />ثبت مأموریت</Button></DialogTrigger>
      <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto thin-scroll">
        <DialogHeader><DialogTitle>درخواست مأموریت (ATT-05)</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5"><Label>پرسنل *</Label><EmployeeSelect value={form.employeeId} onChange={(v) => set("employeeId", v)} /></div>
          <div className="grid grid-cols-2 gap-3">
            <div className="space-y-1.5">
              <Label>نوع</Label>
              <Select value={form.type} onValueChange={(v) => set("type", v)}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>
                  <SelectItem value="داخلی">داخلی</SelectItem>
                  <SelectItem value="خارجی">خارجی</SelectItem>
                </SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5"><Label>مقصد *</Label><Input value={form.destination} onChange={(e) => set("destination", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>از تاریخ</Label><Input type="date" value={form.fromDate} onChange={(e) => set("fromDate", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>تا تاریخ</Label><Input type="date" value={form.toDate} onChange={(e) => set("toDate", e.target.value)} /></div>
          </div>
          <div className="space-y-1.5"><Label>هدف مأموریت</Label><Textarea rows={2} value={form.purpose} onChange={(e) => set("purpose", e.target.value)} /></div>
        </div>
        <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "..." : "ثبت مأموریت"}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

function NewPermitDialog({ onDone }: { onDone: () => void }) {
  const [open, setOpen] = useState(false);
  const [form, setForm] = useState({ employeeId: "", type: "اضافه‌کاری", date: dateKey(), fromTime: "16:00", toTime: "19:00", reason: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/permits", { method: "POST", body: JSON.stringify(form) });
      onDone();
      setOpen(false);
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />ثبت مجوز</Button></DialogTrigger>
      <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto thin-scroll">
        <DialogHeader><DialogTitle>مجوز اضافه‌کاری / کار در تعطیل (ATT-06)</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5"><Label>پرسنل *</Label><EmployeeSelect value={form.employeeId} onChange={(v) => set("employeeId", v)} /></div>
          <div className="space-y-1.5">
            <Label>نوع مجوز</Label>
            <Select value={form.type} onValueChange={(v) => set("type", v)}>
              <SelectTrigger><SelectValue /></SelectTrigger>
              <SelectContent>
                {["اضافه‌کاری", "کار در تعطیل", "خروج غیرمجاز"].map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-3 gap-3">
            <div className="space-y-1.5"><Label>تاریخ</Label><Input type="date" value={form.date} onChange={(e) => set("date", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>از ساعت</Label><Input type="time" value={form.fromTime} onChange={(e) => set("fromTime", e.target.value)} /></div>
            <div className="space-y-1.5"><Label>تا ساعت</Label><Input type="time" value={form.toTime} onChange={(e) => set("toTime", e.target.value)} /></div>
          </div>
          <div className="space-y-1.5"><Label>دلیل</Label><Textarea rows={2} value={form.reason} onChange={(e) => set("reason", e.target.value)} /></div>
        </div>
        <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "..." : "ثبت مجوز"}</Button></DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
