import type { Metadata } from "next";
import "./globals.css";
import { Toaster } from "@/components/ui/toaster";

export const metadata: Metadata = {
  title: "سامانه جامع منابع انسانی، حضور و غیاب، کنترل تردد و حراست",
  description:
    "اکوسیستم یکپارچه: HRMS، حضور و غیاب، کنترل تردد، ارباب رجوع و حراست — الهام‌گرفته از SAP SuccessFactors و Workday",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="fa" dir="rtl" suppressHydrationWarning>
      <body className="antialiased bg-background text-foreground">
        {children}
        <Toaster />
      </body>
    </html>
  );
}
