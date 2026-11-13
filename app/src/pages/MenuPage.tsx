import { useEffect, useMemo, useState } from "react";
import { useSession } from "../App";
import { api } from "../api";
import { Icon } from "../components/Icon";
import { MenuEditor } from "../components/MenuEditor";
import { OptionsPicker, TotalBar } from "../components/SaleEditor";
import { Alert, DateBox, Field, LoadError, Loading, useAction, useFeedback, useLoad } from "../components/ui";
import { dateWithDay, kindLabels, tl } from "../format";
import { cleanDishes, dishSummary } from "../menu";
import { extraTotal, menuTariffText, menuTotal } from "../pricing";
import type { ExtraLine, Menu, MenuDish, Product } from "../types";

interface Draft { items: MenuDish[]; notes: string }

const sameDraft = (a: Draft, b: Draft) =>
  JSON.stringify(cleanDishes(a.items)) === JSON.stringify(cleanDishes(b.items)) && a.notes.trim() === b.notes.trim();
const sameIds = (a: string[], b: string[]) => JSON.stringify([...a].sort()) === JSON.stringify([...b].sort());

/** Rezervasyondaki ek hizmet satırının katalogdaki karşılığı (eski kayıtlarda adla eşlenir). */
const catalogIdOf = (line: ExtraLine, catalog: Product[]) =>
  line.extraId ?? catalog.find((e) => e.name === line.name)?.id ?? null;

/**
 * Rezervasyonun yemek menüsü ve seçenekleri: katalogdan menü seçimi, pasta gibi seçenekler (fiyatı etkiler),
 * düğüne özel yemekler ve mutfak notları. Toplam tutar altta sağda hep görünür.
 */
