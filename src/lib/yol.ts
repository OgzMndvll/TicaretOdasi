/**
 * Panel bir alt yolda yayınlanabilir (ör. ajansorkestra.com.tr/EtsoSecim).
 *
 * next/link, next/image ve router.push basePath'i kendiliğinden ekler; ekleMEyen üç yer var:
 * `window.location`, `window.location.pathname` karşılaştırmaları ve CSS/inline `url(...)`
 * değerleri. Bu dosya o üç durum için tek kaynaktır.
 *
 * Değer derleme anında gömülür (NEXT_PUBLIC_*), sunucu ve istemcide aynıdır.
 */
export const ALT_YOL = (process.env.NEXT_PUBLIC_BASE_PATH ?? "").replace(/\/+$/, "");

/** Uygulama içi bir yolu tam adrese çevirir: "/giris" → "/EtsoSecim/giris". */
export function yol(ic: string): string {
  return `${ALT_YOL}${ic.startsWith("/") ? ic : `/${ic}`}`;
}

/**
 * public/ altındaki bir dosyanın adresi: "/etso.png" → "/EtsoSecim/etso.png".
 *
 * next/image + basePath tuzağı: varsayılan yükleyici alt yolu yalnızca `/_next/image`
 * yoluna ekler, `url` parametresinin içindeki dosya yoluna eklemez. Sonuç, alt yolda
 * yayınlandığımızda her görsel için HTTP 400 ("The requested resource isn't a valid image").
 * Bu yüzden alt yolu `src`'ye biz veriyoruz; yerel geliştirmede varlik() değeri değiştirmez.
 */
export const varlik = yol;

/** Tarayıcıdaki geçerli yolun, alt yol çıkarılmış hâli. Rota karşılaştırmaları için. */
export function icYol(pathname: string): string {
  if (!ALT_YOL) return pathname;
  return pathname.startsWith(ALT_YOL) ? pathname.slice(ALT_YOL.length) || "/" : pathname;
}
