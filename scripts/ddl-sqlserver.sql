CREATE TABLE [AppSettings] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyName] nvarchar(max) NOT NULL,
    [CompanyLegalName] nvarchar(max) NULL,
    [CompanyNationalId] nvarchar(64) NULL,
    [CompanyPhone] nvarchar(max) NULL,
    [CompanyEmail] nvarchar(max) NULL,
    [CompanyWebsite] nvarchar(max) NULL,
    [CompanyAddress] nvarchar(max) NULL,
    [Description] nvarchar(max) NULL,
    [LogoPath] nvarchar(max) NULL,
    [AccentColor] nvarchar(max) NULL,
    [SidebarColor] nvarchar(max) NULL,
    [ActivePaletteId] int NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [UpdatedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_AppSettings] PRIMARY KEY ([Id])
);

CREATE TABLE [AuditLogs] (
    [Id] nvarchar(64) NOT NULL,
    [ActorUserId] int NULL,
    [ActorName] nvarchar(max) NULL,
    [Action] nvarchar(max) NOT NULL,
    [Entity] nvarchar(max) NOT NULL,
    [EntityId] nvarchar(64) NULL,
    [Detail] nvarchar(max) NULL,
    [Ip] nvarchar(max) NULL,
    [At] datetime2 NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [ColorPalettes] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(450) NOT NULL,
    [PrimaryColor] nvarchar(max) NOT NULL,
    [LinkColor] nvarchar(max) NOT NULL,
    [SidebarColor] nvarchar(max) NOT NULL,
    [SidebarTextColor] nvarchar(max) NOT NULL,
    [PageBgColor] nvarchar(max) NOT NULL,
    [SurfaceColor] nvarchar(max) NOT NULL,
    [TableHeaderColor] nvarchar(max) NOT NULL,
    [RowAltColor] nvarchar(max) NOT NULL,
    [RowHoverColor] nvarchar(max) NOT NULL,
    [BorderColor] nvarchar(max) NOT NULL,
    [TextColor] nvarchar(max) NOT NULL,
    [MutedTextColor] nvarchar(max) NOT NULL,
    [SuccessColor] nvarchar(max) NOT NULL,
    [WarningColor] nvarchar(max) NOT NULL,
    [DangerColor] nvarchar(max) NOT NULL,
    [InfoColor] nvarchar(max) NOT NULL,
    [IsBuiltIn] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ColorPalettes] PRIMARY KEY ([Id])
);

CREATE TABLE [Companies] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(450) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Companies] PRIMARY KEY ([Id])
);

CREATE TABLE [GatewaySettings] (
    [Id] nvarchar(64) NOT NULL,
    [Enabled] bit NOT NULL,
    [ListenPort] int NOT NULL,
    [TcpTimeoutMs] int NOT NULL,
    [PollIntervalSec] int NOT NULL,
    [SweeperIntervalSec] int NOT NULL,
    [ConfigRefreshSec] int NOT NULL,
    [LogLevel] nvarchar(max) NOT NULL,
    [DefaultDevicePort] int NOT NULL,
    [DefaultCommKey] int NOT NULL,
    [LastFetchedAt] datetime2 NULL,
    [ConfigVersion] int NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_GatewaySettings] PRIMARY KEY ([Id])
);

CREATE TABLE [Holidays] (
    [Id] nvarchar(64) NOT NULL,
    [Date] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [JYear] int NOT NULL,
    [JMonth] int NOT NULL,
    [JDay] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Holidays] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationLogs] (
    [Id] int NOT NULL IDENTITY,
    [RuleName] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Channel] nvarchar(max) NOT NULL,
    [Recipient] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Error] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NotificationLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationRules] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Enabled] bit NOT NULL,
    [Threshold] int NOT NULL,
    [RecipientEmail] nvarchar(max) NULL,
    [RecipientPhone] nvarchar(max) NULL,
    [ViaEmail] bit NOT NULL,
    [ViaSms] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [LastRunAt] datetime2 NULL,
    CONSTRAINT [PK_NotificationRules] PRIMARY KEY ([Id])
);

CREATE TABLE [NotificationSettings] (
    [Id] nvarchar(64) NOT NULL,
    [EnabledEmail] bit NOT NULL,
    [EnabledSms] bit NOT NULL,
    [SmtpHost] nvarchar(max) NOT NULL,
    [SmtpPort] int NOT NULL,
    [SmtpSsl] bit NOT NULL,
    [SmtpUser] nvarchar(max) NOT NULL,
    [SmtpPass] nvarchar(max) NOT NULL,
    [FromEmail] nvarchar(max) NOT NULL,
    [AdminEmails] nvarchar(max) NOT NULL,
    [SmsApiUrl] nvarchar(max) NOT NULL,
    [SmsApiKey] nvarchar(max) NOT NULL,
    [SmsSender] nvarchar(max) NOT NULL,
    [AlertTtlDays] int NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_NotificationSettings] PRIMARY KEY ([Id])
);

