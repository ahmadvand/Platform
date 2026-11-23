#!/usr/bin/env python3
# شماره‌گذاری صفحات PDF راهنمای ماژول — کاور بدون شماره؛ بدنه از ۱؛ عدد فارسی با فونت وزیرمتن
import io, os
from pypdf import PdfReader, PdfWriter
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont

SRC = "/home/z/my-project/download/HR-Module-Creation-Guide-Dev.pdf"
FONT = "/home/z/my-project/aspnet/src/Platform.Web/wwwroot/fonts/ttf/Vazirmatn-Regular.ttf"
W, H = 794, 1123

pdfmetrics.registerFont(TTFont("Vazir", FONT))
FA = str.maketrans("0123456789", "۰۱۲۳۴۵۶۷۸۹")

reader = PdfReader(SRC)
writer = PdfWriter()
n = len(reader.pages)
print("pages:", n)

for i, page in enumerate(reader.pages):
    if i == 0:  # کاور — بدون شماره
        writer.add_page(page)
        continue
    buf = io.BytesIO()
    c = canvas.Canvas(buf, pagesize=(W, H))
    c.setFont("Vazir", 10)
    c.setFillColorRGB(0x8f/255, 0x8d/255, 0x86/255)
    c.drawCentredString(W/2, 26, str(i).translate(FA))
    c.save()
    buf.seek(0)
    overlay = PdfReader(buf).pages[0]
    page.merge_page(overlay)
    writer.add_page(page)

# متادیتا
writer.add_metadata({
    "/Title": "راهنمای ایجاد ماژول جدید — سامانهٔ یکپارچه منابع انسانی",
    "/Author": "Z.ai",
    "/Subject": "روش استاندارد طراحی و ادغام ماژول در معماری پلاگین سامانه (نسخهٔ 2.7.27)",
    "/Creator": "Z.ai PDF Workbench",
})

with open(SRC, "wb") as f:
    writer.write(f)
print("stamped + metadata OK, size:", os.path.getsize(SRC))
