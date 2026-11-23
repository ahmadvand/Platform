-- 013: پایدارسازی refresh token در DB (PLAN P1.5)
IF OBJECT_ID(N'[attendance].[RefreshToken]') IS NULL
CREATE TABLE [attendance].[RefreshToken] (
  [id] NVARCHAR(191) PRIMARY KEY, [userId] INT NOT NULL,
  [tokenHash] NVARCHAR(128) NOT NULL UNIQUE,
  [expiresAt] DATETIME2 NOT NULL, [revokedAt] DATETIME2 NULL,
  [deviceInfo] NVARCHAR(300) NULL, [ip] NVARCHAR(50) NULL,
  [createdAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE
);
GO
