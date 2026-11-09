import type { CSSProperties, ReactNode } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { LoadError, Loading, useLoad } from "../components/ui";
import { dateLong, dateTime, dateWithDay, kindLabels, methodLabels, tl, today } from "../format";
import type { BusinessSettings, Customer, Payment, ReservationDetail } from "../types";
import { moneyWords } from "../words";

const paper: CSSProperties = {
  width: "210mm", minHeight: "297mm", margin: "0 auto", background: "#fff", padding: "16mm 16mm 14mm",
  boxShadow: "var(--shadow-lg)", color: "#2b2024", fontSize: "10.5pt", lineHeight: 1.45,
};

/** Yazdırılacak sözleşme veya makbuz. Üstteki çubuk yazıcıya çıkmaz. */
export function PrintPage({ kind, id }: { kind: "contract" | "receipt"; id: string }) {
  const { back } = useSession();
  const data = useLoad(async () => {
    const settings = await api.settings();
    if (kind === "contract") {
      const detail = await api.reservation(id);
      const customer = (await api.customer(detail.summary.customerId)).customer;
      return { settings, detail, customer, payment: null as Payment | null };
    }
    const payment = await api.payment(id);
    const detail = await api.reservation(payment.reservationId);
    const customer = (await api.customer(detail.summary.customerId)).customer;
    return { settings, detail, customer, payment };
  }, [kind, id]);

  return (
    <div style={{ height: "100%", overflow: "auto", background: "#e9e2db" }}>
      <div className="no-print" style={{ position: "sticky", top: 0, zIndex: 5, background: "var(--sidebar)", padding: "12px 24px", display: "flex", gap: 12, alignItems: "center" }}>
        <button className="btn" onClick={back}><Icon name="back" size={18} /> Geri</button>
        <span style={{ color: "#fff", fontSize: 18, fontWeight: 600, flex: 1 }}>{kind === "contract" ? "Sözleşme" : "Makbuz"}</span>
        <span style={{ color: "#c9aeb8" }}>PDF olarak kaydetmek için yazıcı olarak “Microsoft Print to PDF” seçin.</span>
        <button className="btn btn-gold" disabled={!data.data} onClick={() => window.print()}><Icon name="printer" /> Yazdır</button>
      </div>
      <div style={{ padding: "24px 0 48px" }}>
        {data.error && <div style={{ maxWidth: 800, margin: "0 auto" }}><LoadError message={data.error} onRetry={data.reload} /></div>}
        {!data.data ? !data.error && <Loading /> : (
          <div style={paper} className="paper">
            {kind === "contract"
              ? <Contract settings={data.data.settings} detail={data.data.detail} customer={data.data.customer} />
              : <Receipt settings={data.data.settings} detail={data.data.detail} customer={data.data.customer} payment={data.data.payment!} />}
          </div>
        )}
      </div>
      <style>{`@media print { .paper { box-shadow: none !important; width: auto !important; min-height: 0 !important; padding: 0 !important; } div[style*="e9e2db"] { background: #fff !important; overflow: visible !important; } }`}</style>
    </div>
  );
}

function Letterhead({ settings, title, no, date }: { settings: BusinessSettings; title: string; no: string; date: string }) {
  return (
    <>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", borderBottom: "2px solid #8a2846", paddingBottom: 12 }}>
        <div>
          <div style={{ fontFamily: "Georgia, serif", fontSize: "20pt", color: "#8a2846" }}>{settings.businessName}</div>
          <div style={{ color: "#5a4a50", fontSize: "9pt", marginTop: 2 }}>
            {[settings.address, settings.phone].filter(Boolean).join(" · ")}
            {settings.taxOffice && <><br />{settings.taxOffice} V.D. {settings.taxNumber}</>}
          </div>
        </div>
        <div style={{ textAlign: "right" }}>
          <div style={{ fontWeight: 700, fontSize: "13pt", letterSpacing: ".04em" }}>{title}</div>
          <div style={{ fontSize: "9.5pt", color: "#5a4a50", marginTop: 4 }}>No: <b>{no}</b><br />Tarih: <b>{date}</b></div>
        </div>
      </div>
    </>
  );
}

