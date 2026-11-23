-- 012: توکن‌های تک‌مصرف بازنشانی رمز (PLAN P1.4)
IF OBJECT_ID(N'[attendance].[PasswordReset]') IS NULL
CREATE TABLE [attendance].[PasswordReset] (
  [id] NVARCHAR(191) PRIMARY KEY, [userId] INT NOT NULL,
  [tokenHash] NVARCHAR(128) NOT NULL UNIQUE,
  [expiresAt] DATETIME2 NOT NULL, [usedAt] DATETIME2 NULL,
  [createdAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE
);
GO
