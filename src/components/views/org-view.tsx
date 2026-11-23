"use client";

import { useState } from "react";
import { Building2, Network, Plus, ChevronDown, ChevronLeft, Briefcase } from "lucide-react";
import { useApiData, api, errToast, Section, LoadingBlock } from "@/components/shared";
import { toFa } from "@/lib/persian";
import type { DeptT, PositionT } from "@/lib/api-types";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger, DialogFooter } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { useToast } from "@/hooks/use-toast";
import { cn } from "@/lib/utils";

export function OrgView() {
  const { data, loading, reload } = useApiData<{ companies: { id: string; name: string; code: string }[]; departments: DeptT[]; positions: PositionT[] }>("/api/org");
  const [open, setOpen] = useState<Record<string, boolean>>({});
  const [deptDialog, setDeptDialog] = useState(false);
  const [posDialog, setPosDialog] = useState(false);
  const { toast } = useToast();

  if (loading) return <LoadingBlock rows={5} />;
  const departments = data?.departments ?? [];
  const positions = data?.positions ?? [];
  const company = data?.companies?.[0];

  const roots = departments.filter((d) => !d.parentId);
  const childrenOf = (id: string) => departments.filter((d) => d.parentId === id);

  const toggle = (id: string) => setOpen((o) => ({ ...o, [id]: !o[id] }));

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        {company && (
          <div className="flex items-center gap-3 rounded-xl border bg-card p-3 shadow-sm">
            <div className="rounded-lg bg-primary/10 p-2.5 text-primary"><Building2 className="h-6 w-6" /></div>
            <div>
              <div className="font-bold">{company.name}</div>
              <div className="text-xs text-muted-foreground num-fa">کد شرکت: {toFa(company.code)} • {toFa(departments.length)} واحد سازمانی • {toFa(positions.length)} پست سازمانی</div>
            </div>
          </div>
        )}
        <div className="flex gap-2">
          <Dialog open={deptDialog} onOpenChange={setDeptDialog}>
            <DialogTrigger asChild>
              <Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />واحد جدید</Button>
            </DialogTrigger>
            <DialogContent className="max-w-md">
              <DeptForm departments={departments} onDone={() => { setDeptDialog(false); reload(); toast({ title: "واحد سازمانی ایجاد شد" }); }} />
            </DialogContent>
          </Dialog>
          <Dialog open={posDialog} onOpenChange={setPosDialog}>
            <DialogTrigger asChild>
              <Button size="sm" variant="outline" className="gap-1.5"><Plus className="h-4 w-4" />پست سازمانی جدید</Button>
            </DialogTrigger>
            <DialogContent className="max-w-md">
              <PosForm departments={departments} onDone={() => { setPosDialog(false); reload(); toast({ title: "پست سازمانی ایجاد شد" }); }} />
            </DialogContent>
          </Dialog>
        </div>
      </div>

      <div className="grid lg:grid-cols-2 gap-4">
        <Section title="چارت سازمانی" icon={Network} contentClass="max-h-[520px] overflow-y-auto thin-scroll space-y-1.5">
          {roots.map((d) => (
            <DeptNode key={d.id} dept={d} childrenOf={childrenOf} open={open} toggle={toggle} depth={0} />
          ))}
        </Section>

        <Section title="پست‌ها و شرح شغل" icon={Briefcase} contentClass="max-h-[520px] overflow-y-auto thin-scroll space-y-2">
          {positions.map((p) => (
            <div key={p.id} className="flex items-center gap-3 rounded-lg border p-2.5 text-sm">
              <Badge variant="outline" className="num-fa shrink-0">{toFa(p.code)}</Badge>
              <div className="flex-1 min-w-0">
                <div className="font-medium">{p.title}</div>
                <div className="text-xs text-muted-foreground">{p.department?.name ?? "—"}{p.grade ? ` • گروه ${p.grade}` : ""}</div>
              </div>
              <Badge variant="secondary" className="num-fa shrink-0">{toFa(p.employees?.length ?? 0)} نفر</Badge>
            </div>
          ))}
        </Section>
      </div>
    </div>
  );
}

