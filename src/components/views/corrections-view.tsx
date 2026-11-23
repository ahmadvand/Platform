"use client";

import { useState } from "react";
import { FilePenLine, Check, X, Plus, Clock3 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  api, errToast, useApiData, StatusBadge, Section, LoadingBlock, EmptyState,
} from "@/components/shared";
import { toast } from "@/hooks/use-toast";
import { toFa, formatJalali, formatTime } from "@/lib/persian";

interface Correction {
  id: string;
  employeeId: string;
  date: string;
  requestedIn: string | null;
  requestedOut: string | null;
  reason: string;
  status: string;
  reviewedBy: string | null;
  createdAt: string;
  employee: { firstName: string; lastName: string; personnelCode: string; department?: { name: string } | null };
}

export function CorrectionsView() {
  const [tab, setTab] = useState("all");
  const { data, loading, reload } = useApiData<{ corrections: Correction[] }>(
    `/api/corrections${tab === "all" ? "" : `?status=${encodeURIComponent(tab)}`}`
  );
  const [createOpen, setCreateOpen] = useState(false);
  const review = async (id: string, action: "approve" | "reject") => {
    try {
      await api("/api/corrections", { method: "PATCH", body: JSON.stringify({ id, action }) });
      toast({ title: action === "approve" ? "اصلاح تأیید و اعمال شد" : "درخواست رد شد" });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  const rows = data?.corrections ?? [];
  const pending = rows.filter((r) => r.status === "در انتظار تأیید").length;

  return (
    <div className="space-y-4 min-w-0">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="all">همه</TabsTrigger>
            <TabsTrigger value="در انتظار تأیید">در انتظار {pending > 0 && `(${toFa(pending)})`}</TabsTrigger>
            <TabsTrigger value="تأیید شده">تأییدشده</TabsTrigger>
            <TabsTrigger value="رد شده">ردشده</TabsTrigger>
          </TabsList>
        </Tabs>
        <Button size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" /> درخواست اصلاح جدید
        </Button>
      </div>

      <Section title="درخواست‌های اصلاح تردد" icon={FilePenLine}>
        {loading ? <LoadingBlock rows={4} /> : rows.length === 0 ? (
          <EmptyState text="درخواستی یافت نشد" />
        ) : (
          <div className="space-y-2">
            {rows.map((c) => (
              <div key={c.id} className="border rounded-lg p-3 space-y-2">
                <div className="flex flex-wrap items-center gap-2 text-sm">
                  <b>{c.employee.firstName} {c.employee.lastName}</b>
                  <span className="text-muted-foreground num-fa text-xs">({c.employee.personnelCode})</span>
                  <StatusBadge value={c.status} />
                  <span className="flex-1" />
                  <span className="num-fa text-xs text-muted-foreground">تاریخ: {formatJalali(c.date)}</span>
                </div>
                <div className="flex flex-wrap items-center gap-3 text-xs text-muted-foreground">
                  <span className="flex items-center gap-1"><Clock3 className="h-3.5 w-3.5" />
                    ورود اصلاحی: <b className="num-fa text-foreground">{c.requestedIn ? formatTime(c.requestedIn) : "—"}</b>
                    — خروج اصلاحی: <b className="num-fa text-foreground">{c.requestedOut ? formatTime(c.requestedOut) : "—"}</b>
                  </span>
                </div>
                <p className="text-xs">دلیل: {c.reason}</p>
                {c.status === "در انتظار تأیید" && (
                  <div className="flex gap-2">
                    <Button size="sm" className="h-8 bg-emerald-600 hover:bg-emerald-500 text-white" onClick={() => review(c.id, "approve")}>
                      <Check className="h-3.5 w-3.5" /> تأیید و اعمال
                    </Button>
                    <Button size="sm" variant="outline" className="h-8" onClick={() => review(c.id, "reject")}>
                      <X className="h-3.5 w-3.5" /> رد
                    </Button>
                  </div>
                )}
                {c.reviewedBy && (
                  <p className="text-[10px] text-muted-foreground">بررسی‌کننده: {c.reviewedBy}</p>
                )}
              </div>
            ))}
          </div>
        )}
      </Section>

      <CreateCorrectionDialog open={createOpen} onOpenChange={setCreateOpen} onDone={reload} />
    </div>
  );
}

function CreateCorrectionDialog({
  open, onOpenChange, onDone,
}: {
  open: boolean;
  onOpenChange: (v: boolean) => void;
  onDone: () => void;
}) {
  const employees = useApiData<{ employees: { id: string; firstName: string; lastName: string; personnelCode: string }[] }>("/api/employees").data;
  const [employeeId, setEmployeeId] = useState("");
  const [date, setDate] = useState("");
  const [inTime, setInTime] = useState("");
  const [outTime, setOutTime] = useState("");
  const [reason, setReason] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await api("/api/corrections", {
        method: "POST",
        body: JSON.stringify({
          employeeId,
          date,
          requestedIn: inTime ? `${date}T${inTime}:00` : undefined,
          requestedOut: outTime ? `${date}T${outTime}:00` : undefined,
          reason,
        }),
      });
      toast({ title: "درخواست اصلاح ثبت شد" });
      onOpenChange(false);
      setReason("");
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md">
        <DialogHeader>
          <DialogTitle className="text-base">ثبت درخواست اصلاح تردد</DialogTitle>
        </DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>پرسنل</Label>
            <Select value={employeeId} onValueChange={setEmployeeId}>
              <SelectTrigger><SelectValue placeholder="انتخاب..." /></SelectTrigger>
              <SelectContent className="max-h-60">
                {employees?.employees.map((e) => (
                  <SelectItem key={e.id} value={e.id}>{e.firstName} {e.lastName} ({e.personnelCode})</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
          <div className="grid grid-cols-3 gap-2">
            <div className="space-y-1.5">
              <Label>تاریخ</Label>
              <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>ورود</Label>
              <Input type="time" value={inTime} onChange={(e) => setInTime(e.target.value)} />
            </div>
            <div className="space-y-1.5">
              <Label>خروج</Label>
              <Input type="time" value={outTime} onChange={(e) => setOutTime(e.target.value)} />
            </div>
          </div>
          <div className="space-y-1.5">
            <Label>دلیل اصلاح</Label>
            <Textarea value={reason} onChange={(e) => setReason(e.target.value)} rows={2} placeholder="مثلاً فراموشی کارت..." />
          </div>
          <Button className="w-full" disabled={!employeeId || !date || !reason || busy} onClick={submit}>
            ثبت درخواست
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
