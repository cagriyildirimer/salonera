import { useEffect, useMemo, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { gridRange, monthOf, MonthGrid } from "../components/MonthGrid";
import {
  emptySale, PackagePicker, PaymentPlanner, saleTotals, toSaleInput, type SaleState,
} from "../components/SaleEditor";
import { Alert, Empty, errorText, Field, Loading, NumberInput, TimeSelect, useLoad } from "../components/ui";
import { dateWithDay, kindLabels, methodLabels, tl, today } from "../format";
import { suggestDeposit } from "../plan";
import { billedGuests, pricingOf } from "../pricing";
import type { Customer, CustomerInput, EventKind, HallAvailability } from "../types";

const STEPS = ["Tarih ve salon", "Müşteri", "Kayıt türü ve paket", "Ödeme", "Kontrol ve onay"];
const KINDS: EventKind[] = ["dugun", "nisan", "kina", "soz", "sunnet", "kokteyl", "diger"];

export function Wizard({ date: initialDate, hallId: initialHall, customerId }: { date?: string; hallId?: string; customerId?: string }) {
  const { navigate, replace } = useSession();
  const now = today();
  const [step, setStep] = useState(0);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  // 1. Tarih ve salon
  const [month, setMonth] = useState(monthOf(initialDate ?? now));
  const [date, setDate] = useState<string | null>(initialDate ?? null);
  const [start, setStart] = useState("19:00");
  const [end, setEnd] = useState("00:00");
  const [hallId, setHallId] = useState<string | null>(initialHall ?? null);
  const [kind, setKind] = useState<EventKind>("dugun");
  const [guests, setGuests] = useState(250);
  const [availability, setAvailability] = useState<HallAvailability[] | null>(null);

  // 2. Müşteri
  const [customerMode, setCustomerMode] = useState<"existing" | "new">(customerId ? "existing" : "new");
  const [selectedCustomer, setSelectedCustomer] = useState<Customer | null>(null);
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<Customer[] | null>(null);
  const [newCustomer, setNewCustomer] = useState<CustomerInput>({ fullName: "", phone: "", brideName: "", groomName: "" });

  // 3-4. Kayıt türü, paket, ödeme
  const [isOption, setIsOption] = useState(false);
  const [optionDays, setOptionDays] = useState(7);
  const [sale, setSale] = useState<SaleState>(emptySale());
  const [notes, setNotes] = useState("");

  const halls = useLoad(() => api.halls());
  const catalog = useLoad(async () => {
    const [packages, extras, settings] = await Promise.all([api.packages(), api.extras(), api.settings()]);
    return { packages, extras, settings };
  });
  const events = useLoad(() => {
    const { from, to } = gridRange(month);
    return api.calendar(from, to);
  }, [month]);

  useEffect(() => {
    if (catalog.data) setOptionDays(catalog.data.settings.defaultOptionDays);
  }, [catalog.data]);

  // Seçilen gün/saat için salonların doluluğu.
  useEffect(() => {
    if (!date) return;
    let alive = true;
    setAvailability(null);
    api.availability(date, start, end).then((list) => {
      if (!alive) return;
      setAvailability(list);
      const current = list.find((h) => h.hallId === hallId);
      if (!current?.isFree) {
        const free = list.filter((h) => h.isFree);
        setHallId(free.length === 1 ? free[0].hallId : null);
      }
    }).catch((e) => alive && setError(errorText(e)));
    return () => { alive = false; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [date, start, end]);

  // Müşteri arama (yazmayı bitirince).
  useEffect(() => {
    let alive = true;
    const timer = setTimeout(() => {
      api.customers(query).then((list) => {
        if (!alive) return;
        setResults(list);
        if (customerId && !selectedCustomer) setSelectedCustomer(list.find((c) => c.id === customerId) ?? null);
      }).catch(() => alive && setResults([]));
    }, 250);
    return () => { alive = false; clearTimeout(timer); };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [query]);

  const packages = catalog.data?.packages ?? [];
  const extras = catalog.data?.extras ?? [];
  const { pkg, packagePrice, total } = saleTotals(sale, packages, extras, guests);
  const hall = availability?.find((h) => h.hallId === hallId) ?? null;
  const skipsPayment = isOption;

  const customerName = customerMode === "existing" ? selectedCustomer?.fullName ?? "" : newCustomer.fullName.trim();

  const validate = (): string | null => {
    switch (step) {
      case 0:
        if (!date) return "Takvimden bir gün seçin.";
        if (!hallId || !hall?.isFree) return "Boş bir salon seçin.";
        if (guests <= 0) return "Davetli sayısını yazın.";
        if (hall && guests > hall.capacity) return `${hall.hallName} en fazla ${hall.capacity} kişi alıyor. Davetli sayısını azaltın veya başka salon seçin.`;
        return null;
      case 1:
        if (customerMode === "existing") return selectedCustomer ? null : "Listeden bir müşteri seçin ya da “Yeni müşteri”ye geçin.";
        if (!newCustomer.fullName.trim()) return "Müşterinin adını ve soyadını yazın.";
        if (newCustomer.phone.replace(/\D/g, "").length < 10) return "Telefon numarasını eksiksiz yazın (örnek: 0532 123 45 67).";
        return null;
      case 2:
        if (isOption) return null;
        if (!sale.packageId) return packages.length ? "Bir paket seçin." : "Paket tanımlı değil. Şimdilik “Ön rezervasyon” seçebilirsiniz.";
        if (sale.discount > total + sale.discount) return "İndirim toplam fiyattan büyük olamaz.";
        if (total <= 0) return "Toplam tutar sıfır olamaz. İndirimi kontrol edin.";
        return null;
      case 3:
        if (sale.deposit > total) return "Kapora, toplam tutardan büyük olamaz.";
        return null;
      default:
        return null;
    }
  };

  const next = async () => {
    const problem = validate();
    if (problem) {
      setError(problem);
      return;
    }
    setError("");
    if (step === 4) {
      await save();
      return;
    }
    let target = step + 1;
    if (target === 3 && skipsPayment) target = 4;
    if (target === 3 && sale.deposit === 0) setSale((s) => ({ ...s, deposit: suggestDeposit(total) }));
    setStep(target);
  };

  const backStep = () => {
    setError("");
    let target = step - 1;
    if (target === 3 && skipsPayment) target = 2;
    setStep(Math.max(0, target));
  };

  const save = async () => {
    if (!date || !hallId) return;
    setSaving(true);
    try {
      const response = await api.createReservation({
        customerId: customerMode === "existing" ? selectedCustomer?.id : null,
        newCustomer: customerMode === "new" ? newCustomer : null,
        hallId,
        date,
        start,
        end,
        kind,
        guestCount: guests,
        notes: notes.trim() || null,
        optionDays: isOption ? optionDays : null,
        sale: isOption ? null : toSaleInput(sale, total, now, date),
      });
      replace({ name: "reservation", id: response.reservationId });
      sessionStorage.setItem("salonera-just-created", response.reservationId);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setSaving(false);
    }
  };

  const summary = useMemo(() => {
    const rows: [string, string][] = [
      ["Tarih", date ? dateWithDay(date) : "—"],
      ["Saat", `${start} – ${end}`],
      ["Salon", hall?.hallName ?? "—"],
      ["Organizasyon", `${kindLabels[kind]} · ${guests} davetli`],
      ["Müşteri", customerMode === "new" ? `${customerName} · ${newCustomer.phone} (yeni)` : `${customerName} · ${selectedCustomer?.phone ?? ""}`],
    ];
    if (isOption) {
      rows.push(["Kayıt türü", `Ön rezervasyon — tarih ${optionDays} gün tutulur`]);
    } else {
      const extraNames = extras.filter((e) => sale.extraIds.includes(e.id)).map((e) => e.name);
      rows.push(["Kayıt türü", "Kesin kayıt (sözleşme hazırlanır)"]);
      rows.push(["Paket", pkg ? `${pkg.name} · ${tl(packagePrice)}${pkg.perGuestPrice > 0 ? ` (${billedGuests(pricingOf(pkg), guests)} kişi)` : ""}` : "—"]);
      rows.push(["Ek hizmetler", extraNames.length ? extraNames.join(", ") : "Yok"]);
      if (sale.discount) rows.push(["İndirim", tl(sale.discount)]);
      rows.push(["Toplam tutar", tl(total)]);
      if (sale.takeNow && sale.deposit > 0) rows.push(["Bugün alınan kapora", `${tl(Math.min(sale.deposit, total))} · ${methodLabels[sale.method]}`]);
    }
    return rows;
  }, [date, start, end, hall, kind, guests, customerMode, customerName, newCustomer.phone, selectedCustomer, isOption, optionDays, extras, sale, pkg, packagePrice, total]);

  if (halls.loading || catalog.loading) return <Loading />;

  return (
    <>
      <div className="steps no-print">
        {STEPS.map((title, i) => {
          const skipped = i === 3 && skipsPayment;
          const cls = ["step", i === step && "active", i < step && "done", i < step && "clickable"].filter(Boolean).join(" ");
          return (
            <div key={title} className="row" style={{ gap: 6, opacity: skipped ? 0.35 : 1 }}>
              <button className={cls} onClick={() => { if (i < step && !skipped) { setError(""); setStep(i); } }}>
                <span className="num">{i < step ? <Icon name="check" size={16} strokeWidth={3} /> : i + 1}</span>{title}
              </button>
              {i < STEPS.length - 1 && <span className={`step-line ${i < step ? "done" : ""}`} />}
            </div>
          );
        })}
      </div>

      <div className="content">
        <div className="content-wide">
          {step === 0 && (
            <div className="split" style={{ gridTemplateColumns: "minmax(0, 3fr) minmax(340px, 2fr)" }}>
              <div className="card">
                <div className="card-title">Hangi gün?</div>
                <div className="card-sub" style={{ marginBottom: 14 }}>Takvimden bir güne tıklayın. Kırmızı günlerde bütün salonlar dolu.</div>
                <MonthGrid month={month} events={events.data ?? []} selected={date} hallCount={halls.data?.length ?? 0}
                  onSelect={(d) => { if (d >= now) { setDate(d); setError(""); } }} onMonth={setMonth} compact disablePast />
              </div>
              <div className="stack">
                <div className="card stack" style={{ gap: 16 }}>
                  <div>
                    <div className="muted small">Seçilen gün</div>
                    <div className="strong" style={{ fontSize: 21, color: "var(--primary)" }}>{date ? dateWithDay(date) : "Takvimden bir gün seçin"}</div>
                  </div>
                  <div className="grid grid-2" style={{ gap: 14 }}>
                    <Field label="Başlangıç saati"><TimeSelect value={start} onChange={setStart} /></Field>
                    <Field label="Bitiş saati"><TimeSelect value={end} onChange={setEnd} from={10} extra={4} /></Field>
                  </div>
                  <Field label="Organizasyon türü">
                    <div className="row" style={{ gap: 8 }}>
                      {KINDS.map((k) => (
                        <button key={k} className={`btn ${kind === k ? "btn-primary" : ""}`} style={{ minHeight: 42, padding: "0 16px", fontSize: 15 }}
                          onClick={() => setKind(k)}>{kindLabels[k]}</button>
                      ))}
                    </div>
                  </Field>
                  <Field label="Davetli sayısı"><NumberInput value={guests} onChange={setGuests} /></Field>
                </div>
                <div className="card">
                  <div className="card-title">Hangi salon?</div>
                  <div className="card-sub" style={{ marginBottom: 14 }}>Seçtiğiniz gün ve saatte salonların durumu</div>
                  {!date ? <Empty icon="calendar">Önce takvimden bir gün seçin.</Empty> : availability === null ? <Loading text="Kontrol ediliyor…" /> : (
                    <div className="stack" style={{ gap: 10 }}>
                      {availability.map((h) => (
                        <button key={h.hallId} className={`choice ${hallId === h.hallId ? "selected" : ""}`} disabled={!h.isFree}
                          onClick={() => { setHallId(h.hallId); setError(""); }}>
                          <div style={{ minWidth: 0 }}>
                            <div className="choice-title">{h.hallName} <span className="muted" style={{ fontWeight: 400, fontSize: 15 }}>· {h.capacity} kişilik</span></div>
                            <div className="small" style={{ color: h.isFree ? "var(--success)" : "var(--danger)", fontWeight: 600 }}>
                              {h.isFree ? "Bu saatlerde boş" : `Dolu: ${h.busyWith}`}
                            </div>
                          </div>
                          {h.isFree && <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>}
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              </div>
            </div>
          )}

          {step === 1 && (
            <div className="content-narrow stack">
              <div>
                <h2 style={{ fontSize: 26, fontWeight: 600 }}>Müşteri kim?</h2>
                <p className="muted" style={{ fontSize: 17 }}>Daha önce gelmiş bir müşteriyse listeden seçin, değilse bilgilerini yazın.</p>
              </div>
              <div className="grid grid-2" style={{ gap: 12 }}>
                <button className={`choice ${customerMode === "new" ? "selected" : ""}`} onClick={() => setCustomerMode("new")}>
                  <div className="choice-icon"><Icon name="userPlus" size={24} /></div>
                  <div><div className="choice-title">Yeni müşteri</div><div className="choice-sub">İlk kez geliyor</div></div>
                </button>
                <button className={`choice ${customerMode === "existing" ? "selected" : ""}`} onClick={() => setCustomerMode("existing")}>
                  <div className="choice-icon"><Icon name="search" size={24} /></div>
                  <div><div className="choice-title">Kayıtlı müşteri</div><div className="choice-sub">Listeden arayıp seçin</div></div>
                </button>
              </div>
              {customerMode === "new" ? (
                <div className="card grid grid-2">
                  <Field label="Adı soyadı *">
                    <input className="input" autoFocus value={newCustomer.fullName} onChange={(e) => setNewCustomer({ ...newCustomer, fullName: e.target.value })} />
                  </Field>
                  <Field label="Telefon *" hint="Örnek: 0532 123 45 67">
                    <input className="input" inputMode="tel" value={newCustomer.phone} onChange={(e) => setNewCustomer({ ...newCustomer, phone: e.target.value })} />
                  </Field>
                  <Field label="Gelinin adı (isteğe bağlı)">
                    <input className="input" value={newCustomer.brideName ?? ""} onChange={(e) => setNewCustomer({ ...newCustomer, brideName: e.target.value })} />
                  </Field>
                  <Field label="Damadın adı (isteğe bağlı)">
                    <input className="input" value={newCustomer.groomName ?? ""} onChange={(e) => setNewCustomer({ ...newCustomer, groomName: e.target.value })} />
                  </Field>
                </div>
              ) : (
                <div className="card stack" style={{ gap: 14 }}>
                  <div className="input-wrap">
                    <span className="prefix"><Icon name="search" size={19} /></span>
                    <input className="input with-prefix" autoFocus placeholder="Ad, soyad veya telefon yazın" value={query} onChange={(e) => setQuery(e.target.value)} />
                  </div>
                  {results === null ? <Loading text="Aranıyor…" /> : results.length === 0 ? (
                    <Empty icon="users">Aramaya uyan müşteri yok. Yukarıdan “Yeni müşteri”yi seçebilirsiniz.</Empty>
                  ) : (
                    <div className="stack" style={{ gap: 8 }}>
                      {results.map((c) => (
                        <button key={c.id} className={`choice ${selectedCustomer?.id === c.id ? "selected" : ""}`} style={{ padding: "12px 16px" }}
                          onClick={() => { setSelectedCustomer(c); setError(""); }}>
                          <div style={{ minWidth: 0 }}>
                            <div className="choice-title">{c.fullName}</div>
                            <div className="choice-sub">
                              {c.phone}{c.brideName || c.groomName ? ` · ${[c.brideName, c.groomName].filter(Boolean).join(" & ")}` : ""}
                              {c.reservationCount > 0 ? ` · ${c.reservationCount} kayıt` : ""}
                            </div>
                          </div>
                          <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
                        </button>
                      ))}
                    </div>
                  )}
                </div>
              )}
            </div>
          )}

          {step === 2 && (
            <div className="stack">
              <div className="grid grid-2" style={{ gap: 12, maxWidth: 900 }}>
                <button className={`choice ${!isOption ? "selected" : ""}`} onClick={() => setIsOption(false)}>
                  <div className="choice-icon"><Icon name="file" size={24} /></div>
                  <div><div className="choice-title">Kesin kayıt</div><div className="choice-sub">Paket seçilir, sözleşme hazırlanır</div></div>
                </button>
                <button className={`choice ${isOption ? "selected" : ""}`} onClick={() => setIsOption(true)}>
                  <div className="choice-icon" style={{ background: "var(--gold-soft)", color: "var(--gold)" }}><Icon name="hourglass" size={24} /></div>
                  <div><div className="choice-title">Ön rezervasyon</div><div className="choice-sub">Müşteri henüz düşünüyor; tarih bir süre tutulur</div></div>
                </button>
              </div>
              {isOption ? (
                <div className="card" style={{ maxWidth: 900 }}>
                  <Field label="Tarih kaç gün tutulsun?" hint="Süre dolunca tarih başkasına verilebilir. Ana sayfada süresi dolanlar ayrıca gösterilir.">
                    <div className="segment">
                      {[3, 7, 15, 30].map((d) => (
                        <button key={d} className={optionDays === d ? "on" : ""} onClick={() => setOptionDays(d)}>{d === 30 ? "1 ay" : d === 7 ? "1 hafta" : `${d} gün`}</button>
                      ))}
                    </div>
                  </Field>
                </div>
              ) : (
                <PackagePicker sale={sale} onChange={setSale} packages={packages} extras={extras} guests={guests} />
              )}
            </div>
          )}

          {step === 3 && date && <PaymentPlanner sale={sale} onChange={setSale} total={total} todayIso={now} eventIso={date} />}

          {step === 4 && (
            <div className="content-narrow stack">
              <div>
                <h2 style={{ fontSize: 26, fontWeight: 600 }}>Her şey doğru mu?</h2>
                <p className="muted" style={{ fontSize: 17 }}>Değiştirmek istediğiniz bir şey varsa yukarıdaki adıma tıklayarak geri dönebilirsiniz.</p>
              </div>
              <div className="card">
                {summary.map(([k, v]) => <div key={k} className="summary-row"><span className="k">{k}</span><span className="v">{v}</span></div>)}
              </div>
              <div className="card">
                <Field label="Not (isteğe bağlı)" hint="Örnek: Pasta çikolatalı olacak, gelin odası 17:00'de hazır olsun.">
                  <textarea className="textarea" value={notes} onChange={(e) => setNotes(e.target.value)} />
                </Field>
              </div>
            </div>
          )}
        </div>
      </div>

      <div className="wizard-foot no-print">
        <button className="btn" onClick={step === 0 ? () => navigate({ name: "home" }) : backStep} disabled={saving}>
          <Icon name="back" size={18} /> {step === 0 ? "Vazgeç" : "Geri"}
        </button>
        <div style={{ flex: 1 }}>{error && <Alert kind="danger">{error}</Alert>}</div>
        <button className="btn btn-primary" style={{ minWidth: 220 }} onClick={next} disabled={saving}>
          {saving ? "Kaydediliyor…" : step === 4 ? "Onayla ve Kaydet" : "Devam Et"} {!saving && <Icon name={step === 4 ? "check" : "next"} size={19} />}
        </button>
      </div>
    </>
  );
}
