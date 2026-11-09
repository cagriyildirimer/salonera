const ones = ["", "Bir", "İki", "Üç", "Dört", "Beş", "Altı", "Yedi", "Sekiz", "Dokuz"];
const tens = ["", "On", "Yirmi", "Otuz", "Kırk", "Elli", "Altmış", "Yetmiş", "Seksen", "Doksan"];
const groups = ["", "Bin", "Milyon", "Milyar"];

function threeDigits(n: number): string {
  const h = Math.floor(n / 100);
  const t = Math.floor((n % 100) / 10);
  const o = n % 10;
  return `${h === 0 ? "" : h === 1 ? "Yüz" : `${ones[h]}Yüz`}${tens[t]}${ones[o]}`;
}

/** 123456 → "YüzYirmiÜçBinDörtYüzElliAltı" (makbuzlardaki gibi bitişik). */
export function numberWords(value: number): string {
  if (value === 0) return "Sıfır";
  let n = Math.floor(value);
  let result = "";
  let group = 0;
  while (n > 0) {
    const part = n % 1000;
    if (part > 0) {
      // Türkçede "bir bin" denmez, sadece "bin".
      const words = group === 1 && part === 1 ? "" : threeDigits(part);
      result = `${words}${groups[group]}${result}`;
    }
    n = Math.floor(n / 1000);
    group++;
  }
  return result;
}

/** Kuruş → "YirmiBinTürkLirası" / "OnBeşTürkLirasıElliKuruş" */
export function moneyWords(kurus: number): string {
  const lira = Math.floor(kurus / 100);
  const cents = kurus % 100;
  return `${numberWords(lira)}TürkLirası${cents ? `${numberWords(cents)}Kuruş` : ""}`;
}
