import { useEffect, useMemo, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { Alert, DateBox, Field, LoadError, Loading, useAction, useLoad } from "../components/ui";
import { addMinutes, dateWithDay, kindLabels, normalizeClock } from "../format";
import type { MusicItem, Program, ProgramItem } from "../types";

/** Organizasyon başlangıcına göre hazır düğün akışı (dakika olarak başlangıçtan uzaklık). */
const WEDDING_TEMPLATE: [number, string][] = [
  [0, "Misafir karşılama"],
  [60, "Gelin ve damadın salona girişi"],
  [70, "İlk dans"],
  [90, "Yemek servisi"],
  [150, "Pasta kesimi"],
  [180, "Takı töreni"],
  [210, "Gelin buketi atma"],
];

const QUICK_ITEMS = [
  "Misafir karşılama", "Gelin ve damadın salona girişi", "İlk dans", "Yemek servisi", "Pasta kesimi",
  "Takı töreni", "Nikâh", "Gelin buketi atma", "Halay / oyun havaları", "Kapanış",
];

const QUICK_MUSIC = [
  "Giriş müziği", "İlk dans müziği", "Pasta kesim müziği", "Takı töreni müziği", "Buket müziği", "Çıkış müziği",
];

const emptyProgram: Program = { items: [], music: [], notes: null };

/** Boş satırları atar; sunucuya gidecek hâle getirir. */
function clean(p: Program): Program {
  return {
    items: p.items.filter((x) => x.title.trim() || x.note?.trim()),
    music: p.music.filter((x) => x.label.trim() || x.song?.trim() || x.link?.trim()),
    notes: p.notes?.trim() ? p.notes : null,
  };
}

export function ProgramPage({ id }: { id: string }) {
  const { navigate, setLeaveGuard } = useSession();
  const { run, busy } = useAction();
  const data = useLoad(async () => {
    const [detail, program] = await Promise.all([api.reservation(id), api.program(id)]);
    return { detail, program };
  }, [id]);
  const [program, setProgram] = useState<Program>(emptyProgram);
  const [saved, setSaved] = useState<Program>(emptyProgram);

  useEffect(() => {
    if (data.data) { setProgram(data.data.program); setSaved(data.data.program); }
  }, [data.data]);

  const dirty = useMemo(() => JSON.stringify(clean(program)) !== JSON.stringify(clean(saved)), [program, saved]);
  useEffect(() => {
    setLeaveGuard(dirty ? "Organizasyon planında kaydedilmemiş değişiklikler var. Çıkarsanız bu değişiklikler silinir." : null);
  }, [dirty, setLeaveGuard]);
  useEffect(() => () => setLeaveGuard(null), [setLeaveGuard]);

  if (data.error) return <div className="content"><LoadError message={data.error} onRetry={data.reload} /></div>;
  if (!data.data) return <div className="content"><Loading /></div>;
  const r = data.data.detail.summary;
  const readOnly = r.status === "iptal";

  const setItem = (i: number, patch: Partial<ProgramItem>) =>
    setProgram((p) => ({ ...p, items: p.items.map((x, j) => (j === i ? { ...x, ...patch } : x)) }));
  const setMusic = (i: number, patch: Partial<MusicItem>) =>
    setProgram((p) => ({ ...p, music: p.music.map((x, j) => (j === i ? { ...x, ...patch } : x)) }));
  const addItem = (title = "") =>
    setProgram((p) => {
      const last = p.items[p.items.length - 1];
      const time = last ? addMinutes(last.time, 30) : r.start;
      return { ...p, items: [...p.items, { time, title, note: null }] };
    });
  const addMusic = (label = "") => setProgram((p) => ({ ...p, music: [...p.music, { label, song: null, link: null }] }));
  const fillTemplate = () =>
    setProgram((p) => ({
      ...p,
      items: [
        ...WEDDING_TEMPLATE.map(([offset, title]) => ({ time: addMinutes(r.start, offset), title, note: null })),
        { time: addMinutes(r.end, -30), title: "Kapanış", note: null },
      ],
    }));

  const save = async () => {
    let result: Program | null = null;
    const ok = await run(async () => { result = await api.saveProgram(id, clean(program)); }, "Organizasyon planı kaydedildi.");
    if (ok && result) { setProgram(result); setSaved(result); }
    return ok;
  };

  const print = async () => {
    if (dirty && !(await save())) return;
    setLeaveGuard(null);
    navigate({ name: "print", kind: "program", id });
  };

  const usedMusic = new Set(program.music.map((m) => m.label));

  return (
    <>
      <div className="content">
        <div className="content-wide stack">
          <div className="card">
            <div className="res-head">
              <DateBox date={r.date} />
              <div style={{ flex: 1, minWidth: 0 }}>
                <h2>{r.customerName}</h2>
                <div className="muted" style={{ fontSize: 17, marginTop: 4 }}>
                  {dateWithDay(r.date)} · {r.start}–{r.end} · {r.hallName} · {kindLabels[r.kind]} · {r.guestCount} davetli
                </div>
              </div>
            </div>
            {readOnly && <div style={{ marginTop: 16 }}><Alert kind="danger">Bu rezervasyon iptal edildi; plan yalnızca görüntülenebilir.</Alert></div>}
          </div>

          {/* Saatlik akış */}
          <div className="card">
            <div className="card-head">
              <div>
                <div className="card-title"><Icon name="clock" size={20} /> Saatlik akış</div>
                <div className="card-sub">Ne zaman ne yapılacak? Kaydedince saate göre sıralanır.</div>
              </div>
              {!readOnly && program.items.length === 0 && (
                <button className="btn" onClick={fillTemplate}><Icon name="list" /> Hazır düğün akışını ekle</button>
              )}
            </div>
            {program.items.length > 0 && (
              <div className="stack" style={{ gap: 8, marginTop: 8 }}>
                <div className="muted small" style={rowGrid}>
                  <span>Saat</span><span>Ne yapılacak?</span><span>Not (isteğe bağlı)</span><span />
                </div>
                {program.items.map((item, i) => (
                  <div key={i} style={rowGrid}>
                    <ClockInput value={item.time} disabled={readOnly} onChange={(time) => setItem(i, { time })} />
                    <input className="input" value={item.title} disabled={readOnly} placeholder="Pasta kesimi"
                      onChange={(e) => setItem(i, { title: e.target.value })} />
                    <input className="input" value={item.note ?? ""} disabled={readOnly} placeholder="Ör. pasta 5 katlı, maket bıçak hazır"
                      onChange={(e) => setItem(i, { note: e.target.value })} />
                    {!readOnly ? (
                      <button className="btn btn-ghost icon-btn" title="Satırı sil"
                        onClick={() => setProgram((p) => ({ ...p, items: p.items.filter((_, j) => j !== i) }))}>
                        <Icon name="trash" size={18} />
                      </button>
                    ) : <span />}
                  </div>
                ))}
              </div>
            )}
            {!readOnly && (
              <div style={{ marginTop: 14 }}>
                <div className="row" style={{ gap: 8, flexWrap: "wrap" }}>
                  <button className="btn btn-primary" onClick={() => addItem()}><Icon name="plus" /> Satır ekle</button>
                  {QUICK_ITEMS.filter((t) => !program.items.some((x) => x.title === t)).map((t) => (
                    <button key={t} className="btn btn-ghost" style={chip} onClick={() => addItem(t)}>+ {t}</button>
                  ))}
                </div>
              </div>
            )}
          </div>

          {/* Müzikler */}
          <div className="card">
            <div className="card-head">
              <div>
                <div className="card-title"><Icon name="music" size={20} /> Müzikler</div>
                <div className="card-sub">Müşterinin istediği şarkılar. Bağlantıyı YouTube / Spotify'dan kopyalayıp yapıştırın.</div>
              </div>
            </div>
            {program.music.length > 0 && (
              <div className="stack" style={{ gap: 8, marginTop: 8 }}>
                <div className="muted small" style={musicGrid}>
                  <span>Ne zaman?</span><span>Şarkı (isteğe bağlı)</span><span>Bağlantı</span><span />
                </div>
                {program.music.map((m, i) => (
                  <div key={i} style={musicGrid}>
                    <input className="input" value={m.label} disabled={readOnly} placeholder="Giriş müziği"
                      onChange={(e) => setMusic(i, { label: e.target.value })} />
                    <input className="input" value={m.song ?? ""} disabled={readOnly} placeholder="Sanatçı – şarkı adı"
                      onChange={(e) => setMusic(i, { song: e.target.value })} />
                    <input className="input" value={m.link ?? ""} disabled={readOnly} placeholder="https://www.youtube.com/…"
                      onChange={(e) => setMusic(i, { link: e.target.value })} />
                    {!readOnly ? (
                      <button className="btn btn-ghost icon-btn" title="Satırı sil"
                        onClick={() => setProgram((p) => ({ ...p, music: p.music.filter((_, j) => j !== i) }))}>
                        <Icon name="trash" size={18} />
                      </button>
                    ) : <span />}
                  </div>
                ))}
              </div>
            )}
            {!readOnly && (
              <div className="row" style={{ gap: 8, flexWrap: "wrap", marginTop: 14 }}>
                <button className="btn btn-primary" onClick={() => addMusic()}><Icon name="plus" /> Müzik ekle</button>
                {QUICK_MUSIC.filter((t) => !usedMusic.has(t)).map((t) => (
                  <button key={t} className="btn btn-ghost" style={chip} onClick={() => addMusic(t)}>+ {t}</button>
                ))}
              </div>
            )}
          </div>

          <div className="card">
            <Field label="Plan notları (isteğe bağlı)" hint="Masa düzeni, süsleme rengi, özel istekler… Çıktıda görünür.">
              <textarea className="textarea" value={program.notes ?? ""} disabled={readOnly}
                onChange={(e) => setProgram((p) => ({ ...p, notes: e.target.value }))} />
            </Field>
          </div>
        </div>
      </div>
      <div className="wizard-foot no-print">
        <span className="muted">{dirty ? "Kaydedilmemiş değişiklikler var" : "Tüm değişiklikler kaydedildi"}</span>
        <span className="spacer" />
        <button className="btn" disabled={busy} onClick={print}><Icon name="printer" /> Yazdır / PDF</button>
        {!readOnly && (
          <button className="btn btn-primary" style={{ minWidth: 180 }} disabled={busy || !dirty} onClick={save}>
            <Icon name="check" /> Kaydet
          </button>
        )}
      </div>
    </>
  );
}

const rowGrid = { display: "grid", gridTemplateColumns: "130px minmax(0, 1.2fr) minmax(0, 1fr) 44px", gap: 10, alignItems: "center" } as const;
const musicGrid = { display: "grid", gridTemplateColumns: "minmax(0, 0.9fr) minmax(0, 1fr) minmax(0, 1.3fr) 44px", gap: 10, alignItems: "center" } as const;
const chip = { minHeight: 38, padding: "6px 12px", fontSize: 15, border: "1px dashed var(--line-strong)" } as const;

/**
 * 24 saatlik saat kutusu (bilgisayarın dil ayarından bağımsız). "2130", "21.30", "9" yazılabilir;
 * kutudan çıkınca "21:30" / "09:00" biçimine getirilir. Geçersizse kırmızı görünür.
 */
function ClockInput({ value, onChange, disabled }: { value: string; onChange: (v: string) => void; disabled?: boolean }) {
  const [text, setText] = useState(value);
  const [focused, setFocused] = useState(false);
  useEffect(() => { if (!focused) setText(value); }, [value, focused]);
  const invalid = normalizeClock(text) === null;
  return (
    <input className="input" inputMode="numeric" maxLength={5} placeholder="20:00" value={text} disabled={disabled}
      style={{ fontWeight: 700, fontSize: 18, textAlign: "center", ...(invalid ? { borderColor: "var(--danger)" } : {}) }}
      onFocus={(e) => { setFocused(true); e.target.select(); }}
      onChange={(e) => {
        setText(e.target.value);
        const v = normalizeClock(e.target.value);
        if (v) onChange(v);
      }}
      onBlur={() => { setFocused(false); const v = normalizeClock(text); setText(v ?? value); if (v) onChange(v); }} />
  );
}
