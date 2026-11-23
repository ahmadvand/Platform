-- ==================================================================
-- سامانه جامع منابع انسانی — اسکیمای کامل SQL Server (نصب تازه)
-- معادل prisma/schema.sqlserver.prisma — شناسه‌ها case-sensitive
-- ==================================================================
IF DB_ID(N'attendance') IS NULL
  CREATE DATABASE attendance;
GO
USE attendance;
GO
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'attendance')
  EXEC('CREATE SCHEMA attendance');
GO

-- ============ Core HR ============
IF OBJECT_ID(N'[attendance].[Company]') IS NULL
CREATE TABLE [attendance].[Company] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_Company PRIMARY KEY,
  [name]      NVARCHAR(200) NOT NULL,
  [code]      NVARCHAR(50)  NOT NULL,
  [active]    BIT           NOT NULL CONSTRAINT DF_Company_active DEFAULT (1),
  [createdAt] DATETIME2     NOT NULL CONSTRAINT DF_Company_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt] DATETIME2     NOT NULL,
  CONSTRAINT UQ_Company_code UNIQUE ([code])
);

IF OBJECT_ID(N'[attendance].[Department]') IS NULL
CREATE TABLE [attendance].[Department] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_Department PRIMARY KEY,
  [name]      NVARCHAR(200) NOT NULL,
  [code]      NVARCHAR(50)  NOT NULL,
  [companyId] NVARCHAR(191) NOT NULL,
  [parentId]  NVARCHAR(191) NULL,
  [createdAt] DATETIME2     NOT NULL CONSTRAINT DF_Department_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt] DATETIME2     NOT NULL,
  CONSTRAINT UQ_Department_code UNIQUE ([code]),
  CONSTRAINT FK_Department_company FOREIGN KEY ([companyId]) REFERENCES [attendance].[Company]([id]),
  CONSTRAINT FK_Department_parent FOREIGN KEY ([parentId]) REFERENCES [attendance].[Department]([id])
);

IF OBJECT_ID(N'[attendance].[Position]') IS NULL
CREATE TABLE [attendance].[Position] (
  [id]           NVARCHAR(191) NOT NULL CONSTRAINT PK_Position PRIMARY KEY,
  [title]        NVARCHAR(200) NOT NULL,
  [code]         NVARCHAR(50)  NOT NULL,
  [grade]        NVARCHAR(50)  NULL,
  [departmentId] NVARCHAR(191) NOT NULL,
  [description]  NVARCHAR(500) NULL,
  [createdAt]    DATETIME2     NOT NULL CONSTRAINT DF_Position_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]    DATETIME2     NOT NULL,
  CONSTRAINT UQ_Position_code UNIQUE ([code]),
  CONSTRAINT FK_Position_department FOREIGN KEY ([departmentId]) REFERENCES [attendance].[Department]([id])
);

IF OBJECT_ID(N'[attendance].[Shift]') IS NULL
CREATE TABLE [attendance].[Shift] (
  [id]            NVARCHAR(191) NOT NULL CONSTRAINT PK_Shift PRIMARY KEY,
  [name]          NVARCHAR(100) NOT NULL,
  [type]          NVARCHAR(30)  NOT NULL,
  [startTime]     NVARCHAR(10)  NOT NULL,
  [endTime]       NVARCHAR(10)  NOT NULL,
  [breakMinutes]  INT           NOT NULL CONSTRAINT DF_Shift_break DEFAULT (60),
  [cyclePattern]  NVARCHAR(20)  NULL,
  [toleranceLate] INT           NOT NULL CONSTRAINT DF_Shift_tolerance DEFAULT (10),
  [active]        BIT           NOT NULL CONSTRAINT DF_Shift_active DEFAULT (1),
  [createdAt]     DATETIME2     NOT NULL CONSTRAINT DF_Shift_createdAt DEFAULT (SYSUTCDATETIME())
);

