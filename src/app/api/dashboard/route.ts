import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { ensureSeed } from "@/lib/seed";
import { dateKey, addDays } from "@/lib/persian";

export const dynamic = "force-dynamic";
import { guard } from "@/lib/auth";

export async function GET(req: Request) {
  const denied = await guard(req, "dashboard.view");
  if (denied) return denied;
  await ensureSeed();

  const today = dateKey();
  const [employees, devices, gates, zones, alerts, leaves, missions, permits] = await Promise.all([
    db.employee.findMany({ include: { department: true, position: true, shift: true } }),
    db.device.findMany({ include: { gates: true } }),
    db.gate.findMany({ include: { zone: true } }),
    db.zone.findMany({ include: { gates: true } }),
    db.securityAlert.findMany({ orderBy: { createdAt: "desc" }, take: 30 }),
    db.leaveRequest.findMany({ where: { status: "تأیید شده" } }),
    db.mission.findMany({ where: { status: "تأیید شده" } }),
    db.overtimePermit.findMany(),
  ]);

  const todayRecords = await db.attendanceRecord.findMany({ where: { date: today } });
  const activeEmployees = employees.filter((e) => e.status === "فعال");

  const leaveNames = new Set(leaves.map((l) => l.employeeId));
  const missionIds = new Set(missions.map((m) => m.employeeId));
  void missionIds;

  const presentToday = todayRecords.filter((r) => r.checkIn && (r.status === "حاضر" || r.status === "تأخیر")).length;
  const lateToday = todayRecords.filter((r) => r.status === "تأخیر").length;
  const onLeaveToday = todayRecords.filter((r) => r.status === "مرخصی").length;
  const absentToday = todayRecords.filter((r) => r.status === "غایب").length;
  void leaveNames;

  const insideVisitors = await db.visitor.findMany({
    where: { status: "داخل سازمان" },
    include: { card: true, contractor: true, logs: true },
    orderBy: { updatedAt: "desc" },
  });
  const pendingVisitors = await db.visitor.count({ where: { status: "در انتظار تأیید" } });

  const recentEvents = await db.accessEvent.findMany({
    orderBy: { timestamp: "desc" },
    take: 8,
    include: { employee: { select: { firstName: true, lastName: true, personnelCode: true } }, gate: { include: { zone: true } } },
  });

  const unresolvedAlerts = alerts.filter((a) => !a.resolved);

  // ---- trend last 7 days ----
  const trend: { day: string; present: number; absent: number; leave: number; late: number }[] = [];
  for (let i = -6; i <= 0; i++) {
    const d = addDays(new Date(), i);
    const dk = dateKey(d);
    const recs = i === 0 ? todayRecords : await db.attendanceRecord.findMany({ where: { date: dk } });
    trend.push({
      day: dk.slice(5),
      present: recs.filter((r) => r.status === "حاضر").length,
      absent: recs.filter((r) => r.status === "غایب").length,
      leave: recs.filter((r) => r.status === "مرخصی").length,
      late: recs.filter((r) => r.status === "تأخیر").length,
    });
  }

  // ---- distributions ----
  const typeCount: Record<string, number> = {};
  for (const e of activeEmployees) typeCount[e.employmentType] = (typeCount[e.employmentType] ?? 0) + 1;
  const employmentTypes = Object.entries(typeCount).map(([name, value]) => ({ name, value }));

  const deptCount: Record<string, number> = {};
  for (const e of activeEmployees) deptCount[e.department?.name ?? "بدون واحد"] = (deptCount[e.department?.name ?? "بدون واحد"] ?? 0) + 1;
  const departmentStats = Object.entries(deptCount)
    .map(([name, value]) => ({ name, value }))
    .sort((a, b) => b.value - a.value)
    .slice(0, 8);

  // ---- expiring contracts (HR-04) ----
  const in30 = addDays(new Date(), 30);
  const contractExpiring = await db.contract.findMany({
    where: { endDate: { lte: in30, gte: new Date() }, status: "فعال" },
    include: { employee: { select: { firstName: true, lastName: true, personnelCode: true } } },
    orderBy: { endDate: "asc" },
    take: 6,
  });

  // ---- pending approvals (ESS/MSS) ----
  const pendingLeaves = await db.leaveRequest.count({ where: { status: "در انتظار تأیید" } });
  const pendingMissions = await db.mission.count({ where: { status: "در انتظار تأیید" } });
  const pendingPermits = await db.overtimePermit.count({ where: { status: "در انتظار تأیید" } });

  const devicesOnline = devices.filter((d) => d.mode === "آنلاین").length;
  const gatesOk = gates.filter((g) => g.status === "فعال").length;

  return NextResponse.json({
    kpis: {
      totalEmployees: employees.length,
      activeEmployees: activeEmployees.length,
      presentToday,
      absentToday,
      lateToday,
      onLeaveToday,
      visitorsInside: insideVisitors.length,
      pendingVisitorApprovals: pendingVisitors,
      pendingRequests: pendingLeaves + pendingMissions + pendingPermits,
      unresolvedAlerts: unresolvedAlerts.length,
      devicesOnline,
      devicesTotal: devices.length,
      gatesOk,
      gatesTotal: gates.length,
    },
    employmentTypes,
    departmentStats,
    attendanceTrend: trend,
    recentEvents,
    alerts: unresolvedAlerts.slice(0, 6),
    insideVisitors,
    devices,
    contractExpiring,
  });
}