CREATE TABLE [Permissions] (
    [Id] int NOT NULL IDENTITY,
    [Key] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Grp] nvarchar(max) NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [Roles] (
    [Id] int NOT NULL IDENTITY,
    [Name] nvarchar(450) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

CREATE TABLE [SecurityAlerts] (
    [Id] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Severity] nvarchar(max) NOT NULL,
    [Message] nvarchar(max) NOT NULL,
    [Source] nvarchar(max) NOT NULL,
    [RefId] nvarchar(64) NULL,
    [Resolved] bit NOT NULL,
    [Expired] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_SecurityAlerts] PRIMARY KEY ([Id])
);

CREATE TABLE [AccessGroups] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [Note] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AccessGroups] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccessGroups_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [Contractors] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [ContactName] nvarchar(max) NULL,
    [Phone] nvarchar(max) NULL,
    [ContractNo] nvarchar(max) NULL,
    [ValidUntil] datetime2 NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Contractors] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Contractors_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [Departments] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [ParentId] nvarchar(64) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Departments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Departments_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Departments_Departments_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [Departments] ([Id])
);

CREATE TABLE [Devices] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Brand] nvarchar(max) NOT NULL,
    [Serial] nvarchar(450) NOT NULL,
    [Ip] nvarchar(max) NULL,
    [Location] nvarchar(max) NULL,
    [Purpose] nvarchar(max) NOT NULL,
    [Transport] nvarchar(max) NOT NULL,
    [DevicePort] int NULL,
    [CommKey] int NULL,
    [Mode] nvarchar(max) NOT NULL,
    [LastSync] datetime2 NULL,
    [Active] bit NOT NULL,
    [WebhookToken] nvarchar(450) NOT NULL,
    [PollingInterval] int NOT NULL,
    [LastSeenAt] datetime2 NULL,
    [LastPulledAt] datetime2 NULL,
    [Firmware] nvarchar(max) NULL,
    [UserCount] int NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Devices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Devices_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [ParkingLots] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [Capacity] int NOT NULL,
    [MonthlyFee] decimal(18,2) NOT NULL,
    [Location] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ParkingLots] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ParkingLots_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [PayItems] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Formula] nvarchar(max) NOT NULL,
    [Taxable] bit NOT NULL,
    [Insurable] bit NOT NULL,
    [Continuous] bit NOT NULL,
    [ScopeType] nvarchar(max) NOT NULL,
    [ScopeId] nvarchar(64) NULL,
    [Priority] int NOT NULL,
    [Order] int NOT NULL,
    [IsSystem] bit NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayItems] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayItems_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PayrollParameters] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [JYear] int NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Value] decimal(18,2) NOT NULL,
    [StrValue] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollParameters] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollParameters_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PayrollRuns] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [JYear] int NOT NULL,
    [JMonth] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Note] nvarchar(max) NULL,
    [TotalGross] decimal(18,2) NOT NULL,
    [TotalNet] decimal(18,2) NOT NULL,
    [EmployeeCount] int NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [FinalizedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollRuns] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollRuns_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [PayrollSettings] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [CalcMethod] nvarchar(max) NOT NULL,
    [WorkSource] int NOT NULL,
    [MonthDays] int NOT NULL,
    [HourBase] int NOT NULL,
    [RoundUnit] int NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollSettings] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollSettings_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Shifts] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [Type] nvarchar(max) NOT NULL,
    [StartTime] nvarchar(max) NOT NULL,
    [EndTime] nvarchar(max) NOT NULL,
    [BreakMinutes] int NOT NULL,
    [CyclePattern] nvarchar(max) NULL,
    [ToleranceLate] int NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Shifts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Shifts_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [Zones] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [RiskLevel] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NULL,
    [AntiPassback] bit NOT NULL,
    [TwoFactor] bit NOT NULL,
    [TwoFactorWindowSec] int NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Zones] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Zones_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id])
);

CREATE TABLE [RolePermissions] (
    [RoleId] int NOT NULL,
    [PermissionId] int NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([RoleId], [PermissionId]),
    CONSTRAINT [FK_RolePermissions_Permissions_PermissionId] FOREIGN KEY ([PermissionId]) REFERENCES [Permissions] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_RolePermissions_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Visitors] (
    [Id] nvarchar(64) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [NationalId] nvarchar(64) NOT NULL,
    [Phone] nvarchar(max) NULL,
    [IdDocType] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [HostName] nvarchar(max) NOT NULL,
    [HostDept] nvarchar(max) NULL,
    [Purpose] nvarchar(max) NOT NULL,
    [ContractorId] nvarchar(64) NULL,
    [Status] nvarchar(max) NOT NULL,
    [ApprovedBy] nvarchar(max) NULL,
    [RequestedAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Visitors] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Visitors_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Visitors_Contractors_ContractorId] FOREIGN KEY ([ContractorId]) REFERENCES [Contractors] ([Id])
);

