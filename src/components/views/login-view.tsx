"use client";

import { useState } from "react";
import { ShieldCheck, User, Lock, LogIn, Loader2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

/** صفحه ورود (PLAN Phase 0 — احراز هویت متمرکز) */
export function LoginView({ onLogin }: { onLogin: () => void }) {
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError(null);
    try {
      const res = await fetch("/api/auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ username, password }),
        cache: "no-store",
      });
      const j = await res.json().catch(() => ({}));
      if (!res.ok) {
        setError((j as { error?: string }).error || "ورود ناموفق بود");
        return;
      }
      onLogin();
    } catch {
      setError("خطای ارتباط با سرور");
    } finally {
      setLoading(false);
    }
  };

  const fillDemo = (u: string, p: string) => {
    setUsername(u);
    setPassword(p);
    setError(null);
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 p-4" dir="rtl">
      <div className="w-full max-w-md space-y-4">
        <div className="text-center space-y-2">
          <div className="inline-flex items-center justify-center rounded-2xl bg-emerald-500/15 p-4 mb-2">
            <ShieldCheck className="h-10 w-10 text-emerald-400" />
          </div>
          <h1 className="text-2xl font-bold text-white">سامانه جامع یکپارچه</h1>
          <p className="text-sm text-slate-400">منابع انسانی • حضور و غیاب • کنترل تردد • ارباب رجوع</p>
        </div>

        <Card className="border-slate-700 bg-slate-900/80 backdrop-blur shadow-2xl">
          <CardHeader className="pb-2">
            <CardTitle className="text-base text-slate-200 flex items-center gap-2">
              <Lock className="h-4 w-4 text-emerald-400" />
              ورود به سامانه
            </CardTitle>
          </CardHeader>
          <CardContent>
            <form onSubmit={submit} className="space-y-4">
              <div className="space-y-2">
                <Label htmlFor="username" className="text-slate-300">نام کاربری</Label>
                <div className="relative">
                  <User className="absolute right-3 top-2.5 h-4 w-4 text-slate-500" />
                  <Input
                    id="username"
                    dir="ltr"
                    className="pr-9 bg-slate-800/70 border-slate-700 text-slate-100 text-left placeholder:text-slate-600"
                    placeholder="admin"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    autoComplete="username"
                    required
                  />
                </div>
              </div>
              <div className="space-y-2">
                <Label htmlFor="password" className="text-slate-300">رمز عبور</Label>
                <div className="relative">
                  <Lock className="absolute right-3 top-2.5 h-4 w-4 text-slate-500" />
                  <Input
                    id="password"
                    type="password"
                    dir="ltr"
                    className="pr-9 bg-slate-800/70 border-slate-700 text-slate-100 text-left placeholder:text-slate-600"
                    placeholder="••••••••"
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    autoComplete="current-password"
                    required
                  />
                </div>
              </div>

              {error && (
                <div className="rounded-lg bg-rose-500/10 border border-rose-500/30 text-rose-300 text-xs px-3 py-2.5">
                  {error}
                </div>
              )}

              <Button
                type="submit"
                disabled={loading || !username || !password}
                className="w-full bg-emerald-600 hover:bg-emerald-500 text-white"
              >
                {loading ? <Loader2 className="h-4 w-4 animate-spin" /> : <LogIn className="h-4 w-4" />}
                {loading ? "در حال ورود..." : "ورود"}
              </Button>
            </form>
          </CardContent>
        </Card>

        <Card className="border-slate-700/60 bg-slate-900/60">
          <CardContent className="p-4 space-y-2">
            <p className="text-[11px] font-semibold text-slate-400">حساب‌های نمایشی (کلیک کنید):</p>
            <div className="grid grid-cols-2 gap-2 text-[11px]">
              {[
                { u: "admin", p: "Admin@1234", t: "مدیر ارشد" },
                { u: "hr", p: "Hr@123456", t: "منابع انسانی" },
                { u: "operator", p: "Oper@1234", t: "اپراتور تردد" },
                { u: "guard", p: "Guard@1234", t: "حراست" },
              ].map((c) => (
                <button
                  key={c.u}
                  type="button"
                  onClick={() => fillDemo(c.u, c.p)}
                  className="text-right rounded-lg border border-slate-700/60 bg-slate-800/40 px-2.5 py-2 hover:border-emerald-500/50 transition-colors"
                >
                  <span className="block text-slate-200 font-medium">{c.t}</span>
                  <span dir="ltr" className="block text-slate-500 font-mono text-[10px]">{c.u} / {c.p}</span>
                </button>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