IF OBJECT_ID(N'[attendance].[Employee]') IS NULL
CREATE TABLE [attendance].[Employee] (
  [id]              NVARCHAR(191) NOT NULL CONSTRAINT PK_Employee PRIMARY KEY,
  [personnelCode]   NVARCHAR(20)  NOT NULL,
  [firstName]       NVARCHAR(100) NOT NULL,
  [lastName]        NVARCHAR(100) NOT NULL,
  [nationalId]      NVARCHAR(20)  NOT NULL,
  [birthDate]       NVARCHAR(20)  NULL,
  [phone]           NVARCHAR(20)  NULL,
  [email]           NVARCHAR(100) NULL,
  [employmentType]  NVARCHAR(30)  NOT NULL,
  [status]          NVARCHAR(30)  NOT NULL CONSTRAINT DF_Employee_status DEFAULT (N'فعال'),
  [hireDate]        DATETIME2     NOT NULL,
  [endDate]         DATETIME2     NULL,
  [companyId]       NVARCHAR(191) NOT NULL,
  [departmentId]    NVARCHAR(191) NULL,
  [positionId]      NVARCHAR(191) NULL,
  [baseSalary]      FLOAT         NOT NULL CONSTRAINT DF_Employee_salary DEFAULT (0),
  [shiftId]         NVARCHAR(191) NULL,
  [annualLeaveEnt]  FLOAT         NOT NULL CONSTRAINT DF_Employee_alEnt DEFAULT (26),
  [annualLeaveUsed] FLOAT         NOT NULL CONSTRAINT DF_Employee_alUsed DEFAULT (0),
  [sickLeaveEnt]    FLOAT         NOT NULL CONSTRAINT DF_Employee_slEnt DEFAULT (2),
  [sickLeaveUsed]   FLOAT         NOT NULL CONSTRAINT DF_Employee_slUsed DEFAULT (0),
  [createdAt]       DATETIME2     NOT NULL CONSTRAINT DF_Employee_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]       DATETIME2     NOT NULL,
  CONSTRAINT UQ_Employee_nationalId UNIQUE ([nationalId]),
  CONSTRAINT FK_Employee_company FOREIGN KEY ([companyId]) REFERENCES [attendance].[Company]([id]),
  CONSTRAINT FK_Employee_department FOREIGN KEY ([departmentId]) REFERENCES [attendance].[Department]([id]),
  CONSTRAINT FK_Employee_position FOREIGN KEY ([positionId]) REFERENCES [attendance].[Position]([id]),
  CONSTRAINT FK_Employee_shift FOREIGN KEY ([shiftId]) REFERENCES [attendance].[Shift]([id])
);

IF OBJECT_ID(N'[attendance].[Contract]') IS NULL
CREATE TABLE [attendance].[Contract] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_Contract PRIMARY KEY,
  [employeeId] NVARCHAR(191) NOT NULL,
  [type]       NVARCHAR(30)  NOT NULL,
  [contractNo] NVARCHAR(50)  NOT NULL,
  [startDate]  DATETIME2     NOT NULL,
  [endDate]    DATETIME2     NULL,
  [salary]     FLOAT         NOT NULL CONSTRAINT DF_Contract_salary DEFAULT (0),
  [status]     NVARCHAR(30)  NOT NULL CONSTRAINT DF_Contract_status DEFAULT (N'فعال'),
  [createdAt]  DATETIME2     NOT NULL CONSTRAINT DF_Contract_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Contract_no UNIQUE ([contractNo]),
  CONSTRAINT FK_Contract_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

