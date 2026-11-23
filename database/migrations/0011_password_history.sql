-- 011: تاریخچه رمز (PLAN P1.3)
IF OBJECT_ID(N'[attendance].[PasswordHistory]') IS NULL
CREATE TABLE [attendance].[PasswordHistory] (
  [id] NVARCHAR(191) PRIMARY KEY, [userId] INT NOT NULL,
  [passwordHash] NVARCHAR(200) NOT NULL,
  [createdAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE
);
IF COL_LENGTH(N'[attendance].[User]', N'passwordExpiresAt') IS NULL
  ALTER TABLE [attendance].[User] ADD [passwordExpiresAt] DATETIME2 NULL;
GO