const h3: CSSProperties = { fontSize: "10pt", fontWeight: 700, color: "#8a2846", letterSpacing: ".05em", margin: "16px 0 6px", textTransform: "uppercase" };
const cell: CSSProperties = { padding: "5px 8px", borderBottom: "1px solid #eadfd6" };
const num: CSSProperties = { ...cell, textAlign: "right", whiteSpace: "nowrap" };

function InfoGrid({ rows }: { rows: [string, ReactNode][] }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: "4px 24px" }}>
      {rows.map(([k, v]) => (
        <div key={k} style={{ display: "flex", gap: 8, padding: "3px 0", borderBottom: "1px dotted #dccfc4" }}>
          <span style={{ color: "#7a6a6f", minWidth: 110 }}>{k}</span><b>{v}</b>
        </div>
      ))}
    </div>
  );
}

function Signatures({ left, right }: { left: string; right: string }) {
  return (
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 40, marginTop: 36 }}>
      {[left, right].map((label) => (
        <div key={label} style={{ textAlign: "center" }}>
          <div style={{ height: 56 }} />
          <div style={{ borderTop: "1px solid #2b2024", paddingTop: 6, fontWeight: 600 }}>{label}</div>
          <div style={{ fontSize: "8.5pt", color: "#7a6a6f" }}>Ad soyad / imza</div>
        </div>
      ))}
    </div>
  );
}

function Contract({ settings, detail, customer }: { settings: BusinessSettings; detail: ReservationDetail; customer: Customer }) {
  const r = detail.summary;
  const gross = detail.packagePrice + detail.extras.reduce((s, e) => s + e.price, 0);
  return (
    <>
      <Letterhead settings={settings} title="ORGANİZASYON SÖZLEŞMESİ" no={r.no} date={dateLong(detail.confirmedAt?.slice(0, 10) ?? today())} />
      <div style={h3}>Müşteri</div>
      <InfoGrid rows={[
        ["Ad soyad", customer.fullName],
        ["Telefon", [customer.phone, customer.phone2].filter(Boolean).join(" / ")],
        ...(customer.brideName || customer.groomName ? [["Gelin / damat", [customer.brideName, customer.groomName].filter(Boolean).join(" & ")] as [string, string]] : []),
      ]} />
      <div style={h3}>Organizasyon</div>
      <InfoGrid rows={[
        ["Tarih", dateWithDay(r.date)],
        ["Saat", `${r.start} – ${r.end}`],
        ["Salon", r.hallName],
        ["Türü", kindLabels[r.kind]],
        ["Davetli sayısı", `${r.guestCount} kişi`],
      ]} />
      <div style={h3}>Paket ve ücret</div>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <tbody>
          <tr><td style={cell}><b>{detail.packageName}</b></td><td style={num}>{tl(detail.packagePrice)}</td></tr>
          {detail.extras.map((e) => <tr key={e.name}><td style={cell}>{e.name}</td><td style={num}>{tl(e.price)}</td></tr>)}
          {detail.discount > 0 && <tr><td style={cell}>Ara toplam</td><td style={num}>{tl(gross)}</td></tr>}
          {detail.discount > 0 && <tr><td style={cell}>İndirim</td><td style={num}>− {tl(detail.discount)}</td></tr>}
          <tr style={{ background: "#f8efdc" }}><td style={{ ...cell, fontWeight: 700 }}>TOPLAM (KDV dahil)</td><td style={{ ...num, fontWeight: 700, fontSize: "12pt" }}>{tl(r.total)}</td></tr>
        </tbody>
      </table>
      <div style={{ fontSize: "9pt", color: "#5a4a50", marginTop: 4 }}>Yazıyla: {moneyWords(r.total)}</div>
      <div style={h3}>Ödeme planı</div>
      <table style={{ width: "100%", borderCollapse: "collapse" }}>
        <thead><tr style={{ background: "#f6f0e9" }}><th style={{ ...cell, textAlign: "left" }}>Ödeme</th><th style={{ ...cell, textAlign: "left" }}>Son ödeme günü</th><th style={num}>Tutar</th><th style={num}>Durum</th></tr></thead>
        <tbody>
          {detail.installments.map((i) => (
            <tr key={i.id}><td style={cell}>{i.label}</td><td style={cell}>{dateLong(i.dueDate)}</td><td style={num}>{tl(i.amount)}</td>
              <td style={num}>{i.state === "odendi" ? "Ödendi" : i.paid > 0 ? `${tl(i.paid)} ödendi` : ""}</td></tr>
          ))}
        </tbody>
      </table>
      <div style={{ display: "flex", justifyContent: "flex-end", gap: 24, marginTop: 6, fontSize: "10pt" }}>
        <span>Ödenen: <b>{tl(r.paid)}</b></span><span>Kalan: <b>{tl(r.remaining)}</b></span>
      </div>
      {detail.notes && (<><div style={h3}>Notlar</div><div style={{ whiteSpace: "pre-wrap" }}>{detail.notes}</div></>)}
      <div style={h3}>Sözleşme şartları</div>
      <div style={{ whiteSpace: "pre-wrap", fontSize: "9.5pt" }}>{settings.contractTerms}</div>
      <p style={{ fontSize: "9pt", marginTop: 12 }}>İşbu sözleşme iki nüsha olarak düzenlenmiş ve taraflarca okunarak imzalanmıştır.</p>
      <Signatures left="Müşteri" right={settings.businessName} />
    </>
  );
}

