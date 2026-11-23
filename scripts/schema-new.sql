CREATE TABLE "AppSettings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AppSettings" PRIMARY KEY,
    "CompanyName" TEXT NOT NULL,
    "CompanyLegalName" TEXT NULL,
    "CompanyNationalId" TEXT NULL,
    "CompanyPhone" TEXT NULL,
    "CompanyEmail" TEXT NULL,
    "CompanyWebsite" TEXT NULL,
    "CompanyAddress" TEXT NULL,
    "Description" TEXT NULL,
    "LogoPath" TEXT NULL,
    "AccentColor" TEXT NULL,
    "SidebarColor" TEXT NULL,
    "ActivePaletteId" INTEGER NULL,
    "UpdatedAt" TEXT NOT NULL,
    "UpdatedBy" TEXT NULL
);


CREATE TABLE "AuditLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY,
    "ActorUserId" INTEGER NULL,
    "ActorName" TEXT NULL,
    "Action" TEXT NOT NULL,
    "Entity" TEXT NOT NULL,
    "EntityId" TEXT NULL,
    "Detail" TEXT NULL,
    "Ip" TEXT NULL,
    "At" TEXT NOT NULL
);


CREATE TABLE "ColorPalettes" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_ColorPalettes" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "PrimaryColor" TEXT NOT NULL,
    "LinkColor" TEXT NOT NULL,
    "SidebarColor" TEXT NOT NULL,
    "SidebarTextColor" TEXT NOT NULL,
    "PageBgColor" TEXT NOT NULL,
    "SurfaceColor" TEXT NOT NULL,
    "TableHeaderColor" TEXT NOT NULL,
    "RowAltColor" TEXT NOT NULL,
    "RowHoverColor" TEXT NOT NULL,
    "BorderColor" TEXT NOT NULL,
    "TextColor" TEXT NOT NULL,
    "MutedTextColor" TEXT NOT NULL,
    "SuccessColor" TEXT NOT NULL,
    "WarningColor" TEXT NOT NULL,
    "DangerColor" TEXT NOT NULL,
    "InfoColor" TEXT NOT NULL,
    "IsBuiltIn" INTEGER NOT NULL,
    "SortOrder" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL
);


CREATE TABLE "Companies" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Companies" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);


CREATE TABLE "GatewaySettings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_GatewaySettings" PRIMARY KEY,
    "Enabled" INTEGER NOT NULL,
    "ListenPort" INTEGER NOT NULL,
    "TcpTimeoutMs" INTEGER NOT NULL,
    "PollIntervalSec" INTEGER NOT NULL,
    "SweeperIntervalSec" INTEGER NOT NULL,
    "ConfigRefreshSec" INTEGER NOT NULL,
    "LogLevel" TEXT NOT NULL,
    "DefaultDevicePort" INTEGER NOT NULL,
    "DefaultCommKey" INTEGER NOT NULL,
    "LastFetchedAt" TEXT NULL,
    "ConfigVersion" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);


CREATE TABLE "Holidays" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Holidays" PRIMARY KEY,
    "Date" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "JYear" INTEGER NOT NULL,
    "JMonth" INTEGER NOT NULL,
    "JDay" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL
);


CREATE TABLE "NotificationLogs" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_NotificationLogs" PRIMARY KEY AUTOINCREMENT,
    "RuleName" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Channel" TEXT NOT NULL,
    "Recipient" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Body" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Error" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);


CREATE TABLE "NotificationRules" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationRules" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Enabled" INTEGER NOT NULL,
    "Threshold" INTEGER NOT NULL,
    "RecipientEmail" TEXT NULL,
    "RecipientPhone" TEXT NULL,
    "ViaEmail" INTEGER NOT NULL,
    "ViaSms" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "LastRunAt" TEXT NULL
);


CREATE TABLE "NotificationSettings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_NotificationSettings" PRIMARY KEY,
    "EnabledEmail" INTEGER NOT NULL,
    "EnabledSms" INTEGER NOT NULL,
    "SmtpHost" TEXT NOT NULL,
    "SmtpPort" INTEGER NOT NULL,
    "SmtpSsl" INTEGER NOT NULL,
    "SmtpUser" TEXT NOT NULL,
    "SmtpPass" TEXT NOT NULL,
    "FromEmail" TEXT NOT NULL,
    "AdminEmails" TEXT NOT NULL,
    "SmsApiUrl" TEXT NOT NULL,
    "SmsApiKey" TEXT NOT NULL,
    "SmsSender" TEXT NOT NULL,
    "AlertTtlDays" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);


CREATE TABLE "Permissions" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Permissions" PRIMARY KEY AUTOINCREMENT,
    "Key" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Grp" TEXT NOT NULL
);


CREATE TABLE "Roles" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Roles" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" TEXT NOT NULL
);


CREATE TABLE "SecurityAlerts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_SecurityAlerts" PRIMARY KEY,
    "Type" TEXT NOT NULL,
    "Severity" TEXT NOT NULL,
    "Message" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "RefId" TEXT NULL,
    "Resolved" INTEGER NOT NULL,
    "Expired" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL
);


