// Shared API payload types used by client components

export interface EmployeeList {
  id: string;
  personnelCode: string;
  firstName: string;
  lastName: string;
  nationalId: string;
  phone?: string | null;
  email?: string | null;
  employmentType: string;
  status: string;
  hireDate: string;
  baseSalary: number;
  annualLeaveEnt: number;
  annualLeaveUsed: number;
  shiftId?: string | null;
  department?: { name: string } | null;
  position?: { title: string } | null;
  shift?: { name: string } | null;
  company?: { name: string } | null;
}

export interface DashboardData {
  kpis: {
    totalEmployees: number;
    activeEmployees: number;
    presentToday: number;
    absentToday: number;
    lateToday: number;
    onLeaveToday: number;
    visitorsInside: number;
    pendingVisitorApprovals: number;
    pendingRequests: number;
    unresolvedAlerts: number;
    devicesOnline: number;
    devicesTotal: number;
    gatesOk: number;
    gatesTotal: number;
  };
  employmentTypes: { name: string; value: number }[];
  departmentStats: { name: string; value: number }[];
  attendanceTrend: { day: string; present: number; absent: number; leave: number; late: number }[];
  recentEvents: AccessEventT[];
  alerts: AlertT[];
  insideVisitors: VisitorT[];
  devices: DeviceT[];
  contractExpiring: ContractT[];
}

export interface ShiftT {
  id: string;
  name: string;
  type: string;
  startTime: string;
  endTime: string;
  breakMinutes: number;
  cyclePattern?: string | null;
  toleranceLate: number;
  active: boolean;
  employees: { id: string; firstName: string; lastName: string }[];
}

export interface DeviceT {
  id: string;
  name: string;
  brand: string;
  serial: string;
  ip?: string | null;
  location?: string | null;
  purpose: string;
  mode: string;
  lastSync?: string | null;
  active: boolean;
  gates?: GateT[];
}

export interface AttendanceT {
  id: string;
  employeeId: string;
  date: string;
  checkIn?: string | null;
  checkOut?: string | null;
  workMinutes: number;
  lateMinutes: number;
  earlyMinutes: number;
  overtimeMinutes: number;
  status: string;
  note?: string | null;
  employee?: {
    firstName: string;
    lastName: string;
    personnelCode: string;
    department?: { name: string } | null;
    shift?: { name: string; startTime: string; endTime: string } | null;
  } | null;
  device?: { name: string; brand: string } | null;
}

export interface LeaveT {
  id: string;
  type: string;
  fromDate: string;
  toDate: string;
  days: number;
  hours?: number | null;
  reason?: string | null;
  status: string;
  approver?: string | null;
  employee?: { firstName: string; lastName: string; personnelCode: string; department?: { name: string } | null } | null;
}

export interface MissionT {
  id: string;
  type: string;
  destination: string;
  purpose?: string | null;
  fromDate: string;
  toDate: string;
  status: string;
  approver?: string | null;
  employee?: { firstName: string; lastName: string; personnelCode: string; department?: { name: string } | null } | null;
}

export interface PermitT {
  id: string;
  type: string;
  date: string;
  fromTime: string;
  toTime: string;
  reason?: string | null;
  status: string;
  approver?: string | null;
  employee?: { firstName: string; lastName: string; personnelCode: string; department?: { name: string } | null } | null;
}

export interface ZoneT {
  id: string;
  name: string;
  code: string;
  riskLevel: string;
  description?: string | null;
  gates: GateT[];
  accessPerms?: { id: string }[];
}

export interface GateT {
  id: string;
  name: string;
  code: string;
  zoneId: string;
  type: string;
  direction: string;
  status: string;
  device?: { name: string; brand: string; mode: string } | null;
  zone?: { name: string; riskLevel?: string } | null;
}

export interface AccessPermT {
  id: string;
  type: string;
  validFrom: string;
  validUntil?: string | null;
  days?: string | null;
  timeFrom?: string | null;
  timeUntil?: string | null;
  status: string;
  employee?: { firstName: string; lastName: string; personnelCode: string } | null;
  zone?: { name: string; riskLevel: string } | null;
}

export interface AccessEventT {
  id: string;
  direction: string;
  method: string;
  result: string;
  reason?: string | null;
  timestamp: string;
  employee?: { firstName: string; lastName: string; personnelCode: string } | null;
  visitor?: { firstName: string; lastName: string } | null;
  gate?: { name: string; zone?: { name: string } | null } | null;
}

export interface VisitorT {
  id: string;
  firstName: string;
  lastName: string;
  nationalId: string;
  phone?: string | null;
  idDocType: string;
  type: string;
  hostName: string;
  hostDept?: string | null;
  purpose: string;
  status: string;
  approvedBy?: string | null;
  requestedAt: string;
  contractor?: { name: string; code: string } | null;
  card?: {
    cardNo: string;
    type: string;
    validUntil: string;
    returned: boolean;
  } | null;
  logs?: { direction: string; timestamp: string; gateName?: string | null }[];
}

export interface ContractorT {
  id: string;
  name: string;
  code: string;
  contactName?: string | null;
  phone?: string | null;
  contractNo?: string | null;
  validUntil?: string | null;
  active: boolean;
  visitors?: { id: string }[];
}

export interface AlertT {
  id: string;
  type: string;
  severity: string;
  message: string;
  source: string;
  resolved: boolean;
  createdAt: string;
}

export interface ContractT {
  id: string;
  type: string;
  contractNo: string;
  startDate: string;
  endDate?: string | null;
  salary: number;
  status: string;
  employee?: { firstName: string; lastName: string; personnelCode: string } | null;
}

export interface DecreeT {
  id: string;
  type: string;
  decreeNo: string;
  date: string;
  content?: string | null;
  acknowledged: boolean;
  employee?: { firstName: string; lastName: string; personnelCode: string } | null;
}

export interface DeptT {
  id: string;
  name: string;
  code: string;
  parentId?: string | null;
  parent?: { name: string } | null;
  positions?: { id: string }[];
  employees?: { id: string }[];
}

export interface PositionT {
  id: string;
  title: string;
  code: string;
  grade?: string | null;
  department?: { name: string } | null;
  employees?: { id: string; firstName: string; lastName: string }[];
}
