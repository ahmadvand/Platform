import { isFriday, dateKey, addDays } from "./persian";

// ============================================================
// موتور محاسبه کارکرد (ATT-03) — rule-based, parameterized engine
// Computes late / early / overtime / worked minutes based on the
// employee's shift parameters (Configuration over Customization).
// ============================================================

export interface ShiftParams {
  name?: string;
  startTime: string; // "08:00"
  endTime: string; // "16:00"
  breakMinutes: number;
  toleranceLate: number; // minutes of tolerated lateness
}

export interface WorkResult {
  lateMinutes: number;
  earlyMinutes: number;
  workMinutes: number;
  overtimeMinutes: number;
}

function hm(min: string): number {
  const [h, m] = min.split(":").map(Number);
  return h * 60 + m;
}

function dateAt(dateStr: string, minutes: number): Date {
  // dateStr: "YYYY-MM-DD" — minutes may exceed 1440 (next day)
  const [y, mo, d] = dateStr.split("-").map(Number);
  const base = new Date(y, mo - 1, d, 0, 0, 0, 0);
  base.setMinutes(minutes);
  return base;
}

export function computeWork(
  shift: ShiftParams | null,
  checkIn: Date | null,
  checkOut: Date | null,
  dateStr: string,
): WorkResult {
  if (!checkIn) {
    return { lateMinutes: 0, earlyMinutes: 0, workMinutes: 0, overtimeMinutes: 0 };
  }

  if (!shift) {
    // بدون شیفت: کارکرد خام
    const work = checkOut ? Math.max(0, (checkOut.getTime() - checkIn.getTime()) / 60000) : 0;
    return { lateMinutes: 0, earlyMinutes: 0, workMinutes: Math.round(work), overtimeMinutes: 0 };
  }

  const startMin = hm(shift.startTime);
  const endMin = hm(shift.endTime);
  const crossesMidnight = endMin <= startMin; // شیفت شب
  const expectedEndMin = crossesMidnight ? endMin + 1440 : endMin;

  const inMin = checkIn.getHours() * 60 + checkIn.getMinutes();
  const expectedIn = dateAt(dateStr, startMin);
  const expectedOut = dateAt(dateStr, expectedEndMin);

  // تأخیر با احتساب رواداری
  const lateRaw = (checkIn.getTime() - expectedIn.getTime()) / 60000;
  const lateMinutes = Math.max(0, Math.round(lateRaw - shift.toleranceLate));

  if (!checkOut) {
    return { lateMinutes, earlyMinutes: 0, workMinutes: 0, overtimeMinutes: 0 };
  }

  const rawWork = (checkOut.getTime() - checkIn.getTime()) / 60000;
  const netWork = Math.max(0, rawWork - shift.breakMinutes);
  const workMinutes = Math.round(netWork);

  // تعجیل در خروج
  let earlyMinutes = 0;
  if (checkOut.getTime() < expectedOut.getTime()) {
    earlyMinutes = Math.max(0, Math.round((expectedOut.getTime() - checkOut.getTime()) / 60000));
  }

  // اضافه‌کاری (ساده: خروج بعد از پایان شیفت با ۱۰ دقیقه رواداری)
  let overtimeMinutes = 0;
  if (checkOut.getTime() > expectedOut.getTime() + 10 * 60000) {
    overtimeMinutes = Math.round((checkOut.getTime() - expectedOut.getTime()) / 60000);
  }

  return { lateMinutes, earlyMinutes, workMinutes, overtimeMinutes };
}

export function statusFromPunch(lateMinutes: number, hasIn: boolean): string {
  if (!hasIn) return "غایب";
  return lateMinutes > 0 ? "تأخیر" : "حاضر";
}

/** ارزیابی مجوز دسترسی (AC-02) — permission evaluation rules */
export interface PermLike {
  type: string; // دائم | موقت
  validFrom: Date;
  validUntil?: Date | null;
  timeFrom?: string | null;
  timeUntil?: string | null;
  days?: string | null; // "0,1,2,3,4" (getDay indexes)
  status: string;
}

export function evaluateAccess(
  perms: PermLike[],
  now: Date,
): { allowed: boolean; reason: string | null } {
  if (perms.length === 0) {
    return { allowed: false, reason: "فاقد مجوز دسترسی به این منطقه" };
  }
  const dayIdx = String(now.getDay());
  const nowMin = now.getHours() * 60 + now.getMinutes();

  for (const p of perms) {
    if (p.status !== "فعال") continue;
    if (p.validFrom && now < p.validFrom) continue;
    if (p.validUntil && now > p.validUntil) continue;
    if (p.days) {
      const allowedDays = p.days.split(",").map((s) => s.trim());
      if (allowedDays.length > 0 && !allowedDays.includes(dayIdx)) {
        continue;
      }
    }
    if (p.timeFrom && p.timeUntil) {
      const from = hm(p.timeFrom);
      const to = hm(p.timeUntil);
      if (from <= to) {
        if (nowMin < from || nowMin > to) continue;
      } else {
        // بازه شبانه
        if (nowMin < from && nowMin > to) continue;
      }
    }
    return { allowed: true, reason: null };
  }
  return { allowed: false, reason: "خارج از ساعات/روزهای مجاز یا مجوز منقضی" };
}

/** آیا این روز برای شیفت اقماری «روز کار» است؟ */
export function isOnDutyForCycle(dateStr: string, cyclePattern: string | null, anchorDate: string): boolean {
  if (!cyclePattern) return true;
  const parts = cyclePattern.split("/").map(Number);
  if (parts.length !== 2) return true;
  const [workDays, offDays] = parts;
  const cycle = workDays + offDays;
  const a = new Date(anchorDate + "T00:00:00");
  const d = new Date(dateStr + "T00:00:00");
  const diff = Math.round((d.getTime() - a.getTime()) / 86400000);
  const phase = ((diff % cycle) + cycle) % cycle;
  return phase < workDays;
}

/** روز تعطیل رسمی ساده: جمعه‌ها (پارامتریک — در نسخه کامل از جدول تقویم خوانده می‌شود) */
export function isHoliday(dateStr: string): boolean {
  return isFriday(new Date(dateStr + "T00:00:00"));
}

export { dateKey, addDays };
