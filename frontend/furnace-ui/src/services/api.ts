import axios, { AxiosError, InternalAxiosRequestConfig } from 'axios';
import type {
  ApiResponse,
  AuthResponse,
  BatchDto,
  CurrentUser,
  DashboardDto,
  FurnaceDto,
  HistoryItem,
  LoginRequest,
  UserManagementDto,
} from '../types';

export interface PartSpecDto {
  id: number;
  partName: string;
  partNo: string;
  gradeName: string;
  colorCode?: string;
  minTargetsJson?: string;
  maxTargetsJson?: string;
  customerMaxTargetsJson?: string;
  customerMinTargetsJson?: string;
}

const API_BASE = '/api';
const TOKEN_KEY = 'fm.token';
const REFRESH_KEY = 'fm.refresh';
const USER_KEY = 'fm.user';

export const tokenStore = {
  getToken: () => localStorage.getItem(TOKEN_KEY),
  getRefresh: () => localStorage.getItem(REFRESH_KEY),
  getUser: (): CurrentUser | null => {
    const raw = localStorage.getItem(USER_KEY);
    return raw ? (JSON.parse(raw) as CurrentUser) : null;
  },
  setSession: (auth: AuthResponse) => {
    const sourceUser = auth.user ?? {
      id: auth.userId ?? 0,
      userName: auth.username ?? '',
      displayName: auth.displayName ?? 'User',
      role: auth.role ?? 'Viewer',
      roleName: auth.roleName ?? 'Viewer',
    };

    const roleString = sourceUser.roleName ?? (typeof sourceUser.role === 'string' ? sourceUser.role : 'Viewer');

    const normalizedUser: CurrentUser = {
      id: sourceUser.id,
      username: sourceUser.username ?? sourceUser.userName ?? '',
      displayName: sourceUser.displayName ?? 'User',
      role: roleString as any,
      roleName: roleString,
    };

    localStorage.setItem(TOKEN_KEY, auth.token);
    localStorage.setItem(REFRESH_KEY, auth.refreshToken);
    localStorage.setItem(USER_KEY, JSON.stringify(normalizedUser));
  },
  clear: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(REFRESH_KEY);
    localStorage.removeItem(USER_KEY);
  },
};

const api = axios.create({
  baseURL: API_BASE,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config: InternalAxiosRequestConfig) => {
  const token = tokenStore.getToken();
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});

let isRefreshing = false;
let pendingQueue: Array<(token: string) => void> = [];

async function refreshAccessToken(): Promise<string | null> {
  const refresh = tokenStore.getRefresh();
  if (!refresh) return null;
  try {
    const { data } = await axios.post<ApiResponse<AuthResponse>>(`${API_BASE}/auth/refresh`, {
      refreshToken: refresh,
    });
    tokenStore.setSession(data.data);
    return data.data.token;
  } catch {
    tokenStore.clear();
    return null;
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError) => {
    const original = error.config as InternalAxiosRequestConfig & { _retry?: boolean };
    if (error.response?.status === 401 && original && !original._retry) {
      original._retry = true;
      if (isRefreshing) {
        return new Promise((resolve) => {
          pendingQueue.push((token) => {
            original.headers.Authorization = `Bearer ${token}`;
            resolve(api(original));
          });
        });
      }
      isRefreshing = true;
      const newToken = await refreshAccessToken();
      isRefreshing = false;
      pendingQueue.forEach((cb) => cb(newToken!));
      pendingQueue = [];
      if (newToken) {
        original.headers.Authorization = `Bearer ${newToken}`;
        return api(original);
      }
    }
    return Promise.reject(error);
  },
);

export function unwrap<T>(response: { data: ApiResponse<T> }): T {
  return response.data.data;
}

export const authApi = {
  login: (payload: LoginRequest) =>
    api.post<ApiResponse<AuthResponse>>('/auth/login', payload).then(unwrap),
  refresh: (refreshToken: string) =>
    api.post<ApiResponse<AuthResponse>>('/auth/refresh', { refreshToken }).then(unwrap),
  me: () => api.get<ApiResponse<CurrentUser>>('/auth/me').then(unwrap).then(u => ({
    ...u,
    role: (u.roleName ?? (typeof u.role === 'string' ? u.role : 'Viewer')) as any
  })),
  logout: () => api.post('/auth/logout'),
};

export const furnaceApi = {
  getAll: () => api.get<ApiResponse<FurnaceDto[]>>('/furnace').then(unwrap),
  getById: (id: number) => api.get<ApiResponse<FurnaceDto>>(`/furnace/${id}`).then(unwrap),
  save: (payload: Partial<FurnaceDto>) => api.post<ApiResponse<FurnaceDto>>('/furnace', payload).then(unwrap),
  updateOperationalStatus: (id: number, operationalStatus: string, operationalRemarks?: string) =>
    api.put<ApiResponse<FurnaceDto>>(`/furnace/${id}/operational-status`, { operationalStatus, operationalRemarks }).then(unwrap),
  delete: (id: number) => api.delete<ApiResponse<null>>(`/furnace/${id}`).then(unwrap),
  hardDelete: (id: number) => api.delete<ApiResponse<null>>(`/furnace/${id}/hard`).then(unwrap),
};