CREATE TABLE [Positions] (
    [Id] nvarchar(64) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [Grade] nvarchar(max) NULL,
    [CompanyId] nvarchar(64) NULL,
    [DepartmentId] nvarchar(64) NOT NULL,
    [Description] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Positions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Positions_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Positions_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DeviceBiometrics] (
    [Id] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NOT NULL,
    [Pin] nvarchar(50) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [BioIndex] int NOT NULL,
    [Valid] int NOT NULL,
    [Quality] int NULL,
    [TemplateData] nvarchar(max) NULL,
    [Length] int NOT NULL,
    [FetchedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DeviceBiometrics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeviceBiometrics_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DeviceCommands] (
    [Id] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Payload] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedByUserId] int NULL,
    [CreatedBy] nvarchar(max) NULL,
    [Result] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [DeliveredAt] datetime2 NULL,
    [AckedAt] datetime2 NULL,
    CONSTRAINT [PK_DeviceCommands] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeviceCommands_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DeviceOfflinePunches] (
    [Id] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NOT NULL,
    [Pin] nvarchar(max) NOT NULL,
    [Timestamp] datetime2 NOT NULL,
    [Source] nvarchar(max) NOT NULL,
    [ImportedAt] datetime2 NULL,
    [Note] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DeviceOfflinePunches] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeviceOfflinePunches_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ParkingGates] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [LotId] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NULL,
    [Type] nvarchar(max) NOT NULL,
    [Direction] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [OpenState] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ParkingGates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ParkingGates_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_ParkingGates_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_ParkingGates_ParkingLots_LotId] FOREIGN KEY ([LotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Gates] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Code] nvarchar(100) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [ZoneId] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NULL,
    [Type] nvarchar(max) NOT NULL,
    [Direction] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [DoorState] nvarchar(max) NOT NULL,
    [ScheduleDays] nvarchar(max) NULL,
    [ScheduleFrom] nvarchar(max) NULL,
    [ScheduleTo] nvarchar(max) NULL,
    [MapX] float NULL,
    [MapY] float NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Gates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Gates_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Gates_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_Gates_Zones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [Zones] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [VisitLogs] (
    [Id] nvarchar(64) NOT NULL,
    [VisitorId] nvarchar(64) NOT NULL,
    [Direction] nvarchar(max) NOT NULL,
    [GateName] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_VisitLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VisitLogs_Visitors_VisitorId] FOREIGN KEY ([VisitorId]) REFERENCES [Visitors] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [VisitorCards] (
    [Id] nvarchar(64) NOT NULL,
    [CardNo] nvarchar(450) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [VisitorId] nvarchar(64) NOT NULL,
    [IssuedAt] datetime2 NOT NULL,
    [ValidUntil] datetime2 NOT NULL,
    [Returned] bit NOT NULL,
    CONSTRAINT [PK_VisitorCards] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_VisitorCards_Visitors_VisitorId] FOREIGN KEY ([VisitorId]) REFERENCES [Visitors] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Employees] (
    [Id] nvarchar(64) NOT NULL,
    [PersonnelCode] nvarchar(450) NOT NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [NationalId] nvarchar(64) NOT NULL,
    [BirthDate] nvarchar(max) NULL,
    [Phone] nvarchar(max) NULL,
    [Email] nvarchar(max) NULL,
    [EmploymentType] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [HireDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [DepartmentId] nvarchar(64) NULL,
    [PositionId] nvarchar(64) NULL,
    [BaseSalary] decimal(18,2) NOT NULL,
    [PhotoPath] nvarchar(max) NULL,
    [MaritalStatus] nvarchar(max) NOT NULL,
    [ChildCount] int NOT NULL,
    [ShiftId] nvarchar(64) NULL,
    [AnnualLeaveEnt] decimal(18,2) NOT NULL,
    [AnnualLeaveUsed] decimal(18,2) NOT NULL,
    [SickLeaveEnt] decimal(18,2) NOT NULL,
    [SickLeaveUsed] decimal(18,2) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Employees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Employees_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Employees_Departments_DepartmentId] FOREIGN KEY ([DepartmentId]) REFERENCES [Departments] ([Id]),
    CONSTRAINT [FK_Employees_Positions_PositionId] FOREIGN KEY ([PositionId]) REFERENCES [Positions] ([Id]),
    CONSTRAINT [FK_Employees_Shifts_ShiftId] FOREIGN KEY ([ShiftId]) REFERENCES [Shifts] ([Id])
);