IF OBJECT_ID(N'[attendance].[Decree]') IS NULL
CREATE TABLE [attendance].[Decree] (
  [id]           NVARCHAR(191) NOT NULL CONSTRAINT PK_Decree PRIMARY KEY,
  [employeeId]   NVARCHAR(191) NOT NULL,
  [type]         NVARCHAR(30)  NOT NULL,
  [decreeNo]     NVARCHAR(50)  NOT NULL,
  [date]         DATETIME2     NOT NULL,
  [content]      NVARCHAR(1000) NULL,
  [acknowledged] BIT           NOT NULL CONSTRAINT DF_Decree_ack DEFAULT (0),
  [createdAt]    DATETIME2     NOT NULL CONSTRAINT DF_Decree_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT FK_Decree_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

-- ============ Devices & Attendance ============
IF OBJECT_ID(N'[attendance].[Device]') IS NULL
CREATE TABLE [attendance].[Device] (
  [id]              NVARCHAR(191) NOT NULL CONSTRAINT PK_Device PRIMARY KEY,
  [name]            NVARCHAR(100) NOT NULL,
  [brand]           NVARCHAR(30)  NOT NULL,
  [serial]          NVARCHAR(50)  NOT NULL,
  [ip]              NVARCHAR(50)  NULL,
  [location]        NVARCHAR(100) NULL,
  [purpose]         NVARCHAR(30)  NOT NULL,
  [mode]            NVARCHAR(20)  NOT NULL CONSTRAINT DF_Device_mode DEFAULT (N'آنلاین'),
  [lastSync]        DATETIME2     NULL,
  [active]          BIT           NOT NULL CONSTRAINT DF_Device_active DEFAULT (1),
  [webhookToken]    NVARCHAR(64)  NOT NULL,
  [pollingInterval] INT           NOT NULL CONSTRAINT DF_Device_poll DEFAULT (60),
  [lastSeenAt]      DATETIME2     NULL,
  [lastPulledAt]    DATETIME2     NULL,
  [firmware]        NVARCHAR(50)  NULL,
  [userCount]       INT           NULL,
  [createdAt]       DATETIME2     NOT NULL CONSTRAINT DF_Device_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Device_serial UNIQUE ([serial]),
  CONSTRAINT UQ_Device_webhookToken UNIQUE ([webhookToken])
);

IF OBJECT_ID(N'[attendance].[AttendanceRecord]') IS NULL
CREATE TABLE [attendance].[AttendanceRecord] (
  [id]              NVARCHAR(191) NOT NULL CONSTRAINT PK_AttendanceRecord PRIMARY KEY,
  [employeeId]      NVARCHAR(191) NOT NULL,
  [deviceId]        NVARCHAR(191) NULL,
  [date]            NVARCHAR(10)  NOT NULL,
  [checkIn]         DATETIME2     NULL,
  [checkOut]        DATETIME2     NULL,
  [workMinutes]     INT           NOT NULL CONSTRAINT DF_AR_work DEFAULT (0),
  [lateMinutes]     INT           NOT NULL CONSTRAINT DF_AR_late DEFAULT (0),
  [earlyMinutes]    INT           NOT NULL CONSTRAINT DF_AR_early DEFAULT (0),
  [overtimeMinutes] INT           NOT NULL CONSTRAINT DF_AR_ot DEFAULT (0),
  [status]          NVARCHAR(30)  NOT NULL,
  [note]            NVARCHAR(500) NULL,
  [createdAt]       DATETIME2     NOT NULL CONSTRAINT DF_AR_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]       DATETIME2     NOT NULL,
  CONSTRAINT UQ_AR_empDate UNIQUE ([employeeId], [date]),
  CONSTRAINT FK_AR_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id]),
  CONSTRAINT FK_AR_device FOREIGN KEY ([deviceId]) REFERENCES [attendance].[Device]([id])
);

IF OBJECT_ID(N'[attendance].[LeaveRequest]') IS NULL
CREATE TABLE [attendance].[LeaveRequest] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_LeaveRequest PRIMARY KEY,
  [employeeId] NVARCHAR(191) NOT NULL,
  [type]       NVARCHAR(30)  NOT NULL,
  [fromDate]   NVARCHAR(10)  NOT NULL,
  [toDate]     NVARCHAR(10)  NOT NULL,
  [days]       FLOAT         NOT NULL CONSTRAINT DF_LR_days DEFAULT (1),
  [hours]      INT           NULL,
  [reason]     NVARCHAR(500) NULL,
  [status]     NVARCHAR(30)  NOT NULL CONSTRAINT DF_LR_status DEFAULT (N'در انتظار تأیید'),
  [approver]   NVARCHAR(100) NULL,
  [createdAt]  DATETIME2     NOT NULL CONSTRAINT DF_LR_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]  DATETIME2     NOT NULL,
  CONSTRAINT FK_LR_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

IF OBJECT_ID(N'[attendance].[Mission]') IS NULL
CREATE TABLE [attendance].[Mission] (
  [id]          NVARCHAR(191) NOT NULL CONSTRAINT PK_Mission PRIMARY KEY,
  [employeeId]  NVARCHAR(191) NOT NULL,
  [type]        NVARCHAR(30)  NOT NULL,
  [destination] NVARCHAR(200) NOT NULL,
  [purpose]     NVARCHAR(500) NULL,
  [fromDate]    NVARCHAR(10)  NOT NULL,
  [toDate]      NVARCHAR(10)  NOT NULL,
  [status]      NVARCHAR(30)  NOT NULL CONSTRAINT DF_M_status DEFAULT (N'در انتظار تأیید'),
  [approver]    NVARCHAR(100) NULL,
  [createdAt]   DATETIME2     NOT NULL CONSTRAINT DF_M_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]   DATETIME2     NOT NULL,
  CONSTRAINT FK_M_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

IF OBJECT_ID(N'[attendance].[OvertimePermit]') IS NULL
CREATE TABLE [attendance].[OvertimePermit] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_OvertimePermit PRIMARY KEY,
  [employeeId] NVARCHAR(191) NOT NULL,
  [type]       NVARCHAR(30)  NOT NULL,
  [date]       NVARCHAR(10)  NOT NULL,
  [fromTime]   NVARCHAR(10)  NOT NULL,
  [toTime]     NVARCHAR(10)  NOT NULL,
  [reason]     NVARCHAR(500) NULL,
  [status]     NVARCHAR(30)  NOT NULL CONSTRAINT DF_OP_status DEFAULT (N'در انتظار تأیید'),
  [approver]   NVARCHAR(100) NULL,
  [createdAt]  DATETIME2     NOT NULL CONSTRAINT DF_OP_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]  DATETIME2     NOT NULL,
  CONSTRAINT FK_OP_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