CREATE TABLE "AccessGroups" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AccessGroups" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "Note" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AccessGroups_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "Contractors" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Contractors" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "ContactName" TEXT NULL,
    "Phone" TEXT NULL,
    "ContractNo" TEXT NULL,
    "ValidUntil" TEXT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Contractors_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "Departments" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Departments" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "CompanyId" TEXT NOT NULL,
    "ParentId" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Departments_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Departments_Departments_ParentId" FOREIGN KEY ("ParentId") REFERENCES "Departments" ("Id")
);


CREATE TABLE "Devices" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Devices" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "Kind" TEXT NOT NULL,
    "Brand" TEXT NOT NULL,
    "Serial" TEXT NOT NULL,
    "Ip" TEXT NULL,
    "Location" TEXT NULL,
    "Purpose" TEXT NOT NULL,
    "Transport" TEXT NOT NULL,
    "DevicePort" INTEGER NULL,
    "CommKey" INTEGER NULL,
    "Mode" TEXT NOT NULL,
    "LastSync" TEXT NULL,
    "Active" INTEGER NOT NULL,
    "WebhookToken" TEXT NOT NULL,
    "PollingInterval" INTEGER NOT NULL,
    "LastSeenAt" TEXT NULL,
    "LastPulledAt" TEXT NULL,
    "Firmware" TEXT NULL,
    "UserCount" INTEGER NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Devices_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "ParkingLots" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ParkingLots" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "Capacity" INTEGER NOT NULL,
    "MonthlyFee" TEXT NOT NULL,
    "Location" TEXT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ParkingLots_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "PayItems" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayItems" PRIMARY KEY,
    "CompanyId" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "Formula" TEXT NOT NULL,
    "Taxable" INTEGER NOT NULL,
    "Insurable" INTEGER NOT NULL,
    "Continuous" INTEGER NOT NULL,
    "ScopeType" TEXT NOT NULL,
    "ScopeId" TEXT NULL,
    "Priority" INTEGER NOT NULL,
    "Order" INTEGER NOT NULL,
    "IsSystem" INTEGER NOT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PayItems_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PayrollParameters" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayrollParameters" PRIMARY KEY,
    "CompanyId" TEXT NOT NULL,
    "JYear" INTEGER NOT NULL,
    "Code" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Value" TEXT NOT NULL,
    "StrValue" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PayrollParameters_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PayrollRuns" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayrollRuns" PRIMARY KEY,
    "CompanyId" TEXT NULL,
    "JYear" INTEGER NOT NULL,
    "JMonth" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "Note" TEXT NULL,
    "TotalGross" TEXT NOT NULL,
    "TotalNet" TEXT NOT NULL,
    "EmployeeCount" INTEGER NOT NULL,
    "CreatedBy" TEXT NULL,
    "FinalizedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PayrollRuns_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "PayrollSettings" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayrollSettings" PRIMARY KEY,
    "CompanyId" TEXT NOT NULL,
    "CalcMethod" TEXT NOT NULL,
    "WorkSource" INTEGER NOT NULL,
    "MonthDays" INTEGER NOT NULL,
    "HourBase" INTEGER NOT NULL,
    "RoundUnit" INTEGER NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PayrollSettings_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Shifts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Shifts" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "Type" TEXT NOT NULL,
    "StartTime" TEXT NOT NULL,
    "EndTime" TEXT NOT NULL,
    "BreakMinutes" INTEGER NOT NULL,
    "CyclePattern" TEXT NULL,
    "ToleranceLate" INTEGER NOT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Shifts_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "Zones" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Zones" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "RiskLevel" TEXT NOT NULL,
    "Description" TEXT NULL,
    "AntiPassback" INTEGER NOT NULL,
    "TwoFactor" INTEGER NOT NULL,
    "TwoFactorWindowSec" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Zones_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id")
);


CREATE TABLE "RolePermissions" (
    "RoleId" INTEGER NOT NULL,
    "PermissionId" INTEGER NOT NULL,
    CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("RoleId", "PermissionId"),
    CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Visitors" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Visitors" PRIMARY KEY,
    "FirstName" TEXT NOT NULL,
    "LastName" TEXT NOT NULL,
    "NationalId" TEXT NOT NULL,
    "Phone" TEXT NULL,
    "IdDocType" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "HostName" TEXT NOT NULL,
    "HostDept" TEXT NULL,
    "Purpose" TEXT NOT NULL,
    "ContractorId" TEXT NULL,
    "Status" TEXT NOT NULL,
    "ApprovedBy" TEXT NULL,
    "RequestedAt" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Visitors_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_Visitors_Contractors_ContractorId" FOREIGN KEY ("ContractorId") REFERENCES "Contractors" ("Id")
);


