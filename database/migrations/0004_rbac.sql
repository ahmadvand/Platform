-- 004: جداول RBAC (نقش‌ها/دسترسی‌ها/اتصال) — PLAN Phase 0
IF OBJECT_ID(N'[attendance].[Role]') IS NULL
CREATE TABLE [attendance].[Role] (
  [id] INT IDENTITY(1,1) PRIMARY KEY, [name] NVARCHAR(30) NOT NULL UNIQUE,
  [title] NVARCHAR(100) NOT NULL, [description] NVARCHAR(300) NULL,
  [createdAt] DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME())
);
IF OBJECT_ID(N'[attendance].[Permission]') IS NULL
CREATE TABLE [attendance].[Permission] (
  [id] INT IDENTITY(1,1) PRIMARY KEY, [key] NVARCHAR(50) NOT NULL UNIQUE,
  [title] NVARCHAR(200) NOT NULL, [grp] NVARCHAR(50) NOT NULL
);
IF OBJECT_ID(N'[attendance].[RolePermission]') IS NULL
CREATE TABLE [attendance].[RolePermission] (
  [roleId] INT NOT NULL, [permissionId] INT NOT NULL,
  PRIMARY KEY ([roleId], [permissionId]),
  FOREIGN KEY ([roleId]) REFERENCES [attendance].[Role]([id]) ON DELETE CASCADE,
  FOREIGN KEY ([permissionId]) REFERENCES [attendance].[Permission]([id]) ON DELETE CASCADE
);
IF OBJECT_ID(N'[attendance].[UserRole]') IS NULL
CREATE TABLE [attendance].[UserRole] (
  [userId] INT NOT NULL, [roleId] INT NOT NULL,
  PRIMARY KEY ([userId], [roleId]),
  FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE,
  FOREIGN KEY ([roleId]) REFERENCES [attendance].[Role]([id]) ON DELETE CASCADE
);
GO
