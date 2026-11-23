-- 014: گزارش حسابرسی (PLAN P1.6)
IF OBJECT_ID(N'[attendance].[AuditLog]') IS NULL
CREATE TABLE [attendance].[AuditLog] (
  [id] NVARCHAR(191) PRIMARY KEY, [actorUserId] INT NULL, [actorName] NVARCHAR(100) NULL,
  [action] NVARCHAR(30) NOT NULL, [entity] NVARCHAR(30) NOT NULL, [entityId] NVARCHAR(64) NULL,
  [detail] NVARCHAR(MAX) NULL, [ip] NVARCHAR(50) NULL,
  [at] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AuditLog_at')
  CREATE INDEX IX_AuditLog_at ON [attendance].[AuditLog]([at] DESC);
GO