CREATE TABLE "Positions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Positions" PRIMARY KEY,
    "Title" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "Grade" TEXT NULL,
    "CompanyId" TEXT NULL,
    "DepartmentId" TEXT NOT NULL,
    "Description" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Positions_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_Positions_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeviceBiometrics" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DeviceBiometrics" PRIMARY KEY,
    "DeviceId" TEXT NOT NULL,
    "Pin" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "BioIndex" INTEGER NOT NULL,
    "Valid" INTEGER NOT NULL,
    "Quality" INTEGER NULL,
    "TemplateData" TEXT NULL,
    "Length" INTEGER NOT NULL,
    "FetchedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DeviceBiometrics_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeviceCommands" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DeviceCommands" PRIMARY KEY,
    "DeviceId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Payload" TEXT NULL,
    "Status" TEXT NOT NULL,
    "CreatedByUserId" INTEGER NULL,
    "CreatedBy" TEXT NULL,
    "Result" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "DeliveredAt" TEXT NULL,
    "AckedAt" TEXT NULL,
    CONSTRAINT "FK_DeviceCommands_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DeviceOfflinePunches" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DeviceOfflinePunches" PRIMARY KEY,
    "DeviceId" TEXT NOT NULL,
    "Pin" TEXT NOT NULL,
    "Timestamp" TEXT NOT NULL,
    "Source" TEXT NOT NULL,
    "ImportedAt" TEXT NULL,
    "Note" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DeviceOfflinePunches_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ParkingGates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ParkingGates" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "LotId" TEXT NOT NULL,
    "DeviceId" TEXT NULL,
    "Type" TEXT NOT NULL,
    "Direction" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "OpenState" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ParkingGates_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_ParkingGates_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_ParkingGates_ParkingLots_LotId" FOREIGN KEY ("LotId") REFERENCES "ParkingLots" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Gates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Gates" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "ZoneId" TEXT NOT NULL,
    "DeviceId" TEXT NULL,
    "Type" TEXT NOT NULL,
    "Direction" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "DoorState" TEXT NOT NULL,
    "ScheduleDays" TEXT NULL,
    "ScheduleFrom" TEXT NULL,
    "ScheduleTo" TEXT NULL,
    "MapX" REAL NULL,
    "MapY" REAL NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Gates_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_Gates_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_Gates_Zones_ZoneId" FOREIGN KEY ("ZoneId") REFERENCES "Zones" ("Id") ON DELETE CASCADE
);


CREATE TABLE "VisitLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_VisitLogs" PRIMARY KEY,
    "VisitorId" TEXT NOT NULL,
    "Direction" TEXT NOT NULL,
    "GateName" TEXT NULL,
    "Timestamp" TEXT NOT NULL,
    CONSTRAINT "FK_VisitLogs_Visitors_VisitorId" FOREIGN KEY ("VisitorId") REFERENCES "Visitors" ("Id") ON DELETE CASCADE
);


CREATE TABLE "VisitorCards" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_VisitorCards" PRIMARY KEY,
    "CardNo" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "VisitorId" TEXT NOT NULL,
    "IssuedAt" TEXT NOT NULL,
    "ValidUntil" TEXT NOT NULL,
    "Returned" INTEGER NOT NULL,
    CONSTRAINT "FK_VisitorCards_Visitors_VisitorId" FOREIGN KEY ("VisitorId") REFERENCES "Visitors" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Employees" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Employees" PRIMARY KEY,
    "PersonnelCode" TEXT NOT NULL,
    "FirstName" TEXT NOT NULL,
    "LastName" TEXT NOT NULL,
    "NationalId" TEXT NOT NULL,
    "BirthDate" TEXT NULL,
    "Phone" TEXT NULL,
    "Email" TEXT NULL,
    "EmploymentType" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "HireDate" TEXT NOT NULL,
    "EndDate" TEXT NULL,
    "CompanyId" TEXT NOT NULL,
    "DepartmentId" TEXT NULL,
    "PositionId" TEXT NULL,
    "BaseSalary" TEXT NOT NULL,
    "PhotoPath" TEXT NULL,
    "MaritalStatus" TEXT NOT NULL,
    "ChildCount" INTEGER NOT NULL,
    "ShiftId" TEXT NULL,
    "AnnualLeaveEnt" TEXT NOT NULL,
    "AnnualLeaveUsed" TEXT NOT NULL,
    "SickLeaveEnt" TEXT NOT NULL,
    "SickLeaveUsed" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Employees_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Employees_Departments_DepartmentId" FOREIGN KEY ("DepartmentId") REFERENCES "Departments" ("Id"),
    CONSTRAINT "FK_Employees_Positions_PositionId" FOREIGN KEY ("PositionId") REFERENCES "Positions" ("Id"),
    CONSTRAINT "FK_Employees_Shifts_ShiftId" FOREIGN KEY ("ShiftId") REFERENCES "Shifts" ("Id")
);


