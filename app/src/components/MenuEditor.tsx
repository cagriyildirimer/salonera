import { addDish, COURSES, groupDishes, moveCourse, renameCourse } from "../menu";
import type { MenuDish } from "../types";
import { Icon } from "./Icon";

/** Yemekleri bölüm bölüm düzenleme (Çorba, Ana yemek, Tatlı…). Katalogda ve rezervasyonda aynı. */
export function MenuEditor({ items, onChange, readOnly }: { items: MenuDish[]; onChange: (items: MenuDish[]) => void; readOnly?: boolean }) {
  const groups = groupDishes(items);
  const used = new Set(groups.map((g) => g.course));
  const setDish = (index: number, name: string) => onChange(items.map((x, i) => (i === index ? { ...x, name } : x)));
  const removeDish = (index: number) => onChange(items.filter((_, i) => i !== index));

  return (
    <div className="stack" style={{ gap: 12 }}>
      {groups.length === 0 && <div className="muted">Henüz yemek yok. Aşağıdan bir bölüm seçerek başlayın.</div>}
      {groups.map((group, gi) => (
        <div key={gi} style={section}>
          <div className="row" style={{ gap: 8, flexWrap: "nowrap", marginBottom: 8 }}>
            <input className="input" style={{ fontWeight: 700, flex: 1 }} value={group.course} disabled={readOnly} placeholder="Bölüm adı (ör. Ana yemek)"
              onChange={(e) => onChange(renameCourse(items, group.course, e.target.value))} />
            {!readOnly && (
              <>
                <button className="btn btn-ghost icon-btn" title="Bölümü yukarı taşı" disabled={gi === 0}
                  onClick={() => onChange(moveCourse(items, group.course, -1))}><Icon name="up" size={18} /></button>
                <button className="btn btn-ghost icon-btn" title="Bölümü aşağı taşı" disabled={gi === groups.length - 1}
                  onClick={() => onChange(moveCourse(items, group.course, 1))}><Icon name="down" size={18} /></button>
                <button className="btn btn-ghost icon-btn" title="Bölümü sil"
                  onClick={() => onChange(items.filter((x) => x.course !== group.course))}><Icon name="trash" size={18} /></button>
              </>
            )}
          </div>
          <div className="stack" style={{ gap: 6, paddingLeft: 12 }}>
            {group.dishes.map((dish) => (
              <div key={dish.index} className="row" style={{ gap: 8, flexWrap: "nowrap" }}>
                <span className="muted">•</span>
                <input className="input" style={{ flex: 1 }} value={dish.name} disabled={readOnly} placeholder="Yemek adı"
                  autoFocus={!readOnly && dish.name === "" && dish.index === items.length - 1}
                  onChange={(e) => setDish(dish.index, e.target.value)} />
                {!readOnly && (
                  <button className="btn btn-ghost icon-btn" title="Yemeği sil" onClick={() => removeDish(dish.index)}><Icon name="trash" size={18} /></button>
                )}
              </div>
            ))}
            {!readOnly && (
              <div><button className="btn btn-ghost" style={{ minHeight: 36 }} onClick={() => onChange(addDish(items, group.course))}>
                <Icon name="plus" size={16} /> Yemek ekle
              </button></div>
            )}
          </div>
        </div>
      ))}
      {!readOnly && (
        <div className="row" style={{ gap: 8, flexWrap: "wrap" }}>
          {COURSES.filter((c) => !used.has(c)).map((c) => (
            <button key={c} className="btn btn-ghost" style={chip} onClick={() => onChange(addDish(items, c))}>+ {c}</button>
          ))}
          <button className="btn btn-ghost" style={chip} onClick={() => onChange(addDish(items, `Bölüm ${groups.length + 1}`))}>+ Başka bölüm</button>
        </div>
      )}
    </div>
  );
}

/** Menünün salt okunur görünümü: bölüm başlıkları ve yemekler. */
export function MenuView({ items, columns = 2 }: { items: MenuDish[]; columns?: number }) {
  const groups = groupDishes(items);
  return (
    <div className="menu-view" style={{ display: "grid", gridTemplateColumns: `repeat(${columns}, minmax(0, 1fr))`, gap: "10px 24px" }}>
      {groups.map((g) => (
        <div key={g.course} className="menu-course" style={{ breakInside: "avoid" }}>
          <div className="strong menu-course-name" style={{ color: "var(--primary)", marginBottom: 2 }}>{g.course}</div>
          {g.dishes.map((d) => <div key={d.index}>{d.name}</div>)}
        </div>
      ))}
    </div>
  );
}

const section = { border: "1px solid var(--line)", borderRadius: 12, padding: 12 } as const;
const chip = { minHeight: 38, padding: "6px 12px", fontSize: 15, border: "1px dashed var(--line-strong)" } as const;
