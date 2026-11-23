import { NextResponse } from "next/server";
import { guard } from "@/lib/auth";
import { PERMISSIONS } from "@/lib/permissions";

export const dynamic = "force-dynamic";

export async function GET(req: Request) {
  const denied = await guard(req, "users.view");
  if (denied) return denied;
  return NextResponse.json({ permissions: PERMISSIONS });
}
