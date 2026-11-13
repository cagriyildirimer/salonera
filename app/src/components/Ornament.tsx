// Süsleme çizimleri: iç içe geçmiş alyanslar, altın ayraç ve parıltılar. Yalnızca görsel; ekran okuyuculardan gizli.
import { useId } from "react";

/** Altın renkli geçişi tanımlar; aynı sayfada birden çok çizim olabileceği için kimlik her seferinde benzersizdir. */
function GoldDefs({ id }: { id: string }) {
  return (
    <defs>
      <linearGradient id={id} x1="0" y1="0" x2="1" y2="1">
        <stop offset="0" stopColor="#fbe9bf" />
        <stop offset=".45" stopColor="#e2bd72" />
        <stop offset=".7" stopColor="#b98a3b" />
        <stop offset="1" stopColor="#f3d79c" />
      </linearGradient>
    </defs>
  );
}

/** İç içe iki alyans ve etrafında parıltılar (giriş ekranı ve karşılama kutusu). */
export function Rings({ size = 220, className }: { size?: number; className?: string }) {
  const id = useId().replace(/:/g, "");
  const g = `url(#g${id})`;
  return (
    <svg className={className} width={size} height={size * 0.72} viewBox="0 0 250 180" fill="none" aria-hidden="true">
      <GoldDefs id={`g${id}`} />
      <circle cx="95" cy="100" r="58" stroke={g} strokeWidth="9" />
      <circle cx="155" cy="100" r="58" stroke={g} strokeWidth="9" />
      {/* Sağ halkanın soldakinin üstünden geçtiği yer, iç içe görünsün diye yeniden çizilir. */}
      <path d="M113.2 45 A58 58 0 0 1 125.5 52" stroke={g} strokeWidth="9" />
      <path d="M155 42 l7 -16 l7 16 l-7 9 z" fill={g} />
      <Sparkle x={40} y={30} s={10} fill={g} />
      <Sparkle x={222} y={46} s={7} fill={g} />
      <Sparkle x={214} y={160} s={9} fill={g} />
      <Sparkle x={22} y={150} s={6} fill={g} />
    </svg>
  );
}

function Sparkle({ x, y, s, fill }: { x: number; y: number; s: number; fill: string }) {
  return (
    <path className="sparkle" style={{ transformOrigin: `${x}px ${y}px` }}
      d={`M${x} ${y - s} Q${x + s * 0.18} ${y - s * 0.18} ${x + s} ${y} Q${x + s * 0.18} ${y + s * 0.18} ${x} ${y + s} Q${x - s * 0.18} ${y + s * 0.18} ${x - s} ${y} Q${x - s * 0.18} ${y - s * 0.18} ${x} ${y - s}Z`}
      fill={fill} />
  );
}

/** Ortasında elmas olan ince altın çizgi (başlık altı ayraç). */
export function Flourish({ width = 160, className }: { width?: number; className?: string }) {
  const id = useId().replace(/:/g, "");
  const g = `url(#f${id})`;
  return (
    <svg className={className} width={width} height={14} viewBox="0 0 160 14" fill="none" aria-hidden="true">
      <GoldDefs id={`f${id}`} />
      <path d="M0 7h66M94 7h66" stroke={g} strokeWidth="1.4" />
      <path d="M80 1l6 6-6 6-6-6z" fill={g} />
      <circle cx="68" cy="7" r="1.8" fill={g} />
      <circle cx="92" cy="7" r="1.8" fill={g} />
    </svg>
  );
}

/** Marka işareti: altın halka içinde "S". */
export function BrandMark({ size = 46 }: { size?: number }) {
  const id = useId().replace(/:/g, "");
  const g = `url(#b${id})`;
  return (
    <svg width={size} height={size} viewBox="0 0 48 48" fill="none" aria-hidden="true" className="brand-svg">
      <GoldDefs id={`b${id}`} />
      <circle cx="24" cy="24" r="22" stroke={g} strokeWidth="1.6" />
      <circle cx="24" cy="24" r="18.5" stroke={g} strokeWidth=".7" opacity=".7" />
      <text x="24" y="32" textAnchor="middle" fill={g} fontFamily="'Cormorant Garamond', Georgia, serif" fontSize="25" fontWeight="700">S</text>
    </svg>
  );
}