function Receipt({ settings, detail, customer, payment }: { settings: BusinessSettings; detail: ReservationDetail; customer: Customer; payment: Payment }) {
  const r = detail.summary;
  const refund = payment.isRefund;
  const half = (copy: string) => (
    <div style={{ border: "1px solid #dccfc4", borderRadius: 8, padding: "12mm 10mm", position: "relative" }}>
      <div style={{ position: "absolute", top: 8, right: 12, fontSize: "8pt", color: "#7a6a6f" }}>{copy}</div>
      <Letterhead settings={settings} title={refund ? "İADE MAKBUZU" : "TAHSİLAT MAKBUZU"} no={payment.no} date={dateTime(payment.date)} />
      {payment.isCancelled && <div style={{ color: "#b3261e", fontWeight: 700, fontSize: "14pt", margin: "8px 0" }}>İPTAL EDİLMİŞTİR — {payment.cancelReason}</div>}
      <div style={{ margin: "14px 0", padding: "12px 16px", background: "#f8efdc", borderRadius: 8, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
        <div>
          <div style={{ color: "#7a6a6f" }}>{refund ? "İade edilen tutar" : "Alınan tutar"}</div>
          <div style={{ fontSize: "9pt" }}>Yazıyla: {moneyWords(payment.amount)}</div>
        </div>
        <div style={{ fontSize: "18pt", fontWeight: 700, color: "#8a2846" }}>{tl(payment.amount)}</div>
      </div>
      <InfoGrid rows={[
        [refund ? "Ödenen kişi" : "Ödeyen", customer.fullName],
        ["Ödeme şekli", methodLabels[payment.method]],
        ["Organizasyon", `${dateLong(r.date)} · ${r.hallName}`],
        ["Kayıt no", r.no],
        ["Toplam tutar", tl(r.total)],
        ["Kalan borç", tl(r.remaining)],
        ...(payment.note ? [["Açıklama", payment.note] as [string, string]] : []),
      ]} />
      <Signatures left={refund ? "Teslim alan" : "Ödeyen"} right={`Teslim alan (${payment.createdBy})`} />
    </div>
  );
  return (
    <div style={{ display: "grid", gap: "10mm" }}>
      {half("İşletme nüshası")}
      <div style={{ borderTop: "1px dashed #b8893a", textAlign: "center", fontSize: "8pt", color: "#b8893a" }}>✂ buradan kesin</div>
      {half("Müşteri nüshası")}
    </div>
  );
}
