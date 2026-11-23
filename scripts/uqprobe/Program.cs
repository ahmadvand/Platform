using System.Globalization;
var d = new DateTime(2026, 11, 1);
Console.WriteLine("HijriCalendar: " + new HijriCalendar().GetYear(d) + "/" + new HijriCalendar().GetMonth(d) + "/" + new HijriCalendar().GetDayOfMonth(d));
Console.WriteLine("PersianCalendar: " + new PersianCalendar().GetYear(d) + "/" + new PersianCalendar().GetMonth(d) + "/" + new PersianCalendar().GetDayOfMonth(d));
try { var ci = new CultureInfo("ar-SA"); Console.WriteLine("ar-SA calendar: " + ci.Calendar.GetType().Name + " -> " + ci.Calendar.GetYear(d) + "/" + ci.Calendar.GetMonth(d) + "/" + ci.Calendar.GetDayOfMonth(d)); } catch (Exception e) { Console.WriteLine("ar-SA: " + e.Message); }
try { var c2 = CultureInfo.GetCultureInfo("ar-SA").Calendar; Console.WriteLine("GetCultureInfo ar-SA: " + c2.GetType().Name); } catch (Exception e) { Console.WriteLine("GetCultureInfo: " + e.Message); }
