"use client";

import { useCallback, useState } from "react";
import {
  UserCog, Plus, KeyRound, LockOpen, Ban, ShieldCheck, Loader2, ScrollText, RotateCcw,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Checkbox } from "@/components/ui/checkbox";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import {
  api, errToast, useApiData, Section, LoadingBlock, EmptyState,
} from "@/components/shared";
import { toast } from "@/hooks/use-toast";
import { toFa, formatJalali, formatTime } from "@/lib/persian";

interface UserRow {
  id: number;
  username: string;
  fullName: string;
  isActive: boolean;
  isSuperadmin: boolean;
  mustChangePassword: boolean;
  lockedUntil: string | null;
  lastLoginAt: string | null;
  failedLoginAttempts: number;
  employee?: { personnelCode: string; firstName: string; lastName: string } | null;
  roles: { role: { id: number; name: string; title: string } }[];
}

interface RoleRow {
  id: number;
  name: string;
  title: string;
  description: string | null;
  userCount: number;
  permissions: string[];
}

interface PermDef { key: string; title: string; grp: string }

export function UsersView() {
  const users = useApiData<{ users: UserRow[] }>("/api/users");
  const roles = useApiData<{ roles: RoleRow[] }>("/api/roles");
  const permsData = useApiData<{ permissions: PermDef[] }>("/api/permissions");
  const [tab, setTab] = useState("users");
  const [createOpen, setCreateOpen] = useState(false);

  const act = useCallback(async (id: number, action: string, body?: unknown, msg?: string) => {
    try {
      await api(`/api/users/${id}`, { method: "PATCH", body: JSON.stringify({ action, ...body }) });
      toast({ title: msg ?? "عملیات انجام شد" });
      users.reload();
    } catch (e) {
      errToast(e);
    }
  }, [users]);

  const revoke = async (id: number) => {
    try {
      const r = await api<{ revoked: number }>(`/api/users/${id}/revoke-sessions`, { method: "POST" });
      toast({ title: `${toFa(r.revoked)} نشست فعال باطل شد` });
    } catch (e) {
      errToast(e);
    }
  };

  const resetPw = async (id: number) => {
    const pw = prompt("رمز عبور جدید (حداقل ۸ کاراکتر شامل حرف و رقم):");
    if (!pw) return;
    await act(id, "reset-password", { password: pw }, "رمز بازنشانی شد — کاربر باید در ورود بعدی تغییر دهد");
  };

  const rows = users.data?.users ?? [];

  return (
    <div className="space-y-4 min-w-0">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <Tabs value={tab} onValueChange={setTab}>
          <TabsList>
            <TabsTrigger value="users">کاربران</TabsTrigger>
            <TabsTrigger value="roles">نقش‌ها و دسترسی‌ها</TabsTrigger>
          </TabsList>
        </Tabs>
        <Button size="sm" onClick={() => setCreateOpen(true)}>
          <Plus className="h-4 w-4" /> کاربر جدید
        </Button>
      </div>

      {tab === "users" && (
        <Section title="حساب‌های کاربری" icon={UserCog}>
          {users.loading ? <LoadingBlock rows={4} /> : rows.length === 0 ? (
            <EmptyState text="کاربری یافت نشد" />
          ) : (
            <div className="space-y-2">
              {rows.map((u) => {
                const locked = u.lockedUntil && new Date(u.lockedUntil) > new Date();
                return (
                  <div key={u.id} className="border rounded-lg p-3 space-y-2">
                    <div className="flex flex-wrap items-center gap-2 text-sm">
                      <b>{u.fullName}</b>
                      <span dir="ltr" className="font-mono text-xs text-muted-foreground">{u.username}</span>
                      {u.isSuperadmin && <Badge className="bg-violet-500/15 text-violet-700 border-violet-500/30">مدیر ارشد</Badge>}
                      {u.isActive ? (
                        <Badge className="bg-emerald-500/15 text-emerald-700 border-emerald-500/30">فعال</Badge>
                      ) : (
                        <Badge className="bg-muted text-muted-foreground border-border">غیرفعال</Badge>
                      )}
                      {locked && (
                        <Badge className="bg-rose-500/15 text-rose-700 border-rose-500/30 num-fa">
                          قفل تا {formatTime(u.lockedUntil)}
                        </Badge>
                      )}
                      <span className="flex-1" />
                      <span className="text-[10px] text-muted-foreground num-fa">
                        آخرین ورود: {u.lastLoginAt ? `${formatJalali(u.lastLoginAt)} ${formatTime(u.lastLoginAt)}` : "—"}
                      </span>
                    </div>
                    <div className="flex flex-wrap items-center gap-2">
                      {u.roles.map((r) => (
                        <Badge key={r.role.id} variant="outline" className="text-[10px]">{r.role.title}</Badge>
                      ))}
                      {u.employee && (
                        <span className="text-[10px] text-muted-foreground num-fa">پرسنل: {u.employee.personnelCode}</span>
                      )}
                    </div>
                    <div className="flex flex-wrap gap-1.5">
                      <Button size="sm" variant="outline" className="h-7 text-[11px]" onClick={() => resetPw(u.id)}>
                        <KeyRound className="h-3 w-3" /> بازنشانی رمز
                      </Button>
                      {locked && (
                        <Button size="sm" variant="outline" className="h-7 text-[11px]" onClick={() => act(u.id, "unlock", null, "قفل حساب برداشته شد")}>
                          <LockOpen className="h-3 w-3" /> رفع قفل
                        </Button>
                      )}
                      <Button size="sm" variant="outline" className="h-7 text-[11px]" onClick={() => revoke(u.id)}>
                        <RotateCcw className="h-3 w-3" /> ابطال نشست‌ها
                      </Button>
                      <Button size="sm" variant="outline" className="h-7 text-[11px]" onClick={() => act(u.id, "toggle-active", null, u.isActive ? "حساب غیرفعال شد" : "حساب فعال شد")}>
                        <Ban className="h-3 w-3" /> {u.isActive ? "غیرفعال‌سازی" : "فعال‌سازی"}
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </Section>
      )}

      {tab === "roles" && (
        <RolesPanel
          roles={roles.data?.roles ?? []}
          loading={roles.loading}
          perms={permsData.data?.permissions ?? []}
          reload={() => { roles.reload(); }}
        />
      )}

      <CreateUserDialog
        open={createOpen}
        onOpenChange={setCreateOpen}
        roles={roles.data?.roles ?? []}
        onDone={() => users.reload()}
      />
    </div>
  );
}

function RolesPanel({ roles, loading, perms, reload }: {
  roles: RoleRow[]; loading: boolean; perms: PermDef[]; reload: () => void;
}) {
  const [editRole, setEditRole] = useState<RoleRow | null>(null);

  if (loading) return <LoadingBlock rows={4} />;

  return (
    <Section title="نقش‌ها و ماتریس دسترسی" icon={ShieldCheck}>
      <div className="space-y-2">
        {roles.map((r) => (
          <div key={r.id} className="border rounded-lg p-3 space-y-2">
            <div className="flex flex-wrap items-center gap-2 text-sm">
              <b>{r.title}</b>
              <span dir="ltr" className="font-mono text-[10px] text-muted-foreground">{r.name}</span>
              <Badge variant="outline" className="text-[10px] num-fa">{toFa(r.userCount)} کاربر</Badge>
              <span className="flex-1" />
              {r.name !== "superadmin" && (
                <Button size="sm" variant="outline" className="h-7 text-[11px]" onClick={() => setEditRole(r)}>
                  <ScrollText className="h-3 w-3" /> ویرایش دسترسی‌ها
                </Button>
              )}
            </div>
            {r.description && <p className="text-xs text-muted-foreground">{r.description}</p>}
            <div className="flex flex-wrap gap-1">
              {r.permissions.slice(0, 12).map((p) => (
                <Badge key={p} variant="secondary" className="text-[9px] font-mono" dir="ltr">{p}</Badge>
              ))}
              {r.permissions.length > 12 && (
                <Badge variant="secondary" className="text-[9px] num-fa">+{toFa(r.permissions.length - 12)}</Badge>
              )}
            </div>
          </div>
        ))}
      </div>

      <EditRoleDialog
        role={editRole}
        onClose={() => setEditRole(null)}
        perms={perms}
        reload={reload}
      />
    </Section>
  );
}

function EditRoleDialog({ role, onClose, perms, reload }: {
  role: RoleRow | null; onClose: () => void; perms: PermDef[]; reload: () => void;
}) {
  const [selected, setSelected] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);
  const [loadedFor, setLoadedFor] = useState<number | null>(null);

  if (role && loadedFor !== role.id) {
    setLoadedFor(role.id);
    setSelected(role.permissions);
  }
  if (!role) return null;

  const groups = [...new Set(perms.map((p) => p.grp))];

  const submit = async () => {
    setBusy(true);
    try {
      await api(`/api/roles/${role.id}/permissions`, {
        method: "PUT",
        body: JSON.stringify({ permissionKeys: selected }),
      });
      toast({ title: "دسترسی‌های نقش به‌روزرسانی شد" });
      reload();
      onClose();
    } catch (e) {
      errToast(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Dialog open onOpenChange={(v) => !v && onClose()}>
      <DialogContent className="max-w-lg max-h-[85vh] overflow-y-auto thin-scroll">
        <DialogHeader>
          <DialogTitle className="text-base">دسترسی‌های نقش «{role.title}»</DialogTitle>
        </DialogHeader>
        <div className="space-y-3">
          {groups.map((g) => (
            <div key={g} className="space-y-1.5">
              <p className="text-xs font-semibold text-muted-foreground">{g}</p>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-1.5">
                {perms.filter((p) => p.grp === g).map((p) => (
                  <label key={p.key} className="flex items-center gap-2 text-xs border rounded-md px-2.5 py-1.5 cursor-pointer hover:bg-muted/60">
                    <Checkbox
                      checked={selected.includes(p.key)}
                      onCheckedChange={(c) =>
                        setSelected((s) => (c ? [...s, p.key] : s.filter((x) => x !== p.key)))
                      }
                    />
                    <span className="flex-1">{p.title}</span>
                  </label>
                ))}
              </div>
            </div>
          ))}
          <Button className="w-full" disabled={busy} onClick={submit}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : null} ذخیره دسترسی‌ها
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

function CreateUserDialog({ open, onOpenChange, roles, onDone }: {
  open: boolean; onOpenChange: (v: boolean) => void; roles: RoleRow[]; onDone: () => void;
}) {
  const [username, setUsername] = useState("");
  const [fullName, setFullName] = useState("");
  const [password, setPassword] = useState("");
  const [roleIds, setRoleIds] = useState<number[]>([]);
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await api("/api/users", {
        method: "POST",
        body: JSON.stringify({ username, fullName, password, roleIds }),
      });
      toast({ title: "کاربر ایجاد شد" });
      onOpenChange(false);
      setUsername(""); setFullName(""); setPassword(""); setRoleIds([]);
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
        <DialogHeader><DialogTitle className="text-base">ایجاد کاربر جدید</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <Label>نام کاربری (انگلیسی)</Label>
            <Input dir="ltr" className="text-left" value={username} onChange={(e) => setUsername(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>نام کامل</Label>
            <Input value={fullName} onChange={(e) => setFullName(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>رمز عبور (حداقل ۸ کاراکتر، حرف + رقم)</Label>
            <Input dir="ltr" className="text-left" type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <Label>نقش‌ها</Label>
            <div className="grid grid-cols-2 gap-1.5">
              {roles.map((r) => (
                <label key={r.id} className="flex items-center gap-2 text-xs border rounded-md px-2.5 py-1.5 cursor-pointer hover:bg-muted/60">
                  <Checkbox
                    checked={roleIds.includes(r.id)}
                    onCheckedChange={(c) => setRoleIds((s) => (c ? [...s, r.id] : s.filter((x) => x !== r.id)))}
                  />
                  {r.title}
                </label>
              ))}
            </div>
          </div>
          <Button className="w-full" disabled={!username || !password || busy} onClick={submit}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />} ایجاد کاربر
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
