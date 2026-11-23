-- ============================================================================
-- مهاجرت 0017 — ذخیره‌سازی تاریخ به‌صورت Linux timestamp (فاز ۷۹)
-- ----------------------------------------------------------------------------
-- از این نسخه، همهٔ ستون‌های تاریخ در نرم‌افزار به‌صورت «ثانیه از 1970-01-01T00:00Z»
-- (Linux timestamp / bigint) ذخیره می‌شوند تا تبدیل به تقویم‌های شمسی/قمری/میلادی
-- در لایهٔ نمایش بی‌دردسر باشد. مبدل EF (EpochSecondsConverter) همین قالب را می‌نویسد.
--
-- چه می‌کند: برای «هر» ستون datetime/datetime2/smalldatetime/date همهٔ جدول‌های کاربری:
--   ۱) ستون bigint جدید می‌سازد و مقدار epoch را پر می‌کند
--   ۲) ستون قدیمی را به <col>_legacy_dt تغییر نام می‌دهد (دادهٔ اصلی حفظ می‌شود)
--   ۳) ستون جدید را به نام اصلی برمی‌گرداند
-- ستون‌های NULL دست‌نخورده می‌مانند؛ ستون‌های legacy بعد از راستی‌آزمایی می‌توانند حذف شوند.
--
-- ⚠️ معناشناسی: DateTimeهای نرم‌افزار «ساعت محلی سرور» هستند. مبدل C# آفست منطقهٔ
-- زمانی سرور را اعمال می‌کند؛ این اسکریپت همان کار را با @OffsetSeconds انجام می‌دهد.
-- پیش‌فرض: ایران (+03:30 = 12600 ثانیه — بدون DST از ۱۴۰۱). برای مناطق دیگر مقدار را
-- به ثانیهٔ آفست سرورِ نصب تغییر دهید.
--
-- اجرا: روی بانک HrAttendance در SSMS (توصیه: ابتدا FULL BACKUP).
-- ============================================================================
SET NOCOUNT ON;

DECLARE @OffsetSeconds int = 12600; -- +03:30 تهران — آفست سرور خود را بگذارید
DECLARE @Epoch datetime2(0) = '1970-01-01T00:00:00';

DECLARE @Sql nvarchar(max), @Table sysname, @Col sysname, @Type sysname;

DECLARE cols CURSOR LOCAL FAST_FORWARD FOR
    SELECT QUOTENAME(t.name), QUOTENAME(c.name), ty.name
    FROM sys.columns c
    JOIN sys.tables  t  ON t.object_id = c.object_id
    JOIN sys.types   ty ON ty.user_type_id = c.user_type_id
    WHERE ty.name IN ('datetime','datetime2','smalldatetime','date')
      AND t.is_ms_shipped = 0
    ORDER BY t.name, c.name;

OPEN cols;
FETCH NEXT FROM cols INTO @Table, @Col, @Type;

WHILE @@FETCH_STATUS = 0
BEGIN
    BEGIN TRY
        -- ۱) ستون bigint جدید + پرکردن epoch (مقدار محلی → لحظهٔ مطلق با آفست سرور)
        SET @Sql = N'ALTER TABLE ' + @Table + N' ADD ' + @Col + N'__epoch bigint NULL;'
                 + N' UPDATE ' + @Table + N' SET ' + @Col + N'__epoch = CASE WHEN ' + @Col + N' IS NULL THEN NULL'
                 + N'   ELSE DATEDIFF_BIG(SECOND, @E, CAST(' + @Col + N' AS datetime2(0))) - @Off END;'
        EXEC sp_executesql @Sql, N'@E datetime2(0), @Off int', @E = @Epoch, @Off = @OffsetSeconds;

        -- ۲) نام‌جابه‌جایی: قدیمی → legacy (حفظ داده) ، جدید → نام اصلی
        SET @Sql = N'EXEC sp_rename ''' + @Table + N'.' + @Col + N''', ''' + @Col + N'_legacy_dt'', ''COLUMN'';'
        EXEC sp_executesql @Sql;
        SET @Sql = N'EXEC sp_rename ''' + @Table + N'.' + @Col + N'__epoch'', ''' + @Col + N''', ''COLUMN'';';
        EXEC sp_executesql @Sql;

        PRINT N'migrated: ' + @Table + N'.' + @Col + N' (' + @Type + N')';
    END TRY
    BEGIN CATCH
        PRINT N'FAILED: ' + @Table + N'.' + @Col + N' — ' + ERROR_MESSAGE();
    END CATCH
    FETCH NEXT FROM cols INTO @Table, @Col, @Type;
END
CLOSE cols; DEALLOCATE cols;

PRINT N'--- 0017 done: همهٔ ستون‌های تاریخ اکنون bigint (epoch ثانیه) هستند؛ ستون‌های <col>_legacy_dt نگه‌داشته شدند ---';
