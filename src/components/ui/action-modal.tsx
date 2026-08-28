"use client";

import { FormEvent, useEffect, useState } from "react";
import { CheckCircle2 } from "lucide-react";
import { CokluSecim } from "./coklu-secim";
import { EsnafSecici } from "./esnaf-secici";
import { FormField, Modal } from "./modal";
import { api, ApiError, grupEtiketi, ODEME_SECENEKLERI, ODENDI, GrupKaydi, KullaniciKaydi } from "@/lib/api";
import { sistemYoneticisiMi } from "@/lib/auth";
import { ILLER, ilceleriGetir } from "@/lib/il-ilce";

type Alan = {
  name: string;
  label: string;
  /** "coklu" = onay kutulu çoklu seçim; değer virgülle ayrılmış tek bir gizli girdide taşınır. */
  tip: "text" | "email" | "date" | "textarea" | "select" | "coklu" | "hidden" | "password" | "tel";
  zorunlu?: boolean;
  secenekKaynagi?: "gruplar" | "gorevliler" | "esnaflar" | "sabit" | "iller" | "ilceler" | "gorusmeSirasi";
  sabitSecenekler?: string[];
  /** true: alan yalnızca sistem yöneticisine gösterilir (şifre gibi hesap alanları). */
  sistemYonetimi?: boolean;
};

/** Telefon: 0 ile başlayan 11 hane (05XX XXX XX XX). Rakam dışı her şey atılır, fazlası kesilir. */
const TELEFON_UZUNLUK = 11;
const telefonTemizle = (deger: string) => deger.replace(/\D/g, "").slice(0, TELEFON_UZUNLUK);

/**
 * Görüşme sonucu seçenekleri. "Takip Edilecek" ve "Gelmeyecek" ayrı bir "takip durumu"
 * alanı olmaktan çıkıp sonucun kendisine taşındı; sunucu `TakipGerekli` bayrağını bu
 * değerden türetir (bkz. GorusmelerController.TakipSonucu).
 */
export const GORUSME_SONUCLARI = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Takip Edilecek", "Gelmeyecek"];
/** Üyenin onay durumu: görüşme sonuçları + hiç görüşülmemiş üyeler. */
const ESNAF_DURUMLARI = [...GORUSME_SONUCLARI, "Görüşülmedi"];
// Odadaki üyelik durumu (kaynak raporun "DURUM TANIMI" kolonu). Görüşme onay durumundan ayrıdır.
const UYELIK_DURUMLARI = ["Faal", "Askı", "Pasif"];

/** `gonder`, isteğe bağlı olarak kendi başarı mesajını döndürebilir (ör. sunucunun türettiği kullanıcı adı). */
type FormTanimi = { alanlar: Alan[]; buton: string; gonder: (v: Record<string, string>) => Promise<string | void> };
type DuzenlemeTanimi = { alanlar: Alan[]; buton: string; gonder: (id: number, v: Record<string, string>) => Promise<void> };

