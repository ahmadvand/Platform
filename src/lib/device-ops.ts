// عملیات دستگاه‌های تردد (PLAN Phase 2)
// صف دستورات، ورود داده تردد از دستگاه‌ها، وضعیت آنلاین/آفلاین

import { db } from "@/lib/db";
import { getSession } from "@/lib/auth";
import { computeWork, statusFromPunch } from "@/lib/att";
import { dateKey } from "@/lib/persian";
import { NextResponse } from "next/server";

// ---------- انواع دستورات پشتیبانی‌شده ----------
export const COMMAND_TYPES = [
  "unlock", // بازکردن درب
  "sync_time", // همگام‌سازی ساعت
  "restart", // ری‌استارت دستگاه
  "push_user", // ارسال/به‌روزرسانی کاربر روی دستگاه
  "delete_user", // حذف کاربر از دستگاه
  "get_user", // دریافت اطلاعات کاربر
  "refresh_data", // به‌روزرسانی داده‌های داخلی دستگاه
  "clear_data", // پاک‌سازی داده‌های تراکنشی
  "enable_device", // فعال‌سازی
  "disable_device", // غیرفعال‌سازی
] as const;
export type CommandType = (typeof COMMAND_TYPES)[number];

export const COMMAND_LABELS: Record<string, string> = {
  unlock: "بازکردن درب",
  sync_time: "همگام‌سازی ساعت",
  restart: "ری‌استارت دستگاه",
  push_user: "ارسال کاربر به دستگاه",
  delete_user: "حذف کاربر از دستگاه",
  get_user: "دریافت اطلاعات کاربر",
  refresh_data: "به‌روزرسانی داده‌ها",
  clear_data: "پاک‌سازی داده‌ها",
  enable_device: "فعال‌سازی دستگاه",
  disable_device: "غیرفعال‌سازی دستگاه",
};

// ---------- صف دستورات (P2.2) ----------
export async function queueCommand(
  deviceId: string,
  type: CommandType,
  payload: unknown,
  actor?: { userId: number; fullName: string } | null
) {
  return db.deviceCommand.create({
    data: {
      deviceId,
      type,
      payload: payload === undefined || payload === null ? null : JSON.stringify(payload),
      createdByUserId: actor?.userId ?? null,
      createdBy: actor?.fullName ?? "سرویس پل",
    },
  });
}

// ---------- احراز دستگاه برای webhook (P2.1) ----------
export type DeviceAuth =
  | { ok: true; device: { id: string; serial: string; name: string; pollingInterval: number } }
  | { ok: false; res: NextResponse };

export async function authenticateDevice(req: Request, sn: string | null): Promise<DeviceAuth> {
  if (!sn) return { ok: false, res: NextResponse.json({ error: "SN دستگاه الزامی است" }, { status: 400 }) };
  const device = await db.device.findUnique({
    where: { serial: sn },
    select: { id: true, serial: true, name: true, pollingInterval: true, active: true, webhookToken: true },
  });
  if (!device || !device.active)
    return { ok: false, res: NextResponse.json({ error: "دستگاه یافت نشد یا غیرفعال است" }, { status: 404 }) };

  const bearerSession = await getSession(req);
  if (bearerSession && bearerSession.permissions.includes("devices.poll")) {
    return { ok: true, device };
  }
  const token = req.headers.get("x-device-token");
  if (!token || token !== device.webhookToken)
    return { ok: false, res: NextResponse.json({ error: "توکن دستگاه نامعتبر است" }, { status: 401 }) };
  return { ok: true, device };
}

export async function markSeen(deviceId: string, extra?: { firmware?: string; userCount?: number }) {
  return db.device.update({
    where: { id: deviceId },
    data: { lastSeenAt: new Date(), lastSync: new Date(), mode: "آنلاین", ...extra },
  });
}

// ---------- ورود رکوردهای تردد (P2.5 backfill) ----------
export interface RawPunch {
  pin: string; // کد پرسنلی
  timestamp: string; // ISO یا "YYYY-MM-DD HH:mm:ss"
  status?: string; // in | out (اختیاری — در نبود آن استنتاج می‌شود)
}

function parseTs(ts: string): Date | null {
  if (!ts) return null;
  const t = ts.includes("T") ? new Date(ts) : new Date(ts.replace(" ", "T"));
  return isNaN(t.getTime()) ? null : t;
}

export async function ingestPunches(deviceId: string, punches: RawPunch[]): Promise<{ ok: number; failed: number; unknownPins: string[] }> {
  let ok = 0;
  let failed = 0;
  const unknownPins: string[] = [];
  for (const p of punches) {
    const ts = parseTs(p.timestamp);
    if (!ts || !p.pin) {
      failed++;
      continue;
    }
    const employee = await db.employee.findUnique({
      where: { personnelCode: p.pin },
      include: { shift: true },
    });
    if (!employee) {
      unknownPins.push(p.pin);
      failed++;
      continue;
    }
    const dk = dateKey(ts);
    let record = await db.attendanceRecord.findUnique({
      where: { employeeId_date: { employeeId: employee.id, date: dk } },
    });
    if (!record) {
      record = await db.attendanceRecord.create({
        data: { employeeId: employee.id, deviceId, date: dk, status: "حاضر" },
      });
    }
    // ورود: اولین رکورد روز؛ خروج: آخرین رکورد روز
    const isFirstIn = !record.checkIn || ts < record.checkIn;
    const isLastOut = !record.checkOut || ts > record.checkOut;
    const checkIn = isFirstIn ? ts : record.checkIn;
    const checkOut = isLastOut ? ts : record.checkOut;
    const sp = employee.shift
      ? {
          startTime: employee.shift.startTime,
          endTime: employee.shift.endTime,
          breakMinutes: employee.shift.breakMinutes,
          toleranceLate: employee.shift.toleranceLate,
        }
      : null;
    const w = computeWork(sp, checkIn, checkOut, dk);
    await db.attendanceRecord.update({
      where: { id: record.id },
      data: {
        checkIn,
        checkOut,
        deviceId,
        workMinutes: w.workMinutes,
        lateMinutes: w.lateMinutes,
        earlyMinutes: w.earlyMinutes,
        overtimeMinutes: w.overtimeMinutes,
        status: record.status === "غایب" || !record.status ? statusFromPunch(w.lateMinutes, true) : record.status,
      },
    });
    ok++;
  }
  return { ok, failed, unknownPins };
}

// ---------- وضعیت آنلاین (P2.6) ----------
export function isDeviceOnline(d: { lastSeenAt: Date | null; pollingInterval: number }): boolean {
  if (!d.lastSeenAt) return false;
  const threshold = Math.max(3 * (d.pollingInterval || 60), 180) * 1000;
  return Date.now() - d.lastSeenAt.getTime() < threshold;
}

/** اسکن تنبلی: وضعیت دستگاه‌های دیده‌نشده را آفلاین کند (بدون نیاز به تایمر) */
export async function sweepOffline(): Promise<number> {
  const devices = await db.device.findMany({
    where: { active: true, mode: "آنلاین" },
    select: { id: true, lastSeenAt: true, pollingInterval: true },
  });
  let n = 0;
  for (const d of devices) {
    if (!isDeviceOnline(d)) {
      await db.device.update({ where: { id: d.id }, data: { mode: "آفلاین" } });
      n++;
    }
  }
  return n;
}
