import type { CSSProperties, ReactNode } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { LoadError, Loading, useLoad } from "../components/ui";
import { dateLong, dateTime, dateWithDay, kindLabels, methodLabels, tl, today } from "../format";
import type { BusinessSettings, Customer, Payment, Program, ReservationDetail } from "../types";
import { breakdownText } from "../pricing";
import { moneyWords } from "../words";

const paper: CSSProperties = {
  width: "210mm", minHeight: "297mm", margin: "0 auto", background: "#fff", padding: "16mm 16mm 14mm",
  boxShadow: "var(--shadow-lg)", color: "#2b2024", fontSize: "10.5pt", lineHeight: 1.45,
};

/** Yazdırılacak sözleşme veya makbuz. Üstteki çubuk yazıcıya çıkmaz. */
export function PrintPage({ kind, id }: { kind: "contract" | "receipt" | "program"; id: string }) {
  const { back } = useSession();
  const data = useLoad(async () => {
    const settings = await api.settings();
    if (kind === "contract" || kind === "program") {
      const [detail, program] = await Promise.all([api.reservation(id), kind === "program" ? api.program(id) : null]);
      const customer = (await api.customer(detail.summary.customerId)).customer;
      return { settings, detail, customer, payment: null as Payment | null, program };
    }
    const payment = await api.payment(id);
    const detail = await api.reservation(payment.reservationId);
    const customer = (await api.customer(detail.summary.customerId)).customer;
    return { settings, detail, customer, payment, program: null as Program | null };
  }, [kind, id]);

  return (
    <div className="print-shell" style={{ height: "100%", overflow: "auto", background: "#e9e2db" }}>
      <div className="no-print" style={{ position: "sticky", top: 0, zIndex: 5, background: "var(--sidebar)", padding: "12px 24px", display: "flex", gap: 12, alignItems: "center" }}>
        <button className="btn" onClick={back}><Icon name="back" size={18} /> Geri</button>
        <span style={{ color: "#fff", fontSize: 18, fontWeight: 600, flex: 1 }}>{{ contract: "Sözleşme", receipt: "Makbuz", program: "Organizasyon planı" }[kind]}</span>
        <span style={{ color: "#c9aeb8" }}>PDF olarak kaydetmek için yazıcı olarak “Microsoft Print to PDF” seçin.</span>
        <button className="btn btn-gold" disabled={!data.data} onClick={() => window.print()}><Icon name="printer" /> Yazdır</button>
      </div>
      <div className="print-pad" style={{ padding: "24px 0 48px" }}>
        {data.error && <div style={{ maxWidth: 800, margin: "0 auto" }}><LoadError message={data.error} onRetry={data.reload} /></div>}
        {!data.data ? !data.error && <Loading /> : (
          <div style={paper} className="paper">
            {kind === "program"
              ? <ProgramSheet settings={data.data.settings} detail={data.data.detail} customer={data.data.customer} program={data.data.program!} />
              : kind === "contract"
              ? <Contract settings={data.data.settings} detail={data.data.detail} customer={data.data.customer} />
              : <Receipt settings={data.data.settings} detail={data.data.detail} customer={data.data.customer} payment={data.data.payment!} />}
          </div>
        )}
      </div>
      <style>{`@media print { .paper { box-shadow: none !important; width: auto !important; min-height: 0 !important; padding: 0 !important; } .print-shell { background: #fff !important; overflow: visible !important; height: auto !important; } .print-pad { padding: 0 !important; } }`}</style>
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
    <div style={{ display: "grid", gridTemplateColumns: "1fr 1fr", gap: 40, marginTop: 36, breakInside: "avoid" }}>
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
          <tr>
            <td style={cell}>
              <b>{detail.packageName}</b>
              {breakdownText(detail.packagePricing, detail.packagePrice) && (
                <div style={{ fontSize: "9pt", color: "#5a4a50" }}>{breakdownText(detail.packagePricing, detail.packagePrice)}</div>
              )}
            </td>
            <td style={num}>{tl(detail.packagePrice)}</td>
          </tr>
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

function ProgramSheet({ settings, detail, customer, program }: { settings: BusinessSettings; detail: ReservationDetail; customer: Customer; program: Program }) {
  const r = detail.summary;
  const th: CSSProperties = { ...cell, textAlign: "left" };
  const row: CSSProperties = { ...cell, padding: "3px 8px" }; // sıkı satırlar: plan tek sayfaya sığsın
  return (
    <>
      <Letterhead settings={settings} title="ORGANİZASYON PLANI" no={r.no} date={dateLong(r.date)} />
      <div style={h3}>Organizasyon</div>
      <InfoGrid rows={[
        ["Müşteri", customer.fullName],
        ["Telefon", [customer.phone, customer.phone2].filter(Boolean).join(" / ")],
        ...(customer.brideName || customer.groomName ? [["Gelin / damat", [customer.brideName, customer.groomName].filter(Boolean).join(" & ")] as [string, string]] : []),
        ["Tarih", dateWithDay(r.date)],
        ["Saat", `${r.start} – ${r.end}`],
        ["Salon", r.hallName],
        ["Türü", kindLabels[r.kind]],
        ["Davetli sayısı", `${r.guestCount} kişi`],
        ...(detail.packageName ? [["Paket", detail.packageName] as [string, string]] : []),
      ]} />

      <div style={h3}>Saatlik akış</div>
      {program.items.length === 0 ? <div style={{ color: "#7a6a6f" }}>Akış henüz belirlenmedi.</div> : (
        <table style={{ width: "100%", borderCollapse: "collapse" }}>
          <thead><tr style={{ background: "#f6f0e9" }}><th style={{ ...th, width: "16mm" }}>Saat</th><th style={th}>Ne yapılacak</th><th style={th}>Not</th></tr></thead>
          <tbody>
            {program.items.map((it, i) => (
              <tr key={i} style={{ breakInside: "avoid" }}>
                <td style={{ ...row, fontWeight: 700, color: "#8a2846", fontSize: "11pt" }}>{it.time}</td>
                <td style={{ ...row, fontWeight: 600 }}>{it.title}</td>
                <td style={{ ...row, color: "#5a4a50" }}>{it.note}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      <div style={h3}>Müzikler</div>
      {program.music.length === 0 ? <div style={{ color: "#7a6a6f" }}>Müzik seçilmedi.</div> : (
        <table style={{ width: "100%", borderCollapse: "collapse", tableLayout: "fixed" }}>
          <thead><tr style={{ background: "#f6f0e9" }}><th style={{ ...th, width: "28%" }}>Ne zaman</th><th style={{ ...th, width: "30%" }}>Şarkı</th><th style={th}>Bağlantı</th></tr></thead>
          <tbody>
            {program.music.map((m, i) => (
              <tr key={i} style={{ breakInside: "avoid" }}>
                <td style={{ ...row, fontWeight: 600 }}>{m.label}</td>
                <td style={row}>{m.song}</td>
                <td style={{ ...row, fontSize: "8.5pt", wordBreak: "break-all", color: "#5a4a50" }}>{m.link}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {(program.notes || detail.notes) && (
        <>
          <div style={h3}>Notlar</div>
          {program.notes && <div style={{ whiteSpace: "pre-wrap" }}>{program.notes}</div>}
          {detail.notes && <div style={{ whiteSpace: "pre-wrap", marginTop: program.notes ? 6 : 0 }}>{detail.notes}</div>}
        </>
      )}
      <Signatures left="Müşteri onayı" right={settings.businessName} />
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