function DeptNode({
  dept, childrenOf, open, toggle, depth,
}: {
  dept: DeptT;
  childrenOf: (id: string) => DeptT[];
  open: Record<string, boolean>;
  toggle: (id: string) => void;
  depth: number;
}) {
  const kids = childrenOf(dept.id);
  const hasKids = kids.length > 0;
  const isOpen = open[dept.id] ?? depth < 1;

  return (
    <div>
      <div
        className={cn(
          "flex items-center gap-2 rounded-lg border p-2.5 text-sm bg-card hover:bg-muted/40 transition-colors",
          depth === 0 && "font-semibold border-primary/30 bg-primary/5"
        )}
        style={{ marginRight: depth * 20 }}
      >
        <button onClick={() => hasKids && toggle(dept.id)} className={cn("shrink-0", !hasKids && "opacity-0 pointer-events-none")}>
          {isOpen ? <ChevronDown className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
        </button>
        <div className="flex-1 min-w-0">
          <span>{dept.name}</span>
          <span className="num-fa text-[10px] text-muted-foreground mr-2">({toFa(dept.code)})</span>
        </div>
        <Badge variant="secondary" className="num-fa shrink-0">{toFa(dept.employees?.length ?? 0)} پرسنل</Badge>
        <Badge variant="outline" className="num-fa shrink-0">{toFa(dept.positions?.length ?? 0)} پست</Badge>
      </div>
      {isOpen && kids.map((k) => (
        <DeptNode key={k.id} dept={k} childrenOf={childrenOf} open={open} toggle={toggle} depth={depth + 1} />
      ))}
    </div>
  );
}

function DeptForm({ departments, onDone }: { departments: DeptT[]; onDone: () => void }) {
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [parentId, setParentId] = useState("");
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/org", { method: "POST", body: JSON.stringify({ kind: "department", name, code, parentId: parentId || null }) });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <DialogHeader><DialogTitle>ایجاد واحد سازمانی (HR-02)</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <div className="space-y-1.5"><Label>نام واحد *</Label><Input value={name} onChange={(e) => setName(e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد واحد *</Label><Input dir="ltr" className="text-right" value={code} onChange={(e) => setCode(e.target.value)} /></div>
        <div className="space-y-1.5">
          <Label>واحد بالادست</Label>
          <Select value={parentId || undefined} onValueChange={setParentId}>
            <SelectTrigger><SelectValue placeholder="بدون والد (واحد اصلی)" /></SelectTrigger>
            <SelectContent>
              {departments.map((d) => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "در حال ثبت..." : "ایجاد"}</Button></DialogFooter>
    </div>
  );
}

function PosForm({ departments, onDone }: { departments: DeptT[]; onDone: () => void }) {
  const [title, setTitle] = useState("");
  const [code, setCode] = useState("");
  const [grade, setGrade] = useState("");
  const [departmentId, setDepartmentId] = useState("");
  const [saving, setSaving] = useState(false);

  const submit = async () => {
    setSaving(true);
    try {
      await api("/api/org", { method: "POST", body: JSON.stringify({ kind: "position", title, code, grade, departmentId }) });
      onDone();
    } catch (e) {
      errToast(e);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="space-y-4">
      <DialogHeader><DialogTitle>ایجاد پست سازمانی (HR-02)</DialogTitle></DialogHeader>
      <div className="space-y-3">
        <div className="space-y-1.5"><Label>عنوان شغل *</Label><Input value={title} onChange={(e) => setTitle(e.target.value)} /></div>
        <div className="space-y-1.5"><Label>کد پست *</Label><Input dir="ltr" className="text-right" value={code} onChange={(e) => setCode(e.target.value)} /></div>
        <div className="space-y-1.5"><Label>گروه شغلی</Label><Input value={grade} onChange={(e) => setGrade(e.target.value)} placeholder="مثلاً گروه ۳" /></div>
        <div className="space-y-1.5">
          <Label>واحد سازمانی *</Label>
          <Select value={departmentId || undefined} onValueChange={setDepartmentId}>
            <SelectTrigger><SelectValue placeholder="انتخاب کنید" /></SelectTrigger>
            <SelectContent>
              {departments.map((d) => <SelectItem key={d.id} value={d.id}>{d.name}</SelectItem>)}
            </SelectContent>
          </Select>
        </div>
      </div>
      <DialogFooter><Button onClick={submit} disabled={saving}>{saving ? "در حال ثبت..." : "ایجاد"}</Button></DialogFooter>
    </div>
  );
}
