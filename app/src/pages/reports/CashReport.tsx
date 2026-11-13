import { useState } from "react";
import { useSession } from "../../App";
import { api } from "../../api";
import { BarChart, compactTl, Donut, HBars } from "../../components/Charts";
import { LoadError, Loading, StatCard, useLoad } from "../../components/ui";
import { addDays, dateLong, dateTime, isoDate, methodLabels, parseDate, today } from "../../format";
import { bucket, dailyTotals, netAmount } from "../../reportData";
import type { Method } from "../../types";
import { tl } from "../../format";
import { downloadCsv, plain, ReportShell, Section } from "./common";

type Preset = "today" | "yesterday" | "week" | "month" | "lastMonth" | "custom";

function presetRange(p: Preset): { from: string; to: string } {
  const now = today();
  const d = parseDate(now);
  switch (p) {
    case "today": return { from: now, to: now };
    case "yesterday": return { from: addDays(now, -1), to: addDays(now, -1) };
    case "week": return { from: addDays(now, -((d.getDay() + 6) % 7)), to: now };
    case "month": return { from: isoDate(new Date(d.getFullYear(), d.getMonth(), 1)), to: now };
    case "lastMonth": return { from: isoDate(new Date(d.getFullYear(), d.getMonth() - 1, 1)), to: isoDate(new Date(d.getFullYear(), d.getMonth(), 0)) };
    default: return { from: now, to: now };
  }
}

const METHODS: Method[] = ["nakit", "kredi_karti", "havale"];