CREATE TABLE "AnprCameras" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AnprCameras" PRIMARY KEY,
    "Name" TEXT NOT NULL,
    "CompanyId" TEXT NULL,
    "GateId" TEXT NOT NULL,
    "Brand" TEXT NOT NULL,
    "Model" TEXT NULL,
    "Serial" TEXT NOT NULL,
    "Ip" TEXT NULL,
    "Location" TEXT NULL,
    "Direction" TEXT NOT NULL,
    "ConfidenceThreshold" INTEGER NOT NULL,
    "StreamUrl" TEXT NULL,
    "Mode" TEXT NOT NULL,
    "HeartbeatInterval" INTEGER NOT NULL,
    "LastSeenAt" TEXT NULL,
    "WebhookToken" TEXT NOT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AnprCameras_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_AnprCameras_ParkingGates_GateId" FOREIGN KEY ("GateId") REFERENCES "ParkingGates" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AccessGroupGates" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AccessGroupGates" PRIMARY KEY,
    "GroupId" TEXT NOT NULL,
    "GateId" TEXT NOT NULL,
    "Days" TEXT NULL,
    "TimeFrom" TEXT NULL,
    "TimeUntil" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AccessGroupGates_AccessGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "AccessGroups" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AccessGroupGates_Gates_GateId" FOREIGN KEY ("GateId") REFERENCES "Gates" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AccessEvents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AccessEvents" PRIMARY KEY,
    "EmployeeId" TEXT NULL,
    "VisitorId" TEXT NULL,
    "GateId" TEXT NOT NULL,
    "Direction" TEXT NOT NULL,
    "Method" TEXT NOT NULL,
    "Result" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "Timestamp" TEXT NOT NULL,
    CONSTRAINT "FK_AccessEvents_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id"),
    CONSTRAINT "FK_AccessEvents_Gates_GateId" FOREIGN KEY ("GateId") REFERENCES "Gates" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AccessEvents_Visitors_VisitorId" FOREIGN KEY ("VisitorId") REFERENCES "Visitors" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AccessGroupMembers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AccessGroupMembers" PRIMARY KEY,
    "GroupId" TEXT NOT NULL,
    "EmployeeId" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AccessGroupMembers_AccessGroups_GroupId" FOREIGN KEY ("GroupId") REFERENCES "AccessGroups" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AccessGroupMembers_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AccessPermissions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AccessPermissions" PRIMARY KEY,
    "EmployeeId" TEXT NULL,
    "VisitorId" TEXT NULL,
    "ZoneId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "ValidFrom" TEXT NOT NULL,
    "ValidUntil" TEXT NULL,
    "Days" TEXT NULL,
    "TimeFrom" TEXT NULL,
    "TimeUntil" TEXT NULL,
    "Status" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AccessPermissions_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id"),
    CONSTRAINT "FK_AccessPermissions_Visitors_VisitorId" FOREIGN KEY ("VisitorId") REFERENCES "Visitors" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_AccessPermissions_Zones_ZoneId" FOREIGN KEY ("ZoneId") REFERENCES "Zones" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AttendanceRecords" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AttendanceRecords" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "DeviceId" TEXT NULL,
    "Date" TEXT NOT NULL,
    "CheckIn" TEXT NULL,
    "CheckOut" TEXT NULL,
    "WorkMinutes" INTEGER NOT NULL,
    "LateMinutes" INTEGER NOT NULL,
    "EarlyMinutes" INTEGER NOT NULL,
    "OvertimeMinutes" INTEGER NOT NULL,
    "Status" TEXT NOT NULL,
    "Note" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_AttendanceRecords_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_AttendanceRecords_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Cards" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Cards" PRIMARY KEY,
    "CardNo" TEXT NOT NULL,
    "EmployeeId" TEXT NULL,
    "Type" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "IssuedAt" TEXT NOT NULL,
    "ExpiresAt" TEXT NULL,
    "ReturnedAt" TEXT NULL,
    "Note" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Cards_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE SET NULL
);


CREATE TABLE "Contracts" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Contracts" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "ContractNo" TEXT NOT NULL,
    "StartDate" TEXT NOT NULL,
    "EndDate" TEXT NULL,
    "Salary" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Contracts_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "CorrectionRequests" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_CorrectionRequests" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "RequestedIn" TEXT NULL,
    "RequestedOut" TEXT NULL,
    "Reason" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "ReviewedBy" TEXT NULL,
    "ReviewNote" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_CorrectionRequests_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Decrees" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Decrees" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "DecreeNo" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "Content" TEXT NULL,
    "Acknowledged" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Decrees_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "DevicePunchLogs" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DevicePunchLogs" PRIMARY KEY,
    "CompanyId" TEXT NULL,
    "DeviceId" TEXT NULL,
    "Pin" TEXT NOT NULL,
    "EmployeeId" TEXT NULL,
    "Timestamp" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "Note" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DevicePunchLogs_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_DevicePunchLogs_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE SET NULL
);


CREATE TABLE "DeviceUsers" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_DeviceUsers" PRIMARY KEY,
    "DeviceId" TEXT NOT NULL,
    "Uid" INTEGER NOT NULL,
    "Pin" TEXT NOT NULL,
    "Name" TEXT NOT NULL,
    "Privilege" INTEGER NOT NULL,
    "CardNumber" TEXT NULL,
    "Password" TEXT NULL,
    "GroupId" TEXT NULL,
    "EmployeeId" TEXT NULL,
    "LastFetchedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_DeviceUsers_Devices_DeviceId" FOREIGN KEY ("DeviceId") REFERENCES "Devices" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_DeviceUsers_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE SET NULL
);


