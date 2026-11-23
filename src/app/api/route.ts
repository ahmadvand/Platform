import { NextResponse } from "next/server";

/** health-check عمومی (بدون احراز هویت) */
export async function GET() {
  return NextResponse.json({ status: "ok", service: "hr-attendance-access", time: new Date().toISOString() });
}