CREATE TABLE [AnprCameras] (
    [Id] nvarchar(64) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [GateId] nvarchar(64) NOT NULL,
    [Brand] nvarchar(max) NOT NULL,
    [Model] nvarchar(max) NULL,
    [Serial] nvarchar(450) NOT NULL,
    [Ip] nvarchar(max) NULL,
    [Location] nvarchar(max) NULL,
    [Direction] nvarchar(max) NOT NULL,
    [ConfidenceThreshold] int NOT NULL,
    [StreamUrl] nvarchar(max) NULL,
    [Mode] nvarchar(max) NOT NULL,
    [HeartbeatInterval] int NOT NULL,
    [LastSeenAt] datetime2 NULL,
    [WebhookToken] nvarchar(450) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AnprCameras] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AnprCameras_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_AnprCameras_ParkingGates_GateId] FOREIGN KEY ([GateId]) REFERENCES [ParkingGates] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AccessGroupGates] (
    [Id] nvarchar(64) NOT NULL,
    [GroupId] nvarchar(64) NOT NULL,
    [GateId] nvarchar(64) NOT NULL,
    [Days] nvarchar(max) NULL,
    [TimeFrom] nvarchar(max) NULL,
    [TimeUntil] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AccessGroupGates] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccessGroupGates_AccessGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [AccessGroups] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessGroupGates_Gates_GateId] FOREIGN KEY ([GateId]) REFERENCES [Gates] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AccessEvents] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NULL,
    [VisitorId] nvarchar(64) NULL,
    [GateId] nvarchar(64) NOT NULL,
    [Direction] nvarchar(max) NOT NULL,
    [Method] nvarchar(max) NOT NULL,
    [Result] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_AccessEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccessEvents_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_AccessEvents_Gates_GateId] FOREIGN KEY ([GateId]) REFERENCES [Gates] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessEvents_Visitors_VisitorId] FOREIGN KEY ([VisitorId]) REFERENCES [Visitors] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AccessGroupMembers] (
    [Id] nvarchar(64) NOT NULL,
    [GroupId] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AccessGroupMembers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccessGroupMembers_AccessGroups_GroupId] FOREIGN KEY ([GroupId]) REFERENCES [AccessGroups] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessGroupMembers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AccessPermissions] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NULL,
    [VisitorId] nvarchar(64) NULL,
    [ZoneId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [ValidFrom] datetime2 NOT NULL,
    [ValidUntil] datetime2 NULL,
    [Days] nvarchar(max) NULL,
    [TimeFrom] nvarchar(max) NULL,
    [TimeUntil] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AccessPermissions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AccessPermissions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]),
    CONSTRAINT [FK_AccessPermissions_Visitors_VisitorId] FOREIGN KEY ([VisitorId]) REFERENCES [Visitors] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_AccessPermissions_Zones_ZoneId] FOREIGN KEY ([ZoneId]) REFERENCES [Zones] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AttendanceRecords] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NULL,
    [Date] nvarchar(20) NOT NULL,
    [CheckIn] datetime2 NULL,
    [CheckOut] datetime2 NULL,
    [WorkMinutes] int NOT NULL,
    [LateMinutes] int NOT NULL,
    [EarlyMinutes] int NOT NULL,
    [OvertimeMinutes] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Note] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_AttendanceRecords] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AttendanceRecords_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_AttendanceRecords_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Cards] (
    [Id] nvarchar(64) NOT NULL,
    [CardNo] nvarchar(450) NOT NULL,
    [EmployeeId] nvarchar(64) NULL,
    [Type] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [IssuedAt] datetime2 NOT NULL,
    [ExpiresAt] datetime2 NULL,
    [ReturnedAt] datetime2 NULL,
    [Note] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Cards] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Cards_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [Contracts] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [ContractNo] nvarchar(450) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NULL,
    [Salary] decimal(18,2) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Contracts] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Contracts_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [CorrectionRequests] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Date] nvarchar(max) NOT NULL,
    [RequestedIn] datetime2 NULL,
    [RequestedOut] datetime2 NULL,
    [Reason] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ReviewedBy] nvarchar(max) NULL,
    [ReviewNote] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_CorrectionRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_CorrectionRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Decrees] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [DecreeNo] nvarchar(max) NOT NULL,
    [Date] datetime2 NOT NULL,
    [Content] nvarchar(max) NULL,
    [Acknowledged] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Decrees] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Decrees_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [DevicePunchLogs] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [DeviceId] nvarchar(64) NULL,
    [Pin] nvarchar(max) NOT NULL,
    [EmployeeId] nvarchar(64) NULL,
    [Timestamp] datetime2 NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Note] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DevicePunchLogs] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DevicePunchLogs_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_DevicePunchLogs_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [DeviceUsers] (
    [Id] nvarchar(64) NOT NULL,
    [DeviceId] nvarchar(64) NOT NULL,
    [Uid] int NOT NULL,
    [Pin] nvarchar(50) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Privilege] int NOT NULL,
    [CardNumber] nvarchar(max) NULL,
    [Password] nvarchar(max) NULL,
    [GroupId] nvarchar(64) NULL,
    [EmployeeId] nvarchar(64) NULL,
    [LastFetchedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_DeviceUsers] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_DeviceUsers_Devices_DeviceId] FOREIGN KEY ([DeviceId]) REFERENCES [Devices] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_DeviceUsers_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [EmployeeBiometrics] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Type] nvarchar(50) NOT NULL,
    [BioIndex] int NOT NULL,
    [Valid] int NOT NULL,
    [Quality] int NULL,
    [TemplateData] nvarchar(max) NULL,
    [Length] int NOT NULL,
    [SourceDeviceId] nvarchar(64) NULL,
    [CardNumber] nvarchar(max) NULL,
    [EnrolledAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_EmployeeBiometrics] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeBiometrics_Devices_SourceDeviceId] FOREIGN KEY ([SourceDeviceId]) REFERENCES [Devices] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_EmployeeBiometrics_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [LeaveRequests] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [FromDate] nvarchar(max) NOT NULL,
    [ToDate] nvarchar(max) NOT NULL,
    [Days] decimal(18,2) NOT NULL,
    [Hours] int NULL,
    [Reason] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [Approver] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_LeaveRequests] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_LeaveRequests_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Missions] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Destination] nvarchar(max) NOT NULL,
    [Purpose] nvarchar(max) NULL,
    [FromDate] nvarchar(max) NOT NULL,
    [ToDate] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Approver] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Missions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Missions_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [OvertimePermits] (
    [Id] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Date] nvarchar(max) NOT NULL,
    [FromTime] nvarchar(max) NOT NULL,
    [ToTime] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [Approver] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_OvertimePermits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_OvertimePermits_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PayrollBonusRows] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [JYear] int NOT NULL,
    [Days] decimal(18,2) NOT NULL,
    [Base] decimal(18,2) NOT NULL,
    [Eid] decimal(18,2) NOT NULL,
    [Seniority] decimal(18,2) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [FinalizedBy] nvarchar(max) NULL,
    [FinalizedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollBonusRows] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollBonusRows_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_PayrollBonusRows_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PayrollSettlements] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [Days] decimal(18,2) NOT NULL,
    [LeaveBalance] decimal(18,2) NOT NULL,
    [LeavePay] decimal(18,2) NOT NULL,
    [Daily] decimal(18,2) NOT NULL,
    [Eid] decimal(18,2) NOT NULL,
    [Seniority] decimal(18,2) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [FinalizedBy] nvarchar(max) NULL,
    [FinalizedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PayrollSettlements] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayrollSettlements_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_PayrollSettlements_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Payslips] (
    [Id] nvarchar(64) NOT NULL,
    [RunId] nvarchar(64) NOT NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [BaseSalary] decimal(18,2) NOT NULL,
    [OvertimePay] decimal(18,2) NOT NULL,
    [Allowances] decimal(18,2) NOT NULL,
    [AbsentDeduct] decimal(18,2) NOT NULL,
    [Insurance] decimal(18,2) NOT NULL,
    [Tax] decimal(18,2) NOT NULL,
    [NetPay] decimal(18,2) NOT NULL,
    [WorkedMin] int NOT NULL,
    [OvertimeMin] int NOT NULL,
    [AbsentDays] int NOT NULL,
    [Detail] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Payslips] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payslips_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Payslips_PayrollRuns_RunId] FOREIGN KEY ([RunId]) REFERENCES [PayrollRuns] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [Users] (
    [Id] int NOT NULL IDENTITY,
    [Username] nvarchar(450) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [FullName] nvarchar(max) NOT NULL,
    [PhotoPath] nvarchar(max) NULL,
    [EmployeeId] nvarchar(64) NULL,
    [CompanyId] nvarchar(64) NULL,
    [IsActive] bit NOT NULL,
    [IsSuperadmin] bit NOT NULL,
    [FailedLoginAttempts] int NOT NULL,
    [LockedUntil] datetime2 NULL,
    [PasswordChangedAt] datetime2 NOT NULL,
    [PasswordExpiresAt] datetime2 NULL,
    [MustChangePassword] bit NOT NULL,
    [LastLoginAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Users_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Users_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [Vehicles] (
    [Id] nvarchar(64) NOT NULL,
    [CompanyId] nvarchar(64) NULL,
    [EmployeeId] nvarchar(64) NOT NULL,
    [PlateNo] nvarchar(max) NOT NULL,
    [PlateKey] nvarchar(100) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Brand] nvarchar(max) NULL,
    [Model] nvarchar(max) NULL,
    [Color] nvarchar(max) NULL,
    [Active] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Vehicles_Companies_CompanyId] FOREIGN KEY ([CompanyId]) REFERENCES [Companies] ([Id]),
    CONSTRAINT [FK_Vehicles_Employees_EmployeeId] FOREIGN KEY ([EmployeeId]) REFERENCES [Employees] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [AnprCommands] (
    [Id] nvarchar(64) NOT NULL,
    [CameraId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Payload] nvarchar(max) NULL,
    [Status] nvarchar(50) NOT NULL,
    [CreatedByUserId] int NULL,
    [CreatedBy] nvarchar(max) NULL,
    [Result] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    [DeliveredAt] datetime2 NULL,
    [AckedAt] datetime2 NULL,
    CONSTRAINT [PK_AnprCommands] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_AnprCommands_AnprCameras_CameraId] FOREIGN KEY ([CameraId]) REFERENCES [AnprCameras] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PayslipLines] (
    [Id] nvarchar(64) NOT NULL,
    [PayslipId] nvarchar(64) NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Title] nvarchar(max) NOT NULL,
    [Kind] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Taxable] bit NOT NULL,
    [Insurable] bit NOT NULL,
    [Order] int NOT NULL,
    CONSTRAINT [PK_PayslipLines] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PayslipLines_Payslips_PayslipId] FOREIGN KEY ([PayslipId]) REFERENCES [Payslips] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PasswordHistories] (
    [Id] nvarchar(64) NOT NULL,
    [UserId] int NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PasswordHistories] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordHistories_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [PasswordResets] (
    [Id] nvarchar(64) NOT NULL,
    [UserId] int NOT NULL,
    [TokenHash] nvarchar(450) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [UsedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_PasswordResets] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_PasswordResets_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [RefreshTokens] (
    [Id] nvarchar(64) NOT NULL,
    [UserId] int NOT NULL,
    [TokenHash] nvarchar(450) NOT NULL,
    [ExpiresAt] datetime2 NOT NULL,
    [RevokedAt] datetime2 NULL,
    [DeviceInfo] nvarchar(max) NULL,
    [Ip] nvarchar(max) NULL,
    [CreatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_RefreshTokens] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_RefreshTokens_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [UserRoles] (
    [UserId] int NOT NULL,
    [RoleId] int NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([UserId], [RoleId]),
    CONSTRAINT [FK_UserRoles_Roles_RoleId] FOREIGN KEY ([RoleId]) REFERENCES [Roles] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_UserRoles_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [ParkingEvents] (
    [Id] nvarchar(64) NOT NULL,
    [GateId] nvarchar(64) NOT NULL,
    [CameraId] nvarchar(64) NULL,
    [VehicleId] nvarchar(64) NULL,
    [PlateText] nvarchar(max) NOT NULL,
    [PlateKey] nvarchar(100) NULL,
    [Direction] nvarchar(max) NOT NULL,
    [Method] nvarchar(max) NOT NULL,
    [Result] nvarchar(max) NOT NULL,
    [Reason] nvarchar(max) NULL,
    [Confidence] int NULL,
    [Timestamp] datetime2 NOT NULL,
    CONSTRAINT [PK_ParkingEvents] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ParkingEvents_AnprCameras_CameraId] FOREIGN KEY ([CameraId]) REFERENCES [AnprCameras] ([Id]) ON DELETE SET NULL,
    CONSTRAINT [FK_ParkingEvents_ParkingGates_GateId] FOREIGN KEY ([GateId]) REFERENCES [ParkingGates] ([Id]),
    CONSTRAINT [FK_ParkingEvents_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE SET NULL
);

CREATE TABLE [ParkingPermits] (
    [Id] nvarchar(64) NOT NULL,
    [VehicleId] nvarchar(64) NOT NULL,
    [LotId] nvarchar(64) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [ValidFrom] datetime2 NOT NULL,
    [ValidUntil] datetime2 NULL,
    [SpotNo] nvarchar(max) NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    CONSTRAINT [PK_ParkingPermits] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ParkingPermits_ParkingLots_LotId] FOREIGN KEY ([LotId]) REFERENCES [ParkingLots] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ParkingPermits_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_AccessEvents_EmployeeId] ON [AccessEvents] ([EmployeeId]);

CREATE INDEX [IX_AccessEvents_GateId] ON [AccessEvents] ([GateId]);

CREATE INDEX [IX_AccessEvents_VisitorId] ON [AccessEvents] ([VisitorId]);

CREATE INDEX [IX_AccessGroupGates_GateId] ON [AccessGroupGates] ([GateId]);

CREATE INDEX [IX_AccessGroupGates_GroupId] ON [AccessGroupGates] ([GroupId]);

CREATE INDEX [IX_AccessGroupMembers_EmployeeId] ON [AccessGroupMembers] ([EmployeeId]);

CREATE INDEX [IX_AccessGroupMembers_GroupId] ON [AccessGroupMembers] ([GroupId]);

CREATE INDEX [IX_AccessGroups_CompanyId] ON [AccessGroups] ([CompanyId]);

CREATE INDEX [IX_AccessPermissions_EmployeeId] ON [AccessPermissions] ([EmployeeId]);

CREATE INDEX [IX_AccessPermissions_VisitorId] ON [AccessPermissions] ([VisitorId]);

CREATE INDEX [IX_AccessPermissions_ZoneId] ON [AccessPermissions] ([ZoneId]);

CREATE INDEX [IX_AnprCameras_CompanyId] ON [AnprCameras] ([CompanyId]);

CREATE INDEX [IX_AnprCameras_GateId] ON [AnprCameras] ([GateId]);

CREATE UNIQUE INDEX [IX_AnprCameras_Serial] ON [AnprCameras] ([Serial]);

CREATE UNIQUE INDEX [IX_AnprCameras_WebhookToken] ON [AnprCameras] ([WebhookToken]);

CREATE INDEX [IX_AnprCommands_CameraId_Status] ON [AnprCommands] ([CameraId], [Status]);

CREATE INDEX [IX_AttendanceRecords_DeviceId] ON [AttendanceRecords] ([DeviceId]);

CREATE UNIQUE INDEX [IX_AttendanceRecords_EmployeeId_Date] ON [AttendanceRecords] ([EmployeeId], [Date]);

CREATE UNIQUE INDEX [IX_Cards_CardNo] ON [Cards] ([CardNo]);

CREATE INDEX [IX_Cards_EmployeeId] ON [Cards] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_ColorPalettes_Name] ON [ColorPalettes] ([Name]);

CREATE UNIQUE INDEX [IX_Companies_Code] ON [Companies] ([Code]);

CREATE INDEX [IX_Contractors_CompanyId] ON [Contractors] ([CompanyId]);

CREATE UNIQUE INDEX [IX_Contracts_ContractNo] ON [Contracts] ([ContractNo]);

CREATE INDEX [IX_Contracts_EmployeeId] ON [Contracts] ([EmployeeId]);

CREATE INDEX [IX_CorrectionRequests_EmployeeId] ON [CorrectionRequests] ([EmployeeId]);

CREATE INDEX [IX_Decrees_EmployeeId] ON [Decrees] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_Departments_CompanyId_Code] ON [Departments] ([CompanyId], [Code]);