CREATE TABLE "EmployeeBiometrics" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_EmployeeBiometrics" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "BioIndex" INTEGER NOT NULL,
    "Valid" INTEGER NOT NULL,
    "Quality" INTEGER NULL,
    "TemplateData" TEXT NULL,
    "Length" INTEGER NOT NULL,
    "SourceDeviceId" TEXT NULL,
    "CardNumber" TEXT NULL,
    "EnrolledAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_EmployeeBiometrics_Devices_SourceDeviceId" FOREIGN KEY ("SourceDeviceId") REFERENCES "Devices" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_EmployeeBiometrics_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "LeaveRequests" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_LeaveRequests" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "FromDate" TEXT NOT NULL,
    "ToDate" TEXT NOT NULL,
    "Days" TEXT NOT NULL,
    "Hours" INTEGER NULL,
    "Reason" TEXT NULL,
    "Status" TEXT NOT NULL,
    "Approver" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_LeaveRequests_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Missions" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Missions" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Destination" TEXT NOT NULL,
    "Purpose" TEXT NULL,
    "FromDate" TEXT NOT NULL,
    "ToDate" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "Approver" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Missions_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "OvertimePermits" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_OvertimePermits" PRIMARY KEY,
    "EmployeeId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Date" TEXT NOT NULL,
    "FromTime" TEXT NOT NULL,
    "ToTime" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "Status" TEXT NOT NULL,
    "Approver" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_OvertimePermits_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PayrollBonusRows" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayrollBonusRows" PRIMARY KEY,
    "CompanyId" TEXT NOT NULL,
    "EmployeeId" TEXT NOT NULL,
    "JYear" INTEGER NOT NULL,
    "Days" TEXT NOT NULL,
    "Base" TEXT NOT NULL,
    "Eid" TEXT NOT NULL,
    "Seniority" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "FinalizedBy" TEXT NULL,
    "FinalizedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PayrollBonusRows_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_PayrollBonusRows_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PayrollSettlements" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayrollSettlements" PRIMARY KEY,
    "CompanyId" TEXT NOT NULL,
    "EmployeeId" TEXT NOT NULL,
    "EndDate" TEXT NOT NULL,
    "Days" TEXT NOT NULL,
    "LeaveBalance" TEXT NOT NULL,
    "LeavePay" TEXT NOT NULL,
    "Daily" TEXT NOT NULL,
    "Eid" TEXT NOT NULL,
    "Seniority" TEXT NOT NULL,
    "Status" TEXT NOT NULL,
    "CreatedBy" TEXT NULL,
    "FinalizedBy" TEXT NULL,
    "FinalizedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PayrollSettlements_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_PayrollSettlements_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Payslips" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Payslips" PRIMARY KEY,
    "RunId" TEXT NOT NULL,
    "EmployeeId" TEXT NOT NULL,
    "BaseSalary" TEXT NOT NULL,
    "OvertimePay" TEXT NOT NULL,
    "Allowances" TEXT NOT NULL,
    "AbsentDeduct" TEXT NOT NULL,
    "Insurance" TEXT NOT NULL,
    "Tax" TEXT NOT NULL,
    "NetPay" TEXT NOT NULL,
    "WorkedMin" INTEGER NOT NULL,
    "OvertimeMin" INTEGER NOT NULL,
    "AbsentDays" INTEGER NOT NULL,
    "Detail" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Payslips_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Payslips_PayrollRuns_RunId" FOREIGN KEY ("RunId") REFERENCES "PayrollRuns" ("Id") ON DELETE CASCADE
);


CREATE TABLE "Users" (
    "Id" INTEGER NOT NULL CONSTRAINT "PK_Users" PRIMARY KEY AUTOINCREMENT,
    "Username" TEXT NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "FullName" TEXT NOT NULL,
    "PhotoPath" TEXT NULL,
    "EmployeeId" TEXT NULL,
    "CompanyId" TEXT NULL,
    "IsActive" INTEGER NOT NULL,
    "IsSuperadmin" INTEGER NOT NULL,
    "FailedLoginAttempts" INTEGER NOT NULL,
    "LockedUntil" TEXT NULL,
    "PasswordChangedAt" TEXT NOT NULL,
    "PasswordExpiresAt" TEXT NULL,
    "MustChangePassword" INTEGER NOT NULL,
    "LastLoginAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Users_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_Users_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE SET NULL
);


CREATE TABLE "Vehicles" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_Vehicles" PRIMARY KEY,
    "CompanyId" TEXT NULL,
    "EmployeeId" TEXT NOT NULL,
    "PlateNo" TEXT NOT NULL,
    "PlateKey" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Brand" TEXT NULL,
    "Model" TEXT NULL,
    "Color" TEXT NULL,
    "Active" INTEGER NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_Vehicles_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id"),
    CONSTRAINT "FK_Vehicles_Employees_EmployeeId" FOREIGN KEY ("EmployeeId") REFERENCES "Employees" ("Id") ON DELETE CASCADE
);


