export type UserRole = 'Admin' | 'Operator' | 'Viewer';
export type IndicatorColor = 1 | 2 | 3; // 1=Green 2=Yellow 3=Red
export type FurnaceStatus = 1 | 2 | 3;
export type BatchStatus = 1 | 2 | 3; // 1=Running 2=Closed 3=Cleared

export interface ApiResponse<T> {
  success: boolean;
  data: T;
  message?: string;
  errors?: Record<string, string[]>;
}

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface AuthUser {
  id: number;
  userName?: string;
  username?: string;
  displayName: string;
  role: UserRole;
  roleName?: string;
  email?: string;
  isActive?: boolean;
}

export interface AuthResponse {
  token: string;
  refreshToken: string;
  expiresAtUtc: string;
  user: AuthUser;
  userId?: number;
  username?: string;
  displayName?: string;
  role?: UserRole;
  roleName?: string;
}

export interface CurrentUser {
  id: number;
  username: string;
  displayName: string;
  role: UserRole;
  roleName: string;
  email?: string;
}

export interface FurnaceDto {
  id: number;
  number: string;
  name: string;
  status: FurnaceStatus;
  isActive: boolean;
  location?: string;
  sortOrder: number;
  activeBatchId?: number | null;
  activeBatchNumber?: string | null;
  operationalStatus?: string;
  operationalRemarks?: string;
  operationalStatusUpdatedAtUtc?: string;
}

export interface FurnaceStatusChangedDto {
  furnaceId: number;
  furnaceNumber: string;
  operationalStatus: string;
  operationalRemarks?: string;
  updatedAtUtc: string;
}

export const COLOR_CODE_OPTIONS = [
  'Yellow-blue',
  'Green',
  'Blue',
  'Yellow-red',
  'Yellow-black',
  'Brown',
  'Pink',
  'Green-black',
  'Red-green',
] as const;

export type ColorCodeOption = typeof COLOR_CODE_OPTIONS[number];

export const UNIT_OPTIONS = [
  'g',
  'kg',
] as const;

export type UnitOption = typeof UNIT_OPTIONS[number];

export interface ActiveBatch extends BatchDto {}

export interface BatchDto {
  id: number;
  batchNumber: string;
  furnaceId: number;
  furnaceNumber: string;
  furnaceName: string;
  operatorId: number;
  operatorName: string;
  status: BatchStatus;
  isActive: boolean;
  startedAtUtc: string;
  endedAtUtc?: string;
  indicator: IndicatorColor;
  instruction?: string;
  remarks?: string;
  closedBy?: string;
  closedTimeUtc?: string;
  colorCode?: string;
  si: number;
  ti: number;
  mg: number;
  sr?: number;
  futureChemical1?: number;
  futureChemical2?: number;
  futureChemical3?: number;
}

export interface FurnaceUpdateDto {
  furnaceNumber: string;
  batchNumber: string;
  si: number;
  ti: number;
  mg: number;
  sr?: number;
  futureChemical1?: number;
  futureChemical2?: number;
  futureChemical3?: number;
  colorCode?: string;
  indicator: IndicatorColor;
  instruction?: string;
  remarks?: string;
  updatedAtUtc: string;
}

export interface FurnaceClearedDto {
  furnaceNumber: string;
  clearedAtUtc: string;
  message: string;
  operationalStatus?: string;
}

export interface DashboardDto {
  totalFurnaces: number;
  activeBatches: number;
  connectedDisplays: number;
  onlineOperators: number;
  greenCount: number;
  yellowCount: number;
  redCount: number;
  inactiveCount: number;
  [key: string]: number;
}

export interface HistoryItem {
  id: number;
  batchNumber: string;
  furnaceNumber: string;
  furnaceName: string;
  operatorName: string;
  startedAtUtc: string;
  endedAtUtc?: string;
  status: BatchStatus;
  closedBy?: string;
  closedTimeUtc?: string;
  duration?: string;
  indicator: IndicatorColor;
  si: number;
  ti: number;
  mg: number;
  sr: number;
  instruction?: string;
  remarks?: string;
}

export interface UserManagementDto {
  id: number;
  userName: string;
  displayName: string;
  email?: string;
  role: UserRole;
  roleName: string;
  isActive: boolean;
  isSystem: boolean;
  lastLoginAtUtc?: string;
  createdAtUtc: string;
}

export interface ParsedSpectroResultDto {
  batchNumber?: string;
  furnaceNumber?: number;
  furnaceWeight?: number;
  colorCode?: string;
  gradeName?: string;
  partName?: string;
  partNo?: string;
  calculatedAdditions?: Record<string, number>;
  actualValues?: Record<string, number>;
  warnings?: string[];
  hiddenChemicals?: string[];
  calculationDetails?: CalculationStepDto[];
}

export interface CalculationStepDto {
  chemical: string;
  actualValue: number;
  minSpec: number;
  maxSpec: number;
  targetValue: number;
  factor: number;
  furnaceWeight: number;
  additionGrams: number;
  formula: string;
}

export interface ChemicalConsumptionPeriodDto {
  period: string;
  consumptions: Record<string, number>;
}
