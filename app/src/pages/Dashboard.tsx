import { useSession } from "../App";
import { api } from "../api";
import { Icon, type IconName } from "../components/Icon";
import { ReservationRow } from "../components/ReservationRow";
import { Empty, LoadError, Loading, useLoad } from "../components/ui";
import { dateWithDay, relativeDay, tl, today } from "../format";

export function Dashboard() {
  const { user, navigate, isAdmin } = useSession();
  const { data, error, loading, reload } = useLoad(() => api.dashboard());

  const hour = new Date().getHours();
  const greeting = hour < 12 ? "Günaydın" : hour < 18 ? "İyi günler" : "İyi akşamlar";

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="hero">
          <div style={{ flex: 1 }}>
            <div className="date">{dateWithDay(today())}</div>
            <h2>{greeting}, {user.name.split(" ")[0]}</h2>
            <p>Yeni bir düğün, nişan veya kına kaydı için sağdaki düğmeye basın. Adım adım yol göstereceğiz.</p>
          </div>
          <button className="btn btn-gold btn-big" onClick={() => navigate({ name: "new" })}>
            <Icon name="plus" size={24} strokeWidth={2.4} /> Yeni Rezervasyon
          </button>
        </div>

        {error && <LoadError message={error} onRetry={reload} />}
        {loading && !data && <Loading />}
        {data && (
          <>
            <div className="grid grid-4">
              <Stat icon="calendar" label="Bugünkü organizasyon" value={String(data.today.length)} />
              <Stat icon="clock" label="Önümüzdeki 7 gün" value={String(data.weekCount)} />
              <Stat icon="wallet" label="Bu ay alınan ödeme" value={tl(data.monthCollected, true)} />
              <Stat icon="alert" label="Geciken ödemeler" value={tl(data.overdueTotal, true)}
                tone={data.overdueTotal > 0 ? "danger" : undefined} />
            </div>

            <div className="split">
              <div className="stack">
                <div className="card">
                  <div className="card-head">
                    <div>
                      <div className="card-title">Bugün</div>
                      <div className="card-sub">Bugün salonda yapılacak organizasyonlar</div>
                    </div>
                  </div>
                  {data.today.length === 0 ? <Empty icon="calendar">Bugün organizasyon yok.</Empty> : (
                    <div className="list">{data.today.map((r) => <ReservationRow key={r.id} r={r} extra="remaining" />)}</div>
                  )}
                </div>
                <div className="card">
                  <div className="card-head">
                    <div>
                      <div className="card-title">Yaklaşan organizasyonlar</div>
                      <div className="card-sub">Önümüzdeki 30 gün</div>
                    </div>
                    <button className="btn btn-ghost" onClick={() => navigate({ name: "calendar" })}>
                      Takvimi aç <Icon name="right" size={16} />
                    </button>
                  </div>
                  {data.upcoming.length === 0 ? <Empty icon="calendar">Önümüzdeki 30 günde kayıt yok.</Empty> : (
                    <div className="list">{data.upcoming.map((r) => <ReservationRow key={r.id} r={r} />)}</div>
                  )}
                </div>
              </div>

              <div className="stack">
                <div className="card">
                  <div className="card-head">
                    <div>
                      <div className="card-title">Ödemesi gecikenler</div>
                      <div className="card-sub">Taksit tarihi geçmiş müşteriler</div>
                    </div>
                  </div>
                  {data.overdue.length === 0 ? (
                    <div className="row" style={{ color: "var(--success)", padding: "6px 2px" }}>
                      <Icon name="check" /> <span style={{ fontSize: 16 }}>Geciken ödeme yok.</span>
                    </div>
                  ) : (
                    <div className="list">{data.overdue.map((r) => <ReservationRow key={r.id} r={r} extra="overdue" />)}</div>
                  )}
                </div>

                {data.options.length > 0 && (
                  <div className="card" style={{ borderColor: "#efd9ae" }}>
                    <div className="card-head">
                      <div>
                        <div className="card-title">Süresi dolan ön rezervasyonlar</div>
                        <div className="card-sub">Müşteriyi arayın: kesinleştirin, süreyi uzatın veya iptal edin.</div>
                      </div>
                    </div>
                    <div className="list">
                      {data.options.map((r) => (
                        <div key={r.id}>
                          <ReservationRow r={r} extra="option" />
                          <div className="small" style={{ color: r.optionExpired ? "var(--danger)" : "var(--warning)", padding: "0 6px 10px 84px", marginTop: -6 }}>
                            Son gün: {r.optionUntil ? relativeDay(r.optionUntil) : "—"}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                <div className="card" style={{ background: "var(--gold-soft)", borderColor: "#ebddbe" }}>
                  <div className="card-title" style={{ marginBottom: 12 }}>Hızlı işlemler</div>
                  <div className="stack" style={{ gap: 10 }}>
                    <button className="btn btn-block" style={{ justifyContent: "flex-start" }} onClick={() => navigate({ name: "calendar" })}>
                      <Icon name="calendar" /> Boş günlere bak
                    </button>
                    <button className="btn btn-block" style={{ justifyContent: "flex-start" }} onClick={() => navigate({ name: "payments" })}>
                      <Icon name="wallet" /> Ödeme al
                    </button>
                    <button className="btn btn-block" style={{ justifyContent: "flex-start" }} onClick={() => navigate({ name: "customers" })}>
                      <Icon name="search" /> Müşteri ara
                    </button>
                    {isAdmin && (
                      <button className="btn btn-block" style={{ justifyContent: "flex-start" }} onClick={() => navigate({ name: "reports" })}>
                        <Icon name="chart" /> Raporlar
                      </button>
                    )}
                  </div>
                </div>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

function Stat({ icon, label, value, tone }: { icon: IconName; label: string; value: string; tone?: "danger" }) {
  return (
    <div className="card stat">
      <div className="stat-label" style={tone ? { color: "var(--danger)" } : undefined}>
        <Icon name={icon} size={19} /> {label}
      </div>
      <div className="big-number" style={tone ? { color: "var(--danger)" } : undefined}>{value}</div>
    </div>
  );
}
