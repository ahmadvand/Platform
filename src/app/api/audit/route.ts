import { NextResponse } from "next/server";
import { db } from "@/lib/db";
import { guard } from "@/lib/auth";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "audit.view");
  if (denied) return denied;
  const url = new URL(req.url);
  const q = url.searchParams.get("q")?.trim() || "";
  const action = url.searchParams.get("action") || "";
  const take = Math.min(Number(url.searchParams.get("take") ?? 200), 500);

  const logs = await db.auditLog.findMany({
    where: {
      AND: [
        action ? { action } : {},
        q
          ? {
              OR: [
                { actorName: { contains: q } },
                { entity: { contains: q } },
                { entityId: { contains: q } },
              ],
            }
          : {},
      ],
    },
    orderBy: { at: "desc" },
    take,
  });
  return NextResponse.json({ logs });
}