-- ============ Access Control ============
IF OBJECT_ID(N'[attendance].[Zone]') IS NULL
CREATE TABLE [attendance].[Zone] (
  [id]          NVARCHAR(191) NOT NULL CONSTRAINT PK_Zone PRIMARY KEY,
  [name]        NVARCHAR(100) NOT NULL,
  [code]        NVARCHAR(30)  NOT NULL,
  [riskLevel]   NVARCHAR(20)  NOT NULL CONSTRAINT DF_Zone_risk DEFAULT (N'عادی'),
  [description] NVARCHAR(500) NULL,
  [createdAt]   DATETIME2     NOT NULL CONSTRAINT DF_Zone_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Zone_code UNIQUE ([code])
);

IF OBJECT_ID(N'[attendance].[Gate]') IS NULL
CREATE TABLE [attendance].[Gate] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_Gate PRIMARY KEY,
  [name]      NVARCHAR(100) NOT NULL,
  [code]      NVARCHAR(30)  NOT NULL,
  [zoneId]    NVARCHAR(191) NOT NULL,
  [deviceId]  NVARCHAR(191) NULL,
  [type]      NVARCHAR(30)  NOT NULL,
  [direction] NVARCHAR(20)  NOT NULL CONSTRAINT DF_Gate_dir DEFAULT (N'دوطرفه'),
  [status]    NVARCHAR(20)  NOT NULL CONSTRAINT DF_Gate_status DEFAULT (N'فعال'),
  [createdAt] DATETIME2     NOT NULL CONSTRAINT DF_Gate_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Gate_code UNIQUE ([code]),
  CONSTRAINT FK_Gate_zone FOREIGN KEY ([zoneId]) REFERENCES [attendance].[Zone]([id]),
  CONSTRAINT FK_Gate_device FOREIGN KEY ([deviceId]) REFERENCES [attendance].[Device]([id])
);

IF OBJECT_ID(N'[attendance].[AccessPermission]') IS NULL
CREATE TABLE [attendance].[AccessPermission] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_AccessPermission PRIMARY KEY,
  [employeeId] NVARCHAR(191) NULL,
  [visitorId]  NVARCHAR(191) NULL,
  [zoneId]     NVARCHAR(191) NOT NULL,
  [type]       NVARCHAR(20)  NOT NULL CONSTRAINT DF_AP_type DEFAULT (N'دائم'),
  [validFrom]  DATETIME2     NOT NULL CONSTRAINT DF_AP_from DEFAULT (SYSUTCDATETIME()),
  [validUntil] DATETIME2     NULL,
  [days]       NVARCHAR(50)  NULL,
  [timeFrom]   NVARCHAR(10)  NULL,
  [timeUntil]  NVARCHAR(10)  NULL,
  [status]     NVARCHAR(20)  NOT NULL CONSTRAINT DF_AP_status DEFAULT (N'فعال'),
  [createdAt]  DATETIME2     NOT NULL CONSTRAINT DF_AP_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]  DATETIME2     NOT NULL,
  CONSTRAINT FK_AP_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id]),
  CONSTRAINT FK_AP_zone FOREIGN KEY ([zoneId]) REFERENCES [attendance].[Zone]([id])
);

IF OBJECT_ID(N'[attendance].[AccessEvent]') IS NULL
CREATE TABLE [attendance].[AccessEvent] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_AccessEvent PRIMARY KEY,
  [employeeId] NVARCHAR(191) NULL,
  [visitorId]  NVARCHAR(191) NULL,
  [gateId]     NVARCHAR(191) NOT NULL,
  [direction]  NVARCHAR(10)  NOT NULL,
  [method]     NVARCHAR(20)  NOT NULL CONSTRAINT DF_AE_method DEFAULT (N'کارت'),
  [result]     NVARCHAR(20)  NOT NULL CONSTRAINT DF_AE_result DEFAULT (N'مجاز'),
  [reason]     NVARCHAR(200) NULL,
  [timestamp]  DATETIME2     NOT NULL CONSTRAINT DF_AE_ts DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT FK_AE_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id]),
  CONSTRAINT FK_AE_gate FOREIGN KEY ([gateId]) REFERENCES [attendance].[Gate]([id])
);