CREATE TABLE "AnprCommands" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_AnprCommands" PRIMARY KEY,
    "CameraId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "Payload" TEXT NULL,
    "Status" TEXT NOT NULL,
    "CreatedByUserId" INTEGER NULL,
    "CreatedBy" TEXT NULL,
    "Result" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    "DeliveredAt" TEXT NULL,
    "AckedAt" TEXT NULL,
    CONSTRAINT "FK_AnprCommands_AnprCameras_CameraId" FOREIGN KEY ("CameraId") REFERENCES "AnprCameras" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PayslipLines" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PayslipLines" PRIMARY KEY,
    "PayslipId" TEXT NOT NULL,
    "Code" TEXT NOT NULL,
    "Title" TEXT NOT NULL,
    "Kind" TEXT NOT NULL,
    "Amount" TEXT NOT NULL,
    "Taxable" INTEGER NOT NULL,
    "Insurable" INTEGER NOT NULL,
    "Order" INTEGER NOT NULL,
    CONSTRAINT "FK_PayslipLines_Payslips_PayslipId" FOREIGN KEY ("PayslipId") REFERENCES "Payslips" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PasswordHistories" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PasswordHistories" PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "PasswordHash" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PasswordHistories_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "PasswordResets" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_PasswordResets" PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "TokenHash" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "UsedAt" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_PasswordResets_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "RefreshTokens" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_RefreshTokens" PRIMARY KEY,
    "UserId" INTEGER NOT NULL,
    "TokenHash" TEXT NOT NULL,
    "ExpiresAt" TEXT NOT NULL,
    "RevokedAt" TEXT NULL,
    "DeviceInfo" TEXT NULL,
    "Ip" TEXT NULL,
    "CreatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "UserRoles" (
    "UserId" INTEGER NOT NULL,
    "RoleId" INTEGER NOT NULL,
    CONSTRAINT "PK_UserRoles" PRIMARY KEY ("UserId", "RoleId"),
    CONSTRAINT "FK_UserRoles_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_UserRoles_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);


CREATE TABLE "ParkingEvents" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ParkingEvents" PRIMARY KEY,
    "GateId" TEXT NOT NULL,
    "CameraId" TEXT NULL,
    "VehicleId" TEXT NULL,
    "PlateText" TEXT NOT NULL,
    "PlateKey" TEXT NULL,
    "Direction" TEXT NOT NULL,
    "Method" TEXT NOT NULL,
    "Result" TEXT NOT NULL,
    "Reason" TEXT NULL,
    "Confidence" INTEGER NULL,
    "Timestamp" TEXT NOT NULL,
    CONSTRAINT "FK_ParkingEvents_AnprCameras_CameraId" FOREIGN KEY ("CameraId") REFERENCES "AnprCameras" ("Id") ON DELETE SET NULL,
    CONSTRAINT "FK_ParkingEvents_ParkingGates_GateId" FOREIGN KEY ("GateId") REFERENCES "ParkingGates" ("Id"),
    CONSTRAINT "FK_ParkingEvents_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("Id") ON DELETE SET NULL
);