CREATE INDEX [IX_Departments_ParentId] ON [Departments] ([ParentId]);

CREATE UNIQUE INDEX [IX_DeviceBiometrics_DeviceId_Pin_Type_BioIndex] ON [DeviceBiometrics] ([DeviceId], [Pin], [Type], [BioIndex]);

CREATE INDEX [IX_DeviceCommands_DeviceId] ON [DeviceCommands] ([DeviceId]);

CREATE INDEX [IX_DeviceOfflinePunches_DeviceId_ImportedAt] ON [DeviceOfflinePunches] ([DeviceId], [ImportedAt]);

CREATE INDEX [IX_DevicePunchLogs_DeviceId_Timestamp] ON [DevicePunchLogs] ([DeviceId], [Timestamp]);

CREATE INDEX [IX_DevicePunchLogs_EmployeeId] ON [DevicePunchLogs] ([EmployeeId]);

CREATE INDEX [IX_DevicePunchLogs_Timestamp] ON [DevicePunchLogs] ([Timestamp]);

CREATE INDEX [IX_Devices_CompanyId] ON [Devices] ([CompanyId]);

CREATE UNIQUE INDEX [IX_Devices_Serial] ON [Devices] ([Serial]);

CREATE UNIQUE INDEX [IX_Devices_WebhookToken] ON [Devices] ([WebhookToken]);