-- ============ Visitors & Security ============
IF OBJECT_ID(N'[attendance].[Contractor]') IS NULL
CREATE TABLE [attendance].[Contractor] (
  [id]          NVARCHAR(191) NOT NULL CONSTRAINT PK_Contractor PRIMARY KEY,
  [name]        NVARCHAR(200) NOT NULL,
  [code]        NVARCHAR(30)  NOT NULL,
  [contactName] NVARCHAR(100) NULL,
  [phone]       NVARCHAR(20)  NULL,
  [contractNo]  NVARCHAR(50)  NULL,
  [validUntil]  DATETIME2     NULL,
  [active]      BIT           NOT NULL CONSTRAINT DF_C_active DEFAULT (1),
  [createdAt]   DATETIME2     NOT NULL CONSTRAINT DF_C_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Contractor_code UNIQUE ([code])
);

IF OBJECT_ID(N'[attendance].[Visitor]') IS NULL
CREATE TABLE [attendance].[Visitor] (
  [id]           NVARCHAR(191) NOT NULL CONSTRAINT PK_Visitor PRIMARY KEY,
  [firstName]    NVARCHAR(100) NOT NULL,
  [lastName]     NVARCHAR(100) NOT NULL,
  [nationalId]   NVARCHAR(20)  NOT NULL,
  [phone]        NVARCHAR(20)  NULL,
  [idDocType]    NVARCHAR(30)  NOT NULL CONSTRAINT DF_V_doc DEFAULT (N'کارت ملی'),
  [type]         NVARCHAR(20)  NOT NULL CONSTRAINT DF_V_type DEFAULT (N'مهمان'),
  [hostName]     NVARCHAR(100) NOT NULL,
  [hostDept]     NVARCHAR(100) NULL,
  [purpose]      NVARCHAR(300) NOT NULL,
  [contractorId] NVARCHAR(191) NULL,
  [status]       NVARCHAR(30)  NOT NULL CONSTRAINT DF_V_status DEFAULT (N'در انتظار تأیید'),
  [approvedBy]   NVARCHAR(100) NULL,
  [requestedAt]  DATETIME2     NOT NULL CONSTRAINT DF_V_reqAt DEFAULT (SYSUTCDATETIME()),
  [createdAt]    DATETIME2     NOT NULL CONSTRAINT DF_V_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]    DATETIME2     NOT NULL,
  CONSTRAINT FK_V_contractor FOREIGN KEY ([contractorId]) REFERENCES [attendance].[Contractor]([id])
);

IF OBJECT_ID(N'[attendance].[VisitorCard]') IS NULL
CREATE TABLE [attendance].[VisitorCard] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_VisitorCard PRIMARY KEY,
  [cardNo]     NVARCHAR(30)  NOT NULL,
  [type]       NVARCHAR(20)  NOT NULL CONSTRAINT DF_VC_type DEFAULT (N'QR'),
  [visitorId]  NVARCHAR(191) NOT NULL,
  [issuedAt]   DATETIME2     NOT NULL CONSTRAINT DF_VC_issuedAt DEFAULT (SYSUTCDATETIME()),
  [validUntil] DATETIME2     NOT NULL,
  [returned]   BIT           NOT NULL CONSTRAINT DF_VC_returned DEFAULT (0),
  CONSTRAINT UQ_VC_cardNo UNIQUE ([cardNo]),
  CONSTRAINT UQ_VC_visitor UNIQUE ([visitorId]),
  CONSTRAINT FK_VC_visitor FOREIGN KEY ([visitorId]) REFERENCES [attendance].[Visitor]([id])
);

IF OBJECT_ID(N'[attendance].[VisitLog]') IS NULL
CREATE TABLE [attendance].[VisitLog] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_VisitLog PRIMARY KEY,
  [visitorId] NVARCHAR(191) NOT NULL,
  [direction] NVARCHAR(10)  NOT NULL,
  [gateName]  NVARCHAR(100) NULL,
  [timestamp] DATETIME2     NOT NULL CONSTRAINT DF_VL_ts DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT FK_VL_visitor FOREIGN KEY ([visitorId]) REFERENCES [attendance].[Visitor]([id])
);

IF OBJECT_ID(N'[attendance].[SecurityAlert]') IS NULL
CREATE TABLE [attendance].[SecurityAlert] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_SecurityAlert PRIMARY KEY,
  [type]      NVARCHAR(30)  NOT NULL,
  [severity]  NVARCHAR(20)  NOT NULL CONSTRAINT DF_SA_sev DEFAULT (N'متوسط'),
  [message]   NVARCHAR(500) NOT NULL,
  [source]    NVARCHAR(30)  NOT NULL CONSTRAINT DF_SA_src DEFAULT (N'کنترل تردد'),
  [refId]     NVARCHAR(64)  NULL,
  [resolved]  BIT           NOT NULL CONSTRAINT DF_SA_res DEFAULT (0),
  [createdAt] DATETIME2     NOT NULL CONSTRAINT DF_SA_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt] DATETIME2     NOT NULL
);