CREATE TABLE "ParkingPermits" (
    "Id" TEXT NOT NULL CONSTRAINT "PK_ParkingPermits" PRIMARY KEY,
    "VehicleId" TEXT NOT NULL,
    "LotId" TEXT NOT NULL,
    "Type" TEXT NOT NULL,
    "ValidFrom" TEXT NOT NULL,
    "ValidUntil" TEXT NULL,
    "SpotNo" TEXT NULL,
    "Status" TEXT NOT NULL,
    "CreatedAt" TEXT NOT NULL,
    "UpdatedAt" TEXT NOT NULL,
    CONSTRAINT "FK_ParkingPermits_ParkingLots_LotId" FOREIGN KEY ("LotId") REFERENCES "ParkingLots" ("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_ParkingPermits_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("Id") ON DELETE CASCADE
);


CREATE INDEX "IX_AccessEvents_EmployeeId" ON "AccessEvents" ("EmployeeId");


CREATE INDEX "IX_AccessEvents_GateId" ON "AccessEvents" ("GateId");


CREATE INDEX "IX_AccessEvents_VisitorId" ON "AccessEvents" ("VisitorId");


CREATE INDEX "IX_AccessGroupGates_GateId" ON "AccessGroupGates" ("GateId");


CREATE INDEX "IX_AccessGroupGates_GroupId" ON "AccessGroupGates" ("GroupId");


CREATE INDEX "IX_AccessGroupMembers_EmployeeId" ON "AccessGroupMembers" ("EmployeeId");


CREATE INDEX "IX_AccessGroupMembers_GroupId" ON "AccessGroupMembers" ("GroupId");


CREATE INDEX "IX_AccessGroups_CompanyId" ON "AccessGroups" ("CompanyId");


CREATE INDEX "IX_AccessPermissions_EmployeeId" ON "AccessPermissions" ("EmployeeId");


CREATE INDEX "IX_AccessPermissions_VisitorId" ON "AccessPermissions" ("VisitorId");


CREATE INDEX "IX_AccessPermissions_ZoneId" ON "AccessPermissions" ("ZoneId");


CREATE INDEX "IX_AnprCameras_CompanyId" ON "AnprCameras" ("CompanyId");


CREATE INDEX "IX_AnprCameras_GateId" ON "AnprCameras" ("GateId");


CREATE UNIQUE INDEX "IX_AnprCameras_Serial" ON "AnprCameras" ("Serial");


CREATE UNIQUE INDEX "IX_AnprCameras_WebhookToken" ON "AnprCameras" ("WebhookToken");


CREATE INDEX "IX_AnprCommands_CameraId_Status" ON "AnprCommands" ("CameraId", "Status");


CREATE INDEX "IX_AttendanceRecords_DeviceId" ON "AttendanceRecords" ("DeviceId");


CREATE UNIQUE INDEX "IX_AttendanceRecords_EmployeeId_Date" ON "AttendanceRecords" ("EmployeeId", "Date");


CREATE UNIQUE INDEX "IX_Cards_CardNo" ON "Cards" ("CardNo");


CREATE INDEX "IX_Cards_EmployeeId" ON "Cards" ("EmployeeId");


CREATE UNIQUE INDEX "IX_ColorPalettes_Name" ON "ColorPalettes" ("Name");


CREATE UNIQUE INDEX "IX_Companies_Code" ON "Companies" ("Code");


CREATE INDEX "IX_Contractors_CompanyId" ON "Contractors" ("CompanyId");


CREATE UNIQUE INDEX "IX_Contracts_ContractNo" ON "Contracts" ("ContractNo");


CREATE INDEX "IX_Contracts_EmployeeId" ON "Contracts" ("EmployeeId");


CREATE INDEX "IX_CorrectionRequests_EmployeeId" ON "CorrectionRequests" ("EmployeeId");


CREATE INDEX "IX_Decrees_EmployeeId" ON "Decrees" ("EmployeeId");


CREATE UNIQUE INDEX "IX_Departments_CompanyId_Code" ON "Departments" ("CompanyId", "Code");


CREATE INDEX "IX_Departments_ParentId" ON "Departments" ("ParentId");


CREATE UNIQUE INDEX "IX_DeviceBiometrics_DeviceId_Pin_Type_BioIndex" ON "DeviceBiometrics" ("DeviceId", "Pin", "Type", "BioIndex");


CREATE INDEX "IX_DeviceCommands_DeviceId" ON "DeviceCommands" ("DeviceId");


CREATE INDEX "IX_DeviceOfflinePunches_DeviceId_ImportedAt" ON "DeviceOfflinePunches" ("DeviceId", "ImportedAt");


CREATE INDEX "IX_DevicePunchLogs_DeviceId_Timestamp" ON "DevicePunchLogs" ("DeviceId", "Timestamp");


CREATE INDEX "IX_DevicePunchLogs_EmployeeId" ON "DevicePunchLogs" ("EmployeeId");


CREATE INDEX "IX_DevicePunchLogs_Timestamp" ON "DevicePunchLogs" ("Timestamp");


CREATE INDEX "IX_Devices_CompanyId" ON "Devices" ("CompanyId");


CREATE UNIQUE INDEX "IX_Devices_Serial" ON "Devices" ("Serial");


CREATE UNIQUE INDEX "IX_Devices_WebhookToken" ON "Devices" ("WebhookToken");


CREATE UNIQUE INDEX "IX_DeviceUsers_DeviceId_Pin" ON "DeviceUsers" ("DeviceId", "Pin");


CREATE INDEX "IX_DeviceUsers_EmployeeId" ON "DeviceUsers" ("EmployeeId");


CREATE INDEX "IX_DeviceUsers_Pin" ON "DeviceUsers" ("Pin");


CREATE UNIQUE INDEX "IX_EmployeeBiometrics_EmployeeId_Type_BioIndex" ON "EmployeeBiometrics" ("EmployeeId", "Type", "BioIndex");


CREATE INDEX "IX_EmployeeBiometrics_SourceDeviceId" ON "EmployeeBiometrics" ("SourceDeviceId");


CREATE INDEX "IX_Employees_CompanyId" ON "Employees" ("CompanyId");


CREATE INDEX "IX_Employees_DepartmentId" ON "Employees" ("DepartmentId");


CREATE UNIQUE INDEX "IX_Employees_NationalId" ON "Employees" ("NationalId");


CREATE UNIQUE INDEX "IX_Employees_PersonnelCode" ON "Employees" ("PersonnelCode");


CREATE INDEX "IX_Employees_PositionId" ON "Employees" ("PositionId");


CREATE INDEX "IX_Employees_ShiftId" ON "Employees" ("ShiftId");


CREATE UNIQUE INDEX "IX_Gates_CompanyId_Code" ON "Gates" ("CompanyId", "Code");


CREATE INDEX "IX_Gates_DeviceId" ON "Gates" ("DeviceId");


CREATE INDEX "IX_Gates_ZoneId" ON "Gates" ("ZoneId");


CREATE UNIQUE INDEX "IX_Holidays_Date" ON "Holidays" ("Date");


CREATE INDEX "IX_LeaveRequests_EmployeeId" ON "LeaveRequests" ("EmployeeId");


CREATE INDEX "IX_Missions_EmployeeId" ON "Missions" ("EmployeeId");


CREATE INDEX "IX_OvertimePermits_EmployeeId" ON "OvertimePermits" ("EmployeeId");


CREATE INDEX "IX_ParkingEvents_CameraId" ON "ParkingEvents" ("CameraId");


CREATE INDEX "IX_ParkingEvents_GateId" ON "ParkingEvents" ("GateId");


CREATE INDEX "IX_ParkingEvents_PlateKey" ON "ParkingEvents" ("PlateKey");


CREATE INDEX "IX_ParkingEvents_Timestamp" ON "ParkingEvents" ("Timestamp");


CREATE INDEX "IX_ParkingEvents_VehicleId" ON "ParkingEvents" ("VehicleId");


CREATE UNIQUE INDEX "IX_ParkingGates_CompanyId_Code" ON "ParkingGates" ("CompanyId", "Code");


CREATE INDEX "IX_ParkingGates_DeviceId" ON "ParkingGates" ("DeviceId");


CREATE INDEX "IX_ParkingGates_LotId" ON "ParkingGates" ("LotId");


CREATE UNIQUE INDEX "IX_ParkingLots_CompanyId_Code" ON "ParkingLots" ("CompanyId", "Code");


CREATE INDEX "IX_ParkingPermits_LotId" ON "ParkingPermits" ("LotId");


CREATE INDEX "IX_ParkingPermits_VehicleId" ON "ParkingPermits" ("VehicleId");


CREATE INDEX "IX_PasswordHistories_UserId" ON "PasswordHistories" ("UserId");


CREATE UNIQUE INDEX "IX_PasswordResets_TokenHash" ON "PasswordResets" ("TokenHash");


CREATE INDEX "IX_PasswordResets_UserId" ON "PasswordResets" ("UserId");


CREATE UNIQUE INDEX "IX_PayItems_CompanyId_Code" ON "PayItems" ("CompanyId", "Code");


CREATE UNIQUE INDEX "IX_PayrollBonusRows_CompanyId_JYear_EmployeeId" ON "PayrollBonusRows" ("CompanyId", "JYear", "EmployeeId");


CREATE INDEX "IX_PayrollBonusRows_EmployeeId" ON "PayrollBonusRows" ("EmployeeId");


CREATE UNIQUE INDEX "IX_PayrollParameters_CompanyId_JYear_Code" ON "PayrollParameters" ("CompanyId", "JYear", "Code");


CREATE UNIQUE INDEX "IX_PayrollRuns_CompanyId_JYear_JMonth" ON "PayrollRuns" ("CompanyId", "JYear", "JMonth");


CREATE UNIQUE INDEX "IX_PayrollSettings_CompanyId" ON "PayrollSettings" ("CompanyId");


CREATE INDEX "IX_PayrollSettlements_CompanyId" ON "PayrollSettlements" ("CompanyId");


CREATE INDEX "IX_PayrollSettlements_EmployeeId" ON "PayrollSettlements" ("EmployeeId");


CREATE INDEX "IX_PayslipLines_PayslipId" ON "PayslipLines" ("PayslipId");


CREATE INDEX "IX_Payslips_EmployeeId" ON "Payslips" ("EmployeeId");


CREATE UNIQUE INDEX "IX_Payslips_RunId_EmployeeId" ON "Payslips" ("RunId", "EmployeeId");


CREATE UNIQUE INDEX "IX_Permissions_Key" ON "Permissions" ("Key");


CREATE UNIQUE INDEX "IX_Positions_CompanyId_Code" ON "Positions" ("CompanyId", "Code");


CREATE INDEX "IX_Positions_DepartmentId" ON "Positions" ("DepartmentId");


CREATE UNIQUE INDEX "IX_RefreshTokens_TokenHash" ON "RefreshTokens" ("TokenHash");


CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");


CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");


CREATE UNIQUE INDEX "IX_Roles_Name" ON "Roles" ("Name");


CREATE INDEX "IX_Shifts_CompanyId" ON "Shifts" ("CompanyId");


CREATE INDEX "IX_UserRoles_RoleId" ON "UserRoles" ("RoleId");


CREATE INDEX "IX_Users_CompanyId" ON "Users" ("CompanyId");


CREATE UNIQUE INDEX "IX_Users_EmployeeId" ON "Users" ("EmployeeId");


CREATE UNIQUE INDEX "IX_Users_Username" ON "Users" ("Username");


CREATE UNIQUE INDEX "IX_Vehicles_CompanyId_PlateKey" ON "Vehicles" ("CompanyId", "PlateKey");


CREATE INDEX "IX_Vehicles_EmployeeId" ON "Vehicles" ("EmployeeId");


CREATE INDEX "IX_VisitLogs_VisitorId" ON "VisitLogs" ("VisitorId");


CREATE UNIQUE INDEX "IX_VisitorCards_CardNo" ON "VisitorCards" ("CardNo");


CREATE UNIQUE INDEX "IX_VisitorCards_VisitorId" ON "VisitorCards" ("VisitorId");


CREATE INDEX "IX_Visitors_CompanyId" ON "Visitors" ("CompanyId");


CREATE INDEX "IX_Visitors_ContractorId" ON "Visitors" ("ContractorId");


CREATE UNIQUE INDEX "IX_Zones_CompanyId_Code" ON "Zones" ("CompanyId", "Code");