CREATE UNIQUE INDEX [IX_DeviceUsers_DeviceId_Pin] ON [DeviceUsers] ([DeviceId], [Pin]);

CREATE INDEX [IX_DeviceUsers_EmployeeId] ON [DeviceUsers] ([EmployeeId]);

CREATE INDEX [IX_DeviceUsers_Pin] ON [DeviceUsers] ([Pin]);

CREATE UNIQUE INDEX [IX_EmployeeBiometrics_EmployeeId_Type_BioIndex] ON [EmployeeBiometrics] ([EmployeeId], [Type], [BioIndex]);

CREATE INDEX [IX_EmployeeBiometrics_SourceDeviceId] ON [EmployeeBiometrics] ([SourceDeviceId]);

CREATE INDEX [IX_Employees_CompanyId] ON [Employees] ([CompanyId]);

CREATE INDEX [IX_Employees_DepartmentId] ON [Employees] ([DepartmentId]);

CREATE UNIQUE INDEX [IX_Employees_NationalId] ON [Employees] ([NationalId]);

CREATE UNIQUE INDEX [IX_Employees_PersonnelCode] ON [Employees] ([PersonnelCode]);

CREATE INDEX [IX_Employees_PositionId] ON [Employees] ([PositionId]);

CREATE INDEX [IX_Employees_ShiftId] ON [Employees] ([ShiftId]);