-- ============ Auth & RBAC (migration 004 + 005) ============
IF OBJECT_ID(N'[attendance].[User]') IS NULL
CREATE TABLE [attendance].[User] (
  [id]                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_User PRIMARY KEY,
  [username]            NVARCHAR(32)  NOT NULL,
  [passwordHash]        NVARCHAR(200) NOT NULL,
  [fullName]            NVARCHAR(100) NOT NULL,
  [employeeId]          NVARCHAR(191) NULL,
  [isActive]            BIT           NOT NULL CONSTRAINT DF_User_active DEFAULT (1),
  [isSuperadmin]        BIT           NOT NULL CONSTRAINT DF_User_sa DEFAULT (0),
  [failedLoginAttempts] INT           NOT NULL CONSTRAINT DF_User_fla DEFAULT (0),   -- migration 010
  [lockedUntil]         DATETIME2     NULL,                                           -- migration 010
  [passwordChangedAt]   DATETIME2     NOT NULL CONSTRAINT DF_User_pwcAt DEFAULT (SYSUTCDATETIME()),
  [passwordExpiresAt]   DATETIME2     NULL,
  [mustChangePassword]  BIT           NOT NULL CONSTRAINT DF_User_mcp DEFAULT (0),
  [lastLoginAt]         DATETIME2     NULL,
  [createdAt]           DATETIME2     NOT NULL CONSTRAINT DF_User_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]           DATETIME2     NOT NULL,
  CONSTRAINT UQ_User_username UNIQUE ([username]),
  CONSTRAINT UQ_User_employee UNIQUE ([employeeId]),
  CONSTRAINT FK_User_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

IF OBJECT_ID(N'[attendance].[Role]') IS NULL
CREATE TABLE [attendance].[Role] (
  [id]          INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Role PRIMARY KEY,
  [name]        NVARCHAR(30)  NOT NULL,
  [title]       NVARCHAR(100) NOT NULL,
  [description] NVARCHAR(300) NULL,
  [createdAt]   DATETIME2     NOT NULL CONSTRAINT DF_Role_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Role_name UNIQUE ([name])
);

IF OBJECT_ID(N'[attendance].[Permission]') IS NULL
CREATE TABLE [attendance].[Permission] (
  [id]    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Permission PRIMARY KEY,
  [key]   NVARCHAR(50)  NOT NULL,
  [title] NVARCHAR(200) NOT NULL,
  [grp]   NVARCHAR(50)  NOT NULL,
  CONSTRAINT UQ_Permission_key UNIQUE ([key])
);

IF OBJECT_ID(N'[attendance].[UserRole]') IS NULL
CREATE TABLE [attendance].[UserRole] (
  [userId] INT NOT NULL,
  [roleId] INT NOT NULL,
  CONSTRAINT PK_UserRole PRIMARY KEY ([userId], [roleId]),
  CONSTRAINT FK_UR_user FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE,
  CONSTRAINT FK_UR_role FOREIGN KEY ([roleId]) REFERENCES [attendance].[Role]([id]) ON DELETE CASCADE
);

IF OBJECT_ID(N'[attendance].[RolePermission]') IS NULL
CREATE TABLE [attendance].[RolePermission] (
  [roleId]       INT NOT NULL,
  [permissionId] INT NOT NULL,
  CONSTRAINT PK_RolePermission PRIMARY KEY ([roleId], [permissionId]),
  CONSTRAINT FK_RP_role FOREIGN KEY ([roleId]) REFERENCES [attendance].[Role]([id]) ON DELETE CASCADE,
  CONSTRAINT FK_RP_perm FOREIGN KEY ([permissionId]) REFERENCES [attendance].[Permission]([id]) ON DELETE CASCADE
);

IF OBJECT_ID(N'[attendance].[RefreshToken]') IS NULL
CREATE TABLE [attendance].[RefreshToken] (
  [id]         NVARCHAR(191) NOT NULL CONSTRAINT PK_RefreshToken PRIMARY KEY,
  [userId]     INT           NOT NULL,
  [tokenHash]  NVARCHAR(128) NOT NULL,
  [expiresAt]  DATETIME2     NOT NULL,
  [revokedAt]  DATETIME2     NULL,
  [deviceInfo] NVARCHAR(300) NULL,
  [ip]         NVARCHAR(50)  NULL,
  [createdAt]  DATETIME2     NOT NULL CONSTRAINT DF_RT_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_RT_hash UNIQUE ([tokenHash]),
  CONSTRAINT FK_RT_user FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE
);

