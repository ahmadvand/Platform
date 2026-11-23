-- 015: صف دستورات دستگاه‌ها (PLAN P2.2)
IF OBJECT_ID(N'[attendance].[DeviceCommand]') IS NULL
CREATE TABLE [attendance].[DeviceCommand] (
  [id] NVARCHAR(191) PRIMARY KEY, [deviceId] NVARCHAR(191) NOT NULL,
  [type] NVARCHAR(20) NOT NULL, [payload] NVARCHAR(MAX) NULL,
  [status] NVARCHAR(15) NOT NULL DEFAULT (N'pending'),
  [createdByUserId] INT NULL, [createdBy] NVARCHAR(100) NULL,
  [result] NVARCHAR(300) NULL,
  [createdAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
  [deliveredAt] DATETIME2 NULL, [ackedAt] DATETIME2 NULL,
  FOREIGN KEY ([deviceId]) REFERENCES [attendance].[Device]([id]) ON DELETE CASCADE
);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DC_device_status')
  CREATE INDEX IX_DC_device_status ON [attendance].[DeviceCommand]([deviceId], [status]);
GO