export function MenuPage({ id }: { id: string }) {
  const { navigate, setLeaveGuard } = useSession();
  const { confirm, toast } = useFeedback();
  const { run, busy } = useAction();
  const data = useLoad(async () => {
    const [detail, menus, extras] = await Promise.all([api.reservation(id), api.menus(), api.extras(true)]);
    return { detail, menus, extras };
  }, [id]);
  const [draft, setDraft] = useState<Draft>({ items: [], notes: "" });
  const [saved, setSaved] = useState<Draft>({ items: [], notes: "" });
  const [options, setOptions] = useState<string[]>([]);
  const [savedOptions, setSavedOptions] = useState<string[]>([]);

  useEffect(() => {
    if (!data.data) return;
    const loaded = { items: data.data.detail.menuItems, notes: data.data.detail.menuNotes ?? "" };
    setDraft(loaded);
    setSaved(loaded);
    const ids = data.data.detail.extras.map((x) => catalogIdOf(x, data.data!.extras)).filter((x): x is string => !!x);
    setOptions(ids);
    setSavedOptions(ids);
  }, [data.data]);

  const dishesDirty = useMemo(() => !sameDraft(draft, saved), [draft, saved]);
  const optionsDirty = !sameIds(options, savedOptions);
  const dirty = dishesDirty || optionsDirty;
  useEffect(() => {
    setLeaveGuard(dirty ? "Menüde veya seçeneklerde kaydedilmemiş değişiklikler var. Çıkarsanız bu değişiklikler silinir." : null);
  }, [dirty, setLeaveGuard]);
  useEffect(() => () => setLeaveGuard(null), [setLeaveGuard]);

  if (data.error) return <div className="content"><LoadError message={data.error} onRetry={data.reload} /></div>;
  if (!data.data) return <div className="content"><Loading /></div>;
  const { detail, menus, extras } = data.data;
  const r = detail.summary;
  const editable = r.status === "kesin";
  const minGuests = detail.packagePricing.minGuests;

  // Rezervasyonda zaten olan seçenek satış anındaki fiyatıyla kalır; yeni seçilen güncel katalog fiyatıyla gelir.
  const priceOf = (e: Product) => {
    const line = detail.extras.find((x) => catalogIdOf(x, extras) === e.id);
    return line ? line.price : extraTotal(e, r.guestCount, minGuests);
  };
  const savedExtras = detail.extras.reduce((s, x) => s + x.price, 0);
  const totalWith = (ids: string[]) =>
    r.total - savedExtras + extras.filter((e) => ids.includes(e.id)).reduce((s, e) => s + priceOf(e), 0);
  const total = totalWith(options);
  const visibleExtras = extras.filter((e) => e.isActive || options.includes(e.id) || savedOptions.includes(e.id));

  const save = async () => {
    if (optionsDirty) {
      if (total !== r.total) {
        const ok = await confirm({
          title: "Seçenekler değişsin mi?",
          message: (
            <>
              Toplam tutar <b>{tl(r.total)}</b> → <b>{tl(total)}</b> olacak.{" "}
              {total > r.total
                ? `Fark (${tl(total - r.total)}) ödenmemiş son taksite eklenir.`
                : `Fark (${tl(r.total - total)}) ödenmemiş taksitlerden düşülür.`}
            </>
          ),
          confirmText: "Evet, kaydet",
        });
        if (!ok) return false;
      }
      if (!(await run(() => api.changeExtras(id, options)))) return false;
    }
    if (dishesDirty) {
      const body = { items: cleanDishes(draft.items), notes: draft.notes.trim() || null };
      if (!(await run(() => api.saveReservationMenu(id, body)))) return false;
    }
    toast(optionsDirty && total !== r.total ? "Kaydedildi, fiyat ve ödeme planı güncellendi." : "Kaydedildi.");
    setLeaveGuard(null);
    await data.reload();
    return true;
  };

  const choose = async (menu: Menu | null) => {
    const newPrice = menu ? menuTotal(menu.perGuestPrice, r.guestCount, minGuests) : 0;
    const newTotal = r.total - detail.menuPrice + newPrice;
    const ok = await confirm({
      title: menu ? `“${menu.name}” seçilsin mi?` : "Menü kaldırılsın mı?",
      message: (
        <>
          {menu
            ? <>Yemek listesi bu menünün yemekleriyle değiştirilecek{draft.items.length > 0 && " (düğüne özel yaptığınız değişiklikler silinir)"}.</>
            : "Yemek listesi boşaltılacak."}
          {" "}Mutfak notları kalır.
          {optionsDirty && <> Kaydedilmemiş seçenek değişiklikleri silinir.</>}
          {newTotal !== r.total && (
            <>
              <br /><br />
              Toplam tutar <b>{tl(r.total)}</b> → <b>{tl(newTotal)}</b> olacak.{" "}
              {newTotal > r.total
                ? `Fark (${tl(newTotal - r.total)}) ödenmemiş son taksite eklenir.`
                : `Fark (${tl(r.total - newTotal)}) ödenmemiş taksitlerden düşülür.`}
            </>
          )}
        </>
      ),
      confirmText: menu ? "Evet, seç" : "Evet, kaldır",
      danger: !menu,
    });
    if (!ok) return;
    if (await run(() => api.selectMenu(id, menu?.id ?? null), menu ? "Menü seçildi." : "Menü kaldırıldı.")) {
      setLeaveGuard(null);
      await data.reload();
    }
  };

  const print = async () => {
    if (dirty && !(await save())) return;
    setLeaveGuard(null);
    navigate({ name: "print", kind: "menu", id });
  };

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
            {r.status === "iptal" && <div style={{ marginTop: 16 }}><Alert kind="danger">Bu rezervasyon iptal edildi; menü yalnızca görüntülenebilir.</Alert></div>}
            {r.status === "on_rezervasyon" && (
              <div style={{ marginTop: 16 }}><Alert kind="info">Menü ve seçenekler kesin kayıtta seçilir. Ön rezervasyonu kesin kayda çevirirken seçebilirsiniz.</Alert></div>
            )}
          </div>

          {editable && (
            <div className="card">
              <div className="card-head">
                <div>
                  <div className="card-title">Menü seçimi</div>
                  <div className="card-sub">
                    Seçili: <b>{detail.menuName ?? "yok"}</b>
                    {detail.menuPrice > 0 && <> · fark {tl(detail.menuPrice)}</>}
                    {" "}· Menü değişince fiyat farkı ödeme planına yansır.
                  </div>
                </div>
                {detail.menuName && (
                  <button className="btn btn-ghost" disabled={busy} onClick={() => choose(null)}><Icon name="x" size={18} /> Menüyü kaldır</button>
                )}
              </div>
              {menus.length === 0 ? (
                <div className="muted">Katalogda menü yok. Yönetici, Ayarlar → Yemek menüleri bölümünden menü ekleyebilir. Aşağıya yemekleri elle de yazabilirsiniz.</div>
              ) : (
                <div className="grid grid-3" style={{ gap: 12 }}>
                  {menus.map((m) => {
                    const current = m.name === detail.menuName;
                    const price = menuTotal(m.perGuestPrice, r.guestCount, minGuests);
                    const ifChosen = r.total - detail.menuPrice + price;
                    return (
                      <button key={m.id} className={`choice ${current ? "selected" : ""}`} style={{ alignItems: "flex-start" }}
                        disabled={busy} onClick={() => !current && choose(m)}>
                        <div style={{ minWidth: 0, flex: 1 }}>
                          <div className="choice-title">{m.name}</div>
                          <div className="choice-sub">{m.description || dishSummary(m.items, 3)}</div>
                          <div className="choice-sub strong" style={{ color: "var(--primary)", marginTop: 4 }}>
                            {m.perGuestPrice ? `+ ${tl(price, true)} (${menuTariffText(m.perGuestPrice)})` : "Paket fiyatına dahil"}
                          </div>
                          <div className="choice-total">
                            {current ? <>Seçili · Toplam <b>{tl(r.total, true)}</b></> : <>Seçilirse toplam <b>{tl(ifChosen, true)}</b></>}
                          </div>
                        </div>
                        <span className="tick"><Icon name="check" size={15} strokeWidth={3} /></span>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          )}

          {editable && visibleExtras.length > 0 && (
            <OptionsPicker extras={visibleExtras} selected={options} onChange={setOptions}
              priceOf={priceOf} totalWith={totalWith} current={total} columns={3} />
          )}

          <div className="card">
            <div className="card-head">
              <div>
                <div className="card-title"><Icon name="utensils" size={20} /> Bu düğünün yemekleri</div>
                <div className="card-sub">Müşterinin isteğine göre yemek ekleyin, çıkarın veya değiştirin. Fiyat değişmez.</div>
              </div>
            </div>
            <MenuEditor items={draft.items} onChange={(items) => setDraft((d) => ({ ...d, items }))} readOnly={!editable} />
          </div>

          <div className="card">
            <Field label="Mutfağa notlar (isteğe bağlı)" hint="Alerjiler, çocuk menüsü / vejetaryen sayısı, servis saati… Mutfak çıktısında görünür.">
              <textarea className="textarea" value={draft.notes} disabled={!editable}
                placeholder="Ör. 15 çocuk menüsü, 4 vejetaryen, 1 kişi glütensiz"
                onChange={(e) => setDraft((d) => ({ ...d, notes: e.target.value }))} />
            </Field>
          </div>
        </div>
      </div>
      <div className="wizard-foot no-print">
        <span className="muted">
          {optionsDirty && total !== r.total ? "Yeni fiyat henüz kaydedilmedi" : dirty ? "Kaydedilmemiş değişiklikler var" : "Tüm değişiklikler kaydedildi"}
        </span>
        <span className="spacer" />
        <TotalBar total={total} was={optionsDirty ? r.total : undefined} label={optionsDirty && total !== r.total ? "Yeni toplam" : "Toplam"} />
        <button className="btn" disabled={busy || (draft.items.length === 0 && !dirty)} onClick={print}><Icon name="printer" /> Mutfak çıktısı</button>
        {editable && (
          <button className="btn btn-primary" style={{ minWidth: 180 }} disabled={busy || !dirty} onClick={save}>
            <Icon name="check" /> Kaydet
          </button>
        )}
      </div>
    </>
  );
}