IF OBJECT_ID(N'[attendance].[PasswordHistory]') IS NULL
CREATE TABLE [attendance].[PasswordHistory] (
  [id]           NVARCHAR(191) NOT NULL CONSTRAINT PK_PasswordHistory PRIMARY KEY,
  [userId]       INT           NOT NULL,
  [passwordHash] NVARCHAR(200) NOT NULL,
  [createdAt]    DATETIME2     NOT NULL CONSTRAINT DF_PH_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT FK_PH_user FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE
);

IF OBJECT_ID(N'[attendance].[PasswordReset]') IS NULL
CREATE TABLE [attendance].[PasswordReset] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_PasswordReset PRIMARY KEY,
  [userId]    INT           NOT NULL,
  [tokenHash] NVARCHAR(128) NOT NULL,
  [expiresAt] DATETIME2     NOT NULL,
  [usedAt]    DATETIME2     NULL,
  [createdAt] DATETIME2     NOT NULL CONSTRAINT DF_PR_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_PR_hash UNIQUE ([tokenHash]),
  CONSTRAINT FK_PR_user FOREIGN KEY ([userId]) REFERENCES [attendance].[User]([id]) ON DELETE CASCADE
);

IF OBJECT_ID(N'[attendance].[AuditLog]') IS NULL
CREATE TABLE [attendance].[AuditLog] (
  [id]          NVARCHAR(191) NOT NULL CONSTRAINT PK_AuditLog PRIMARY KEY,
  [actorUserId] INT           NULL,
  [actorName]   NVARCHAR(100) NULL,
  [action]      NVARCHAR(30)  NOT NULL,
  [entity]      NVARCHAR(30)  NOT NULL,
  [entityId]    NVARCHAR(64)  NULL,
  [detail]      NVARCHAR(MAX) NULL,
  [ip]          NVARCHAR(50)  NULL,
  [at]          DATETIME2     NOT NULL CONSTRAINT DF_AL_at DEFAULT (SYSUTCDATETIME())
);
CREATE INDEX IX_AuditLog_at ON [attendance].[AuditLog]([at] DESC);
CREATE INDEX IX_AuditLog_action ON [attendance].[AuditLog]([action]);

-- ============ Device Commands (migration 015/016) ============
IF OBJECT_ID(N'[attendance].[DeviceCommand]') IS NULL
CREATE TABLE [attendance].[DeviceCommand] (
  [id]              NVARCHAR(191) NOT NULL CONSTRAINT PK_DeviceCommand PRIMARY KEY,
  [deviceId]        NVARCHAR(191) NOT NULL,
  [type]            NVARCHAR(20)  NOT NULL,
  [payload]         NVARCHAR(MAX) NULL,
  [status]          NVARCHAR(15)  NOT NULL CONSTRAINT DF_DC_status DEFAULT (N'pending'),
  [createdByUserId] INT           NULL,
  [createdBy]       NVARCHAR(100) NULL,
  [result]          NVARCHAR(300) NULL,
  [createdAt]       DATETIME2     NOT NULL CONSTRAINT DF_DC_createdAt DEFAULT (SYSUTCDATETIME()),
  [deliveredAt]     DATETIME2     NULL,
  [ackedAt]         DATETIME2     NULL,
  CONSTRAINT FK_DC_device FOREIGN KEY ([deviceId]) REFERENCES [attendance].[Device]([id]) ON DELETE CASCADE
);
CREATE INDEX IX_DC_device_status ON [attendance].[DeviceCommand]([deviceId], [status]);

-- ============ Payroll / Corrections / Holidays (migration 007) ============
IF OBJECT_ID(N'[attendance].[CorrectionRequest]') IS NULL
CREATE TABLE [attendance].[CorrectionRequest] (
  [id]           NVARCHAR(191) NOT NULL CONSTRAINT PK_CorrectionRequest PRIMARY KEY,
  [employeeId]   NVARCHAR(191) NOT NULL,
  [date]         NVARCHAR(10)  NOT NULL,
  [requestedIn]  DATETIME2     NULL,
  [requestedOut] DATETIME2     NULL,
  [reason]       NVARCHAR(500) NOT NULL,
  [status]       NVARCHAR(30)  NOT NULL CONSTRAINT DF_CR_status DEFAULT (N'در انتظار تأیید'),
  [reviewedBy]   NVARCHAR(100) NULL,
  [reviewNote]   NVARCHAR(300) NULL,
  [createdAt]    DATETIME2     NOT NULL CONSTRAINT DF_CR_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]    DATETIME2     NOT NULL,
  CONSTRAINT FK_CR_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

