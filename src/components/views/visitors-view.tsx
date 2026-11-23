"use client";

import { useState } from "react";
import {
  ShieldCheck, UserPlus, Users2, Building, AlertTriangle, LogIn, LogOut, QrCode, Check, X, ClipboardList,
} from "lucide-react";
import {
  useApiData, api, errToast, StatusBadge, Section, EmptyState, LoadingBlock, StatCard,
} from "@/components/shared";
import { formatJalali, formatJalaliLong, formatTime, toFa } from "@/lib/persian";
import type { VisitorT, ContractorT, AlertT } from "@/lib/api-types";
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

export function VisitorsView() {
  const { toast } = useToast();
  const all = useApiData<{ visitors: VisitorT[]; contractors: ContractorT[]; insideCount: number }>("/api/visitors", 15000);
  const alerts = useApiData<{ alerts: AlertT[] }>("/api/alerts", 15000);

  const act = async (id: string, action: string, extra?: string) => {
    try {
      const r = await api<{ card?: { cardNo: string; type: string } }>("/api/visitors", {
        method: "PATCH",
        body: JSON.stringify({ id, action }),
      });
      if (action === "checkin" && r.card) {
        toast({ title: "ورود ثبت و کارت موقت صادر شد", description: `کارت ${toFa(r.card.cardNo)} (${r.card.type}) — اعتبار ۸ ساعت` });
      } else {
        toast({ title: extra ?? "عملیات انجام شد" });
      }
      all.reload();
    } catch (e) {
      errToast(e);
    }
  };

  const resolve = async (id: string) => {
    try {
      await api("/api/alerts", { method: "PATCH", body: JSON.stringify({ id, resolved: true }) });
      toast({ title: "هشدار رسیدگی شد" });
      alerts.reload();
    } catch (e) {
      errToast(e);
    }
  };

  const visitors = all.data?.visitors ?? [];
  const pending = visitors.filter((v) => v.status === "در انتظار تأیید");
  const inside = visitors.filter((v) => v.status === "داخل سازمان");
  const openAlerts = (alerts.data?.alerts ?? []).filter((a) => !a.resolved);

  return (
    <Tabs defaultValue="register" className="space-y-4" dir="rtl">
      <TabsList className="flex-wrap h-auto">
        <TabsTrigger value="register" className="gap-1.5"><UserPlus className="h-4 w-4" />ثبت و تأیید</TabsTrigger>
        <TabsTrigger value="inside" className="gap-1.5"><Users2 className="h-4 w-4" />افراد حاضر</TabsTrigger>
        <TabsTrigger value="history" className="gap-1.5"><ClipboardList className="h-4 w-4" />سابقه مراجعین</TabsTrigger>
        <TabsTrigger value="contractors" className="gap-1.5"><Building className="h-4 w-4" />پیمانکاران</TabsTrigger>
        <TabsTrigger value="alerts" className="gap-1.5">
          <AlertTriangle className="h-4 w-4" />هشدارهای حراست
          {openAlerts.length > 0 && (
            <span className="num-fa mr-1 inline-flex h-5 min-w-5 items-center justify-center rounded-full bg-rose-500/20 text-rose-700 dark:text-rose-300 text-[10px] font-bold px-1">
              {toFa(openAlerts.length)}
            </span>
          )}
        </TabsTrigger>
      </TabsList>

      {/* -------- Register & approval workflow -------- */}
      <TabsContent value="register" className="space-y-4">
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          <StatCard icon={UserPlus} label="در انتظار تأیید" value={pending.length} tone="amber" />
          <StatCard icon={LogIn} label="داخل سازمان" value={inside.length} tone="green" />
          <StatCard icon={ClipboardList} label="کل ثبت امروز" value={visitors.filter((v) => new Date(v.requestedAt).toDateString() === new Date().toDateString()).length} />
          <StatCard icon={AlertTriangle} label="هشدارهای باز" value={openAlerts.length} tone={openAlerts.length ? "red" : "default"} />
        </div>

        <div className="grid lg:grid-cols-2 gap-4">
          <Section title="ثبت مراجع و مهمان (VIS-01)" icon={UserPlus}>
            <VisitorRegisterForm onDone={() => all.reload()} />
          </Section>

          <Section title="گردش تأیید ورود (VIS-04)" icon={ShieldCheck} contentClass="max-h-[420px] overflow-y-auto thin-scroll space-y-2">
            {pending.length === 0 ? <EmptyState text="درخواستی در انتظار تأیید نیست" /> : pending.map((v) => (
              <div key={v.id} className="rounded-xl border p-3 space-y-2 bg-card">
                <div className="flex items-center justify-between gap-2">
                  <div>
                    <div className="font-medium text-sm">{v.firstName} {v.lastName}</div>
                    <div className="num-fa text-[11px] text-muted-foreground">کد ملی: {toFa(v.nationalId)} • {v.idDocType}</div>
                  </div>
                  <StatusBadge value={v.type} />
                </div>
                <div className="text-xs text-muted-foreground space-y-0.5">
                  <p>میزبان: <span className="text-foreground font-medium">{v.hostName}</span> {v.hostDept ? `— ${v.hostDept}` : ""}</p>
                  <p>هدف مراجعه: {v.purpose}</p>
                  {v.contractor && <p>پیمانکار: {v.contractor.name}</p>}
                  <p className="num-fa">{formatJalaliLong(v.requestedAt)} — {formatTime(v.requestedAt)}</p>
                </div>
                <div className="flex gap-2 pt-1">
                  <Button size="sm" className="h-8 gap-1 flex-1" onClick={() => act(v.id, "approve", "ورود تأیید شد")}>
                    <Check className="h-3.5 w-3.5" />تأیید حراست
                  </Button>
                  <Button size="sm" variant="outline" className="h-8 gap-1 flex-1 text-rose-700 border-rose-500/40" onClick={() => act(v.id, "reject", "درخواست رد شد")}>
                    <X className="h-3.5 w-3.5" />رد
                  </Button>
                </div>
              </div>
            ))}
          </Section>
        </div>

        {/* approved → waiting entry */}
        {visitors.filter((v) => v.status === "تأیید شده").length > 0 && (
          <Section title="تأییدشده‌های در انتظار ورود" icon={QrCode} contentClass="grid sm:grid-cols-2 xl:grid-cols-3 gap-2">
            {visitors.filter((v) => v.status === "تأیید شده").map((v) => (
              <div key={v.id} className="rounded-xl border p-3 space-y-2">
                <div className="flex items-center justify-between">
                  <span className="font-medium text-sm">{v.firstName} {v.lastName}</span>
                  <StatusBadge value={v.status} />
                </div>
                <p className="text-xs text-muted-foreground">میزبان: {v.hostName} — {v.purpose}</p>
                <Button size="sm" className="h-8 gap-1 w-full" onClick={() => act(v.id, "checkin")}>
                  <LogIn className="h-3.5 w-3.5" />ثبت ورود + صدور کارت موقت
                </Button>
              </div>
            ))}
          </Section>
        )}
      </TabsContent>

      {/* -------- Inside visitors -------- */}
      <TabsContent value="inside">
        <Section title="افراد حاضر در سازمان (VIS-05)" icon={Users2} contentClass="grid sm:grid-cols-2 xl:grid-cols-3 gap-3">
          {inside.length === 0 ? <EmptyState text="کسی داخل سازمان نیست" /> : inside.map((v) => {
            const entered = v.logs?.find((l) => l.direction === "ورود");
            const hours = entered ? (Date.now() - new Date(entered.timestamp).getTime()) / 3600000 : 0;
            return (
              <div key={v.id} className={cn("rounded-xl border p-3.5 space-y-2 bg-card", hours > 8 && "border-amber-500/50")}>
                <div className="flex items-center justify-between">
                  <span className="font-semibold text-sm">{v.firstName} {v.lastName}</span>
                  <StatusBadge value={v.type} />
                </div>
                <p className="text-xs text-muted-foreground">میزبان: {v.hostName} {v.hostDept ? `— ${v.hostDept}` : ""}</p>
                <p className="text-xs text-muted-foreground">{v.purpose}</p>
                {v.card && !v.card.returned && (
                  <div className="rounded-lg bg-muted/60 p-2 num-fa text-[11px] space-y-0.5">
                    <div className="flex justify-between"><span>کارت موقت:</span><span className="font-bold">{toFa(v.card.cardNo)}</span></div>
                    <div className="flex justify-between"><span>نوع:</span><span>{v.card.type}</span></div>
                    <div className="flex justify-between"><span>انقضا:</span><span>{formatTime(v.card.validUntil)}</span></div>
                  </div>
                )}
                <div className="flex items-center justify-between">
                  <span className={cn("num-fa text-[11px]", hours > 8 ? "text-amber-600 font-bold" : "text-muted-foreground")}>
                    {entered ? `ورود: ${formatTime(entered.timestamp)} (${toFa(Math.floor(hours))} ساعت)` : "—"}
                  </span>
                  <Button size="sm" variant="outline" className="h-7 gap-1 text-[11px]" onClick={() => act(v.id, "checkout", "خروج ثبت شد")}>
                    <LogOut className="h-3 w-3" />ثبت خروج
                  </Button>
                </div>
                {hours > 8 && <p className="text-[10px] text-amber-600 font-medium">بیش از ۸ ساعت داخل سایت — هشدار تأخیر خروج</p>}
              </div>
            );
          })}
        </Section>
      </TabsContent>

      {/* -------- History -------- */}
      <TabsContent value="history">
        <Section title="سابقه کامل مراجعین (VIS-06)" icon={ClipboardList} contentClass="p-0">
          {all.loading ? <div className="p-4"><LoadingBlock /></div> : visitors.length === 0 ? (
            <div className="p-4"><EmptyState text="سابقه‌ای وجود ندارد" /></div>
          ) : (
            <div className="overflow-x-auto thin-scroll">
              <Table>
                <TableHeader>
                  <TableRow className="bg-muted/40">
                    <TableHead className="text-right">نام</TableHead>
                    <TableHead className="text-right">نوع</TableHead>
                    <TableHead className="text-right">میزبان</TableHead>
                    <TableHead className="text-right">هدف</TableHead>
                    <TableHead className="text-right">پیمانکار</TableHead>
                    <TableHead className="text-right">کارت موقت</TableHead>
                    <TableHead className="text-right">تاریخ ثبت</TableHead>
                    <TableHead className="text-right">وضعیت</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {visitors.map((v) => (
                    <TableRow key={v.id} className="hover:bg-muted/30">
                      <TableCell>
                        <div className="font-medium text-xs">{v.firstName} {v.lastName}</div>
                        <div className="num-fa text-[10px] text-muted-foreground">{toFa(v.nationalId)}</div>
                      </TableCell>
                      <TableCell><StatusBadge value={v.type} /></TableCell>
                      <TableCell className="text-xs">{v.hostName}</TableCell>
                      <TableCell className="text-xs max-w-44 truncate">{v.purpose}</TableCell>
                      <TableCell className="text-xs">{v.contractor?.name ?? "—"}</TableCell>
                      <TableCell className="num-fa text-[11px]">
                        {v.card ? `${toFa(v.card.cardNo)} (${v.card.type})${v.card.returned ? " — برگشتی" : ""}` : "—"}
                      </TableCell>
                      <TableCell className="num-fa text-[11px]">{formatJalali(v.requestedAt)}</TableCell>
                      <TableCell><StatusBadge value={v.status} /></TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </Section>
      </TabsContent>

      {/* -------- Contractors -------- */}
      <TabsContent value="contractors">
        <ContractorsPanel onReload={all.reload} contractors={all.data?.contractors ?? []} />
      </TabsContent>

      {/* -------- Security alerts -------- */}
      <TabsContent value="alerts">
        <Section title="هشدارهای حراست و کنترل (VIS-05)" icon={AlertTriangle} contentClass="max-h-[560px] overflow-y-auto thin-scroll space-y-2">
          {(alerts.data?.alerts ?? []).length === 0 ? <EmptyState text="هشداری ثبت نشده است" /> : (alerts.data?.alerts ?? []).map((a) => (
            <div key={a.id} className={cn("flex items-start gap-3 rounded-xl border p-3", a.resolved ? "opacity-55" : "bg-card")}>
              <StatusBadge value={a.severity} />
              <div className="flex-1 min-w-0">
                <p className="text-sm leading-relaxed">{a.message}</p>
                <p className="num-fa text-[11px] text-muted-foreground mt-1">
                  {a.type} • {a.source} • {formatJalali(a.createdAt)} {formatTime(a.createdAt)}
                </p>
              </div>
              {!a.resolved && (
                <Button size="sm" variant="outline" className="h-7 text-[11px] shrink-0" onClick={() => resolve(a.id)}>رسیدگی</Button>
              )}
              {a.resolved && <Badge variant="secondary" className="shrink-0">رسیدگی شده</Badge>}
            </div>
          ))}
        </Section>
      </TabsContent>
    </Tabs>
  );
}

// ================= Visitor register form =================
function VisitorRegisterForm({ onDone }: { onDone: () => void }) {
  const { data } = useApiData<{ contractors: ContractorT[] }>("/api/contractors");
  const [form, setForm] = useState({
    firstName: "", lastName: "", nationalId: "", phone: "",
    idDocType: "کارت ملی", type: "مهمان", hostName: "", hostDept: "", purpose: "", contractorId: "",
  });
  const [saving, setSaving] = useState(false);
  const { toast } = useToast();
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    if (!form.firstName || !form.lastName || !form.nationalId) {
      errToast(new Error("نام، نام خانوادگی و کد ملی الزامی است"));
      return;
    }
    setSaving(true);
    try {
      await api("/api/visitors", { method: "POST", body: JSON.stringify({ ...form, contractorId: form.contractorId || null }) });
      toast({ title: "مراجع ثبت شد", description: "برای ورود، تأیید حراست الزامی است (VIS-04)" });
      setForm({ firstName: "", lastName: "", nationalId: "", phone: "", idDocType: "کارت ملی", type: "مهمان", hostName: "", hostDept: "", purpose: "", contractorId: "" });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-3">
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5"><Label>نام *</Label><Input value={form.firstName} onChange={(e) => set("firstName", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>نام خانوادگی *</Label><Input value={form.lastName} onChange={(e) => set("lastName", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد ملی *</Label><Input dir="ltr" className="text-right" value={form.nationalId} onChange={(e) => set("nationalId", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>تلفن</Label><Input dir="ltr" className="text-right" value={form.phone} onChange={(e) => set("phone", e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>نوع مراجع</Label>
          <Select value={form.type} onValueChange={(v) => set("type", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {["مهمان", "مراجع", "پیمانکار"].map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        <div className="space-y-1.5">
          <Label>مدرک شناسایی</Label>
          <Select value={form.idDocType} onValueChange={(v) => set("idDocType", v)}>
            <SelectTrigger><SelectValue /></SelectTrigger>
            <SelectContent>
              {["کارت ملی", "گواهینامه", "شناسنامه", "پاسپورت"].map((t) => <SelectItem key={t} value={t}>{t}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
        {form.type === "پیمانکار" && (
          <div className="space-y-1.5 col-span-2">
            <Label>شرکت پیمانکار</Label>
            <Select value={form.contractorId || undefined} onValueChange={(v) => set("contractorId", v)}>
              <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
              <SelectContent>
                {(data?.contractors ?? []).map((c) => <SelectItem key={c.id} value={c.id}>{c.name}</SelectItem>)}
              </SelectContent>
            </Select>
          </div>
        )}
        <div className="space-y-1.5"><Label>شخص میزبان *</Label><Input value={form.hostName} onChange={(e) => set("hostName", e.target.value)} placeholder="نام میزبان" /></div>
        <div className="space-y-1.5"><Label>واحد میزبان</Label><Input value={form.hostDept} onChange={(e) => set("hostDept", e.target.value)} /></div>
        <div className="space-y-1.5 col-span-2"><Label>دلیل مراجعه *</Label><Input value={form.purpose} onChange={(e) => set("purpose", e.target.value)} /></div>
      </div>
      <Button onClick={submit} disabled={saving} className="w-full gap-1.5">
        <UserPlus className="h-4 w-4" />{saving ? "در حال ثبت..." : "ثبت مراجع"}
      </Button>
    </div>
  );
}

// ================= Contractors panel =================
function ContractorsPanel({ contractors, onReload }: { contractors: ContractorT[]; onReload: () => void }) {
  const [open, setOpen] = useState(false);
  const { toast } = useToast();

  return (
    <Section
      title="مدیریت شرکت‌های پیمانکار (VIS-03)"
      icon={Building}
      action={
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild><Button size="sm" variant="outline" className="gap-1.5"><UserPlus className="h-4 w-4" />پیمانکار جدید</Button></DialogTrigger>
          <DialogContent className="max-w-md">
            <ContractorForm onDone={() => { setOpen(false); onReload(); toast({ title: "پیمانکار ثبت شد" }); }} />
          </DialogContent>
        </Dialog>
      }
      contentClass="p-0"
    >
      {contractors.length === 0 ? (
        <div className="p-4"><EmptyState text="پیمانکاری ثبت نشده است" /></div>
      ) : (
        <div className="overflow-x-auto thin-scroll">
          <Table>
            <TableHeader>
              <TableRow className="bg-muted/40">
                <TableHead className="text-right">شرکت</TableHead>
                <TableHead className="text-right">کد</TableHead>
                <TableHead className="text-right">رابط</TableHead>
                <TableHead className="text-right">تلفن</TableHead>
                <TableHead className="text-right">شماره قرارداد</TableHead>
                <TableHead className="text-right">اعتبار مجوز کار</TableHead>
                <TableHead className="text-right">مراجعین</TableHead>
                <TableHead className="text-right">وضعیت</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {contractors.map((c) => {
                const expired = c.validUntil ? new Date(c.validUntil) < new Date() : false;
                return (
                  <TableRow key={c.id} className="hover:bg-muted/30">
                    <TableCell className="font-medium">{c.name}</TableCell>
                    <TableCell className="num-fa">{toFa(c.code)}</TableCell>
                    <TableCell className="text-xs">{c.contactName ?? "—"}</TableCell>
                    <TableCell className="num-fa text-xs">{c.phone ? toFa(c.phone) : "—"}</TableCell>
                    <TableCell className="num-fa text-xs">{c.contractNo ? toFa(c.contractNo) : "—"}</TableCell>
                    <TableCell className="num-fa text-xs">
                      {c.validUntil ? formatJalali(c.validUntil) : "—"}
                      {expired && <Badge variant="outline" className="mr-1.5 text-[10px] border-rose-500/40 text-rose-700">منقضی</Badge>}
                    </TableCell>
                    <TableCell className="num-fa">{toFa(c.visitors?.length ?? 0)}</TableCell>
                    <TableCell>
                      <StatusBadge value={expired ? "منقضی" : c.active ? "فعال" : "غیرفعال"} />
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </div>
      )}
    </Section>
  );
}

function ContractorForm({ onDone }: { onDone: () => void }) {
  const [form, setForm] = useState({ name: "", code: "", contactName: "", phone: "", contractNo: "", validUntil: "" });
  const [saving, setSaving] = useState(false);
  const set = (k: string, v: string) => setForm((f) => ({ ...f, [k]: v }));

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/contractors", {
        method: "POST",
        body: JSON.stringify({ ...form, validUntil: form.validUntil ? new Date(form.validUntil).toISOString() : null }),
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
      <DialogHeader><DialogTitle>ثبت شرکت پیمانکار (VIS-03)</DialogTitle></DialogHeader>
      <div className="grid grid-cols-2 gap-3">
        <div className="space-y-1.5 col-span-2"><Label>نام شرکت *</Label><Input value={form.name} onChange={(e) => set("name", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد *</Label><Input dir="ltr" className="text-right" value={form.code} onChange={(e) => set("code", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>شماره قرارداد</Label><Input dir="ltr" className="text-right" value={form.contractNo} onChange={(e) => set("contractNo", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>نام رابط</Label><Input value={form.contactName} onChange={(e) => set("contactName", e.target.value)} /></div>
        <div className="space-y-1.5"><Label>تلفن</Label><Input dir="ltr" className="text-right" value={form.phone} onChange={(e) => set("phone", e.target.value)} /></div>
        <div className="space-y-1.5 col-span-2"><Label>اعتبار مجوز کار تا</Label><Input type="date" value={form.validUntil} onChange={(e) => set("validUntil", e.target.value)} /></div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "..." : "ثبت"}</Button></DialogFooter>
    </div>
  );
}
