-- 010: قفل‌شدگی حساب پس از تلاش‌های ناموفق (PLAN P1.1)
IF COL_LENGTH(N'[attendance].[User]', N'failedLoginAttempts') IS NULL
  ALTER TABLE [attendance].[User] ADD [failedLoginAttempts] INT NOT NULL CONSTRAINT DF_User_fla DEFAULT (0);
IF COL_LENGTH(N'[attendance].[User]', N'lockedUntil') IS NULL
  ALTER TABLE [attendance].[User] ADD [lockedUntil] DATETIME2 NULL;
GO