IF OBJECT_ID(N'[attendance].[Holiday]') IS NULL
CREATE TABLE [attendance].[Holiday] (
  [id]        NVARCHAR(191) NOT NULL CONSTRAINT PK_Holiday PRIMARY KEY,
  [date]      NVARCHAR(10)  NOT NULL,
  [title]     NVARCHAR(100) NOT NULL,
  [jYear]     INT           NOT NULL,
  [jMonth]    INT           NOT NULL,
  [jDay]      INT           NOT NULL,
  [createdAt] DATETIME2     NOT NULL CONSTRAINT DF_H_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_Holiday_date UNIQUE ([date])
);

IF OBJECT_ID(N'[attendance].[PayrollRun]') IS NULL
CREATE TABLE [attendance].[PayrollRun] (
  [id]            NVARCHAR(191) NOT NULL CONSTRAINT PK_PayrollRun PRIMARY KEY,
  [jYear]         INT           NOT NULL,
  [jMonth]        INT           NOT NULL,
  [status]        NVARCHAR(20)  NOT NULL CONSTRAINT DF_PR_status DEFAULT (N'پیش‌نویس'),
  [note]          NVARCHAR(300) NULL,
  [totalGross]    FLOAT         NOT NULL CONSTRAINT DF_PR_gross DEFAULT (0),
  [totalNet]      FLOAT         NOT NULL CONSTRAINT DF_PR_net DEFAULT (0),
  [employeeCount] INT           NOT NULL CONSTRAINT DF_PR_count DEFAULT (0),
  [createdBy]     NVARCHAR(100) NULL,
  [finalizedAt]   DATETIME2     NULL,
  [createdAt]     DATETIME2     NOT NULL CONSTRAINT DF_PR_createdAt DEFAULT (SYSUTCDATETIME()),
  [updatedAt]     DATETIME2     NOT NULL,
  CONSTRAINT UQ_PR_period UNIQUE ([jYear], [jMonth])
);

IF OBJECT_ID(N'[attendance].[Payslip]') IS NULL
CREATE TABLE [attendance].[Payslip] (
  [id]           NVARCHAR(191) NOT NULL CONSTRAINT PK_Payslip PRIMARY KEY,
  [runId]        NVARCHAR(191) NOT NULL,
  [employeeId]   NVARCHAR(191) NOT NULL,
  [baseSalary]   FLOAT         NOT NULL CONSTRAINT DF_PS_base DEFAULT (0),
  [overtimePay]  FLOAT         NOT NULL CONSTRAINT DF_PS_ot DEFAULT (0),
  [allowances]   FLOAT         NOT NULL CONSTRAINT DF_PS_allow DEFAULT (0),
  [absentDeduct] FLOAT         NOT NULL CONSTRAINT DF_PS_abs DEFAULT (0),
  [insurance]    FLOAT         NOT NULL CONSTRAINT DF_PS_ins DEFAULT (0),
  [tax]          FLOAT         NOT NULL CONSTRAINT DF_PS_tax DEFAULT (0),
  [netPay]       FLOAT         NOT NULL CONSTRAINT DF_PS_net DEFAULT (0),
  [workedMin]    INT           NOT NULL CONSTRAINT DF_PS_work DEFAULT (0),
  [overtimeMin]  INT           NOT NULL CONSTRAINT DF_PS_otMin DEFAULT (0),
  [absentDays]   FLOAT         NOT NULL CONSTRAINT DF_PS_absDays DEFAULT (0),
  [detail]       NVARCHAR(MAX) NULL,
  [createdAt]    DATETIME2     NOT NULL CONSTRAINT DF_PS_createdAt DEFAULT (SYSUTCDATETIME()),
  CONSTRAINT UQ_PS_runEmp UNIQUE ([runId], [employeeId]),
  CONSTRAINT FK_PS_run FOREIGN KEY ([runId]) REFERENCES [attendance].[PayrollRun]([id]) ON DELETE CASCADE,
  CONSTRAINT FK_PS_employee FOREIGN KEY ([employeeId]) REFERENCES [attendance].[Employee]([id])
);

GO
-- جدول ثبت مهاجرت‌ها (برای runner)
IF OBJECT_ID(N'[attendance].[_migrations]') IS NULL
CREATE TABLE [attendance].[_migrations] (
  [Name]      NVARCHAR(100) NOT NULL CONSTRAINT PK__migrations PRIMARY KEY,
  [AppliedAt] DATETIME2     NOT NULL CONSTRAINT DF__mig_at DEFAULT (SYSUTCDATETIME())
);
GO
PRINT N'اسکیمای SQL Server با موفقیت ایجاد شد.';
GO
