"use client";

import { useState } from "react";
import { Banknote, Plus, FileSpreadsheet, Lock, Eye, Trash2, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  api, errToast, useApiData, StatusBadge, Section, LoadingBlock, EmptyState,
} from "@/components/shared";
import { toast } from "@/hooks/use-toast";
import { toFa, formatNumber, formatJalali, JALALI_MONTHS } from "@/lib/persian";

interface Run {
  id: string;
  jYear: number;
  jMonth: number;
  status: string;
  note: string | null;
  totalGross: number;
  totalNet: number;
  employeeCount: number;
  createdBy: string | null;
  createdAt: string;
}

interface Payslip {
  id: string;
  employeeId: string;
  baseSalary: number;
  overtimePay: number;
  allowances: number;
  absentDeduct: number;
  insurance: number;
  tax: number;
  netPay: number;
  workedMin: number;
  overtimeMin: number;
  employee: { firstName: string; lastName: string; personnelCode: string; department?: { name: string } | null };
}

export function PayrollView() {
  const { data, loading, reload } = useApiData<{ runs: Run[] }>("/api/payroll/runs");
  const [createOpen, setCreateOpen] = useState(false);
  const [viewRun, setViewRun] = useState<string | null>(null);

  const runs = data?.runs ?? [];

  return (
    <div className="space-y-4 min-w-0">
      <div className="flex justify-end">
        <Button size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" /> لیست حقوق جدید
        </Button>
      </div>

      <Section title="لیست‌های حقوق ماهانه" icon={Banknote}>
        {loading ? <LoadingBlock rows={3} /> : runs.length === 0 ? (
          <EmptyState text="هنوز لیست حقوقی ایجاد نشده است" />
        ) : (
          <div className="space-y-2">
            {runs.map((r) => (
              <div key={r.id} className="border rounded-lg p-3 flex flex-wrap items-center gap-3 text-sm">
                <div className="font-semibold num-fa">{JALALI_MONTHS[r.jMonth - 1]} {toFa(r.jYear)}</div>
                <StatusBadge value={r.status} />
                <span className="text-xs text-muted-foreground num-fa">{toFa(r.employeeCount)} پرسنل</span>
                <span className="text-xs num-fa">ناخالص: {formatNumber(r.totalGross)}</span>
                <span className="text-xs num-fa font-semibold text-emerald-700">خالص: {formatNumber(r.totalNet)}</span>
                <span className="flex-1" />
                <div className="flex gap-1.5">
                  <Button size="sm" variant="outline" className="h-8" onClick={() => setViewRun(r.id)}>
                    <Eye className="h-3.5 w-3.5" /> مشاهده
                  </Button>
                  {r.status === "پیش‌نویس" && <FinalizeButton id={r.id} onDone={reload} />}
                  <a
                    href={`/api/export?type=payroll&runId=${r.id}`}
                    className="inline-flex items-center gap-1 h-8 px-3 rounded-md border text-xs hover:bg-muted"
                  >
                    <FileSpreadsheet className="h-3.5 w-3.5" /> اکسل
                  </a>
                </div>
              </div>
            ))}
          </div>
        )}
      </Section>

      <CreateRunDialog open={createOpen} onOpenChange={setCreateOpen} onDone={reload} />
      <RunDetailDialog runId={viewRun} onClose={() => setViewRun(null)} />
    </div>
  );
}

