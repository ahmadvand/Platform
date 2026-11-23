"use client";

import { useMemo, useState } from "react";
import { CalendarDays, ChevronLeft, ChevronRight, Plus, Trash2, Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  api, errToast, useApiData, Section, LoadingBlock,
} from "@/components/shared";
import { toast } from "@/hooks/use-toast";
import { toFa, toJalali, jalaliToGregorian, JALALI_MONTHS, formatJalaliLong } from "@/lib/persian";

interface Holiday {
  id: string;
  date: string; // gregorian YYYY-MM-DD
  title: string;
  jYear: number;
  jMonth: number;
  jDay: number;
}

const WEEKDAY_HEADERS = ["ش", "ی", "د", "س", "چ", "پ", "ج"];

export function HolidaysView() {
  const today = new Date();
  const tj = toJalali(today);
  const [jy, setJy] = useState(tj.jy);
  const [jm, setJm] = useState(tj.jm);
  const { data, loading, reload } = useApiData<{ holidays: Holiday[] }>(`/api/holidays?jYear=${jy}`);
  const [addOpen, setAddOpen] = useState(false);

  const holidays = useMemo(() => data?.holidays ?? [], [data]);
  const byJDay = useMemo(() => {
    const m = new Map<number, Holiday>();
    for (const h of holidays) if (h.jMonth === jm) m.set(h.jDay, h);
    return m;
  }, [holidays, jm]);

  // شبکه ماه جلالی: روز اول هفته شنبه است
  const grid = useMemo(() => {
    const first = jalaliToGregorian(jy, jm, 1);
    // getDay: 0=Sunday..6=Saturday → شنبه=6 → ستون شنبه=0
    const offset = (first.getDay() + 1) % 7;
    const daysInMonth = jm <= 6 ? 31 : jm <= 11 ? 30 : (jy % 4 === (jy % 33 % 4) ? 30 : 29);
    const cells: (number | null)[] = Array(offset).fill(null);
    for (let d = 1; d <= daysInMonth; d++) cells.push(d);
    return cells;
  }, [jy, jm]);

  const prevMonth = () => {
    if (jm === 1) { setJm(12); setJy((y) => y - 1); } else setJm((m) => m - 1);
  };
  const nextMonth = () => {
    if (jm === 12) { setJm(1); setJy((y) => y + 1); } else setJm((m) => m + 1);
  };

  const remove = async (h: Holiday) => {
    try {
      await api(`/api/holidays?id=${h.id}`, { method: "DELETE" });
      toast({ title: "تعطیلی حذف شد" });
      reload();
    } catch (e) {
      errToast(e);
    }
  };

  return (
    <div className="space-y-4 min-w-0">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div className="flex items-center gap-2">
          <Button variant="outline" size="icon" className="h-8 w-8" onClick={prevMonth} aria-label="ماه قبل">
            <ChevronRight className="h-4 w-4" />
          </Button>
          <span className="font-bold num-fa min-w-36 text-center">{JALALI_MONTHS[jm - 1]} {toFa(jy)}</span>
          <Button variant="outline" size="icon" className="h-8 w-8" onClick={nextMonth} aria-label="ماه بعد">
            <ChevronLeft className="h-4 w-4" />
          </Button>
        </div>
        <Button size="sm" onClick={() => setAddOpen(true)}>
          <Plus className="h-4 w-4" /> افزودن تعطیلی
        </Button>
      </div>

      {loading ? <LoadingBlock rows={4} /> : (
        <Section title="تقویم ماه" icon={CalendarDays}>
          <div className="grid grid-cols-7 gap-1.5">
            {WEEKDAY_HEADERS.map((w, i) => (
              <div key={w} className={`text-center text-[11px] font-semibold py-1.5 rounded ${i === 6 ? "text-rose-600" : "text-muted-foreground"}`}>{w}</div>
            ))}
            {grid.map((d, i) => {
              if (d === null) return <div key={`e${i}`} />;
              const h = byJDay.get(d);
              const isToday = tj.jy === jy && tj.jm === jm && tj.jd === d;
              const isFriday = (i % 7) === 6;
              return (
                <div
                  key={d}
                  className={`relative rounded-lg border p-1.5 min-h-16 text-sm ${
                    h ? "bg-rose-500/10 border-rose-500/40" : isFriday ? "bg-muted/60 border-border" : "border-border"
                  } ${isToday ? "ring-2 ring-primary/60" : ""}`}
                >
                  <span className={`num-fa font-semibold ${isFriday && !h ? "text-rose-500" : ""}`}>{toFa(d)}</span>
                  {h && (
                    <div className="mt-1 space-y-1">
                      <p className="text-[9px] leading-tight text-rose-700 font-medium">{h.title}</p>
                      <button
                        onClick={() => remove(h)}
                        className="text-[9px] text-muted-foreground hover:text-rose-600 flex items-center gap-0.5"
                        title="حذف تعطیلی"
                      >
                        <Trash2 className="h-2.5 w-2.5" /> حذف
                      </button>
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </Section>
      )}

      <Section title={`تعطیلات ثبت‌شده سال ${toFa(jy)}`} contentClass="max-h-64 overflow-y-auto thin-scroll">
        {holidays.length === 0 ? (
          <p className="text-xs text-muted-foreground py-4 text-center">تعطیلی رسمی‌ای برای این سال ثبت نشده است</p>
        ) : (
          <div className="flex flex-wrap gap-1.5">
            {holidays.map((h) => (
              <span key={h.id} className="text-[11px] rounded-full border border-rose-500/30 bg-rose-500/10 text-rose-700 px-2.5 py-1 num-fa">
                {toFa(h.jDay)} {JALALI_MONTHS[h.jMonth - 1]} — {h.title}
              </span>
            ))}
          </div>
        )}
      </Section>

      <AddHolidayDialog open={addOpen} onOpenChange={setAddOpen} onDone={reload} />
    </div>
  );
}

function AddHolidayDialog({ open, onOpenChange, onDone }: {
  open: boolean; onOpenChange: (v: boolean) => void; onDone: () => void;
}) {
  const [date, setDate] = useState("");
  const [title, setTitle] = useState("");
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await api("/api/holidays", { method: "POST", body: JSON.stringify({ date, title }) });
      toast({ title: "تعطیلی ثبت شد", description: date ? formatJalaliLong(new Date(date + "T12:00:00")) : undefined });
      onOpenChange(false);
      setTitle("");
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
        <DialogHeader><DialogTitle className="text-base">افزودن تعطیلی رسمی</DialogTitle></DialogHeader>
        <div className="space-y-3">
          <div className="space-y-1.5">
            <label className="text-xs">تاریخ (میلادی — در تقویم شمسی نمایش داده می‌شود)</label>
            <Input type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          </div>
          <div className="space-y-1.5">
            <label className="text-xs">عنوان</label>
            <Input value={title} onChange={(e) => setTitle(e.target.value)} placeholder="مثلاً عید قربان" />
          </div>
          <Button className="w-full" disabled={!date || !title || busy} onClick={submit}>
            {busy ? <Loader2 className="h-4 w-4 animate-spin" /> : <Plus className="h-4 w-4" />} ثبت
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
