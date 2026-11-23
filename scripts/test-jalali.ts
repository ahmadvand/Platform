// تست مستقیم jalaliToGregorian و seed تعطیلات
import { jalaliToGregorian, toJalali } from "../src/lib/persian.ts";

const cases: [number, number, number][] = [
  [1404, 1, 1], [1404, 1, 12], [1404, 3, 14], [1404, 11, 22], [1404, 12, 29],
  [1405, 1, 1], [1405, 11, 22],
];
for (const [jy, jm, jd] of cases) {
  const g = jalaliToGregorian(jy, jm, jd);
  const back = toJalali(g);
  const ok = back.jy === jy && back.jm === jm && back.jd === jd;
  console.log(`${jy}/${jm}/${jd} -> ${g.toISOString().slice(0, 10)} -> back ${back.jy}/${back.jm}/${back.jd} ${ok ? "OK" : "FAIL"}`);
}