// Alan düzeni odanın "ÜYE LİSTE DETAY RAPORU" kolonlarını izler.
const esnafAlanlari: Alan[] = [
  { name: "uyeSicilNo", label: "Üye Sicil No", tip: "text" },
  { name: "isletme", label: "Unvan", tip: "text", zorunlu: true },
  { name: "tabelaUnvani", label: "Tabela Unvanı", tip: "text" },
  { name: "adSoyad", label: "Yetkili Adı Soyadı", tip: "text", zorunlu: true },
  { name: "gorevi", label: "Yetkilinin Görevi", tip: "text" },
  { name: "sirketTipi", label: "Şirket Tipi", tip: "text" },
  { name: "ticaretSicilNo", label: "Ticaret Sicil No", tip: "text" },
  { name: "grupId", label: "Meslek Grubu", tip: "select", secenekKaynagi: "gruplar", zorunlu: true },
  { name: "uyelikDurumu", label: "Üyelik Durumu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: UYELIK_DURUMLARI, zorunlu: true },
  { name: "durumDegisimTarihi", label: "Durum Değişim Tarihi", tip: "date" },
  { name: "durumDegisimNedeni", label: "Durum Değişim Nedeni", tip: "text" },
  { name: "telefon", label: "Cep Telefonu (GSM)", tip: "tel" },
  { name: "isTelefonu", label: "İş Telefonu", tip: "tel" },
  { name: "vergiDairesi", label: "Vergi Dairesi", tip: "text" },
  { name: "vergiNo", label: "Vergi Numarası", tip: "text" },
  { name: "kurulusTarihi", label: "Kuruluş Tarihi", tip: "date" },
  { name: "kayitTarihi", label: "Üye Kayıt Tarihi", tip: "date" },
  { name: "naceKodu", label: "NACE Faaliyet Kodu", tip: "text" },
  { name: "naceAdi", label: "NACE Faaliyet Adı", tip: "text" },
  { name: "gorevliId", label: "Görevli", tip: "select", secenekKaynagi: "gorevliler" },
  { name: "il", label: "İl", tip: "select", secenekKaynagi: "iller", zorunlu: true },
  { name: "ilce", label: "İlçe", tip: "select", secenekKaynagi: "ilceler", zorunlu: true },
  { name: "mahalle", label: "Mahalle", tip: "text" },
  { name: "adres", label: "Adres", tip: "textarea" },
  { name: "faaliyetDetayi", label: "Faaliyet Detayı", tip: "textarea" },
];

function esnafGovdesi(v: Record<string, string>) {
  return {
    adSoyad: v.adSoyad, isletme: v.isletme, telefon: v.telefon || null, isTelefonu: v.isTelefonu || null,
    grupId: v.grupId ? Number(v.grupId) : null, gorevliId: v.gorevliId ? Number(v.gorevliId) : null,
    il: v.il || null, ilce: v.ilce || null, mahalle: v.mahalle || null, adres: v.adres || null, vergiNo: v.vergiNo || null,
    durum: v.durum || null,
    uyeSicilNo: v.uyeSicilNo || null, ticaretSicilNo: v.ticaretSicilNo || null, sirketTipi: v.sirketTipi || null,
    tabelaUnvani: v.tabelaUnvani || null, gorevi: v.gorevi || null, vergiDairesi: v.vergiDairesi || null,
    uyelikDurumu: v.uyelikDurumu || null,
    durumDegisimTarihi: v.durumDegisimTarihi || null, durumDegisimNedeni: v.durumDegisimNedeni || null,
    kurulusTarihi: v.kurulusTarihi || null, kayitTarihi: v.kayitTarihi || null,
    naceKodu: v.naceKodu || null, naceAdi: v.naceAdi || null, faaliyetDetayi: v.faaliyetDetayi || null,
  };
}

const kullaniciAlanlari: Alan[] = [
  { name: "adSoyad", label: "Ad Soyad", tip: "text", zorunlu: true },
  { name: "eposta", label: "E-posta", tip: "email" },
  { name: "telefon", label: "Telefon", tip: "tel" },
  { name: "rol", label: "Rol (Görevli = yalnızca çalışan kaydı, panele giremez)", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Görevli", "Yönetici"], zorunlu: true },
  { name: "gorev", label: "Görev", tip: "text" },
  { name: "birim", label: "Birim", tip: "text" },
  // Odanın grup listesinde bir kişi birden çok gruba bakabildiği için çoklu seçim.
  { name: "grupIdler", label: "Sorumlu Olduğu Meslek Grupları", tip: "coklu", secenekKaynagi: "gruplar" },
];

function kullaniciGovdesi(v: Record<string, string>) {
  return {
    adSoyad: v.adSoyad, kullaniciAdi: v.kullaniciAdi ?? "", rol: v.rol,
    gorev: v.gorev || null, birim: v.birim || null, eposta: v.eposta || null, telefon: v.telefon || null,
    durum: v.durum || null, sifre: v.sifre || null,
    // Gönderilen liste kaydın tam karşılığıdır; boş dizi "grubu kalmadı" demektir.
    grupIdler: (v.grupIdler ?? "").split(",").filter(Boolean).map(Number),
  };
}

export const formlar: Record<string, FormTanimi> = {
  "Yeni Üye Ekle": {
    alanlar: esnafAlanlari,
    buton: "Üye Kaydını Oluştur",
    gonder: async v => { await api.post("/api/esnaflar", esnafGovdesi(v)); },
  },
  "Yeni Görüşme": {
    alanlar: [
      { name: "esnafId", label: "Üye", tip: "select", secenekKaynagi: "esnaflar", zorunlu: true },
      { name: "sira", label: "Kaçıncı Görüşme", tip: "select", secenekKaynagi: "gorusmeSirasi", zorunlu: true },
      { name: "gorevliId", label: "Görüşen Aktif Çalışan", tip: "select", secenekKaynagi: "gorevliler", zorunlu: true },
      { name: "ikinciGorevliId", label: "Görüşecek Kişi (isteğe bağlı ikinci çalışan)", tip: "select", secenekKaynagi: "gorevliler" },
      { name: "tarih", label: "Görüşme Tarihi (boş bırakılırsa bugün)", tip: "date" },
      { name: "sonuc", label: "Görüşme Sonucu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: GORUSME_SONUCLARI, zorunlu: true },
      { name: "not", label: "Not / Yorum", tip: "textarea" },
    ],
    buton: "Görüşmeyi Kaydet",
    gonder: async v => {
      await api.post("/api/gorusmeler", {
        esnafId: Number(v.esnafId), gorevliId: v.gorevliId ? Number(v.gorevliId) : 0,
        ikinciGorevliId: v.ikinciGorevliId ? Number(v.ikinciGorevliId) : null,
        // Takip bayrağını sunucu sonuçtan türetir; forma ayrı bir alan olarak sorulmaz.
        tarih: v.tarih || null, sonuc: v.sonuc, not: v.not || null, takipGerekli: false,
        sira: v.sira ? Number(v.sira) : null,
      });
    },
  },
  "Yeni Grup Ekle": {
    alanlar: [
      { name: "no", label: "Meslek Grubu No (gruplar listelerde \"5. Grup\" gibi görünür)", tip: "text", zorunlu: true },
      { name: "ad", label: "Grup Adı", tip: "text", zorunlu: true },
      { name: "tur", label: "Grup Türü", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Sektörel", "Bölgesel", "Özel"], zorunlu: true },
      { name: "ustGrupId", label: "Üst Grup", tip: "select", secenekKaynagi: "gruplar" },
      { name: "aciklama", label: "Açıklama", tip: "textarea" },
    ],
    buton: "Grubu Oluştur",
    gonder: async v => {
      await api.post("/api/gruplar", {
        no: v.no ? Number(v.no) : null,
        ad: v.ad, tur: v.tur, ustGrupId: v.ustGrupId ? Number(v.ustGrupId) : null, aciklama: v.aciklama || null,
      });
    },
  },
  "Yeni Çalışan Ekle": {
    // Şifre yalnızca panele girecek Yönetici hesapları için gereklidir; çalışan kaydı şifresiz açılır
    // ve giriş yapamaz, yalnızca görüşmelerde seçilir.
    // Şifre alanı yalnızca sistem yöneticisinde görünür: giriş yapabilen hesap açmak ona özeldir.
    // Diğer yöneticiler buradan yalnızca şifresiz görevli kaydı ekler.
    alanlar: [...kullaniciAlanlari, { name: "sifre", label: "Geçici Şifre (yalnızca Yönetici için; en az 10 karakter, büyük/küçük harf ve rakam)", tip: "password", sistemYonetimi: true }],
    buton: "Çalışanı Kaydet",
    // Kullanıcı adını sunucu Ad Soyad'dan türetir; yöneticinin bilemeyeceği tek bilgi budur ve
    // başarı bildiriminde gösterilir. Şifreyi yönetici zaten kendisi yazdığı için tekrar gösterilmez.
    gonder: async v => {
      const olusan = await api.post<{ id: number; kullaniciAdi: string }>("/api/kullanicilar", kullaniciGovdesi(v));
      return `Çalışan kaydedildi. Kullanıcı adı: ${olusan.kullaniciAdi}`;
    },
  },
};

export const duzenlemeFormlari: Record<string, DuzenlemeTanimi> = {
  esnaf: {
    alanlar: [...esnafAlanlari, { name: "durum", label: "Onay Durumu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ESNAF_DURUMLARI, zorunlu: true }],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/esnaflar/${id}`, esnafGovdesi(v)),
  },
  // Üye kartındaki "Düzenle" düğmesi bu formu açar: görünen ve gönderilen tek alan üyelik
  // durumudur. Ayrı ve dar bir uç kullanılır (tam gövde bekleyen PUT /api/esnaflar/{id} değil),
  // böylece üyenin diğer bilgileri bu ekrandan hiçbir şekilde değiştirilemez.
  esnafDurum: {
    alanlar: [
      { name: "uyelikDurumu", label: "Üyelik Durumu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: UYELIK_DURUMLARI, zorunlu: true },
      // Askıdaki üye borcunu ödeyince aynı ekrandan hem "Faal"a alınır hem ödeme işaretlenir.
      { name: "odendi", label: "Ödeme Durumu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ODEME_SECENEKLERI, zorunlu: true },
    ],
    buton: "Üyelik Durumunu Kaydet",
    gonder: (id, v) => api.put(`/api/esnaflar/${id}/uyelik-durumu`, {
      uyelikDurumu: v.uyelikDurumu,
      // Sunucu bool bekler; ödeme tarihini kendisi damgalar.
      odendi: v.odendi === ODENDI,
    }),
  },
  grup: {
    alanlar: [
      { name: "no", label: "Meslek Grubu No (gruplar listelerde \"5. Grup\" gibi görünür)", tip: "text", zorunlu: true },
      { name: "ad", label: "Grup Adı", tip: "text", zorunlu: true },
      { name: "tur", label: "Grup Türü", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Sektörel", "Bölgesel", "Özel"], zorunlu: true },
      { name: "ustGrupId", label: "Üst Grup", tip: "select", secenekKaynagi: "gruplar" },
      { name: "durum", label: "Durum", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Aktif", "Pasif"], zorunlu: true },
      { name: "aciklama", label: "Açıklama", tip: "textarea" },
    ],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/gruplar/${id}`, {
      no: v.no ? Number(v.no) : null,
      ad: v.ad, tur: v.tur, ustGrupId: v.ustGrupId ? Number(v.ustGrupId) : null,
      aciklama: v.aciklama || null, durum: v.durum,
    }),
  },
  kullanici: {
    alanlar: [
      ...kullaniciAlanlari,
      { name: "durum", label: "Durum", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Aktif", "Pasif"], zorunlu: true },
      { name: "kullaniciAdi", label: "", tip: "hidden" },
    ],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/kullanicilar/${id}`, kullaniciGovdesi(v)),
  },
  gorusme: {
    alanlar: [
      { name: "sira", label: "Kaçıncı Görüşme", tip: "select", secenekKaynagi: "gorusmeSirasi", zorunlu: true },
      { name: "gorevliId", label: "Görüşen Aktif Çalışan", tip: "select", secenekKaynagi: "gorevliler", zorunlu: true },
      { name: "ikinciGorevliId", label: "Görüşecek Kişi (isteğe bağlı ikinci çalışan)", tip: "select", secenekKaynagi: "gorevliler" },
      { name: "tarih", label: "Görüşme Tarihi (boş bırakılırsa bugün)", tip: "date" },
      { name: "sonuc", label: "Görüşme Sonucu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: GORUSME_SONUCLARI, zorunlu: true },
      { name: "not", label: "Not / Yorum", tip: "textarea" },
      { name: "esnafId", label: "", tip: "hidden" },
    ],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/gorusmeler/${id}`, {
      esnafId: Number(v.esnafId), gorevliId: v.gorevliId ? Number(v.gorevliId) : 0,
      ikinciGorevliId: v.ikinciGorevliId ? Number(v.ikinciGorevliId) : null,
      tarih: v.tarih || null, sonuc: v.sonuc, not: v.not || null, takipGerekli: false,
      sira: v.sira ? Number(v.sira) : null,
    }),
  },
};

export interface DuzenlemeIstegi { form: keyof typeof duzenlemeFormlari; id: number; degerler: Record<string, string>; baslik: string }

export function ActionModal({ action, duzenleme, open, onClose, onSuccess, onSaved, onDoldurma }: {
  action: string; duzenleme?: DuzenlemeIstegi | null; open: boolean; onClose: () => void;
  onSuccess: (message: string) => void; onSaved?: () => void;
  /** Yeni kayıt formuna önden doldurulacak değerler (ör. üye ekranından açılan görüşme). */
  onDoldurma?: Record<string, string>;
}) {
  const [busy, setBusy] = useState(false);
  const [hata, setHata] = useState("");
  const [gruplar, setGruplar] = useState<GrupKaydi[]>([]);
  const [gorevliler, setGorevliler] = useState<KullaniciKaydi[]>([]);
  // İlçe listesi seçili ile bağlıdır; il değişince ilçe seçimi sıfırlanır.
  const [secilenIl, setSecilenIl] = useState("");
  // Görüşme sırası seçilen üyenin mevcut görüşme sayısına bağlıdır; üye seçilince yeniden hesaplanır.
  const [secilenEsnafId, setSecilenEsnafId] = useState("");
  const [gorusmeSayisi, setGorusmeSayisi] = useState<number | null>(null);
  // Çoklu seçim alanları (ör. meslek grupları) denetimli bileşendir; değer virgülle ayrılmış
  // tek bir gizli girdide taşınır, çünkü FormData çok değerli bir alanın yalnızca sonuncusunu tutar.
  const [cokluDegerler, setCokluDegerler] = useState<Record<string, string>>({});

  const form: FormTanimi | DuzenlemeTanimi | undefined = duzenleme ? duzenlemeFormlari[duzenleme.form] : formlar[action];
  const baslik = duzenleme ? duzenleme.baslik : action;
  const degerler = duzenleme?.degerler ?? onDoldurma ?? {};

  useEffect(() => {
    if (!open || !form) return;
    setHata("");
    // Düzenlemede kayıtlı il ile açılır; yeni kayıtta boş başlar.
    setSecilenIl(duzenleme?.degerler.il ?? "");
    setSecilenEsnafId(duzenleme?.degerler.esnafId ?? onDoldurma?.esnafId ?? "");
    setGorusmeSayisi(null);
    const baslangic = duzenleme?.degerler ?? onDoldurma ?? {};
    setCokluDegerler(Object.fromEntries(form.alanlar
      .filter(a => a.tip === "coklu")
      .map(a => [a.name, baslangic[a.name] ?? ""])));
    const kaynaklar = new Set(form.alanlar.map(a => a.secenekKaynagi).filter(Boolean));
    if (kaynaklar.has("gruplar")) api.get<GrupKaydi[]>("/api/gruplar").then(setGruplar).catch(() => setGruplar([]));
    if (kaynaklar.has("gorevliler")) api.get<KullaniciKaydi[]>("/api/kullanicilar?durum=Aktif").then(setGorevliler).catch(() => setGorevliler([]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, action, duzenleme?.form, duzenleme?.id]);

  // Görüşme sırası seçenekleri üyenin mevcut görüşme sayısına bağlı; liste ucunun
  // "toplam" değeri sayıyı zaten veriyor, ayrı bir uç gerekmiyor.
  useEffect(() => {
    if (!open || !secilenEsnafId) { setGorusmeSayisi(null); return; }
    let aktif = true;
    api.get<{ toplam: number }>(`/api/gorusmeler?esnafId=${secilenEsnafId}&sayfaBoyutu=1`)
      .then(v => { if (aktif) setGorusmeSayisi(v.toplam); })
      .catch(() => { if (aktif) setGorusmeSayisi(null); });
    return () => { aktif = false; };
  }, [open, secilenEsnafId]);

  if (!form) return null;

  // Yeni kayıtta sıradaki numara (mevcut + 1), düzenlemede kaydın kendi numarası varsayılan gelir.
  const siraUstSinir = gorusmeSayisi === null ? null : gorusmeSayisi + (duzenleme ? 0 : 1);
  const varsayilanSira = duzenleme ? degerler.sira : (siraUstSinir === null ? "" : String(siraUstSinir));

  function secenekler(alan: Alan): { deger: string; etiket: string }[] {
    switch (alan.secenekKaynagi) {
      case "gruplar": return gruplar.map(g => ({ deger: String(g.id), etiket: grupEtiketi(g.no, g.ad) }));
      case "gorevliler": {
        const liste = gorevliler.map(k => ({ deger: String(k.id), etiket: k.adSoyad }));
        // Liste yalnızca aktif çalışanları getirir. Kayıttaki kişi pasife alınmışsa seçenek
        // arasında olmaz, select boşa düşer ve kaydetmek alanı silerdi.
        const mevcut = degerler[alan.name];
        if (mevcut && !liste.some(s => s.deger === mevcut))
          liste.unshift({ deger: mevcut, etiket: degerler[`${alan.name}Etiket`] || `Pasif çalışan (#${mevcut})` });
        return liste;
      }
      case "iller": return ILLER.map(i => ({ deger: i, etiket: i }));
      case "ilceler": return ilceleriGetir(secilenIl).map(i => ({ deger: i, etiket: i }));
      case "gorusmeSirasi": {
        // Numara atlanamaz: en fazla "mevcut görüşme sayısı + 1" seçilebilir.
        const ust = siraUstSinir ?? (duzenleme ? Number(degerler.sira || 1) : 1);
        return Array.from({ length: Math.max(1, ust) }, (_, i) => ({
          deger: String(i + 1), etiket: `${i + 1}. Görüşme`,
        }));
      }
      default: {
        // "Yönetici" rolü panele giriş demektir; bu seçeneği yalnızca sistem yöneticisi görür.
        // Diğerleri görevli (şifresiz, giriş yapamayan) kaydı açabilir.
        const sabit = alan.name === "rol" && !sistemYoneticisiMi()
          ? (alan.sabitSecenekler ?? []).filter(x => x !== "Yönetici")
          : alan.sabitSecenekler ?? [];
        // Kaydın mevcut değeri sabit listede yoksa (ör. karar bekleyen görevlendirme) seçili kalabilmesi
        // için listeye eklenir; aksi halde zorunlu alan boş açılır ve form gönderilemez.
        const mevcut = degerler[alan.name];
        const liste = mevcut && !sabit.includes(mevcut) ? [mevcut, ...sabit] : sabit;
        return liste.map(s => ({ deger: s, etiket: s }));
      }
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) return;
    const veri = Object.fromEntries(new FormData(event.currentTarget).entries()) as Record<string, string>;
    setBusy(true);
    setHata("");
    try {
      let mesaj: string;
      if (duzenleme) {
        await (form as DuzenlemeTanimi).gonder(duzenleme.id, veri);
        mesaj = "Kayıt güncellendi.";
      } else {
        // Form kendi mesajını verebilir; vermezse genel metin kullanılır. Sunucu 201 gövdesi
        // döndürse bile burada hiçbir ek ekran açılmaz.
        mesaj = await (form as FormTanimi).gonder(veri) || `${action} işlemi başarıyla kaydedildi.`;
      }
      onClose();
      onSuccess(mesaj);
      onSaved?.();
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı. API'nin çalıştığından emin olun.");
    } finally {
      setBusy(false);
    }
  }

  // Panele yalnızca yönetici girdiği için görüşmeyi kimin yaptığı her zaman elle seçilir.
  const kisitli = false;
  // Hesap alanları (şifre) yalnızca sistem yöneticisinde çizilir; sunucu da aynı kuralı uygular.
  const gosterilecekAlanlar = form.alanlar.filter(a => !a.sistemYonetimi || sistemYoneticisiMi());

  return <Modal open={open} title={baslik} onClose={onClose}>
    <form className="action-form" onSubmit={submit} autoComplete="off" key={duzenleme ? `d-${duzenleme.form}-${duzenleme.id}` : action}>
      <div className="form-grid">
        {gosterilecekAlanlar.map((alan, index) => alan.tip === "hidden"
          ? <input key={alan.name} type="hidden" name={alan.name} defaultValue={degerler[alan.name] ?? ""} />
          : <FormField key={alan.name} label={alan.label} genis={alan.secenekKaynagi === "esnaflar" || alan.tip === "coklu"}>
            {alan.tip === "coklu"
              ? <>
                  <CokluSecim label={alan.label} options={secenekler(alan)}
                    value={cokluDegerler[alan.name] ?? ""}
                    onChange={deger => setCokluDegerler(d => ({ ...d, [alan.name]: deger }))} />
                  <input type="hidden" name={alan.name} value={cokluDegerler[alan.name] ?? ""} readOnly />
                </>
              : alan.secenekKaynagi === "esnaflar"
              // Üye seçici yalnızca görüşme formunda kullanılıyor; görüşme de yalnızca
              // üyeliği faal olanlarla yapılabildiği için liste buna göre süzülür.
              ? <EsnafSecici name={alan.name} required={alan.zorunlu} sadeceGorevlendirilmis={kisitli} sadeceFaal
                  defaultId={degerler[alan.name] || undefined} defaultEtiket={degerler[`${alan.name}Etiket`] || undefined}
                  onSecim={setSecilenEsnafId} />
              : alan.tip === "textarea" ? <textarea name={alan.name} required={alan.zorunlu} maxLength={500} placeholder={`${alan.label} giriniz`} defaultValue={degerler[alan.name] ?? ""} />
              : alan.secenekKaynagi === "ilceler"
                // İlçe listesi seçili ile bağlı: il değişince key değişir, seçim sıfırdan başlar.
                ? <select key={`ilce-${secilenIl}`} name={alan.name} required={alan.zorunlu} disabled={!secilenIl}
                    defaultValue={secilenIl && secilenIl === degerler.il ? degerler[alan.name] ?? "" : ""}>
                    <option value="" disabled={alan.zorunlu}>{secilenIl ? "Seçiniz" : "Önce il seçiniz"}</option>
                    {secenekler(alan).map(s => <option key={s.deger} value={s.deger}>{s.etiket}</option>)}
                  </select>
              : alan.secenekKaynagi === "gorusmeSirasi"
                // Üye değişince liste ve varsayılan yeniden kurulur (key ile yeniden oluşturulur).
                ? <select key={`sira-${secilenEsnafId}-${gorusmeSayisi}`} name={alan.name} required={alan.zorunlu}
                    disabled={!secilenEsnafId || (!duzenleme && gorusmeSayisi === null)}
                    defaultValue={varsayilanSira ?? ""}>
                    {!secilenEsnafId
                      ? <option value="">Önce üye seçiniz</option>
                      : gorusmeSayisi === null && !duzenleme
                        ? <option value="">Hesaplanıyor...</option>
                        : secenekler(alan).map(s => <option key={s.deger} value={s.deger}>{s.etiket}</option>)}
                  </select>
              : alan.tip === "select" ? <select name={alan.name} required={alan.zorunlu} defaultValue={degerler[alan.name] ?? ""}
                  onChange={alan.secenekKaynagi === "iller" ? e => setSecilenIl(e.target.value) : undefined}>
                  <option value="" disabled={alan.zorunlu}>{alan.zorunlu ? "Seçiniz" : "Seçiniz (isteğe bağlı)"}</option>
                  {secenekler(alan).map(s => <option key={s.deger} value={s.deger}>{s.etiket}</option>)}
                </select>
              : alan.tip === "tel" ? <input name={alan.name} type="tel" inputMode="numeric" required={alan.zorunlu}
                  maxLength={TELEFON_UZUNLUK} pattern={`0[0-9]{${TELEFON_UZUNLUK - 1}}`}
                  title={`Telefon 0 ile başlayan ${TELEFON_UZUNLUK} haneli olmalıdır. Örnek: 05321234567`}
                  placeholder="05321234567"
                  // Yapıştırma ve otomatik doldurma da dahil her girdi rakama indirgenir, fazlası kesilir.
                  onInput={e => { e.currentTarget.value = telefonTemizle(e.currentTarget.value); }}
                  defaultValue={telefonTemizle(degerler[alan.name] ?? "")} autoFocus={index === 0} />
              : <input name={alan.name} type={alan.tip} required={alan.zorunlu}
                  maxLength={alan.tip === "password" ? 128 : 120} minLength={alan.tip === "password" ? 10 : undefined}
                  autoComplete={alan.tip === "password" ? "new-password" : undefined} defaultValue={degerler[alan.name] ?? ""}
                  placeholder={alan.tip === "email" ? "ornek@erzto.org.tr" : alan.tip === "password" ? "••••••••••" : `${alan.label} giriniz`} autoFocus={index === 0} />}
          </FormField>)}
      </div>
      {hata && <p className="security-note" role="alert" style={{ color: "#c0392b" }}>{hata}</p>}
      <footer>
        <button type="button" className="secondary-button" onClick={onClose}>Vazgeç</button>
        <button type="submit" className="primary-button" disabled={busy}><CheckCircle2 size={17} />{busy ? "Kaydediliyor..." : form.buton}</button>
      </footer>
    </form>
  </Modal>;
}
