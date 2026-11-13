// Sunucudaki model.rs ile birebir aynı. Tutarlar KURUŞ cinsindendir.

export type Role = "yonetici" | "calisan";
export type EventKind = "dugun" | "nisan" | "kina" | "soz" | "sunnet" | "kokteyl" | "diger";
export type Status = "on_rezervasyon" | "kesin" | "iptal";
export type Method = "nakit" | "kredi_karti" | "havale";
export type InstallmentState = "bekliyor" | "kismen" | "odendi" | "gecikti";

export interface ServerInfo { businessName: string; isSetUp: boolean; version: string }
export interface User { id: string; name: string; username: string; role: Role; isActive: boolean }
export interface LoginResponse { token: string; user: User }
export interface LoginUserOption { name: string; username: string }

export interface BusinessSettings {
  businessName: string;
  phone: string | null;
  address: string | null;
  taxOffice: string | null;
  taxNumber: string | null;
  contractTerms: string;
  defaultOptionDays: number;
}

export interface Hall { id: string; name: string; capacity: number; isActive: boolean }
/** Paketlerde `price` sabit tutardır; üzerine kişi başı × max(davetli, minGuests) eklenir. Ek hizmetlerde ikisi 0. */
export interface Product {
  id: string;
  name: string;
  description: string | null;
  price: number;
  perGuestPrice: number;
  minGuests: number;
  /** Yalnızca ek hizmetlerde: seçenek grubu (ör. "Pasta"); aynı gruptan tek seçim. */
  groupName: string | null;
  isActive: boolean;
}
/** Rezervasyondaki ek hizmet; `price` kişi sayısıyla hesaplanmış tutardır. */
export interface ExtraLine {
  extraId: string | null;
  name: string;
  groupName: string | null;
  price: number;
  basePrice: number;
  perGuest: number;
}
export interface PackagePricing { basePrice: number; perGuestPrice: number; minGuests: number }

/** Menüdeki bir yemek, ör. { course: "Ana yemek", name: "Kuzu tandır" }. */
export interface MenuDish { course: string; name: string }
/** Yemek menüsü. `perGuestPrice` kişi başı farktır (0 = paket fiyatına dahil). */
export interface Menu {
  id: string;
  name: string;
  description: string | null;
  perGuestPrice: number;
  isActive: boolean;
  items: MenuDish[];
}

export interface Customer {
  id: string;
  fullName: string;
  phone: string;
  phone2: string | null;
  brideName: string | null;
  groomName: string | null;
  notes: string | null;
  reservationCount: number;
}

export interface CustomerInput {
  fullName: string;
  phone: string;
  phone2?: string | null;
  brideName?: string | null;
  groomName?: string | null;
  notes?: string | null;
}

export interface ReservationSummary {
  id: string;
  no: string;
  date: string;
  start: string;
  end: string;
  hallId: string;
  hallName: string;
  customerId: string;
  customerName: string;
  customerPhone: string;
  kind: EventKind;
  guestCount: number;
  status: Status;
  optionUntil: string | null;
  optionExpired: boolean;
  total: number;
  paid: number;
  remaining: number;
  overdue: number;
}

export interface Installment { id: string; label: string; dueDate: string; amount: number; paid: number; state: InstallmentState }

export interface Payment {
  id: string;
  reservationId: string;
  reservationNo: string;
  no: string;
  date: string;
  amount: number;
  method: Method;
  isRefund: boolean;
  note: string | null;
  isCancelled: boolean;
  cancelReason: string | null;
  createdBy: string;
  customerName: string;
}

export interface ReservationDetail {
  summary: ReservationSummary;
  packageName: string | null;
  packagePrice: number;
  packagePricing: PackagePricing;
  extras: ExtraLine[];
  /** Seçilen menü (satış anındaki adı ve kişi başı farkı). */
  menuName: string | null;
  menuPerGuest: number;
  menuPrice: number;
  menuItems: MenuDish[];
  menuNotes: string | null;
  discount: number;
  notes: string | null;
  cancelReason: string | null;
  installments: Installment[];
  payments: Payment[];
  createdBy: string;
  createdAt: string;
  confirmedAt: string | null;
}

/** Organizasyon planı: saatlik akış ve müşterinin seçtiği müzikler. */
export interface ProgramItem { time: string; title: string; note: string | null }
export interface MusicItem { label: string; song: string | null; link: string | null }
export interface Program { items: ProgramItem[]; music: MusicItem[]; notes: string | null }

export interface InstallmentInput { label: string; dueDate: string; amount: number }
export interface PaymentInput { amount: number; method: Method; note?: string | null }

export interface SaleInput {
  packageId: string;
  extraIds: string[];
  menuId: string | null;
  discount: number;
  plan: InstallmentInput[];
  firstPayment: PaymentInput | null;
}

export interface CreateReservationRequest {
  customerId?: string | null;
  newCustomer?: CustomerInput | null;
  hallId: string;
  date: string;
  start: string;
  end: string;
  kind: EventKind;
  guestCount: number;
  notes?: string | null;
  optionDays?: number | null;
  sale?: SaleInput | null;
}

export interface HallAvailability { hallId: string; hallName: string; capacity: number; isFree: boolean; busyWith: string | null }

export interface Dashboard {
  today: ReservationSummary[];
  upcoming: ReservationSummary[];
  options: ReservationSummary[];
  overdue: ReservationSummary[];
  weekCount: number;
  monthCollected: number;
  overdueTotal: number;
}

export interface MonthRow { month: number; eventCount: number; sales: number; collected: number; refunded: number }
export interface YearReport { year: number; months: MonthRow[]; totalSales: number; totalCollected: number; totalRefunded: number }
export interface HallUsageRow { hallName: string; eventCount: number; sales: number }
export interface SalesExtra { name: string; groupName: string | null; price: number }
export interface SalesRow {
  id: string; no: string; date: string; hallName: string; customerName: string; kind: EventKind; guestCount: number;
  status: Status; packageName: string | null; packagePrice: number; menuName: string | null; menuPrice: number;
  discount: number; total: number; paid: number; extras: SalesExtra[]; createdOn: string; createdBy: string;
  cancelReason: string | null;
}
export interface DueRow {
  reservationId: string; no: string; customerName: string; customerPhone: string; eventDate: string; hallName: string;
  label: string; dueDate: string; amount: number; paid: number; state: InstallmentState;
}
export interface BackupInfo { fileName: string; createdAt: string; sizeBytes: number }
export interface FoundServer { address: string; port: number; businessName: string; machineName: string }
export interface ShellConfig { mode: "main" | "client" | null; serverUrl: string | null }

// ---------------- Takvim eşitleme (Google Takvim / Outlook) ----------------
export type CalendarProvider = "google" | "microsoft";
export interface CalendarAccount {
  id: string; provider: CalendarProvider; email: string; calendarName: string; connectedAt: string;
  lastSyncAt: string | null; lastError: string | null; needsReconnect: boolean; eventCount: number;
}
export interface CalendarSyncStatus {
  googleAvailable: boolean; microsoftAvailable: boolean; accounts: CalendarAccount[];
  pending: { provider: CalendarProvider; authUrl: string; error: string | null } | null;
  syncing: boolean;
  demo: boolean;
}
