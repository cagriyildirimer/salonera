import { Fragment } from "react";
import { useSession } from "../../App";
import { api } from "../../api";
import { BarChart, compactTl } from "../../components/Charts";
import { Empty, LoadError, Loading, StatCard, useLoad } from "../../components/ui";
import { dateLong, installmentLabels, monthName, monthShort, tl, today } from "../../format";
import { dueByMonth } from "../../reportData";
import type { DueRow } from "../../types";
import { downloadCsv, plain, ReportShell, Section } from "./common";

const open = (r: DueRow) => r.amount - r.paid;
const monthTitle = (key: string) => `${monthName(Number(key.slice(5, 7)))} ${key.slice(0, 4)}`;

/** Alacaklar ve tahsilat takvimi: geciken taksitler ve önümüzdeki aylarda vadesi gelecek paralar. */
export function DueReport() {
  const { navigate } = useSession();
  const now = today();
  const due = useLoad(() => api.dueReport());
  const rows = due.data ?? [];
  const overdue = rows.filter((r) => r.dueDate < now);
  const months = dueByMonth(rows, now, 12);
  const thisMonth = rows.filter((r) => r.dueDate >= now && r.dueDate.slice(0, 7) === now.slice(0, 7));
  const next3 = months.slice(1, 4).reduce((s, m) => s + m.amount, 0);
  const totalOpen = rows.reduce((s, r) => s + open(r), 0);

  // Tablo: önce gecikenler, sonra ay ay.
  const groups: { title: string; rows: DueRow[]; danger?: boolean }[] = [];
  if (overdue.length) groups.push({ title: "Vadesi geçmiş (gecikmiş)", rows: overdue, danger: true });
  for (const r of rows.filter((x) => x.dueDate >= now)) {
    const title = monthTitle(r.dueDate.slice(0, 7));
    const g = groups.find((x) => x.title === title) ?? groups[groups.push({ title, rows: [] }) - 1];
    g.rows.push(r);
  }

  const excel = () => downloadCsv(`salonera-alacaklar-${now}.csv`, [
    [`Alacaklar ve tahsilat takvimi (${dateLong(now)})`],
    ["Vade", "Durum", "Müşteri", "Telefon", "Kayıt no", "Organizasyon tarihi", "Salon", "Taksit", "Tutar (TL)", "Ödenen (TL)", "Kalan (TL)"],
    ...rows.map((r) => [dateLong(r.dueDate), r.dueDate < now ? "Gecikmiş" : installmentLabels[r.state], r.customerName, r.customerPhone, r.no,
      dateLong(r.eventDate), r.hallName, r.label, plain(r.amount), plain(r.paid), plain(open(r))]),
    [],
    ["Ay", "Beklenen tahsilat (TL)", "Taksit sayısı"],
    ...months.map((m) => [m.overdue ? "Gecikmiş" : monthTitle(m.key), plain(m.amount), m.count]),
  ]);

  return (
    <ReportShell title="Alacaklar ve tahsilat takvimi" period={`${dateLong(now)} itibarıyla`} ready={!!due.data} onExcel={excel}>
      {due.error && <LoadError message={due.error} onRetry={due.reload} />}
      {due.loading && !due.data ? <Loading /> : (
        <>
          <div className="grid grid-4 report-kpis">
            <StatCard icon="alert" tone={overdue.length ? "danger" : "green"} label="Gecikmiş" value={tl(overdue.reduce((s, r) => s + open(r), 0), true)}
              sub={`${new Set(overdue.map((r) => r.reservationId)).size} müşteri`} />
            <StatCard icon="calendar" tone="wine" label="Bu ay vadesi gelecek" value={tl(thisMonth.reduce((s, r) => s + open(r), 0), true)} sub={`${thisMonth.length} taksit`} />
            <StatCard icon="clock" tone="plum" label="Önümüzdeki 3 ay" value={tl(next3, true)} />
            <StatCard icon="wallet" tone="gold" label="Toplam açık alacak" value={tl(totalOpen, true)} sub={`${new Set(rows.map((r) => r.reservationId)).size} kayıt`} />
          </div>

          <Section title="Tahsilat takvimi" sub="Ay ay vadesi gelecek para · ilk sütun vadesi geçmiş olanlar">
            <BarChart format={compactTl} labels={months.map((m) => (m.overdue ? "Gecikmiş" : `${monthShort(Number(m.key.slice(5, 7)))} ${m.key.slice(2, 4)}`))}
              series={[
                { name: "Gecikmiş", cls: "s-danger", values: months.map((m) => (m.overdue ? m.amount : 0)) },
                { name: "Vadesi gelecek", cls: "s-wine", values: months.map((m) => (m.overdue ? 0 : m.amount)) },
              ]} />
          </Section>

          <Section title="Taksitler" sub="Ödemesi tamamlanmamış kesin kayıtlar, vadeye göre">
            {rows.length === 0 ? <Empty icon="check">Bekleyen alacak yok.</Empty> : (
              <table className="table">
                <thead><tr><th>Vade</th><th>Müşteri</th><th>Organizasyon</th><th>Taksit</th><th className="num">Tutar</th><th className="num">Kalan</th></tr></thead>
                <tbody>
                  {groups.map((g) => (
                    <Fragment key={g.title}>
                      <tr className={`group-row ${g.danger ? "danger" : ""}`}>
                        <td colSpan={5}>{g.title}</td>
                        <td className="num">{tl(g.rows.reduce((s, r) => s + open(r), 0), true)}</td>
                      </tr>
                      {g.rows.map((r, i) => (
                        <tr key={r.reservationId + r.label + i} className="clickable" onClick={() => navigate({ name: "reservation", id: r.reservationId })}>
                          <td style={{ color: r.dueDate < now ? "var(--danger)" : undefined, fontWeight: r.dueDate < now ? 700 : undefined }}>{dateLong(r.dueDate)}</td>
                          <td className="strong">{r.customerName}<div className="muted small">{r.customerPhone}</div></td>
                          <td>{dateLong(r.eventDate)}<div className="muted small">{r.hallName} · {r.no}</div></td>
                          <td>{r.label}</td>
                          <td className="num">{tl(r.amount, true)}</td>
                          <td className="num strong">{tl(open(r), true)}</td>
                        </tr>
                      ))}
                    </Fragment>
                  ))}
                </tbody>
                <tfoot><tr><td colSpan={5} className="strong">Toplam açık alacak</td><td className="num strong">{tl(totalOpen, true)}</td></tr></tfoot>
              </table>
            )}
          </Section>
        </>
      )}
    </ReportShell>
  );
}
