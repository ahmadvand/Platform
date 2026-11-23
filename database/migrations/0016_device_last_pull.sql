-- 016: وضعیت poll دستگاه‌ها (PLAN P2.5/P2.6)
IF COL_LENGTH(N'[attendance].[Device]', N'lastPulledAt') IS NULL
  ALTER TABLE [attendance].[Device] ADD [lastPulledAt] DATETIME2 NULL;
IF COL_LENGTH(N'[attendance].[Device]', N'lastSeenAt') IS NULL
  ALTER TABLE [attendance].[Device] ADD [lastSeenAt] DATETIME2 NULL;
IF COL_LENGTH(N'[attendance].[Device]', N'webhookToken') IS NULL
BEGIN
  ALTER TABLE [attendance].[Device] ADD [webhookToken] NVARCHAR(64) NULL;
  UPDATE [attendance].[Device] SET [webhookToken] = LOWER(REPLACE(NEWID(), '-', ''));
  ALTER TABLE [attendance].[Device] ALTER COLUMN [webhookToken] NVARCHAR(64) NOT NULL;
END
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UQ_Device_webhookToken')
  CREATE UNIQUE INDEX UQ_Device_webhookToken ON [attendance].[Device]([webhookToken]);
GO
