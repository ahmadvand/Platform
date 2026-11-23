// Persian (Jalali) date utilities — ported from the well-known jalaali algorithm
// plus digit localization helpers used across the whole app.

const div = (a: number, b: number) => ~~(a / b);
const mod = (a: number, b: number) => a - ~~(a / b) * b;

const BREAKS = [
  -61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097,
  2192, 2262, 2324, 2394, 2456, 3178,
];

function jalCal(jy: number): { leap: number; gy: number; march: number } {
  const bl = BREAKS.length;
  const gy = jy + 621;
  let leapJ = -14;
  let jp = BREAKS[0];
  let jm = 0;
  let jump = 0;
  for (let i = 1; i < bl; i += 1) {
    jm = BREAKS[i];
    jump = jm - jp;
    if (jy < jm) break;
    leapJ = leapJ + div(jump, 33) * 8 + div(mod(jump, 33), 4);
    jp = jm;
  }
  let n = jy - jp;
  leapJ = leapJ + div(n, 33) * 8 + div(mod(n, 33) + 3, 4);
  if (mod(jump, 33) === 4 && jump - n === 4) leapJ += 1;
  const leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150;
  const march = 20 + leapJ - leapG;
  if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33;
  let leap = mod(mod(n + 1, 33) - 1, 4);
  if (leap === -1) leap = 4;
  return { leap, gy, march };
}

function g2d(gy: number, gm: number, gd: number): number {
  let d =
    div((gy + div(gm - 8, 6) + 100100) * 1461, 4) +
    div(153 * mod(gm + 9, 12) + 2, 5) +
    gd -
    34840408;
  d = d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752;
  return d;
}

function d2g(jdn: number): { gy: number; gm: number; gd: number } {
  let j = 4 * jdn + 139361631;
  j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
  const i = div(mod(j, 1461), 4) * 5 + 308;
  const gd = div(mod(i, 153), 5) + 1;
  const gm = mod(div(i, 153), 12) + 1;
  const gy = div(j, 1461) - 100100 + div(8 - gm, 6);
  return { gy, gm, gd };
}

export function toJalali(date: Date): { jy: number; jm: number; jd: number } {
  const jdn = g2d(date.getFullYear(), date.getMonth() + 1, date.getDate());
  const gy = d2g(jdn).gy;
  let jy = gy - 621;
  const r = jalCal(jy);
  const jdn1f = g2d(r.gy, 3, r.march);
  let k = jdn - jdn1f;
  if (k >= 0) {
    if (k <= 185) {
      return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 };
    }
    k -= 186;
  } else {
    jy -= 1;
    k += 179;
    if (r.leap === 1) k += 1;
  }
  return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 };
}

const FA_DIGITS = ["۰", "۱", "۲", "۳", "۴", "۵", "۶", "۷", "۸", "۹"];

export function toFa(input: string | number): string {
  return String(input).replace(/\d/g, (d) => FA_DIGITS[Number(d)]);
}

export function toEnDigits(input: string): string {
  return input
    .replace(/[۰-۹]/g, (d) => String(FA_DIGITS.indexOf(d)))
    .replace(/[٠-٩]/g, (d) => String("٠١٢٣٤٥٦٧٨٩".indexOf(d)));
}

export const JALALI_MONTHS = [
  "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
  "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند",
];

const WEEKDAYS = [
  "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه", "شنبه",
];

/** e.g. "۱۴۰۴/۰۷/۲۶" */
export function formatJalali(date: Date | string | null | undefined): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  const { jy, jm, jd } = toJalali(d);
  const p2 = (n: number) => String(n).padStart(2, "0");
  return toFa(`${jy}/${p2(jm)}/${p2(jd)}`);
}

/** e.g. "شنبه ۲۶ مهر ۱۴۰۴" */
export function formatJalaliLong(date: Date | string | null | undefined): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  const { jy, jm, jd } = toJalali(d);
  return `${WEEKDAYS[d.getDay()]} ${toFa(jd)} ${JALALI_MONTHS[jm - 1]} ${toFa(jy)}`;
}

/** e.g. "۰۸:۳۲" */
export function formatTime(date: Date | string | null | undefined): string {
  if (!date) return "—";
  const d = typeof date === "string" ? new Date(date) : date;
  if (isNaN(d.getTime())) return "—";
  const p2 = (n: number) => String(n).padStart(2, "0");
  return toFa(`${p2(d.getHours())}:${p2(d.getMinutes())}`);
}

/** "YYYY-MM-DD" in local server time — key used for attendance days */
export function dateKey(d: Date = new Date()): string {
  const p2 = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${p2(d.getMonth() + 1)}-${p2(d.getDate())}`;
}

export function addDays(date: Date, days: number): Date {
  const d = new Date(date);
  d.setDate(d.getDate() + days);
  return d;
}

/** 1250000 → "۱٬۲۵۰٬۰۰۰" */
export function formatNumber(n: number | null | undefined): string {
  if (n === null || n === undefined) return "—";
  return toFa(Math.round(n).toLocaleString("en-US").replace(/,/g, "٬"));
}

/** minutes → "۸ ساعت و ۳۰ دقیقه" */
export function formatMinutes(mins: number | null | undefined): string {
  if (!mins || mins <= 0) return "۰";
  const h = Math.floor(mins / 60);
  const m = mins % 60;
  if (h && m) return `${toFa(h)} ساعت و ${toFa(m)} دقیقه`;
  if (h) return `${toFa(h)} ساعت`;
  return `${toFa(m)} دقیقه`;
}

export function formatSalary(n: number | null | undefined): string {
  if (!n) return "—";
  return `${formatNumber(n)} ریال`;
}

export function weekdayName(d: Date): string {
  return WEEKDAYS[d.getDay()];
}

export function isFriday(d: Date): boolean {
  return d.getDay() === 5;
}

// ---------- تبدیل جلالی → میلادی (برای بازه ماه حقوق و تقویم) ----------
export function jalaliToGregorian(jy: number, jm: number, jd: number): Date {
  // الگوریتم استاندارد jalaali: شماره روز جلالی → یولیانی → میلادی
  const { march } = jalCal(jy);
  const jdn = g2d(jy + 621, 3, march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1;
  const { gy, gm, gd } = d2g(jdn);
  return new Date(gy, gm - 1, gd, 12, 0, 0, 0);
}

/** اولین و آخرین روز یک ماه جلالی → بازه میلادی */
export function jalaliMonthRange(jy: number, jm: number): { start: Date; end: Date } {
  const start = jalaliToGregorian(jy, jm, 1);
  let njy = jy;
  let njm = jm + 1;
  if (njm > 12) {
    njm = 1;
    njy += 1;
  }
  const end = jalaliToGregorian(njy, njm, 1); // اول ماه بعد
  return { start, end };
}

/** تعداد روزهای یک ماه جلالی */
export function jalaliMonthDays(jy: number, jm: number): number {
  const { start, end } = jalaliMonthRange(jy, jm);
  return Math.round((end.getTime() - start.getTime()) / 86400000);
}