CREATE UNIQUE INDEX [IX_Gates_CompanyId_Code] ON [Gates] ([CompanyId], [Code]) WHERE [CompanyId] IS NOT NULL;

CREATE INDEX [IX_Gates_DeviceId] ON [Gates] ([DeviceId]);

CREATE INDEX [IX_Gates_ZoneId] ON [Gates] ([ZoneId]);

CREATE UNIQUE INDEX [IX_Holidays_Date] ON [Holidays] ([Date]);

CREATE INDEX [IX_LeaveRequests_EmployeeId] ON [LeaveRequests] ([EmployeeId]);

CREATE INDEX [IX_Missions_EmployeeId] ON [Missions] ([EmployeeId]);

CREATE INDEX [IX_OvertimePermits_EmployeeId] ON [OvertimePermits] ([EmployeeId]);

CREATE INDEX [IX_ParkingEvents_CameraId] ON [ParkingEvents] ([CameraId]);

CREATE INDEX [IX_ParkingEvents_GateId] ON [ParkingEvents] ([GateId]);

CREATE INDEX [IX_ParkingEvents_PlateKey] ON [ParkingEvents] ([PlateKey]);

CREATE INDEX [IX_ParkingEvents_Timestamp] ON [ParkingEvents] ([Timestamp]);

CREATE INDEX [IX_ParkingEvents_VehicleId] ON [ParkingEvents] ([VehicleId]);