/** Kasa raporu: bir gün (gün sonu) veya dönem için alınan ödemeler, iadeler, ödeme şekli ve kişi bazında. */
export function CashReport() {
  const { navigate } = useSession();
  const [preset, setPreset] = useState<Preset>("today");
  const [custom, setCustom] = useState(presetRange("month"));
  const { from, to } = preset === "custom" ? custom : presetRange(preset);
  const list = useLoad(() => api.payments(from, to), [from, to]);
  const pays = list.data ?? [];
  const active = pays.filter((p) => !p.isCancelled);
  const byMethod = METHODS.map((m) => ({ method: m, amount: active.filter((p) => p.method === m).reduce((s, p) => s + netAmount(p), 0) }));
  const incoming = active.filter((p) => !p.isRefund).reduce((s, p) => s + p.amount, 0);
  const refunds = active.filter((p) => p.isRefund).reduce((s, p) => s + p.amount, 0);
  const net = incoming - refunds;
  const period = from === to ? dateLong(from) : `${dateLong(from)} – ${dateLong(to)}`;
  const days = from === to ? [] : dailyTotals(pays, from, to);

  const excel = () => downloadCsv(`salonera-kasa-${from}${from === to ? "" : "_" + to}.csv`, [
    [`Kasa raporu: ${period}`],
    ...byMethod.map((m) => [methodLabels[m.method], plain(m.amount)]),
    ["Giriş", plain(incoming)], ["İade", plain(-refunds)], ["Net kasa", plain(net)],
    [],
    ["Tarih", "Makbuz", "Kayıt no", "Müşteri", "Şekli", "Alan", "Not", "Tutar (TL)", "Durum"],
    ...pays.map((p) => [dateTime(p.date), p.no, p.reservationNo, p.customerName, methodLabels[p.method], p.createdBy, p.note ?? "",
      plain(p.isRefund ? -p.amount : p.amount), p.isCancelled ? `İptal: ${p.cancelReason ?? ""}` : p.isRefund ? "İade" : ""]),
  ]);

  const presets: [Preset, string][] = [["today", "Bugün"], ["yesterday", "Dün"], ["week", "Bu hafta"], ["month", "Bu ay"], ["lastMonth", "Geçen ay"], ["custom", "Tarih seç"]];

  return (
    <ReportShell title={from === to ? "Gün sonu kasa raporu" : "Kasa raporu"} period={period} ready={!!list.data} onExcel={excel}
      controls={
        <>
          <div className="segment">
            {presets.map(([id, label]) => <button key={id} className={preset === id ? "on" : ""} onClick={() => setPreset(id)}>{label}</button>)}
          </div>
          {preset === "custom" && (
            <div className="row" style={{ gap: 8 }}>
              <input type="date" className="input" style={{ width: 170 }} value={custom.from} max={custom.to}
                onChange={(e) => e.target.value && setCustom({ ...custom, from: e.target.value })} />
              <span className="muted">–</span>
              <input type="date" className="input" style={{ width: 170 }} value={custom.to} min={custom.from}
                onChange={(e) => e.target.value && setCustom({ ...custom, to: e.target.value })} />
            </div>
          )}
        </>
      }>
      {list.error && <LoadError message={list.error} onRetry={list.reload} />}
      {list.loading && !list.data ? <Loading /> : (
        <>
          <div className="grid grid-4 report-kpis">
            <StatCard icon="money" tone="green" label="Nakit" value={tl(byMethod[0].amount, true)} />
            <StatCard icon="wallet" tone="plum" label="Kredi kartı" value={tl(byMethod[1].amount, true)} />
            <StatCard icon="building" tone="wine" label="Havale / EFT" value={tl(byMethod[2].amount, true)} />
            <StatCard icon="check" tone="gold" label="Net kasa" value={tl(net, true)}
              sub={`${active.filter((p) => !p.isRefund).length} makbuz${refunds ? ` · ${tl(refunds, true)} iade` : ""}`} />
          </div>

          <div className="grid grid-2">
            <Section title="Ödeme şekline göre">
              <Donut parts={byMethod.map((m) => ({ label: methodLabels[m.method], value: m.amount }))} center={compactTl(net)} sub="net" format={(n) => tl(n, true)} />
            </Section>
            <Section title="Ödemeyi alan kişi" sub="Kim ne kadar tahsil etti">
              <HBars rows={bucket(active, (p) => p.createdBy, netAmount)} value="amount" cls="s-gold"
                right={(r) => <><b>{tl(r.amount, true)}</b> · {r.count} işlem</>} empty="Bu dönemde ödeme yok." />
            </Section>
          </div>

          {days.length > 1 && (
            <Section title="Günlere göre" sub="Her gün kasaya giren net para">
              <BarChart labels={days.map((d) => days.length > 31 ? d.date.slice(5).split("-").reverse().join(".") : String(parseDate(d.date).getDate()))}
                format={compactTl} height={200} series={[{ name: "Tahsilat", cls: "s-gold", values: days.map((d) => d.amount) }]} />
            </Section>
          )}

          <Section title="Makbuzlar" sub={`${pays.length} kayıt`}>
            {pays.length === 0 ? <div className="muted">Bu dönemde ödeme alınmadı.</div> : (
              <table className="table">
                <thead><tr><th>Tarih</th><th>Makbuz</th><th>Müşteri</th><th>Şekli</th><th>Alan</th><th className="num">Tutar</th></tr></thead>
                <tbody>
                  {pays.map((p) => (
                    <tr key={p.id} className={`clickable ${p.isCancelled ? "cancelled" : ""}`} onClick={() => navigate({ name: "reservation", id: p.reservationId })}>
                      <td>{dateTime(p.date)}</td>
                      <td>{p.no}{p.isRefund && <span className="badge badge-warning" style={{ marginLeft: 8 }}>İade</span>}{p.isCancelled && <span className="badge badge-muted" style={{ marginLeft: 8 }}>İptal</span>}</td>
                      <td className="strong">{p.customerName}<div className="muted small">{p.reservationNo}{p.note ? ` · ${p.note}` : ""}</div></td>
                      <td><span className={`method m-${p.method}`}>{methodLabels[p.method]}</span></td>
                      <td className="muted">{p.createdBy}</td>
                      <td className="num strong" style={{ color: p.isRefund ? "var(--danger)" : undefined }}>{p.isRefund ? "− " : ""}{tl(p.amount)}</td>
                    </tr>
                  ))}
                </tbody>
                <tfoot>
                  <tr><td colSpan={5} className="strong">Giriş</td><td className="num strong">{tl(incoming)}</td></tr>
                  {refunds > 0 && <tr><td colSpan={5} className="strong">İade</td><td className="num strong" style={{ color: "var(--danger)" }}>− {tl(refunds)}</td></tr>}
                  <tr><td colSpan={5} className="strong">Net kasa</td><td className="num strong" style={{ fontSize: 18 }}>{tl(net)}</td></tr>
                </tfoot>
              </table>
            )}
          </Section>
          <div className="print-only report-sign">
            <div>Teslim eden<br /><br />İmza</div>
            <div>Teslim alan<br /><br />İmza</div>
          </div>
        </>
      )}
    </ReportShell>
  );
}
