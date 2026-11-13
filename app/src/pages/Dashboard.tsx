import { useSession } from "../App";
import { api } from "../api";
import { Icon, type IconName } from "../components/Icon";
import { Rings } from "../components/Ornament";
import { ReservationRow } from "../components/ReservationRow";
import { Empty, LoadError, Loading, StatCard, type Tone, useLoad } from "../components/ui";
import { dateWithDay, relativeDay, tl, today } from "../format";

export function Dashboard() {
  const { user, navigate, isAdmin } = useSession();
  const { data, error, loading, reload } = useLoad(() => api.dashboard());

  const hour = new Date().getHours();
  const greeting = hour < 12 ? "Günaydın" : hour < 18 ? "İyi günler" : "İyi akşamlar";

  return (
    <div className="content">
      <div className="content-wide stack">
        <div className="hero rise">
          <Rings size={330} className="hero-rings" />
          <div style={{ flex: 1 }}>
            <div className="date">{dateWithDay(today())}</div>
            <h2>{greeting}, <em className="gold-text">{user.name.split(" ")[0]}</em></h2>
            {data ? (
              <div className="hero-chips">
                <span className="hero-chip"><b>{data.today.length}</b> bugünkü organizasyon</span>
                <span className="hero-chip"><b>{data.weekCount}</b> bu hafta</span>
                {data.options.length > 0 && <span className="hero-chip"><b>{data.options.length}</b> ön rezervasyon bekliyor</span>}
              </div>
            ) : (
              <p>Yeni bir düğün, nişan veya kına kaydı için sağdaki düğmeye basın. Adım adım yol göstereceğiz.</p>
            )}
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
              <StatCard icon="calendar" tone="wine" label="Bugünkü organizasyon" value={String(data.today.length)} />
              <StatCard icon="clock" tone="plum" label="Önümüzdeki 7 gün" value={String(data.weekCount)} />
              <StatCard icon="wallet" tone="gold" label="Bu ay alınan ödeme" value={tl(data.monthCollected, true)} />
              <StatCard icon={data.overdueTotal > 0 ? "alert" : "check"} tone={data.overdueTotal > 0 ? "danger" : "green"}
                label="Geciken ödemeler" value={tl(data.overdueTotal, true)} />
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
                    <div className="list today-list">{data.today.map((r) => <ReservationRow key={r.id} r={r} extra="remaining" />)}</div>
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
                          <div className="small" style={{ color: r.optionExpired ? "var(--danger)" : "var(--warning)", padding: "0 10px 10px 88px", marginTop: -6 }}>
                            Son gün: {r.optionUntil ? relativeDay(r.optionUntil) : "—"}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                <div className="card quick">
                  <div className="card-title" style={{ marginBottom: 14 }}>Hızlı işlemler</div>
                  <div className="quick-grid">
                    <QuickButton icon="calendar" tone="wine" label="Boş günlere bak" onClick={() => navigate({ name: "calendar" })} />
                    <QuickButton icon="wallet" tone="gold" label="Ödeme al" onClick={() => navigate({ name: "payments" })} />
                    <QuickButton icon="search" tone="plum" label="Müşteri ara" onClick={() => navigate({ name: "customers" })} />
                    {isAdmin && <QuickButton icon="chart" tone="green" label="Raporlar" onClick={() => navigate({ name: "reports" })} />}
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

function QuickButton({ icon, tone, label, onClick }: { icon: IconName; tone: Tone; label: string; onClick: () => void }) {
  return (
    <button className={`quick-btn tone-${tone}`} onClick={onClick}>
      <span className="qi"><Icon name={icon} size={20} /></span>
      {label}
    </button>
  );
}
