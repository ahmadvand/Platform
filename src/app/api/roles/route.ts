import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { guard } from "@/lib/auth";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "users.view");
  if (denied) return denied;
  const roles = await db.role.findMany({
    include: {
      permissions: { include: { permission: { select: { key: true } } } },
      users: { select: { userId: true } },
    },
    orderBy: { id: "asc" },
  });
  return NextResponse.json({
    roles: roles.map((r) => ({
      id: r.id,
      name: r.name,
      title: r.title,
      description: r.description,
      userCount: r.users.length,
      permissions: r.permissions.map((p) => p.permission.key),
    })),
  });
}