function FinalizeButton({ id, onDone }: { id: string; onDone: () => void }) {
  const [busy, setBusy] = useState(false);
  const go = async () => {
    setBusy(true);
    try {
      await api(`/api/payroll/runs/${id}`, { method: "PATCH", body: JSON.stringify({ action: "finalize" }) });
      toast({ title: "لیست حقوق نهایی و قفل شد" });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Button size="sm" variant="outline" className="h-8" disabled={busy} onClick={go}>
      {busy ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Lock className="h-3.5 w-3.5" />} نهایی‌سازی
    </Button>
  );
}

function CreateRunDialog({ open, onOpenChange, onDone }: {
  open: boolean; onOpenChange: (v: boolean) => void; onDone: () => void;
}) {
  const now = new Date();
  const jNow = Math.floor((now.getTime() - new Date(now.getFullYear(), 2, 21).getTime()) / 31557600000) + 1404;
  const [jYear, setJYear] = useState(String(jNow));
  const [jMonth, setJMonth] = useState(String(now.getMonth() >= 3 ? now.getMonth() - 2 : now.getMonth() + 10));
  const [allowances, setAllowances] = useState("5000000");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await api("/api/payroll/runs", {
        method: "POST",
        body: JSON.stringify({ jYear: Number(jYear), jMonth: Number(jMonth), allowancesPerEmployee: Number(allowances) || 0 }),
      });
      toast({ title: "لیست حقوق محاسبه و ایجاد شد" });
      onOpenChange(false);
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-sm">
        <DialogHeader><DialogTitle className="text-base">محاسبه لیست حقوق ماهانه</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="grid grid-cols-2 gap-2">
            <div className="space-y-1.5">
              <label className="text-xs">سال جلالی</label>
              <Select value={jYear} onValueChange={setJYear}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{["1403", "1404", "1405"].map((y) => <SelectItem key={y} value={y}>{toFa(y)}</SelectItem>)}</SelectContent>
              </Select>
            </div>
            <div className="space-y-1.5">
              <label className="text-xs">ماه</label>
              <Select value={jMonth} onValueChange={setJMonth}>
                <SelectTrigger><SelectValue /></SelectTrigger>
                <SelectContent>{JALALI_MONTHS.map((m, i) => <SelectItem key={i} value={String(i + 1)}>{m}</SelectItem>)}</SelectContent>
              </Select>
            </div>
          </div>
          <div className="space-y-1.5">
            <label className="text-xs">مزایای ماهانه هر نفر (ریال)</label>
            <input
              dir="ltr"
              className="w-full h-9 rounded-md border bg-transparent px-3 text-sm text-left"
              value={allowances}
              onChange={(e) => setAllowances(e.target.value.replace(/\D/g, ""))}
            />
          </div>
          <p className="text-[10px] text-muted-foreground">
            فرمول: پایه + اضافه‌کاری (ضریب ۱٫۴) + مزایا − کسور غیبت − بیمه (۷٪) − مالیات (پلکانی ساده با معافیت ماهانه)
          </p>
          <Button className="w-full" disabled={busy} onClick={submit}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null} محاسبه و ایجاد
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function RunDetailDialog({ runId, onClose }: { runId: string | null; onClose: () => void }) {
  const { data } = useApiData<{ run: Run & { payslips: Payslip[] } }>(runId ? `/api/payroll/runs/${runId}` : "/api/payroll/runs");
  if (!runId) return null;
  const slips = data?.run.payslips ?? [];

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-4xl max-h-[85vh] overflow-y-auto thin-scroll">
        <DialogHeader>
          <DialogTitle className="text-base num-fa">
            فیش‌های حقوق — {JALALI_MONTHS[(data?.run.jMonth ?? 1) - 1]} {toFa(data?.run.jYear ?? "")}
          </DialogTitle>
        </DialogHeader>
        <div className="overflow-x-auto">
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>پرسنل</TableHead>
                <TableHead>پایه</TableHead>
                <TableHead>اضافه‌کاری</TableHead>
                <TableHead>مزایا</TableHead>
                <TableHead>غیبت</TableHead>
                <TableHead>بیمه</TableHead>
                <TableHead>مالیات</TableHead>
                <TableHead>خالص</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {slips.map((s) => (
                <TableRow key={s.id}>
                  <TableCell className="text-xs">{s.employee.firstName} {s.employee.lastName}</TableCell>
                  <TableCell className="num-fa text-xs">{formatNumber(s.baseSalary)}</TableCell>
                  <TableCell className="num-fa text-xs">{formatNumber(s.overtimePay)}</TableCell>
                  <TableCell className="num-fa text-xs">{formatNumber(s.allowances)}</TableCell>
                  <TableCell className="num-fa text-xs text-rose-600">{s.absentDeduct ? `− ${formatNumber(s.absentDeduct)}` : "—"}</TableCell>
                  <TableCell className="num-fa text-xs text-rose-600">− {formatNumber(s.insurance)}</TableCell>
                  <TableCell className="num-fa text-xs text-rose-600">{s.tax ? `− ${formatNumber(s.tax)}` : "معاف"}</TableCell>
                  <TableCell className="num-fa text-xs font-bold text-emerald-700">{formatNumber(s.netPay)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </div>
        {data?.run.status === "پیش‌نویس" && (
          <div className="flex justify-end">
            <Button size="sm" variant="outline" onClick={async () => {
              try {
                await api(`/api/payroll/runs/${runId}`, { method: "DELETE" });
                toast({ title: "پیش‌نویس حذف شد" });
                onClose();
              } catch (e) { errToast(e); }
            }}>
              <Trash2 className="h-3.5 w-3.5" /> حذف پیش‌نویس
            </Button>
          </div>
        )}
      </DialogContent>
    </Dialog>
  );
}