export const batchApi = {
  getActive: (furnaceId: number) =>
    api.get<ApiResponse<BatchDto | null>>(`/batch/active/${furnaceId}`).then(unwrap),
  getById: (id: number) => api.get<ApiResponse<BatchDto>>(`/batch/${id}`).then(unwrap),
  create: (payload: { batchNumber: string; furnaceId: number; indicator?: number; colorCode?: string; si?: number; ti?: number; mg?: number; sr?: number; instruction?: string; remarks?: string; actualOperatorName?: string }) =>
    api.post<ApiResponse<BatchDto>>('/batch', payload).then(unwrap),
  update: (id: number, payload: { batchNumber?: string; si: number; ti: number; mg: number; sr?: number; colorCode?: string; indicator: number; instruction?: string; remarks?: string; actualOperatorName?: string }) =>
    api.put<ApiResponse<BatchDto>>(`/batch/${id}`, payload).then(unwrap),
  clear: (furnaceId: number, confirm: boolean, actualOperatorName?: string) =>
    api.post<ApiResponse<null>>(`/batch/clear/${furnaceId}`, { confirm, actualOperatorName }).then(unwrap),
  close: (id: number, confirm: boolean, actualOperatorName?: string) =>
    api.post<ApiResponse<null>>(`/batch/close/${id}`, { confirm, actualOperatorName }).then(unwrap),
};

export const dashboardApi = {
  get: () => api.get<ApiResponse<DashboardDto>>('/dashboard').then(unwrap),
};

export const historyApi = {
  batches: (params?: Record<string, unknown>) =>
    api.get<ApiResponse<HistoryItem[]>>('/history/batches', { params }).then(unwrap),
  consumption: (fromUtc: string, toUtc: string, groupBy: string) =>
    api.get<ApiResponse<import('../types').ChemicalConsumptionPeriodDto[]>>('/history/consumption', { params: { fromUtc, toUtc, groupBy } }).then(unwrap),
  reset: () => api.delete<ApiResponse<null>>('/history/reset').then(unwrap),
};

export const usersApi = {
  getAll: () => api.get<ApiResponse<UserManagementDto[]>>('/users').then(unwrap),
  create: (payload: unknown) => api.post<ApiResponse<UserManagementDto>>('/users', payload).then(unwrap),
  update: (id: number, payload: unknown) => api.put<ApiResponse<UserManagementDto>>(`/users/${id}`, payload).then(unwrap),
  deactivate: (id: number) => api.delete<ApiResponse<null>>(`/users/${id}`).then(unwrap),
  resetPassword: (id: number, payload: { adminPassword: string; newPassword: string }) =>
    api.post<ApiResponse<null>>(`/users/${id}/password`, payload).then(unwrap),
};

export const partSpecsApi = {
  getAll: () => api.get<ApiResponse<PartSpecDto[]>>('/partspecifications').then(unwrap),
  create: (payload: Partial<PartSpecDto>) => api.post<ApiResponse<PartSpecDto>>('/partspecifications', payload).then(unwrap),
  update: (id: number, payload: Partial<PartSpecDto>) => api.put<ApiResponse<null>>(`/partspecifications/${id}`, payload).then(unwrap),
  delete: (id: number) => api.delete<ApiResponse<null>>(`/partspecifications/${id}`).then(unwrap),
  deleteAll: () => api.delete<ApiResponse<null>>(`/partspecifications/all`).then(unwrap),
  uploadExcel: (file: File, force: boolean = false) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post<ApiResponse<null>>(`/partspecifications/upload?force=${force}`, formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    }).then(unwrap).catch((err: any) => {
      if (err.response?.status === 409 && err.response?.data?.message === 'CONFLICT_EXISTING') {
        throw new Error('CONFLICT_EXISTING');
      }
      throw err;
    });
  },
  exportExcel: async () => {
    const response = await api.get('/partspecifications/export', { responseType: 'blob' });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'master_specs_export.xlsx');
    document.body.appendChild(link);
    link.click();
    link.parentNode?.removeChild(link);
    window.URL.revokeObjectURL(url);
  },
  downloadTemplate: async () => {
    const response = await api.get('/partspecifications/template', { responseType: 'blob' });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'master_specs_template.xlsx');
    document.body.appendChild(link);
    link.click();
    link.parentNode?.removeChild(link);
    window.URL.revokeObjectURL(url);
  }
};

export interface MasterConfigStatus {
  exists: boolean;
  lastModified: string | null;
  size: number;
}

export interface MasterConfigDataRow {
  slNo: string;
  partName: string;
  partNo: string;
  gradeName: string;
  colorCode: string;
  specType: string;
  si: string;
  fe: string;
  cu: string;
  mn: string;
  mg: string;
  zn: string;
  ti: string;
  sr: string;
  pb: string;
  sn: string;
  ni: string;
  cr: string;
}

export const masterConfigApi = {
  getStatus: () => api.get<ApiResponse<MasterConfigStatus>>('/masterconfiguration/status').then(unwrap),
  getData: () => api.get<ApiResponse<MasterConfigDataRow[]>>('/masterconfiguration/data').then(unwrap),
  uploadExcel: (file: File) => {
    const formData = new FormData();
    formData.append('file', file);
    return api.post<ApiResponse<null>>('/masterconfiguration/upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    }).then(unwrap);
  },
  downloadExcel: async () => {
    const response = await api.get('/masterconfiguration/download', { responseType: 'blob' });
    const url = window.URL.createObjectURL(new Blob([response.data]));
    const link = document.createElement('a');
    link.href = url;
    link.setAttribute('download', 'MasterConfiguration.xlsx');
    document.body.appendChild(link);
    link.click();
    link.parentNode?.removeChild(link);
    window.URL.revokeObjectURL(url);
  },
  deleteExcel: () => api.delete<ApiResponse<null>>('/masterconfiguration').then(unwrap)
};

export interface NetworkInfoDto {
  success: boolean;
  lanIp: string;
  port: number;
  localUrl: string;
  tvUrl: string;
  spectroPdfPath: string;
}

export const systemApi = {
  getNetworkInfo: () => api.get<NetworkInfoDto>('/system/network-info').then((res) => res.data),
};

export default api;