CREATE UNIQUE INDEX [IX_ParkingGates_CompanyId_Code] ON [ParkingGates] ([CompanyId], [Code]) WHERE [CompanyId] IS NOT NULL;

CREATE INDEX [IX_ParkingGates_DeviceId] ON [ParkingGates] ([DeviceId]);

CREATE INDEX [IX_ParkingGates_LotId] ON [ParkingGates] ([LotId]);

CREATE UNIQUE INDEX [IX_ParkingLots_CompanyId_Code] ON [ParkingLots] ([CompanyId], [Code]) WHERE [CompanyId] IS NOT NULL;

CREATE INDEX [IX_ParkingPermits_LotId] ON [ParkingPermits] ([LotId]);

CREATE INDEX [IX_ParkingPermits_VehicleId] ON [ParkingPermits] ([VehicleId]);

CREATE INDEX [IX_PasswordHistories_UserId] ON [PasswordHistories] ([UserId]);

CREATE UNIQUE INDEX [IX_PasswordResets_TokenHash] ON [PasswordResets] ([TokenHash]);

CREATE INDEX [IX_PasswordResets_UserId] ON [PasswordResets] ([UserId]);

CREATE UNIQUE INDEX [IX_PayItems_CompanyId_Code] ON [PayItems] ([CompanyId], [Code]);

CREATE UNIQUE INDEX [IX_PayrollBonusRows_CompanyId_JYear_EmployeeId] ON [PayrollBonusRows] ([CompanyId], [JYear], [EmployeeId]);

CREATE INDEX [IX_PayrollBonusRows_EmployeeId] ON [PayrollBonusRows] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_PayrollParameters_CompanyId_JYear_Code] ON [PayrollParameters] ([CompanyId], [JYear], [Code]);

CREATE UNIQUE INDEX [IX_PayrollRuns_CompanyId_JYear_JMonth] ON [PayrollRuns] ([CompanyId], [JYear], [JMonth]) WHERE [CompanyId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_PayrollSettings_CompanyId] ON [PayrollSettings] ([CompanyId]);

CREATE INDEX [IX_PayrollSettlements_CompanyId] ON [PayrollSettlements] ([CompanyId]);

CREATE INDEX [IX_PayrollSettlements_EmployeeId] ON [PayrollSettlements] ([EmployeeId]);

CREATE INDEX [IX_PayslipLines_PayslipId] ON [PayslipLines] ([PayslipId]);

CREATE INDEX [IX_Payslips_EmployeeId] ON [Payslips] ([EmployeeId]);

CREATE UNIQUE INDEX [IX_Payslips_RunId_EmployeeId] ON [Payslips] ([RunId], [EmployeeId]);

CREATE UNIQUE INDEX [IX_Permissions_Key] ON [Permissions] ([Key]);

CREATE UNIQUE INDEX [IX_Positions_CompanyId_Code] ON [Positions] ([CompanyId], [Code]) WHERE [CompanyId] IS NOT NULL;

CREATE INDEX [IX_Positions_DepartmentId] ON [Positions] ([DepartmentId]);

CREATE UNIQUE INDEX [IX_RefreshTokens_TokenHash] ON [RefreshTokens] ([TokenHash]);

CREATE INDEX [IX_RefreshTokens_UserId] ON [RefreshTokens] ([UserId]);

CREATE INDEX [IX_RolePermissions_PermissionId] ON [RolePermissions] ([PermissionId]);

CREATE UNIQUE INDEX [IX_Roles_Name] ON [Roles] ([Name]);

CREATE INDEX [IX_Shifts_CompanyId] ON [Shifts] ([CompanyId]);

CREATE INDEX [IX_UserRoles_RoleId] ON [UserRoles] ([RoleId]);

CREATE INDEX [IX_Users_CompanyId] ON [Users] ([CompanyId]);

CREATE UNIQUE INDEX [IX_Users_EmployeeId] ON [Users] ([EmployeeId]) WHERE [EmployeeId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_Users_Username] ON [Users] ([Username]);

CREATE UNIQUE INDEX [IX_Vehicles_CompanyId_PlateKey] ON [Vehicles] ([CompanyId], [PlateKey]) WHERE [CompanyId] IS NOT NULL;

CREATE INDEX [IX_Vehicles_EmployeeId] ON [Vehicles] ([EmployeeId]);

CREATE INDEX [IX_VisitLogs_VisitorId] ON [VisitLogs] ([VisitorId]);

CREATE UNIQUE INDEX [IX_VisitorCards_CardNo] ON [VisitorCards] ([CardNo]);

CREATE UNIQUE INDEX [IX_VisitorCards_VisitorId] ON [VisitorCards] ([VisitorId]);

CREATE INDEX [IX_Visitors_CompanyId] ON [Visitors] ([CompanyId]);

CREATE INDEX [IX_Visitors_ContractorId] ON [Visitors] ([ContractorId]);

CREATE UNIQUE INDEX [IX_Zones_CompanyId_Code] ON [Zones] ([CompanyId], [Code]) WHERE [CompanyId] IS NOT NULL;

