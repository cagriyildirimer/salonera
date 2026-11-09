import type * as T from "./types";

/** Sunucudan gelen, kullanıcıya gösterilecek hata. */
export class ApiError extends Error {
  constructor(message: string, public status: number) {
    super(message);
  }
}

let baseUrl = "";
let token: string | null = null;
let onUnauthorized: (() => void) | null = null;

export function configureApi(url: string, onLogout: () => void) {
  baseUrl = url.replace(/\/$/, "");
  onUnauthorized = onLogout;
}

export const setToken = (value: string | null) => {
  token = value;
};

async function request<R>(method: string, path: string, body?: unknown): Promise<R> {
  let response: Response;
  try {
    response = await fetch(baseUrl + path, {
      method,
      headers: {
        "content-type": "application/json",
        ...(token ? { authorization: `Bearer ${token}` } : {}),
      },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    throw new ApiError(
      "Ana bilgisayara ulaşılamıyor. Ana bilgisayarın açık ve Salonera'nın çalışır durumda olduğundan emin olun.",
      0,
    );
  }
  const text = await response.text();
  const data = text ? JSON.parse(text) : null;
  if (!response.ok) {
    if (response.status === 401 && onUnauthorized) onUnauthorized();
    throw new ApiError(data?.message ?? "Beklenmeyen bir sorun oluştu.", response.status);
  }
  return data as R;
}

const get = <R>(path: string) => request<R>("GET", path);
const post = <R>(path: string, body?: unknown) => request<R>("POST", path, body ?? {});
const put = <R>(path: string, body: unknown) => request<R>("PUT", path, body);
const del = <R>(path: string) => request<R>("DELETE", path);
const q = (value: string) => encodeURIComponent(value);

export const api = {
  info: () => get<T.ServerInfo>("/api/info"),
  setup: (body: unknown) => post<null>("/api/setup", body),
  loginUsers: () => get<T.LoginUserOption[]>("/api/login-users"),
  login: (username: string, password: string) => post<T.LoginResponse>("/api/login", { username, password }),
  logout: () => post<null>("/api/logout"),
  me: () => get<T.User>("/api/me"),
  changePassword: (currentPassword: string, newPassword: string) =>
    post<null>("/api/me/password", { currentPassword, newPassword }),

  settings: () => get<T.BusinessSettings>("/api/settings"),
  saveSettings: (s: T.BusinessSettings) => put<T.BusinessSettings>("/api/settings", s),
  halls: (all = false) => get<T.Hall[]>(`/api/halls${all ? "?all=true" : ""}`),
  saveHall: (id: string | null, body: Omit<T.Hall, "id">) =>
    id ? put<string>(`/api/halls/${id}`, body) : post<string>("/api/halls", body),
  packages: (all = false) => get<T.Product[]>(`/api/packages${all ? "?all=true" : ""}`),
  savePackage: (id: string | null, body: Omit<T.Product, "id">) =>
    id ? put<string>(`/api/packages/${id}`, body) : post<string>("/api/packages", body),
  extras: (all = false) => get<T.Product[]>(`/api/extras${all ? "?all=true" : ""}`),
  saveExtra: (id: string | null, body: Omit<T.Product, "id">) =>
    id ? put<string>(`/api/extras/${id}`, body) : post<string>("/api/extras", body),
  users: () => get<T.User[]>("/api/users"),
  saveUser: (id: string | null, body: Omit<T.User, "id"> & { password?: string }) =>
    id ? put<T.User>(`/api/users/${id}`, body) : post<T.User>("/api/users", body),

  customers: (query = "") => get<T.Customer[]>(`/api/customers?q=${q(query)}`),
  customer: (id: string) => get<{ customer: T.Customer; reservations: T.ReservationSummary[] }>(`/api/customers/${id}`),
  createCustomer: (body: T.CustomerInput) => post<T.Customer>("/api/customers", body),
  updateCustomer: (id: string, body: T.CustomerInput) => put<T.Customer>(`/api/customers/${id}`, body),
  deleteCustomer: (id: string) => del<null>(`/api/customers/${id}`),

  calendar: (from: string, to: string) => get<T.ReservationSummary[]>(`/api/calendar?from=${from}&to=${to}`),
  availability: (date: string, start: string, end: string, exclude?: string) =>
    get<T.HallAvailability[]>(
      `/api/availability?date=${date}&start=${q(start)}&end=${q(end)}${exclude ? `&exclude=${exclude}` : ""}`,
    ),
  searchReservations: (query: string) => get<T.ReservationSummary[]>(`/api/reservations?q=${q(query)}`),
  createReservation: (body: T.CreateReservationRequest) =>
    post<{ reservationId: string; no: string; paymentId: string | null }>("/api/reservations", body),
  reservation: (id: string) => get<T.ReservationDetail>(`/api/reservations/${id}`),
  updateReservation: (id: string, body: unknown) => put<null>(`/api/reservations/${id}`, body),
  confirm: (id: string, sale: T.SaleInput) => post<string | null>(`/api/reservations/${id}/confirm`, sale),
  extend: (id: string, days: number) => post<null>(`/api/reservations/${id}/extend`, { days }),
  cancel: (id: string, reason: string) => post<null>(`/api/reservations/${id}/cancel`, { reason }),
  setPlan: (id: string, plan: T.InstallmentInput[]) => put<null>(`/api/reservations/${id}/plan`, { plan }),
  takePayment: (id: string, body: T.PaymentInput) => post<string>(`/api/reservations/${id}/payments`, body),
  refund: (id: string, body: { amount: number; method: T.Method; reason: string }) =>
    post<string>(`/api/reservations/${id}/refunds`, body),

  payments: (from: string, to: string) => get<T.Payment[]>(`/api/payments?from=${from}&to=${to}`),
  payment: (id: string) => get<T.Payment>(`/api/payments/${id}`),
  cancelPayment: (id: string, reason: string) => post<null>(`/api/payments/${id}/cancel`, { reason }),

  dashboard: () => get<T.Dashboard>("/api/dashboard"),
  yearReport: (year: number) => get<T.YearReport>(`/api/reports/year?year=${year}`),
  receivables: () => get<T.ReservationSummary[]>("/api/reports/receivables"),
  hallUsage: (from: string, to: string) => get<T.HallUsageRow[]>(`/api/reports/halls?from=${from}&to=${to}`),
  backups: () => get<T.BackupInfo[]>("/api/backups"),
  createBackup: () => post<T.BackupInfo>("/api/backups"),
};
