import { useEffect, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { emptySale, PackagePicker, PaymentPlanner, saleTotals, toSaleInput, type SaleState } from "../components/SaleEditor";
import {
  Alert, DateBox, errorText, Field, LoadError, Loading, Modal, MoneyInput, NumberInput, StatusBadge, TimeSelect,
  useAction, useFeedback, useLoad,
} from "../components/ui";
import {
  dateLong, dateTime, dateWithDay, installmentLabels, kindLabels, methodLabels, relativeDay, telHref, tl, today,
} from "../format";
import { suggestDeposit } from "../plan";
import { billedGuests, breakdownText, packageTotal, tariffText } from "../pricing";
import type { EventKind, HallAvailability, InstallmentInput, Method, ReservationDetail } from "../types";

export function ReservationPage({ id }: { id: string }) {
  const { navigate, isAdmin } = useSession();
  const { confirm } = useFeedback();
  const { run, busy } = useAction();
  const { data, error, loading, reload } = useLoad(() => api.reservation(id), [id]);
  const program = useLoad(() => api.program(id), [id]);
  const [modal, setModal] = useState<null | "pay" | "refund" | "edit" | "plan" | "extend">(null);
  const [converting, setConverting] = useState(false);
  const [justCreated] = useState(() => {
    const created = sessionStorage.getItem("salonera-just-created") === id;
    if (created) sessionStorage.removeItem("salonera-just-created");
    return created;
  });

  if (loading && !data) return <div className="content"><Loading /></div>;
  if (error || !data) return <div className="content"><LoadError message={error ?? ""} onRetry={reload} /></div>;
  if (converting) return <ConvertOption detail={data} onDone={() => { setConverting(false); void reload(); }} onCancel={() => setConverting(false)} />;

  const r = data.summary;
  const isOption = r.status === "on_rezervasyon";
  const isConfirmed = r.status === "kesin";
  const isCancelled = r.status === "iptal";
  const lastReceipt = data.payments.find((p) => !p.isRefund && !p.isCancelled);
  const percent = r.total > 0 ? Math.min(100, Math.round((r.paid / r.total) * 100)) : 0;

  const cancelReservation = async () => {
    const reason = await confirm({
      title: "Rezervasyon iptal edilsin mi?",
      message: <>
        {r.customerName} – {dateLong(r.date)} ({r.hallName}) iptal edilecek ve salon bu saat için boşalacak.
        {r.paid > 0 && <><br /><br />Alınan {tl(r.paid)} ödeme kayıtlarda kalır. İade yapılacaksa iptalden sonra “İade yap” düğmesini kullanın.</>}
      </>,
      confirmText: "Evet, iptal et",
      danger: true,
      reasonLabel: "İptal nedeni",
    });
    if (reason) await run(() => api.cancel(id, reason).then(reload), "Rezervasyon iptal edildi.");
  };

  const cancelPayment = async (paymentId: string, no: string) => {
    const reason = await confirm({
      title: "Ödeme kaydı iptal edilsin mi?",
      message: `${no} numaralı kayıt iptal edilecek ve tutar hesaplardan düşülecek. Bunu yalnızca yanlış girilen kayıtlar için kullanın.`,
      confirmText: "Kaydı iptal et",
      danger: true,
      reasonLabel: "Neden?",
    });
    if (reason) await run(() => api.cancelPayment(paymentId, reason).then(reload), "Ödeme kaydı iptal edildi.");
  };

  return (
    <div className="content">
      <div className="content-wide stack">
        {justCreated && (
          <div className="alert alert-success" style={{ alignItems: "center", fontSize: 17 }}>
            <Icon name="check" size={24} />
            <span style={{ flex: 1 }}>
              <b>Kayıt tamamlandı.</b> {isOption ? "Tarih müşteri adına ayrıldı." : "Sözleşmeyi yazdırıp müşteriye imzalatabilirsiniz."}
            </span>
            {isConfirmed && (
              <button className="btn btn-primary" onClick={() => navigate({ name: "print", kind: "contract", id })}>
                <Icon name="printer" /> Sözleşmeyi Yazdır
              </button>
            )}
            {lastReceipt && (
              <button className="btn" onClick={() => navigate({ name: "print", kind: "receipt", id: lastReceipt.id })}>
                <Icon name="printer" /> Makbuzu Yazdır
              </button>
            )}
          </div>
        )}

        {/* Başlık */}
        <div className="card">
          <div className="res-head">
            <DateBox date={r.date} />
            <div style={{ flex: 1, minWidth: 0 }}>
              <div className="row" style={{ gap: 10 }}>
                <h2>{r.customerName}</h2>
                <StatusBadge r={r} />
              </div>
              <div className="muted" style={{ fontSize: 17, marginTop: 4 }}>
                {dateWithDay(r.date)} · {r.start}–{r.end} · {r.hallName}
              </div>
              <div className="muted" style={{ marginTop: 2 }}>
                {kindLabels[r.kind]} · {r.guestCount} davetli · Kayıt no {r.no} ·{" "}
                <a href={telHref(r.customerPhone)} style={{ fontWeight: 600 }}>{r.customerPhone}</a>
              </div>
            </div>
          </div>

          {isOption && (
            <div style={{ marginTop: 18 }}>
              <Alert kind={r.optionExpired ? "danger" : "warning"}>
                {r.optionExpired
                  ? <>Ön rezervasyonun süresi <b>{r.optionUntil ? dateLong(r.optionUntil) : ""}</b> tarihinde doldu. Tarih başkasına verilebilir.</>
                  : <>Bu tarih <b>{r.optionUntil ? dateLong(r.optionUntil) : ""}</b> tarihine kadar ({r.optionUntil ? relativeDay(r.optionUntil).toLowerCase() : ""}) müşteri adına tutuluyor.</>}
              </Alert>
            </div>
          )}
          {isCancelled && (
            <div style={{ marginTop: 18 }}><Alert kind="danger">Bu rezervasyon iptal edildi. Neden: {data.cancelReason}</Alert></div>
          )}

          <div className="row" style={{ marginTop: 20, gap: 10 }}>
            {isOption && (
              <>
                <button className="btn btn-primary" onClick={() => setConverting(true)}><Icon name="file" /> Kesin kayda çevir</button>
                <button className="btn" onClick={() => setModal("extend")}><Icon name="hourglass" /> Süreyi uzat</button>
              </>
            )}
            {isConfirmed && (
              <>
                {r.remaining > 0 && (
                  <button className="btn btn-primary" onClick={() => setModal("pay")}><Icon name="money" /> Ödeme Al</button>
                )}
                <button className="btn" onClick={() => navigate({ name: "print", kind: "contract", id })}><Icon name="printer" /> Sözleşme</button>
              </>
            )}
            <button className="btn" onClick={() => navigate({ name: "program", id })}><Icon name="list" /> Organizasyon planı</button>
            {!isCancelled && <button className="btn" onClick={() => setModal("edit")}><Icon name="edit" /> Tarih / salon değiştir</button>}
            <button className="btn" onClick={() => navigate({ name: "customer", id: r.customerId })}><Icon name="user" /> Müşteri bilgileri</button>
            <span className="spacer" />
            {r.paid > 0 && <button className="btn" onClick={() => setModal("refund")}><Icon name="undo" /> İade yap</button>}
            {!isCancelled && <button className="btn btn-danger" disabled={busy} onClick={cancelReservation}><Icon name="x" /> İptal et</button>}
          </div>
        </div>

        {(isConfirmed || r.paid > 0) && (
          <div className="split">
            <div className="stack">
              {/* Para durumu */}
              <div className="card">
                <div className="grid grid-3">
                  <div><div className="muted">Toplam</div><div className="big-number">{tl(r.total, true)}</div></div>
                  <div><div className="muted">Ödenen</div><div className="big-number" style={{ color: "var(--success)" }}>{tl(r.paid, true)}</div></div>
                  <div><div className="muted">Kalan</div><div className="big-number" style={{ color: r.remaining ? "var(--primary)" : "var(--success)" }}>{tl(r.remaining, true)}</div></div>
                </div>
                <div className="money-bar"><div style={{ width: `${percent}%` }} /></div>
                <div className="muted small">Ödemenin %{percent}'i tamamlandı{r.overdue > 0 && <b style={{ color: "var(--danger)" }}> · {tl(r.overdue)} gecikmiş</b>}</div>
              </div>

              {/* Ödemeler */}
              <div className="card">
                <div className="card-head">
                  <div><div className="card-title">Ödemeler</div><div className="card-sub">Alınan ödemeler ve iadeler</div></div>
                </div>
                {data.payments.length === 0 ? <div className="muted">Henüz ödeme alınmadı.</div> : (
                  <table className="table">
                    <thead><tr><th>Tarih</th><th>Makbuz</th><th>Şekli</th><th className="num">Tutar</th><th /></tr></thead>
                    <tbody>
                      {data.payments.map((p) => (
                        <tr key={p.id} className={p.isCancelled ? "cancelled" : undefined}>
                          <td>{dateTime(p.date)}<div className="muted small">{p.createdBy}</div></td>
                          <td>{p.no}{p.isRefund && <span className="badge badge-warning" style={{ marginLeft: 8 }}>İade</span>}</td>
                          <td>{methodLabels[p.method]}</td>
                          <td className="num strong" style={{ color: p.isRefund ? "var(--danger)" : undefined }}>
                            {p.isRefund ? "− " : ""}{tl(p.amount)}
                          </td>
                          <td className="num" style={{ textDecoration: "none" }}>
                            {!p.isCancelled && (
                              <>
                                <button className="btn btn-ghost" title="Makbuzu yazdır" onClick={() => navigate({ name: "print", kind: "receipt", id: p.id })}>
                                  <Icon name="printer" size={18} />
                                </button>
                                {isAdmin && (
                                  <button className="btn btn-ghost" title="Yanlış girilen kaydı iptal et" style={{ color: "var(--danger)" }}
                                    onClick={() => cancelPayment(p.id, p.no)}>
                                    <Icon name="x" size={18} />
                                  </button>
                                )}
                              </>
                            )}
                            {p.isCancelled && <span className="badge badge-muted">İptal</span>}
                          </td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            </div>

            <div className="stack">
              {/* Ödeme planı */}
              {isConfirmed && (
                <div className="card">
                  <div className="card-head">
                    <div><div className="card-title">Ödeme planı</div><div className="card-sub">Ödemeler sırayla taksitlere düşülür</div></div>
                    <button className="btn btn-ghost" onClick={() => setModal("plan")}><Icon name="edit" size={18} /> Değiştir</button>
                  </div>
                  <div className="list">
                    {data.installments.map((i) => (
                      <div key={i.id} className="list-item" style={{ cursor: "default" }}>
                        <div style={{ flex: 1 }}>
                          <div className="strong">{i.label}</div>
                          <div className="item-sub">{dateLong(i.dueDate)}{i.paid > 0 && i.paid < i.amount ? ` · ${tl(i.paid)} ödendi` : ""}</div>
                        </div>
                        <div style={{ textAlign: "right" }}>
                          <div className="strong">{tl(i.amount)}</div>
                          <span className={`badge badge-${i.state === "odendi" ? "success" : i.state === "gecikti" ? "danger" : i.state === "kismen" ? "warning" : "muted"}`}>
                            {installmentLabels[i.state]}
                          </span>
                        </div>
                      </div>
                    ))}
                  </div>
                </div>
              )}

              {/* Satış ayrıntısı */}
              {data.packageName && (
                <div className="card">
                  <div className="card-title" style={{ marginBottom: 10 }}>Paket ve fiyat</div>
                  <div className="price-line"><span>{data.packageName}</span><span>{tl(data.packagePrice)}</span></div>
                  {breakdownText(data.packagePricing, data.packagePrice) && (
                    <div className="muted small" style={{ marginTop: -4, marginBottom: 6 }}>{breakdownText(data.packagePricing, data.packagePrice)}</div>
                  )}
                  {data.extras.map((e) => <div key={e.name} className="price-line"><span>{e.name}</span><span>{tl(e.price)}</span></div>)}
                  {data.discount > 0 && <div className="price-line"><span className="k">İndirim</span><span style={{ color: "var(--success)" }}>− {tl(data.discount)}</span></div>}
                  <div className="hr" style={{ margin: "8px 0" }} />
                  <div className="price-line strong"><span>Toplam</span><span>{tl(r.total)}</span></div>
                </div>
              )}
            </div>
          </div>
        )}

        {/* Organizasyon planı özeti */}
        <div className="card">
          <div className="card-head">
            <div>
              <div className="card-title">Organizasyon planı</div>
              <div className="card-sub">Saatlik akış ve müşterinin seçtiği müzikler</div>
            </div>
            <div className="row" style={{ gap: 8 }}>
              {program.data && (program.data.items.length > 0 || program.data.music.length > 0) && (
                <button className="btn btn-ghost" onClick={() => navigate({ name: "print", kind: "program", id })}><Icon name="printer" size={18} /> Yazdır / PDF</button>
              )}
              <button className="btn btn-ghost" onClick={() => navigate({ name: "program", id })}><Icon name="edit" size={18} /> {program.data?.items.length || program.data?.music.length ? "Düzenle" : "Plan hazırla"}</button>
            </div>
          </div>
          {program.data && program.data.items.length === 0 && program.data.music.length === 0 ? (
            <div className="muted">Henüz plan yok. Yemek, pasta, takı töreni saatlerini ve giriş / ilk dans müziklerini buradan planlayabilirsiniz.</div>
          ) : program.data && (
            <div className="grid grid-2" style={{ alignItems: "start" }}>
              <div className="list">
                {program.data.items.map((it, i) => (
                  <div key={i} className="list-item" style={{ cursor: "default", padding: "8px 4px" }}>
                    <span className="strong" style={{ width: 56, color: "var(--primary)" }}>{it.time}</span>
                    <span style={{ flex: 1 }}>{it.title}</span>
                  </div>
                ))}
              </div>
              <div className="list">
                {program.data.music.map((m, i) => (
                  <div key={i} className="list-item" style={{ cursor: "default", padding: "8px 4px" }}>
                    <Icon name="music" size={18} />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <div className="strong">{m.label}</div>
                      <div className="item-sub" style={{ overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>{m.song ?? m.link}</div>
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>

        {data.notes && (
          <div className="card">
            <div className="card-title" style={{ marginBottom: 8 }}>Notlar</div>
            <div style={{ whiteSpace: "pre-wrap", fontSize: 16 }}>{data.notes}</div>
          </div>
        )}
        <div className="muted small">Kaydı oluşturan: {data.createdBy} · {dateTime(data.createdAt)}</div>
      </div>

      {modal === "pay" && <PayModal detail={data} onClose={() => setModal(null)} onDone={(paymentId) => {
        setModal(null);
        void reload();
        if (paymentId) navigate({ name: "print", kind: "receipt", id: paymentId });
      }} />}
      {modal === "refund" && <RefundModal detail={data} onClose={() => setModal(null)} onDone={() => { setModal(null); void reload(); }} />}
      {modal === "edit" && <EditModal detail={data} onClose={() => setModal(null)} onDone={() => { setModal(null); void reload(); }} />}
      {modal === "plan" && <PlanModal detail={data} onClose={() => setModal(null)} onDone={() => { setModal(null); void reload(); }} />}
      {modal === "extend" && <ExtendModal detail={data} onClose={() => setModal(null)} onDone={() => { setModal(null); void reload(); }} />}
    </div>
  );
}

// ---------------- Ödeme al ----------------
function PayModal({ detail, onClose, onDone }: { detail: ReservationDetail; onClose: () => void; onDone: (paymentId: string | null) => void }) {
  const r = detail.summary;
  const nextDue = detail.installments.find((i) => i.state !== "odendi");
  const [amount, setAmount] = useState(nextDue ? Math.min(nextDue.amount - nextDue.paid, r.remaining) : r.remaining);
  const [method, setMethod] = useState<Method>("nakit");
  const [note, setNote] = useState("");
  const [printReceipt, setPrintReceipt] = useState(true);
  const { run, busy } = useAction();
  const too = amount > r.remaining;

  const save = async () => {
    let paymentId: string | null = null;
    const ok = await run(async () => { paymentId = await api.takePayment(r.id, { amount, method, note: note || null }); }, "Ödeme kaydedildi.");
    if (ok) onDone(printReceipt ? paymentId : null);
  };

  return (
    <Modal onClose={onClose}>
      <h2>Ödeme al</h2>
      <p className="muted" style={{ marginBottom: 18 }}>{r.customerName} · Kalan borç: <b style={{ color: "var(--primary)" }}>{tl(r.remaining)}</b></p>
      <div className="stack" style={{ gap: 16 }}>
        <Field label="Alınan tutar" hint={nextDue ? `Sıradaki: ${nextDue.label} (${dateLong(nextDue.dueDate)})` : undefined}>
          <MoneyInput value={amount} onChange={setAmount} autoFocus />
        </Field>
        <div className="row" style={{ gap: 8 }}>
          {nextDue && <button className="btn" onClick={() => setAmount(Math.min(nextDue.amount - nextDue.paid, r.remaining))}>Sıradaki taksit</button>}
          <button className="btn" onClick={() => setAmount(r.remaining)}>Kalanın tamamı</button>
        </div>
        {too && <div className="form-error">Ödeme, kalan borçtan büyük olamaz.</div>}
        <div className="field">
          <label>Nasıl ödendi?</label>
          <div className="segment">
            {(["nakit", "kredi_karti", "havale"] as Method[]).map((m) => (
              <button key={m} className={method === m ? "on" : ""} onClick={() => setMethod(m)}>{methodLabels[m]}</button>
            ))}
          </div>
        </div>
        <Field label="Not (isteğe bağlı)"><input className="input" value={note} onChange={(e) => setNote(e.target.value)} /></Field>
        <label className="checkbox">
          <input type="checkbox" checked={printReceipt} onChange={(e) => setPrintReceipt(e.target.checked)} /> Kaydettikten sonra makbuzu aç
        </label>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy || amount <= 0 || too} onClick={save}>{tl(amount)} Ödemeyi Kaydet</button>
      </div>
    </Modal>
  );
}

// ---------------- İade ----------------
function RefundModal({ detail, onClose, onDone }: { detail: ReservationDetail; onClose: () => void; onDone: () => void }) {
  const r = detail.summary;
  const [amount, setAmount] = useState(0);
  const [method, setMethod] = useState<Method>("nakit");
  const [reason, setReason] = useState("");
  const { run, busy } = useAction();
  const save = async () => {
    if (await run(() => api.refund(r.id, { amount, method, reason }), "İade kaydedildi.")) onDone();
  };
  return (
    <Modal onClose={onClose}>
      <h2>Müşteriye iade</h2>
      <p className="muted" style={{ marginBottom: 18 }}>Müşterinin şimdiye kadar ödediği: <b>{tl(r.paid)}</b></p>
      <div className="stack" style={{ gap: 16 }}>
        <Field label="İade edilen tutar"><MoneyInput value={amount} onChange={setAmount} autoFocus /></Field>
        {amount > r.paid && <div className="form-error">İade, ödenen tutardan büyük olamaz.</div>}
        <div className="field">
          <label>Nasıl iade edildi?</label>
          <div className="segment">
            {(["nakit", "kredi_karti", "havale"] as Method[]).map((m) => (
              <button key={m} className={method === m ? "on" : ""} onClick={() => setMethod(m)}>{methodLabels[m]}</button>
            ))}
          </div>
        </div>
        <Field label="İade nedeni"><input className="input" value={reason} onChange={(e) => setReason(e.target.value)} /></Field>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-danger-solid" disabled={busy || amount <= 0 || amount > r.paid || !reason.trim()} onClick={save}>İadeyi Kaydet</button>
      </div>
    </Modal>
  );
}

// ---------------- Tarih / salon değiştir ----------------
const KINDS: EventKind[] = ["dugun", "nisan", "kina", "soz", "sunnet", "kokteyl", "diger"];

function EditModal({ detail, onClose, onDone }: { detail: ReservationDetail; onClose: () => void; onDone: () => void }) {
  const r = detail.summary;
  const [date, setDate] = useState(r.date);
  const [start, setStart] = useState(r.start);
  const [end, setEnd] = useState(r.end);
  const [hallId, setHallId] = useState(r.hallId);
  const [kind, setKind] = useState<EventKind>(r.kind);
  const [guests, setGuests] = useState(r.guestCount);
  const [notes, setNotes] = useState(detail.notes ?? "");
  const [reprice, setReprice] = useState(true);
  const [halls, setHalls] = useState<HallAvailability[] | null>(null);
  const [error, setError] = useState("");
  const { run, busy } = useAction();

  const check = async (d = date, s = start, e = end) => {
    try { setHalls(await api.availability(d, s, e, r.id)); } catch (err) { setError(errorText(err)); }
  };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  useEffect(() => { void check(); }, []);

  // Kesin kayıtta kişi başı fiyatlı paket varsa, davetli sayısı değişince yeni fiyatı gösterip sorar.
  // Hesap satış anındaki fiyatlarla yapılır (sunucudaki reprice ile aynı).
  const pricing = detail.packagePricing;
  const newPackagePrice = packageTotal(pricing, guests);
  const priceChange = r.status === "kesin" && pricing.perGuestPrice > 0 && guests > 0 && guests !== r.guestCount
    && newPackagePrice !== detail.packagePrice
    ? { newTotal: r.total + newPackagePrice - detail.packagePrice, billed: billedGuests(pricing, guests) }
    : null;
  const willReprice = !!priceChange && reprice;

  const save = async () => {
    const body = { hallId, date, start, end, kind, guestCount: guests, notes: notes || null, reprice: willReprice };
    const message = willReprice ? "Değişiklikler kaydedildi, fiyat ve ödeme planı güncellendi." : "Değişiklikler kaydedildi.";
    if (await run(() => api.updateReservation(r.id, body), message)) onDone();
  };

  return (
    <Modal onClose={onClose} wide>
      <h2>Tarih, saat veya salonu değiştir</h2>
      <div className="stack" style={{ gap: 16, marginTop: 16 }}>
        <div className="grid grid-3" style={{ gap: 14 }}>
          <Field label="Tarih"><input className="input" type="date" value={date} min={today()} onChange={(e) => { setDate(e.target.value); void check(e.target.value); }} /></Field>
          <Field label="Başlangıç"><TimeSelect value={start} onChange={(v) => { setStart(v); void check(date, v); }} /></Field>
          <Field label="Bitiş"><TimeSelect value={end} onChange={(v) => { setEnd(v); void check(date, start, v); }} from={10} extra={4} /></Field>
        </div>
        <div className="field">
          <label>Salon</label>
          <div className="grid grid-2" style={{ gap: 10 }}>
            {(halls ?? []).map((h) => (
              <button key={h.hallId} className={`choice ${hallId === h.hallId ? "selected" : ""}`} disabled={!h.isFree} onClick={() => setHallId(h.hallId)} style={{ padding: "12px 14px" }}>
                <div>
                  <div className="choice-title">{h.hallName}</div>
                  <div className="small" style={{ color: h.isFree ? "var(--success)" : "var(--danger)" }}>{h.isFree ? "Boş" : `Dolu: ${h.busyWith}`}</div>
                </div>
              </button>
            ))}
          </div>
        </div>
        <div className="grid grid-2" style={{ gap: 14 }}>
          <Field label="Organizasyon türü">
            <select className="select" value={kind} onChange={(e) => setKind(e.target.value as EventKind)}>
              {KINDS.map((k) => <option key={k} value={k}>{kindLabels[k]}</option>)}
            </select>
          </Field>
          <Field label="Davetli sayısı"><NumberInput value={guests} onChange={setGuests} /></Field>
        </div>
        {priceChange && (
          <div className="card" style={{ background: "var(--primary-soft)", boxShadow: "none" }}>
            <div className="strong">Davetli sayısı değişti, paket fiyatı da değişiyor</div>
            <div className="price-line" style={{ marginTop: 8 }}>
              <span className="k">Şimdiki toplam ({r.guestCount} davetli)</span><span>{tl(r.total)}</span>
            </div>
            <div className="price-line strong">
              <span>Yeni toplam ({guests} davetli{priceChange.billed > guests ? `, ${priceChange.billed} kişi üzerinden` : ""})</span>
              <span>{tl(priceChange.newTotal)}</span>
            </div>
            <div className="muted small" style={{ marginBottom: 10 }}>{tariffText(pricing)} · sözleşmedeki fiyatlarla</div>
            <label className="checkbox">
              <input type="checkbox" checked={reprice} onChange={(e) => setReprice(e.target.checked)} />
              <span><b>Fiyatı ve ödeme planını güncelle</b><br />
                <span className="muted small">
                  {priceChange.newTotal > r.total
                    ? `Fark (${tl(priceChange.newTotal - r.total)}) ödenmemiş son taksite eklenir.`
                    : `Fark (${tl(r.total - priceChange.newTotal)}) ödenmemiş taksitlerden düşülür.`}
                  {" "}İşaretlemezseniz sözleşme tutarı değişmez.
                </span>
              </span>
            </label>
            {willReprice && priceChange.newTotal < r.paid && (
              <Alert kind="danger">Müşteri şimdiye kadar {tl(r.paid)} ödedi; yeni tutar bundan az olamaz. Önce iade yapın.</Alert>
            )}
          </div>
        )}
        <Field label="Notlar"><textarea className="textarea" value={notes} onChange={(e) => setNotes(e.target.value)} /></Field>
        {error && <Alert kind="danger">{error}</Alert>}
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy} onClick={save}>Kaydet</button>
      </div>
    </Modal>
  );
}

// ---------------- Ödeme planını değiştir ----------------
function PlanModal({ detail, onClose, onDone }: { detail: ReservationDetail; onClose: () => void; onDone: () => void }) {
  const total = detail.summary.total;
  const [rows, setRows] = useState<InstallmentInput[]>(detail.installments.map((i) => ({ label: i.label, dueDate: i.dueDate, amount: i.amount })));
  const { run, busy } = useAction();
  const sum = rows.reduce((s, x) => s + x.amount, 0);
  const diff = total - sum;
  const update = (index: number, patch: Partial<InstallmentInput>) => setRows(rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));

  const save = async () => {
    if (await run(() => api.setPlan(detail.summary.id, rows), "Ödeme planı güncellendi.")) onDone();
  };

  return (
    <Modal onClose={onClose} wide>
      <h2>Ödeme planını değiştir</h2>
      <p className="muted">Taksitlerin toplamı sözleşme tutarına (<b>{tl(total)}</b>) eşit olmalı. Daha önce alınan ödemeler sırayla düşülür.</p>
      <div className="stack" style={{ gap: 10, marginTop: 16 }}>
        {rows.map((row, i) => (
          <div key={i} className="row" style={{ flexWrap: "nowrap" }}>
            <input className="input" style={{ flex: 2 }} value={row.label} onChange={(e) => update(i, { label: e.target.value })} />
            <input className="input" style={{ flex: 2 }} type="date" value={row.dueDate} onChange={(e) => update(i, { dueDate: e.target.value })} />
            <div style={{ flex: 2 }}><MoneyInput value={row.amount} onChange={(v) => update(i, { amount: v })} /></div>
            <button className="btn icon-btn" title="Satırı sil" onClick={() => setRows(rows.filter((_, j) => j !== i))}><Icon name="trash" size={18} /></button>
          </div>
        ))}
        <div className="row">
          <button className="btn btn-ghost" onClick={() => setRows([...rows, { label: `${rows.length}. taksit`, dueDate: today(), amount: Math.max(diff, 0) }])}>
            <Icon name="plus" size={18} /> Taksit ekle
          </button>
          <span className="spacer" />
          {diff !== 0 ? (
            <span style={{ color: "var(--danger)", fontWeight: 600 }}>
              {diff > 0 ? `${tl(diff)} eksik` : `${tl(-diff)} fazla`}
              {rows.length > 0 && <button className="btn btn-ghost" onClick={() => update(rows.length - 1, { amount: rows[rows.length - 1].amount + diff })}>Son taksite ekle</button>}
            </span>
          ) : <span style={{ color: "var(--success)", fontWeight: 600 }}><Icon name="check" size={16} /> Toplam tutuyor</span>}
        </div>
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy || diff !== 0 || rows.length === 0} onClick={save}>Planı Kaydet</button>
      </div>
    </Modal>
  );
}

// ---------------- Ön rezervasyon süresini uzat ----------------
function ExtendModal({ detail, onClose, onDone }: { detail: ReservationDetail; onClose: () => void; onDone: () => void }) {
  const [days, setDays] = useState(7);
  const { run, busy } = useAction();
  return (
    <Modal onClose={onClose}>
      <h2>Ön rezervasyon süresini uzat</h2>
      <p className="muted" style={{ marginBottom: 18 }}>Tarih bugünden itibaren seçtiğiniz süre kadar daha {detail.summary.customerName} adına tutulur.</p>
      <div className="segment">
        {[3, 7, 15, 30].map((d) => <button key={d} className={days === d ? "on" : ""} onClick={() => setDays(d)}>{d} gün</button>)}
      </div>
      <div className="modal-actions">
        <button className="btn" onClick={onClose}>Vazgeç</button>
        <button className="btn btn-primary" disabled={busy}
          onClick={async () => { if (await run(() => api.extend(detail.summary.id, days), "Süre uzatıldı.")) onDone(); }}>Süreyi Uzat</button>
      </div>
    </Modal>
  );
}

// ---------------- Ön rezervasyonu kesinleştir ----------------
function ConvertOption({ detail, onDone, onCancel }: { detail: ReservationDetail; onDone: () => void; onCancel: () => void }) {
  const catalog = useLoad(async () => ({ packages: await api.packages(), extras: await api.extras() }));
  const [sale, setSale] = useState<SaleState>(emptySale());
  const [step, setStep] = useState<"package" | "payment">("package");
  const { run, busy } = useAction();
  const { navigate } = useSession();
  if (!catalog.data) return <div className="content"><Loading /></div>;
  const r = detail.summary;
  const { total } = saleTotals(sale, catalog.data.packages, catalog.data.extras, r.guestCount);

  const save = async () => {
    let paymentId: string | null = null;
    const ok = await run(async () => { paymentId = await api.confirm(r.id, toSaleInput(sale, total, today(), r.date)); }, "Kayıt kesinleşti.");
    if (ok) {
      sessionStorage.setItem("salonera-just-created", r.id);
      onDone();
      if (paymentId) navigate({ name: "print", kind: "receipt", id: paymentId });
    }
  };

  return (
    <>
      <div className="content">
        <div className="content-wide stack">
          <div>
            <h2 style={{ fontSize: 24, fontWeight: 600 }}>{r.customerName} – kesin kayda çevir</h2>
            <p className="muted">{dateWithDay(r.date)} · {r.hallName}</p>
          </div>
          {step === "package"
            ? <PackagePicker sale={sale} onChange={setSale} packages={catalog.data.packages} extras={catalog.data.extras} guests={r.guestCount} />
            : <PaymentPlanner sale={sale} onChange={setSale} total={total} todayIso={today()} eventIso={r.date} />}
        </div>
      </div>
      <div className="wizard-foot">
        <button className="btn" onClick={step === "package" ? onCancel : () => setStep("package")}><Icon name="back" size={18} /> {step === "package" ? "Vazgeç" : "Geri"}</button>
        <span className="spacer" />
        {step === "package" ? (
          <button className="btn btn-primary" disabled={!sale.packageId || total <= 0}
            onClick={() => { setSale((s) => ({ ...s, deposit: s.deposit || suggestDeposit(total) })); setStep("payment"); }}>
            Devam Et <Icon name="next" />
          </button>
        ) : (
          <button className="btn btn-primary" disabled={busy || sale.deposit > total} onClick={save}><Icon name="check" /> Kesinleştir ve Kaydet</button>
        )}
      </div>
    </>
  );
}
