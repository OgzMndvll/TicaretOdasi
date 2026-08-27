import { cikisYap, tokenAl } from "./auth";
import { icYol } from "./yol";

export const API_URL = (process.env.NEXT_PUBLIC_API_URL ?? "https://api.courseintellect.com.tr").replace(/\/+$/, "");
export const API_ERISIM_HATASI = `Veriler alınamadı. API'ye ulaşılamadı (${API_URL}).`;

export class ApiError extends Error {
  constructor(public status: number, message: string) { super(message); }
}

function yetkiBasligi(): Record<string, string> {
  const token = tokenAl();
  return token ? { Authorization: `Bearer ${token}` } : {};
}

/** 401 dönen isteklerde oturum düşmüş demektir: token temizlenip girişe yönlendirilir. */
function oturumKontrol(res: Response) {
  // pathname alt yolu da içerir; karşılaştırma öncesi ayıklanır.
  if (res.status === 401 && typeof window !== "undefined" && !icYol(window.location.pathname).startsWith("/giris")) {
    cikisYap();
  }
}

async function istek<T>(yol: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${API_URL}${yol}`, {
    ...options,
    headers: { "Content-Type": "application/json", ...yetkiBasligi(), ...options?.headers },
  });
  if (!res.ok) {
    oturumKontrol(res);
    let mesaj = `İstek başarısız (${res.status})`;
    try { mesaj = (await res.json())?.mesaj ?? mesaj; } catch { /* gövde JSON değilse varsayılan mesaj kalır */ }
    if (res.status === 403) mesaj = "Bu işlem için yetkiniz yok (yalnızca Yönetici).";
    throw new ApiError(res.status, mesaj);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

export const api = {
  get: <T>(yol: string) => istek<T>(yol),
  post: <T>(yol: string, veri: unknown) => istek<T>(yol, { method: "POST", body: JSON.stringify(veri) }),
  put: <T>(yol: string, veri: unknown) => istek<T>(yol, { method: "PUT", body: JSON.stringify(veri) }),
  delete: <T>(yol: string) => istek<T>(yol, { method: "DELETE" }),

  /** Dosya yükler (multipart). Content-Type başlığını tarayıcı belirler. */
  async yukle<T>(yol: string, dosya: File): Promise<T> {
    const veri = new FormData();
    veri.append("dosya", dosya);
    const res = await fetch(`${API_URL}${yol}`, { method: "POST", body: veri, headers: yetkiBasligi() });
    if (!res.ok) {
      oturumKontrol(res);
      let mesaj = `Yükleme başarısız (${res.status})`;
      try { mesaj = (await res.json())?.mesaj ?? mesaj; } catch { /* gövde JSON değilse varsayılan mesaj kalır */ }
      if (res.status === 403) mesaj = "Bu işlem için yetkiniz yok (yalnızca Yönetici).";
      throw new ApiError(res.status, mesaj);
    }
    return res.json();
  },

  /** Sunucudan dosya indirir ve tarayıcıda kaydetme işlemini tetikler. */
  async indir(yol: string): Promise<void> {
    const res = await fetch(`${API_URL}${yol}`, { headers: yetkiBasligi() });
    if (!res.ok) { oturumKontrol(res); throw new ApiError(res.status, `İndirme başarısız (${res.status})`); }
    const blob = await res.blob();
    const baslik = res.headers.get("content-disposition") ?? "";
    const eslesme = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(baslik);
    const ad = eslesme ? decodeURIComponent(eslesme[1]) : "indirilen-dosya";
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = ad;
    document.body.appendChild(a);
    a.click();
    a.remove();
    URL.revokeObjectURL(url);
  },
};

export interface IceAktarmaSonucu {
  eklenen: number;
  /** Zaten kayıtlı olup bu dosyayla en az bir alanı değişen kayıt sayısı. */
  guncellenen: number;
  /** Kayıtlı olup hiçbir alanı değişmeyen (ve kullanılamayan) satırlar. */
  atlanan: number;
  hatalar: string[];
}

// ---- Tipler ----

export interface Sayfali<T> {
  toplam: number; sayfa: number; sayfaBoyutu: number; kayitlar: T[];
  /** Listeyle aynı filtre sorgusundan atomik olarak hesaplanan özet değerler. */
  istatistik?: Record<string, number>;
}

export interface EsnafYetkilisi {
  id: number; adSoyad: string; gorevi?: string | null;
  yetkiBaslangic?: string | null; yetkiBitis?: string | null;
}

export interface EsnafKaydi {
  id: number; adSoyad: string; isletme: string; vergiNo?: string | null;
  grupId?: number | null; grup?: string | null; grupNo?: number | null;
  il?: string | null; ilce?: string | null; mahalle?: string | null;
  adres?: string | null; telefon?: string | null; isTelefonu?: string | null;
  gorevliId?: number | null; gorevli?: string | null;
  durum: string; sonGorusmeTarihi?: string | null; kayitTarihi: string;
  // Oda kayıt bilgileri (ÜYE LİSTE DETAY RAPORU kolonları)
  uyeSicilNo?: string | null; ticaretSicilNo?: string | null; sirketTipi?: string | null;
  tabelaUnvani?: string | null; uyruk?: string | null; sermaye?: string | null; derece?: string | null;
  vergiDairesi?: string | null; vergiTerkTarihi?: string | null;
  kurulusTarihi?: string | null; odaKararTarihi?: string | null; gorevi?: string | null;
  uyelikDurumu?: string | null; durumDegisimTarihi?: string | null; durumDegisimNedeni?: string | null;
  faaliyetDetayi?: string | null; naceKodu?: string | null; naceAdi?: string | null;
  yetkililer?: EsnafYetkilisi[];
  /** Liste ucundan gelir: üyenin oda kaydındaki yetkili sayısı (detayda tam liste bulunur). */
  yetkiliSayisi?: number;
}

export interface GrupKaydi {
  id: number; no?: number | null; ad: string; aciklama?: string | null; tur: string;
  ustGrupId?: number | null; ustGrup?: string | null;
  esnafSayisi: number; aktifGorevli: number; durum: string; guncellemeTarihi: string;
}

export interface KullaniciKaydi {
  id: number; adSoyad: string; kullaniciAdi: string; rol: string;
  gorev?: string | null; birim?: string | null; eposta?: string | null;
  telefon?: string | null; durum: string;
}

export interface GorusmeKaydi {
  id: number; tarih: string; sonuc: string; not?: string | null; takipGerekli: boolean;
  /** Üyeyle kaçıncı görüşme olduğu (1, 2, 3...). Sunucuda hesaplanır. */
  sira: number;
  esnafId: number; esnaf: string; isletme: string; grup?: string | null; grupNo?: number | null;
  ilce?: string | null; mahalle?: string | null; telefon?: string | null; esnafDurum: string;
  gorevliId: number; gorevli: string;
  /** Görüşmeye eşlik eden ikinci çalışan ("Görüşecek Kişi"); isteğe bağlıdır. */
  ikinciGorevliId?: number | null; ikinciGorevli?: string | null;
}


export interface OnayKaydi {
  id: number; islemTuru: string; tarih: string; durum: string;
  esnafId: number; esnaf: string; isletme: string; grup?: string | null;
  ilce?: string | null; mahalle?: string | null; telefon?: string | null;
  gorevliId?: number | null; gorevli?: string | null;
}

export interface DashboardOzet {
  toplamEsnaf: number; gorusulen: number; onayVeren: number; onayVermeyen: number;
  kararsiz: number; gorusulmemis: number;
  /** Seçili tarih aralığındaki görüşme sayısı. */
  aralikGorusme: number;
  baslangic: string; bitis: string;
  gunlukGorusmeler: { tarih: string; ay: string; adet: number }[];
  gorevliPerformans: { adSoyad: string; adet: number }[];
  sonGorusmeler: {
    id: number; tarih: string; sonuc: string; sira: number; esnaf: string; isletme: string;
    grup?: string | null; grupNo?: number | null; esnafDurum: string; gorevli: string;
  }[];
}

export interface GrupRaporSatiri {
  id: number; no?: number | null; ad: string; toplamEsnaf: number; gorusme: number;
  onaylayan: number; reddedilen: number; kararsiz: number; gorusulmeyen: number; onayOrani: number;
}

export interface CalisanRaporSatiri {
  id: number; adSoyad: string; gorev?: string | null; birim?: string | null; durum: string; rol: string;
  /** Çalışanın görüşen görevli olarak yaptığı görüşme sayısı (ikinci kişi katılımı buraya girmez). */
  gorusme: number;
  /** Kaç farklı üyeyle görüşüldüğü. */
  uyeSayisi: number;
  onayVerdi: number; onayVermedi: number; kararsiz: number;
  /** Bu çalışanın görüşmesinde onay veren farklı üye sayısı. */
  onayliUyeSayisi: number;
  /** İkinci kişi olarak katıldığı görüşme sayısı; sayımlara dahil değildir. */
  ikinciKatilim: number;
  sonGorusme?: string | null; onayOrani: number;
}

export interface CalisanRaporu {
  satirlar: CalisanRaporSatiri[];
  toplam: { calisan: number; gorusme: number; onayVerdi: number; onayVermedi: number; kararsiz: number };
}

export interface CalisanGorusmeSatiri {
  id: number; gorevliId: number; gorevli: string; ikinciGorevli?: string | null;
  esnafId: number; esnaf: string; isletme: string; grup?: string | null; grupNo?: number | null;
  ilce?: string | null; telefon?: string | null;
  sira: number; tarih: string; sonuc: string; takipGerekli: boolean; not?: string | null;
}

// ---- Yardımcılar ----

export type StatusTone = "success" | "danger" | "warning" | "neutral" | "info" | "gray";

export function durumTonu(durum: string): StatusTone {
  switch (durum) {
    case "Onay Verdi": case "Onaylandı": case "Aktif": case "Tamamlandı": case "Faal": return "success";
    case "Onay Vermedi": case "Reddedildi": case "İptal Edildi": return "danger";
    case "Kararsız": case "Bekliyor": case "Bekleyen": case "Askı": return "warning";
    case "Görüşülmedi": case "Pasif": return "neutral";
    default: return "info";
  }
}

export function tarihGoster(iso?: string | null): string {
  if (!iso) return "-";
  const t = new Date(iso);
  return t.toLocaleString("tr-TR", { day: "2-digit", month: "2-digit", year: "numeric", hour: "2-digit", minute: "2-digit" });
}

export function sayiGoster(n: number): string {
  return n.toLocaleString("tr-TR");
}

export function yuzde(pay: number, payda: number): string {
  if (!payda) return "%0";
  return `%${((pay * 100) / payda).toLocaleString("tr-TR", { maximumFractionDigits: 1 })}`;
}
