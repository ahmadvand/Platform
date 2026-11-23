"use client";

import { useMemo, useState } from "react";
import { UserPlus, Search, FileText, ScrollText, Eye, UserCheck, CalendarClock, FileSpreadsheet } from "lucide-react";
import {
  useApiData, api, errToast, StatusBadge, Section, EmptyState, LoadingBlock,
} from "@/components/shared";
import { formatJalaliLong, toFa, formatSalary, formatMinutes, formatJalali, dateKey } from "@/lib/persian";
import type { EmployeeList, ContractT, DecreeT, DeptT, PositionT, ShiftT, AttendanceT, AccessPermT, LeaveT } from "@/lib/api-types";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { useToast } from "@/hooks/use-toast";
import { cn } from "@/lib/utils";

const EMPLOYMENT_TYPES = ["رسمی", "قراردادی", "پیمانکاری", "اقماری", "کارآموز"];

interface EmployeeDetail extends EmployeeList {
  birthDate?: string | null;
  email?: string | null;
  contracts: ContractT[];
  decrees: DecreeT[];
  leaves: LeaveT[];
  attendance: AttendanceT[];
  accessPerms: AccessPermT[];
  department?: { name: string } | null;
  position?: { title: string } | null;
  shift?: { name: string } | null;
}

export function EmployeesView() {
  const { toast } = useToast();
  const [q, setQ] = useState("");
  const [dept, setDept] = useState("all");
  const [type, setType] = useState("all");

  const { data: empData, loading, reload } = useApiData<{ employees: EmployeeList[] }>(
    `/api/employees?q=${encodeURIComponent(q)}&dept=${dept === "all" ? "" : dept}&type=${type === "all" ? "" : type}`
  );
  const { data: orgData } = useApiData<{ departments: DeptT[]; positions: PositionT[] }>("/api/org");
  const { data: shiftData } = useApiData<{ shifts: ShiftT[] }>("/api/shifts");
  const { data: contractData, reload: reloadContracts } = useApiData<{ contracts?: ContractT[] }>("/api/employees"); // placeholder replaced below
  void contractData;

  const { data: allContracts, reload: reloadC } = useApiData<{ contracts: ContractT[] }>("/api/contracts?all=1");
  const { data: allDecrees, reload: reloadD } = useApiData<{ decrees: DecreeT[] }>("/api/decrees?all=1");

  const [detail, setDetail] = useState<EmployeeDetail | null>(null);
  const [addOpen, setAddOpen] = useState(false);

  const employees = empData?.employees ?? [];
  const departments = orgData?.departments ?? [];
  const positions = orgData?.positions ?? [];
  const shifts = shiftData?.shifts ?? [];

  const openDetail = async (id: string) => {
    try {
      const d = await api<{ employee: EmployeeDetail }>(`/api/employees/${id}`);
      setDetail(d.employee);
    } catch (e) {
      errToast(e);
    }
  };

  return (
    <Tabs defaultValue="list" className="space-y-4" dir="rtl">
      <TabsList>
        <TabsTrigger value="list" className="gap-1.5"><UserCheck className="h-4 w-4" />فهرست پرسنل</TabsTrigger>
        <TabsTrigger value="contracts" className="gap-1.5"><FileText className="h-4 w-4" />قراردادها</TabsTrigger>
        <TabsTrigger value="decrees" className="gap-1.5"><ScrollText className="h-4 w-4" />احکام کارگزینی</TabsTrigger>
        <TabsTrigger value="balances" className="gap-1.5"><CalendarClock className="h-4 w-4" />مانده مرخصی</TabsTrigger>
      </TabsList>

      {/* ---------------- Employees list ---------------- */}
      <TabsContent value="list" className="space-y-4">
        <Section
          title="پرسنل"
          icon={UserPlus}
          action={
            <div className="flex items-center gap-1.5">
              <a
                href="/api/export?type=employees"
                className="inline-flex items-center gap-1.5 h-8 px-3 rounded-md border text-xs hover:bg-muted"
                title="خروجی اکسل فهرست پرسنل"
              >
                <FileSpreadsheet className="h-3.5 w-3.5" /> خروجی اکسل
              </a>
              <Dialog open={addOpen} onOpenChange={setAddOpen}>
                <DialogTrigger asChild>
                  <Button size="sm" className="gap-1.5"><UserPlus className="h-4 w-4" />افزودن پرسنل</Button>
                </DialogTrigger>
              <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto thin-scroll">
                <AddEmployeeForm
                  departments={departments}
                  positions={positions}
                  shifts={shifts}
                  onDone={() => {
                    setAddOpen(false);
                    reload();
                    toast({ title: "پرسنل جدید ثبت شد", description: "قرارداد و حکم استخدام به‌صورت خودکار صادر شد" });
                  }}
                />
              </DialogContent>
            </Dialog>
            </div>
          }
          contentClass="space-y-3"
        >
          <div className="flex flex-wrap gap-2">
            <div className="relative flex-1 min-w-52">
              <Search className="absolute right-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input placeholder="جستجو: نام، کد پرسنلی، کد ملی..." value={q} onChange={(e) => setQ(e.target.value)} className="pr-8" />
            </div>
            <Select value={dept} onValueChange={setDept}>
              <SelectTrigger className="w-44"><SelectValue placeholder="واحد سازمانی" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">همه واحدها</SelectItem>
                {departments.map((d) => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
              </SelectContent>
            </Select>
            <Select value={type} onValueChange={setType}>
              <SelectTrigger className="w-36"><SelectValue placeholder="نوع استخدام" /></SelectTrigger>
              <SelectContent>
                <SelectItem value="all">همه انواع</SelectItem>
                {EMPLOYMENT_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>

          {loading ? <LoadingBlock /> : employees.length === 0 ? (
            <EmptyState text="پرسنلی یافت نشد" />
          ) : (
            <div className="overflow-x-auto thin-scroll rounded-lg border">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">کد</TableHead>
                    <TableHead className="text-right">نام و نام خانوادگی</TableHead>
                    <TableHead className="text-right">واحد</TableHead>
                    <TableHead className="text-right">سمت</TableHead>
                    <TableHead className="text-right">نوع استخدام</TableHead>
                    <TableHead className="text-right">شیفت</TableHead>
                    <TableHead className="text-right">مانده مرخصی</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                    <TableHead className="text-right">پرونده</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {employees.map((e) => {
                    const remain = Math.max(0, e.annualLeaveEnt - e.annualLeaveUsed);
                    return (
                      <TableRow key={e.id} className="hover:bg-muted/30">
                        <TableCell className="num-fa font-medium">{toFa(e.personnelCode)}</TableCell>
                        <TableCell className="font-medium">{e.firstName} {e.lastName}</TableCell>
                        <TableCell>{e.department?.name ?? "—"}</TableCell>
                        <TableCell>{e.position?.title ?? "—"}</TableCell>
                        <TableCell><StatusBadge value={e.employmentType} /></TableCell>
                        <TableCell className="text-xs">{e.shift?.name ?? "—"}</TableCell>
                        <TableCell className="num-fa">{toFa(remain)} روز</TableCell>
                        <TableCell><StatusBadge value={e.status} /></TableCell>
                        <TableCell>
                          <Button size="sm" variant="ghost" className="h-8 gap-1 text-xs" onClick={() => openDetail(e.id)}>
                            <Eye className="h-3.5 w-3.5" />مشاهده
                          </Button>
                        </TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* ---------------- Contracts ---------------- */}
      <TabsContent value="contracts">
        <Section title="مدیریت قراردادها (HR-04)" icon={FileText} contentClass="p-0">
          {!allContracts ? <div className="p-4"><LoadingBlock /></div> : allContracts.contracts.length === 0 ? (
            <div className="p-4"><EmptyState text="قراردادی ثبت نشده است" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">شماره قرارداد</TableHead>
                    <TableHead className="text-right">پرسنل</TableHead>
                    <TableHead className="text-right">نوع</TableHead>
                    <TableHead className="text-right">شروع</TableHead>
                    <TableHead className="text-right">پایان</TableHead>
                    <TableHead className="text-right">حقوق</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {allContracts.contracts.map((c) => {
                    const daysLeft = c.endDate ? Math.ceil((new Date(c.endDate).getTime() - Date.now()) / 86400000) : null;
                    return (
                      <TableRow key={c.id} className="hover:bg-muted/30">
                        <TableCell className="num-fa font-medium">{toFa(c.contractNo)}</TableCell>
                        <TableCell>{c.employee ? `${c.employee.firstName} ${c.employee.lastName}` : "—"}</TableCell>
                        <TableCell><StatusBadge value={c.type} /></TableCell>
                        <TableCell className="num-fa text-xs">{formatJalali(c.startDate)}</TableCell>
                        <TableCell className="num-fa text-xs">
                          {c.endDate ? formatJalali(c.endDate) : "—"}
                          {daysLeft !== null && daysLeft >= 0 && daysLeft <= 30 && (
                            <span className="mr-1.5 text-[10px] text-rose-600 font-bold">({toFa(daysLeft)} روز)</span>
                          )}
                        </TableCell>
                        <TableCell className="num-fa text-xs">{formatSalary(c.salary)}</TableCell>
                        <TableCell><StatusBadge value={c.status} /></TableCell>
                      </TableRow>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* ---------------- Leave balances (PLAN P4.4) ---------------- */}
      <TabsContent value="balances">
        <LeaveBalancesPanel />
      </TabsContent>

      {/* ---------------- Decrees ---------------- */}
      <TabsContent value="decrees">
        <Section title="احکام کارگزینی (HR-03)" icon={ScrollText} contentClass="p-0">
          {!allDecrees ? <div className="p-4"><LoadingBlock /></div> : allDecrees.decrees.length === 0 ? (
            <div className="p-4"><EmptyState text="حکمی ثبت نشده است" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">شماره حکم</TableHead>
                    <TableHead className="text-right">پرسنل</TableHead>
                    <TableHead className="text-right">نوع</TableHead>
                    <TableHead className="text-right">تاریخ</TableHead>
                    <TableHead className="text-right">متن</TableHead>
                    <TableHead className="text-right">ابلاغ الکترونیکی</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {allDecrees.decrees.map((d) => (
                    <TableRow key={d.id} className="hover:bg-muted/30">
                      <TableCell className="num-fa font-medium">{toFa(d.decreeNo)}</TableCell>
                      <TableCell>{d.employee ? `${d.employee.firstName} ${d.employee.lastName}` : "—"}</TableCell>
                      <TableCell><StatusBadge value={d.type} /></TableCell>
                      <TableCell className="num-fa text-xs">{formatJalali(d.date)}</TableCell>
                      <TableCell className="text-xs max-w-72 truncate">{d.content ?? "—"}</TableCell>
                      <TableCell>
                        <span className={cn("text-xs font-medium", d.acknowledged ? "text-emerald-600" : "text-amber-600")}>
                          {d.acknowledged ? "ابلاغ شده" : "در انتظار ابلاغ"}
                        </span>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* Detail dialog */}
      <Dialog open={!!detail} onOpenChange={(o) => !o && setDetail(null)}>
        <DialogContent className="max-w-3xl max-h-[90vh] overflow-y-auto thin-scroll">
          {detail && <EmployeeDetailBody emp={detail} />}
        </DialogContent>
      </Dialog>

      {/* keep lints silent for unused reloads */}
      <span className="hidden" onClick={() => { reloadContracts(); reloadC(); reloadD(); }} />
    </Tabs>
  );
}

// ================= Add employee form =================
// ---------------- پنل مانده مرخصی قانونی (PLAN P4.4) ----------------
function LeaveBalancesPanel() {
  const { data, loading } = useApiData<{
    balances: {
      employeeId: string; personnelCode: string; name: string; employmentType: string;
      annualEnt: number; annualUsed: number; annualRemaining: number;
      sickEnt: number; sickUsed: number; sickRemaining: number; jYear?: number;
    }[];
    jYear: number;
  }>("/api/leave-balances");

  const rows = data?.balances ?? [];
  return (
    <Section title={`مانده مرخصی قانونی سال ${toFa(data?.jYear ?? "")} — استحقاقی ۲۶ روز + استعلاجی`} icon={CalendarClock} contentClass="p-0">
      {loading ? (
        <div className="p-4"><LoadingBlock rows={5} /></div>
      ) : rows.length === 0 ? (
        <div className="p-4"><EmptyState text="داده‌ای یافت نشد" /></div>
      ) : (
        <div className="overflow-x-auto thin-scroll">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/40">
                <TableHead className="text-right">پرسنل</TableHead>
                <TableHead className="text-right">نوع استخدام</TableHead>
                <TableHead className="text-right">استحقاقی</TableHead>
                <TableHead className="text-right">مانده استحقاقی</TableHead>
                <TableHead className="text-right">استعلاجی</TableHead>
                <TableHead className="text-right">مانده استعلاجی</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map((b) => (
                <TableRow key={b.employeeId}>
                  <TableCell>
                    <div className="text-xs font-medium">{b.name}</div>
                    <div className="num-fa text-[10px] text-muted-foreground">{b.personnelCode}</div>
                  </TableCell>
                  <TableCell><StatusBadge value={b.employmentType} /></TableCell>
                  <TableCell className="num-fa text-xs">{toFa(b.annualUsed)} / {toFa(b.annualEnt)}</TableCell>
                  <TableCell>
                    <div className="flex items-center gap-2">
                      <div className="h-1.5 w-16 rounded-full bg-muted overflow-hidden">
                        <div
                          className="h-full bg-emerald-500 rounded-full"
                          style={{ width: `${Math.min(100, (b.annualRemaining / Math.max(1, b.annualEnt)) * 100)}%` }}
                        />
                      </div>
                      <span className="num-fa text-xs font-semibold">{toFa(b.annualRemaining)} روز</span>
                    </div>
                  </TableCell>
                  <TableCell className="num-fa text-xs">{toFa(b.sickUsed)} / {toFa(b.sickEnt)}</TableCell>
                  <TableCell className="num-fa text-xs font-semibold">{toFa(b.sickRemaining)} روز</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
      )}
    </Section>
  );
}

function AddEmployeeForm({
  departments, positions, shifts, onDone,
}: {
  departments: DeptT[];
  positions: PositionT[];
  shifts: ShiftT[];
  onDone: () => void;
}) {
  const [form, setForm] = useState({
    firstName: "", lastName: "", nationalId: "", phone: "", email: "",
    employmentType: "قراردادی", departmentId: "", positionId: "", shiftId: "", baseSalary: "",
  });
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    if (!form.firstName || !form.lastName || !form.nationalId) {
      errToast(new Error("نام، نام خانوادگی و کد ملی الزامی است"));
      return;
    }
    setSaving(true);
    try {
      await api("/api/employees", {
        method: "POST",
        body: JSON.stringify({
          ...form,
          baseSalary: Number(form.baseSalary.replace(/\D/g, "")) || 0,
          hireDate: new Date().toISOString(),
        }),
      });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  return (
    <div className="space-y-4">
      <DialogHeader>
        <DialogTitle>ثبت پرسنل جدید (HR-01)</DialogTitle>
      </DialogHeader>
      <div className="grid sm:grid-cols-2 gap-3">
        <div className="space-y-1.5"><Label>نام *</Label><Input value={form.firstName} onChange={(e) => set("firstName", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>نام خانوادگی *</Label><Input value={form.lastName} onChange={(e) => set("lastName", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد ملی *</Label><Input dir="ltr" className="text-right" value={form.nationalId} onChange={(e) => set("nationalId", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>تلفن همراه</Label><Input dir="ltr" className="text-right" value={form.phone} onChange={(e) => set("phone", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>نوع استخدام</Label>
          <Select value={form.employmentType} onValueChange={(v) => set("employmentType", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>{EMPLOYMENT_TYPES.map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>واحد سازمانی</Label>
          <Select value={form.departmentId || undefined} onValueChange={(v) => set("departmentId", v)}>
            <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
            <SelectContent>{departments.map((d) => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>سمت</Label>
          <Select value={form.positionId || undefined} onValueChange={(v) => set("positionId", v)}>
            <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
            <SelectContent>{positions.map((p) => <SelectItem key={p.id} value={p.id}>{p.title} — {p.department?.name}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>شیفت پیش‌فرض</Label>
          <Select value={form.shiftId || undefined} onValueChange={(v) => set("shiftId", v)}>
            <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
            <SelectContent>{shifts.map((s) => <SelectItem key={s.id} value={s.id}>{s.name}</SelectItem>)}</SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5 sm:col-span-2">
          <Label>حقوق پایه (ریال)</Label>
          <Input dir="ltr" className="text-right num-fa" value={form.baseSalary} onChange={(e) => set("baseSalary", e.target.value)} placeholder="240000000" />
        </div>
      </div>
      <DialogFooter>
        <Button onClick={submit} disabled={saving} className="w-full sm:w-auto">
          {saving ? "در حال ثبت..." : "ثبت پرسنل"}
        </Button>
      </DialogFooter>
    </div>
  );
}

// ================= Employee detail =================
function EmployeeDetailBody({ emp }: { emp: EmployeeDetail }) {
  const remain = Math.max(0, emp.annualLeaveEnt - emp.annualLeaveUsed);
  return (
    <div className="space-y-4">
      <DialogHeader>
        <DialogTitle className="flex flex-wrap items-center gap-2">
          {emp.firstName} {emp.lastName}
          <StatusBadge value={emp.employmentType} />
          <StatusBadge value={emp.status} />
        </DialogTitle>
      </DialogHeader>

      <div className="grid sm:grid-cols-2 lg:grid-cols-3 gap-3 text-xs">
        <InfoRow label="کد پرسنلی" value={toFa(emp.personnelCode)} />
        <InfoRow label="کد ملی" value={toFa(emp.nationalId)} />
        <InfoRow label="تلفن" value={emp.phone ? toFa(emp.phone) : "—"} />
        <InfoRow label="واحد سازمانی" value={emp.department?.name ?? "—"} />
        <InfoRow label="سمت" value={emp.position?.title ?? "—"} />
        <InfoRow label="شیفت" value={emp.shift?.name ?? "—"} />
        <InfoRow label="تاریخ استخدام" value={formatJalali(emp.hireDate)} />
        <InfoRow label="حقوق پایه" value={formatSalary(emp.baseSalary)} />
        <InfoRow label="مانده مرخصی استحقاقی" value={`${toFa(remain)} از ${toFa(emp.annualLeaveEnt)} روز`} />
      </div>

      <div className="grid md:grid-cols-2 gap-4">
        <div className="space-y-2">
          <h4 className="text-sm font-semibold">قراردادها</h4>
          {emp.contracts.length === 0 ? <EmptyState text="بدون قرارداد" /> : emp.contracts.map((c) => (
            <div key={c.id} className="rounded-lg border p-2.5 text-xs flex items-center justify-between gap-2">
              <div>
                <span className="num-fa font-medium">{toFa(c.contractNo)}</span>
                <span className="text-muted-foreground mr-2">{formatJalali(c.startDate)} تا {c.endDate ? formatJalali(c.endDate) : "نامحدود"}</span>
              </div>
              <StatusBadge value={c.status} />
            </div>
          ))}
        </div>
        <div className="space-y-2">
          <h4 className="text-sm font-semibold">احکام</h4>
          {emp.decrees.length === 0 ? <EmptyState text="بدون حکم" /> : emp.decrees.map((d) => (
            <div key={d.id} className="rounded-lg border p-2.5 text-xs flex items-center justify-between gap-2">
              <div className="min-w-0">
                <span className="font-medium">{d.type}</span>
                <p className="text-muted-foreground truncate">{d.content}</p>
              </div>
              <span className="num-fa text-[10px] text-muted-foreground shrink-0">{formatJalali(d.date)}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="space-y-2">
        <h4 className="text-sm font-semibold">کارکرد ۱۴ روز اخیر</h4>
        {emp.attendance.length === 0 ? <EmptyState text="سابقه‌ای نیست" /> : (
          <div className="space-y-1">
            {emp.attendance.map((a) => (
              <div key={a.id} className="flex items-center gap-2 rounded border p-2 text-xs">
                <span className="num-fa w-24">{formatJalali(a.date)}</span>
                <span className="text-muted-foreground">
                  {formatTimeSafe(a.checkIn)} — {formatTimeSafe(a.checkOut)}
                </span>
                <span className="num-fa text-muted-foreground">کارکرد: {formatMinutes(a.workMinutes)}</span>
                {a.lateMinutes > 0 && <span className="num-fa text-amber-600">تأخیر: {toFa(a.lateMinutes)}′</span>}
                {a.overtimeMinutes > 0 && <span className="num-fa text-emerald-600">اضافه‌کاری: {toFa(a.overtimeMinutes)}′</span>}
                <span className="mr-auto"><StatusBadge value={a.status} /></span>
              </div>
            ))}
          </div>
        )}
      </div>

      {emp.accessPerms.length > 0 && (
        <div className="space-y-2">
          <h4 className="text-sm font-semibold">مجوزهای دسترسی فعال</h4>
          <div className="flex flex-wrap gap-1.5">
            {emp.accessPerms.map((p) => (
              <span key={p.id} className="text-xs rounded-full border px-2.5 py-1 bg-muted/50">
                {p.zone?.name} ({p.type})
              </span>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}

function InfoRow({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border p-2.5 flex items-center justify-between">
      <span className="text-muted-foreground">{label}</span>
      <span className="font-medium num-fa">{value}</span>
    </div>
  );
}

function formatTimeSafe(iso?: string | null): string {
  if (!iso) return "—";
  const d = new Date(iso);
  if (isNaN(d.getTime())) return "—";
  const p2 = (n: number) => String(n).padStart(2, "0");
  return toFa(`${p2(d.getHours())}:${p2(d.getMinutes())}`);
}

// Keep dateKey imported usage explicit (used by future filters)
void dateKey;
