"use client";

import { FormEvent, useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, CircleHelp, Clock3, CloudDownload, CloudUpload, Eye, ListFilter, Maximize2, MessageSquareText, Minimize2, Pencil, ShieldCheck, Store, Trash2, UserRound, UsersRound, XCircle } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { ActionModal, DuzenlemeIstegi } from "@/components/ui/action-modal";
import { BarList, DonutChart, DonutSegment } from "@/components/ui/charts";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { DetailModal, DetayGorusme, DetaySatiri } from "@/components/ui/detail-modal";
import { FilterBar, FiltreSecim } from "@/components/ui/filter-bar";
import { ImportModal } from "@/components/ui/import-modal";
import { Modal } from "@/components/ui/modal";
import { StatCard } from "@/components/ui/stat-card";
import { Toast } from "@/components/ui/toast";
import {
  api, ApiError, API_ERISIM_HATASI, durumTonu, sayiGoster, tarihGoster, yuzde,
  EsnafKaydi, GorusmeKaydi, GrupKaydi, KullaniciKaydi, OnayKaydi, Sayfali,
} from "@/lib/api";
import { kimlik, yoneticiMi } from "@/lib/auth";

/** Üyenin görüşme sonucundan gelen onay durumu. */
const ESNAF_ONAY_DURUMLARI = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Görüşülmedi"];

type PageKind = "esnaflar" | "onaylar" | "gruplar" | "calisanlar";

const config: Record<PageKind, { title: string; action: string; tabs: { etiket: string; filtre: Record<string, string> }[] }> = {
  esnaflar: {
    title: "Üyeler", action: "Yeni Üye Ekle",
    tabs: [
      { etiket: "Tümü", filtre: {} },
      { etiket: "Faal", filtre: { uyelikDurumu: "Faal" } },
      { etiket: "Askıdaki", filtre: { uyelikDurumu: "Askı" } },
      { etiket: "Onay Veren", filtre: { durum: "Onay Verdi" } },
      { etiket: "Onay Vermeyen", filtre: { durum: "Onay Vermedi" } },
      { etiket: "Kararsız", filtre: { durum: "Kararsız" } },
      { etiket: "Görüşülmemiş", filtre: { durum: "Görüşülmedi" } },
    ],
  },
  onaylar: {
    title: "Onaylar", action: "",
    tabs: [
      { etiket: "Bekleyen Onaylar", filtre: { durum: "Bekliyor" } },
      { etiket: "Onaylananlar", filtre: { durum: "Onaylandı" } },
      { etiket: "Reddedilenler", filtre: { durum: "Reddedildi" } },
      { etiket: "İptal Edilenler", filtre: { durum: "İptal Edildi" } },
      { etiket: "Tümü", filtre: {} },
    ],
  },
  gruplar: {
    title: "Gruplar", action: "Yeni Grup Ekle",
    tabs: [
      { etiket: "Tüm Gruplar", filtre: {} },
      { etiket: "Ana Gruplar", filtre: { seviye: "ana" } },
      { etiket: "Alt Gruplar", filtre: { seviye: "alt" } },
    ],
  },
  calisanlar: {
    title: "Aktif Çalışanlar", action: "Yeni Çalışan Ekle",
    tabs: [
      { etiket: "Aktif Çalışanlar", filtre: { durum: "Aktif" } },
      { etiket: "Pasif", filtre: { durum: "Pasif" } },
      { etiket: "Yöneticiler", filtre: { rol: "Yönetici" } },
      { etiket: "Tümü", filtre: {} },
    ],
  },
};

type Istatistik = Record<string, number>;
type SilmeIstegi = { yol: string; mesaj: string; sonra?: () => void };
type DetayIstegi = {
  baslik: string; satirlar: DetaySatiri[]; gorusmeler?: DetayGorusme[];
  /** Görüşme eklerken forma önden doldurulacak üye. */
  esnaf?: { id: number; etiket: string };
};

/**
 * Süzgeç değerlerini sorgu dizesine çevirir. Çoklu seçimler "a,b" biçiminde tutulur ve
 * tekrarlı parametreye açılır (?durum=A&durum=B) — sunucu bunları VEYA olarak işler.
 */
function sorguParametreleri(secimler: Record<string, string>, arama: string): URLSearchParams {
  const params = new URLSearchParams();
  Object.entries(secimler).forEach(([anahtar, deger]) => {
    if (!deger || anahtar === "seviye") return;
    deger.split(",").filter(Boolean).forEach(tek => params.append(anahtar, tek));
  });
  if (arama.trim()) params.set("arama", arama.trim());
  return params;
}

function tarihGirdisi(iso?: string | null): string {
  return iso ? iso.slice(0, 10) : "";
}

/** Ekran adı ile API uç noktası ayrışabiliyor: "Aktif Çalışanlar" ekranı /api/kullanicilar ucunu kullanır. */
const apiYollari: Record<PageKind, string> = {
  esnaflar: "esnaflar", onaylar: "onaylar", gruplar: "gruplar", calisanlar: "kullanicilar",
};

export function ModulePage({ kind }: { kind: PageKind }) {
  const page = config[kind];
  const apiYolu = apiYollari[kind];
  const [action, setAction] = useState("");
  const [toast, setToast] = useState("");
  const [tab, setTab] = useState(0);
  const [sayfa, setSayfa] = useState(1);
  const [sayfaBoyutu, setSayfaBoyutu] = useState(20);
  const [arama, setArama] = useState("");
  const [filtreler, setFiltreler] = useState<Record<string, string>>({});
  const [yenileme, setYenileme] = useState(0);

  const [kayitlar, setKayitlar] = useState<unknown[]>([]);
  const [toplam, setToplam] = useState(0);
  const [istatistik, setIstatistik] = useState<Istatistik | null>(null);
  const [performans, setPerformans] = useState<{ adSoyad: string; adet: number }[]>([]);
  const [gruplar, setGruplar] = useState<GrupKaydi[]>([]);
  const [gorevliler, setGorevliler] = useState<KullaniciKaydi[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState("");

  const [detay, setDetay] = useState<DetayIstegi | null>(null);
  const [onDoldurma, setOnDoldurma] = useState<Record<string, string> | null>(null);
  const [duzenleme, setDuzenleme] = useState<DuzenlemeIstegi | null>(null);
  const [silme, setSilme] = useState<SilmeIstegi | null>(null);
  const [iceAktarAcik, setIceAktarAcik] = useState(false);
  const [rolYonetimiAcik, setRolYonetimiAcik] = useState(false);
  const [tamEkran, setTamEkran] = useState(false);
  const yenile = useCallback(() => setYenileme(n => n + 1), []);

  // Tam ekran liste, açık bir detay/form yokken Esc ile normal görünüme döner.
  useEffect(() => {
    if (!tamEkran) return;
    const kapat = (event: KeyboardEvent) => {
      const modalAcik = !!detay || !!action || !!duzenleme || !!silme || iceAktarAcik || rolYonetimiAcik;
      if (event.key === "Escape" && !modalAcik) setTamEkran(false);
    };
    document.addEventListener("keydown", kapat);
    return () => document.removeEventListener("keydown", kapat);
  }, [tamEkran, detay, action, duzenleme, silme, iceAktarAcik, rolYonetimiAcik]);

  // Filtre seçenekleri için referans listeler
  useEffect(() => {
    if (kind === "calisanlar") return;
    api.get<GrupKaydi[]>("/api/gruplar").then(setGruplar).catch(() => {});
    api.get<KullaniciKaydi[]>("/api/kullanicilar").then(setGorevliler).catch(() => {});
  }, [kind, yenileme]);

  // Liste verisi
  useEffect(() => {
    const params = sorguParametreleri({ ...page.tabs[tab].filtre, ...filtreler }, arama);
    params.set("sayfa", String(sayfa));
    params.set("sayfaBoyutu", String(sayfaBoyutu));

    let iptal = false;
    setYukleniyor(true);
    setHata("");
    if (kind === "esnaflar") setIstatistik(null);

    async function yukle() {
      try {
        if (kind === "gruplar") {
          let liste = await api.get<GrupKaydi[]>(`/api/gruplar?${params}`);
          const seviye = page.tabs[tab].filtre.seviye;
          if (seviye === "ana") liste = liste.filter(g => !g.ustGrupId);
          if (seviye === "alt") liste = liste.filter(g => g.ustGrupId);
          if (!iptal) { setKayitlar(liste); setToplam(liste.length); }
        } else if (kind === "calisanlar") {
          const liste = await api.get<KullaniciKaydi[]>(`/api/kullanicilar?${params}`);
          if (!iptal) { setKayitlar(liste); setToplam(liste.length); }
        } else {
          const veri = await api.get<Sayfali<unknown>>(`/api/${apiYolu}?${params}`);
          if (!iptal) {
            setKayitlar(veri.kayitlar);
            setToplam(veri.toplam);
            // Üye kartları listeyle aynı API yanıtından gelir; eski/genel istatistiğe düşmez.
            if (kind === "esnaflar" && veri.istatistik) setIstatistik(veri.istatistik);
          }
        }
      } catch {
        if (!iptal) setHata(API_ERISIM_HATASI);
      } finally {
        if (!iptal) setYukleniyor(false);
      }
    }
    yukle();
    return () => { iptal = true; };
  }, [kind, tab, sayfa, sayfaBoyutu, arama, filtreler, yenileme, page.tabs]);

  // İstatistikler + görevli performansı. Üye kartları listeyle aynı aktif süzgeçleri kullanır.
  useEffect(() => {
    let iptal = false;
    // Üyelerin istatistiği yukarıdaki liste isteğiyle birlikte gelir.
    if (kind !== "esnaflar")
      api.get<Istatistik>(`/api/${apiYolu}/istatistik`)
        .then(v => { if (!iptal) setIstatistik(v); })
        .catch(() => {});
    if (kind !== "calisanlar")
      api.get<{ gorevliPerformans: { adSoyad: string; adet: number }[] }>("/api/gorusmeler/istatistik")
        .then(v => setPerformans(v.gorevliPerformans)).catch(() => {});
    return () => { iptal = true; };
  }, [kind, apiYolu, yenileme]);

  const filtreTanimlari = useMemo<FiltreSecim[]>(() => {
    const yap = (label: string, anahtar: string, options: { deger: string; etiket: string }[], coklu = false): FiltreSecim => ({
      label, options, coklu, value: filtreler[anahtar] ?? "",
      onChange: deger => { setSayfa(1); setFiltreler(f => ({ ...f, [anahtar]: deger })); },
    });
    const grupSecenek = gruplar.map(g => ({ deger: String(g.id), etiket: g.no ? `${g.no}. ${g.ad}` : g.ad }));
    const gorevliSecenek = gorevliler.map(k => ({ deger: String(k.id), etiket: k.adSoyad }));
    switch (kind) {
      case "esnaflar": return [
        yap("Meslek Grubu", "grupId", grupSecenek, true),
        yap("Onay Durumu", "durum", ESNAF_ONAY_DURUMLARI.map(d => ({ deger: d, etiket: d })), true),
        // Üyeye atanan kişi değil, üyeyle fiilen görüşme yapmış çalışan.
        yap("Görüşen Çalışan", "gorusenId", gorevliSecenek, true),
        yap("Üyelik Durumu", "uyelikDurumu", ["Faal", "Askı", "Pasif"].map(d => ({ deger: d, etiket: d })), true),
        yap("Görüşme Durumu", "gorusuldu", [
          { deger: "true", etiket: "Görüşülmüş" }, { deger: "false", etiket: "Hiç görüşülmemiş" },
        ]),
        yap("İlçe", "ilce", ["Yakutiye", "Palandöken", "Aziziye", "Aşkale", "Çat", "Hınıs", "Horasan", "İspir",
          "Karaçoban", "Karayazı", "Köprüköy", "Narman", "Oltu", "Olur", "Pasinler",
          "Pazaryolu", "Şenkaya", "Tekman", "Tortum", "Uzundere"].map(i => ({ deger: i, etiket: i })), true),
      ];
      case "onaylar": return [
        yap("İşlem Türü", "islemTuru", ["Üyelik Onayı", "Bilgi Güncelleme", "Grup Değişikliği", "Kayıt Silme Talebi"].map(t => ({ deger: t, etiket: t }))),
        yap("Görevli", "gorevliId", gorevliSecenek),
      ];
      case "gruplar": return [
        yap("Durum", "durum", [{ deger: "Aktif", etiket: "Aktif" }, { deger: "Pasif", etiket: "Pasif" }]),
        yap("Grup Türü", "tur", ["Sektörel", "Bölgesel", "Özel"].map(t => ({ deger: t, etiket: t }))),
      ];
      case "calisanlar": return [
        yap("Rol", "rol", [{ deger: "Yönetici", etiket: "Yönetici" }, { deger: "Görevli", etiket: "Görevli" }]),
        yap("Durum", "durum", [{ deger: "Aktif", etiket: "Aktif" }, { deger: "Pasif", etiket: "Pasif" }]),
      ];
    }
  }, [kind, filtreler, gruplar, gorevliler]);

  async function onayKarar(id: number, durum: string) {
    try {
      await api.put(`/api/onaylar/${id}/karar`, { durum });
      setToast(durum === "Onaylandı" ? "Kayıt onaylandı." : `Kayıt durumu "${durum}" olarak güncellendi.`);
      yenile();
    } catch {
      setToast("İşlem başarısız oldu.");
    }
  }

  async function sil() {
    if (!silme) return;
    try {
      await api.delete(silme.yol);
      setToast("Kayıt silindi.");
      yenile();
      silme.sonra?.();
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : "Silme işlemi başarısız oldu.");
    }
  }

  // Görüşme formu kapandığında üyenin ekranı tazelenmiş olarak geri açılır; aksi halde
  // kullanıcı listeye düşer ve az önce eklediği görüşmeyi göremezdi.
  const [donulecekEsnafId, setDonulecekEsnafId] = useState<number | null>(null);

  const esnafDetayiniAc = useCallback((esnafId: number) => {
    api.get<EsnafKaydi & { gorusmeler: DetayGorusme[] }>(`/api/esnaflar/${esnafId}`)
      .then(tam => setDetay(d => d ? { ...d, gorusmeler: tam.gorusmeler } : d))
      .catch(() => {});
  }, []);

  function gorusmeEkle() {
    if (!detay?.esnaf) return;
    setDonulecekEsnafId(detay.esnaf.id);
    setDetay(null);
    setDuzenleme(null);
    setAction("Yeni Görüşme");
    setOnDoldurma({ esnafId: String(detay.esnaf.id), esnafIdEtiket: detay.esnaf.etiket });
  }

  function gorusmeDuzenle(g: DetayGorusme) {
    if (!detay?.esnaf) return;
    setDonulecekEsnafId(detay.esnaf.id);
    setDetay(null);
    setDuzenleme({
      form: "gorusme", id: g.id, baslik: `Görüşmeyi Düzenle — ${g.sira}. Görüşme`,
      degerler: {
        sira: String(g.sira), gorevliId: String(g.gorevliId ?? ""), tarih: tarihGirdisi(g.tarih),
        sonuc: g.sonuc, takipGerekli: g.takipGerekli ? "Evet" : "Hayır",
        not: g.not ?? "", esnafId: String(detay.esnaf.id),
      },
    });
  }

  function gorusmeSil(g: DetayGorusme) {
    if (!detay?.esnaf) return;
    const esnafId = detay.esnaf.id;
    setSilme({
      yol: `/api/gorusmeler/${g.id}`,
      mesaj: `"${g.sira}. Görüşme" kaydı kalıcı olarak silinecek. Bu işlem geri alınamaz. Devam edilsin mi?`,
      sonra: () => esnafDetayiniAc(esnafId),
    });
  }

  // ---- Satır işlemleri ----

  function goster(kayit: unknown) {
    if (kind === "esnaflar") {
      const e = kayit as EsnafKaydi;
      // Görüşme geçmişi dahil tam detayı API'den al
      api.get<EsnafKaydi & { gorusmeler: DetayGorusme[] }>(`/api/esnaflar/${e.id}`)
        .then(tam => setDetay({
          baslik: `${tam.isletme}`,
          satirlar: [
            { etiket: "Üye Sicil No", deger: tam.uyeSicilNo }, { etiket: "Ticaret Sicil No", deger: tam.ticaretSicilNo },
            { etiket: "Tabela Unvanı", deger: tam.tabelaUnvani }, { etiket: "Şirket Tipi", deger: tam.sirketTipi },
            { etiket: "Uyruk", deger: tam.uyruk }, { etiket: "Sermaye", deger: tam.sermaye }, { etiket: "Derece", deger: tam.derece },
            { etiket: "Meslek Grubu", deger: tam.grupNo ? `${tam.grupNo}. ${tam.grup}` : tam.grup },
            { etiket: "Üyelik Durumu", deger: tam.uyelikDurumu, rozet: true },
            { etiket: "Durum Değişim Tarihi", deger: tarihGoster(tam.durumDegisimTarihi) },
            { etiket: "Durum Değişim Nedeni", deger: tam.durumDegisimNedeni },
            { etiket: "Yetkili", deger: tam.adSoyad }, { etiket: "Yetkilinin Görevi", deger: tam.gorevi },
            { etiket: "Diğer Yetkililer", deger: (tam.yetkililer ?? []).slice(1).map(y => y.gorevi ? `${y.adSoyad} (${y.gorevi})` : y.adSoyad).join(", ") || null },
            { etiket: "Cep Telefonu", deger: tam.telefon }, { etiket: "İş Telefonu", deger: tam.isTelefonu },
            { etiket: "Vergi Dairesi", deger: tam.vergiDairesi }, { etiket: "Vergi No", deger: tam.vergiNo },
            { etiket: "Vergi Terk Tarihi", deger: tarihGoster(tam.vergiTerkTarihi) },
            { etiket: "Kuruluş Tarihi", deger: tarihGoster(tam.kurulusTarihi) },
            { etiket: "Üye Kayıt Tarihi", deger: tarihGoster(tam.kayitTarihi) },
            { etiket: "Oda Karar Tarihi", deger: tarihGoster(tam.odaKararTarihi) },
            { etiket: "NACE Kodu", deger: tam.naceKodu }, { etiket: "NACE Faaliyet Adı", deger: tam.naceAdi },
            { etiket: "Faaliyet Detayı", deger: tam.faaliyetDetayi },
            { etiket: "İl", deger: tam.il }, { etiket: "İlçe", deger: tam.ilce }, { etiket: "Mahalle", deger: tam.mahalle },
            { etiket: "Adres", deger: tam.adres },
            { etiket: "Görevli", deger: tam.gorevli },
            { etiket: "Son Görüşme", deger: tarihGoster(tam.sonGorusmeTarihi) },
            { etiket: "Onay Durumu", deger: tam.durum, rozet: true },
          ],
          gorusmeler: tam.gorusmeler,
          esnaf: { id: tam.id, etiket: `${tam.adSoyad} — ${tam.isletme}` },
        }))
        .catch(() => setToast("Detay alınamadı."));
      return;
    }
    if (kind === "gruplar") {
      const g = kayit as GrupKaydi;
      setDetay({
        baslik: g.ad,
        satirlar: [
          { etiket: "Açıklama", deger: g.aciklama }, { etiket: "Tür", deger: g.tur },
          { etiket: "Üst Grup", deger: g.ustGrup }, { etiket: "Üye Sayısı", deger: sayiGoster(g.esnafSayisi) },
          { etiket: "Aktif Görevli", deger: String(g.aktifGorevli) },
          { etiket: "Son Güncelleme", deger: tarihGoster(g.guncellemeTarihi) },
          { etiket: "Durum", deger: g.durum, rozet: true },
        ],
      });
      return;
    }
    if (kind === "calisanlar") {
      const k = kayit as KullaniciKaydi;
      setDetay({
        baslik: k.adSoyad,
        satirlar: [
          { etiket: "Kullanıcı Adı", deger: k.kullaniciAdi }, { etiket: "Rol", deger: k.rol, rozet: true },
          { etiket: "Görev", deger: k.gorev }, { etiket: "Birim", deger: k.birim },
          { etiket: "E-posta", deger: k.eposta }, { etiket: "Telefon", deger: k.telefon },
          { etiket: "Durum", deger: k.durum, rozet: true },
        ],
      });
      return;
    }
    const o = kayit as OnayKaydi;
    setDetay({
      baslik: `${o.islemTuru} — ${o.esnaf}`,
      satirlar: [
        { etiket: "Üye", deger: `${o.esnaf} (${o.isletme})` }, { etiket: "İşlem Türü", deger: o.islemTuru },
        { etiket: "Grup", deger: o.grup }, { etiket: "Görevli", deger: o.gorevli },
        { etiket: "Tarih", deger: tarihGoster(o.tarih) }, { etiket: "Durum", deger: o.durum, rozet: true },
      ],
    });
  }

  function duzenle(kayit: unknown) {
    if (kind === "esnaflar") {
      const e = kayit as EsnafKaydi;
      setDuzenleme({
        form: "esnaf", id: e.id, baslik: `Üyeyi Düzenle — ${e.isletme}`,
        degerler: {
          adSoyad: e.adSoyad, isletme: e.isletme, telefon: e.telefon ?? "", isTelefonu: e.isTelefonu ?? "",
          grupId: e.grupId ? String(e.grupId) : "", gorevliId: e.gorevliId ? String(e.gorevliId) : "",
          il: e.il ?? "", ilce: e.ilce ?? "", mahalle: e.mahalle ?? "", adres: e.adres ?? "", vergiNo: e.vergiNo ?? "", durum: e.durum,
          uyeSicilNo: e.uyeSicilNo ?? "", ticaretSicilNo: e.ticaretSicilNo ?? "", sirketTipi: e.sirketTipi ?? "",
          tabelaUnvani: e.tabelaUnvani ?? "", gorevi: e.gorevi ?? "", vergiDairesi: e.vergiDairesi ?? "",
          uyelikDurumu: e.uyelikDurumu ?? "Faal",
          durumDegisimTarihi: tarihGirdisi(e.durumDegisimTarihi), durumDegisimNedeni: e.durumDegisimNedeni ?? "",
          kurulusTarihi: tarihGirdisi(e.kurulusTarihi), kayitTarihi: tarihGirdisi(e.kayitTarihi),
          naceKodu: e.naceKodu ?? "", naceAdi: e.naceAdi ?? "", faaliyetDetayi: e.faaliyetDetayi ?? "",
        },
      });
      return;
    }
    if (kind === "gruplar") {
      const g = kayit as GrupKaydi;
      setDuzenleme({
        form: "grup", id: g.id, baslik: `Grubu Düzenle — ${g.ad}`,
        degerler: {
          no: g.no ? String(g.no) : "", ad: g.ad, tur: g.tur, ustGrupId: g.ustGrupId ? String(g.ustGrupId) : "",
          aciklama: g.aciklama ?? "", durum: g.durum,
        },
      });
      return;
    }
    if (kind === "calisanlar") {
      const k = kayit as KullaniciKaydi;
      setDuzenleme({
        form: "kullanici", id: k.id, baslik: `Çalışanı Düzenle — ${k.adSoyad}`,
        degerler: {
          adSoyad: k.adSoyad, eposta: k.eposta ?? "", telefon: k.telefon ?? "",
          rol: k.rol, gorev: k.gorev ?? "", birim: k.birim ?? "", durum: k.durum, kullaniciAdi: k.kullaniciAdi,
        },
      });
      return;
    }
  }

  function silmeIste(kayit: unknown) {
    const tekil: Record<PageKind, string> = {
      esnaflar: "üye", onaylar: "onay", gruplar: "grup", calisanlar: "çalışan",
    };
    const k = kayit as { id: number; adSoyad?: string; ad?: string; esnaf?: string };
    const ad = k.adSoyad ?? k.ad ?? k.esnaf ?? `#${k.id}`;
    setSilme({
      yol: `/api/${apiYolu}/${k.id}`,
      mesaj: `"${ad}" ${tekil[kind]} kaydı kalıcı olarak silinecek. Bu işlem geri alınamaz. Devam edilsin mi?`,
    });
  }

  const sayfaSayisi = kind === "gruplar" || kind === "calisanlar" ? 1 : Math.max(1, Math.ceil(toplam / sayfaBoyutu));

  const disaAktarYolu = kind === "esnaflar"
    ? `/api/esnaflar/disa-aktar?${sorguParametreleri({ ...page.tabs[tab].filtre, ...filtreler }, arama)}`
    : null;

  const yonetici = yoneticiMi();
  const etkinFiltreler = { ...page.tabs[tab].filtre, ...filtreler };
  const filtreAktif = !!arama.trim() || Object.values(etkinFiltreler).some(Boolean);
  const seciliGrupSayisi = (etkinFiltreler.grupId ?? "").split(",").filter(Boolean).length;
  const filtreleriTemizle = () => { setArama(""); setFiltreler({}); setSayfa(1); setTab(0); };
  const tamEkraniAc = () => {
    setTamEkran(true);
    // Geniş çalışma alanında daha çok satır göster; API'nin güvenli üst sınırı 100'dür.
    if (sayfaBoyutu < 50) { setSayfaBoyutu(50); setSayfa(1); }
  };

  return <AppShell title={page.title}>
    {tamEkran && kind === "esnaflar" && <section className="fullscreen-list-view" role="dialog" aria-modal="true" aria-label="Tam ekran üye listesi">
      <header className="fullscreen-list-header">
        <div className="fullscreen-list-title">
          <span><ListFilter size={22} /></span>
          <div><small>TAM EKRAN ÇALIŞMA ALANI</small><h1>Üye Listesi</h1></div>
        </div>
        <div className="fullscreen-list-actions">
          <span className="fullscreen-record-count"><b>{sayiGoster(toplam)}</b> filtrelenmiş kayıt</span>
          <button className="fullscreen-close-button" onClick={() => setTamEkran(false)}>
            <Minimize2 size={17} /> Normal Görünüme Dön <kbd>Esc</kbd>
          </button>
        </div>
      </header>

      <div className="fullscreen-compact-filter">
        <FilterBar
          filters={filtreTanimlari}
          searchValue={arama}
          onSearch={d => { setSayfa(1); setArama(d); }}
          onReset={filtreleriTemizle}
        />
      </div>

      <section className="panel table-panel fullscreen-table-panel">
        <div className="fullscreen-table-tabs">
          <div className="tabs">{page.tabs.map((t, i) =>
            <button className={i === tab ? "active" : ""} key={t.etiket} onClick={() => { setTab(i); setSayfa(1); }}>{t.etiket}</button>)}
          </div>
          <span className={yukleniyor ? "fullscreen-loading active" : "fullscreen-loading"}>{yukleniyor ? "Liste güncelleniyor…" : "Liste güncel"}</span>
        </div>
        {hata ? <p className="security-note" role="alert">{hata}</p>
          : <ModuleTable kind={kind} kayitlar={kayitlar} yukleniyor={yukleniyor}
              onKarar={onayKarar} onGoster={goster} onDuzenle={duzenle} onSil={silmeIste} />}
        <Pagination toplam={toplam} sayfa={sayfa} sayfaSayisi={sayfaSayisi} onSayfa={setSayfa}
          sayfaBoyutu={sayfaBoyutu} onSayfaBoyutu={b => { setSayfaBoyutu(b); setSayfa(1); }} />
      </section>
    </section>}

    <FilterBar
      action={page.action || undefined}
      onAction={() => setAction(page.action)}
      filters={filtreTanimlari}
      searchValue={kind === "esnaflar" ? arama : undefined}
      onSearch={kind === "esnaflar" ? d => { setSayfa(1); setArama(d); } : undefined}
      onReset={filtreleriTemizle}
      extra={kind === "esnaflar" ? <>
        <button className="secondary-button" onClick={() => setIceAktarAcik(true)}><CloudUpload size={16} /> İçe Aktar</button>
        <button className="secondary-button" onClick={() => disaAktarYolu && api.indir(disaAktarYolu).catch(() => setToast("Dışa aktarma başarısız oldu."))}><CloudDownload size={16} /> Dışa Aktar</button>
      </> : undefined}
    />
    <Stats kind={kind} ist={istatistik} filtreAktif={filtreAktif} seciliGrupSayisi={seciliGrupSayisi} />
    <section className="panel table-panel">
      <div className="table-view-toolbar">
        <div className="tabs">{page.tabs.map((t, i) =>
          <button className={i === tab ? "active" : ""} key={t.etiket} onClick={() => { setTab(i); setSayfa(1); }}>{t.etiket}</button>)}
        </div>
        {kind === "esnaflar" && <button className="open-fullscreen-button" onClick={tamEkraniAc} title="Listeyi tam ekran çalışma alanında aç">
          <Maximize2 size={16} /> Tam Ekranda Aç
        </button>}
      </div>
      {hata ? <p className="security-note" role="alert">{hata}</p>
        : <ModuleTable kind={kind} kayitlar={kayitlar} yukleniyor={yukleniyor}
            onKarar={onayKarar}
            onGoster={goster} onDuzenle={duzenle} onSil={silmeIste} />}
      <Pagination toplam={toplam} sayfa={sayfa} sayfaSayisi={sayfaSayisi} onSayfa={setSayfa}
        sayfaBoyutu={sayfaBoyutu} onSayfaBoyutu={b => { setSayfaBoyutu(b); setSayfa(1); }} />
    </section>
    <div className="module-bottom-grid">
      <section className="panel">
        <h2>{kind === "gruplar" ? "Grup Dağılımı" : kind === "calisanlar" ? "Rol Dağılımı" : kind === "onaylar" ? "Onay Dağılımı" : "Dağılım"}</h2>
        <DonutChart segments={donutVerisi(kind, istatistik)} />
      </section>
      <section className="panel">
        <h2>{kind === "calisanlar" ? "Hızlı İşlemler" : "Görevlilere Göre Performans"}</h2>
        {kind === "calisanlar"
          ? <div className="quick-list">
              {yonetici && <button onClick={() => setAction(page.action)}>+ Yeni Çalışan Ekle</button>}
              {yonetici && <button onClick={() => setRolYonetimiAcik(true)}>Rol Yönetimi</button>}
              {yonetici && <button onClick={() => setIceAktarAcik(true)}>Excel&apos;den İçe Aktar</button>}
              <button onClick={() => api.indir("/api/kullanicilar/disa-aktar").catch(() => setToast("Rapor indirilemedi."))}>Çalışan Raporu (Excel)</button>
              {!yonetici && <p style={{ color: "#7a8699", fontSize: 12.5, margin: 0 }}>Çalışan ekleme ve rol yönetimi yalnızca Yönetici rolüne açıktır.</p>}
            </div>
          : <BarList items={performans.map(p => ({ name: p.adSoyad, value: p.adet }))} />}
      </section>
    </div>

    <ActionModal action={action} duzenleme={duzenleme} open={!!action || !!duzenleme}
      onDoldurma={onDoldurma ?? undefined}
      onClose={() => {
        setAction(""); setDuzenleme(null); setOnDoldurma(null);
        // Görüşme formu üye ekranından açıldıysa oraya geri dönülür.
        if (donulecekEsnafId !== null) { goster({ id: donulecekEsnafId }); setDonulecekEsnafId(null); }
      }}
      onSuccess={setToast} onSaved={yenile} />
    <DetailModal open={!!detay} baslik={detay?.baslik ?? ""} satirlar={detay?.satirlar ?? []}
      gorusmeler={detay?.gorusmeler} onClose={() => setDetay(null)}
      onGorusmeEkle={detay?.esnaf ? gorusmeEkle : undefined}
      onGorusmeDuzenle={detay?.esnaf ? gorusmeDuzenle : undefined}
      onGorusmeSil={detay?.esnaf ? gorusmeSil : undefined} />
    <ConfirmModal open={!!silme} baslik="Kaydı Sil" mesaj={silme?.mesaj ?? ""}
      onClose={() => setSilme(null)} onConfirm={sil} />
    {(kind === "esnaflar" || kind === "calisanlar") && <ImportModal
      open={iceAktarAcik}
      baslik={kind === "esnaflar" ? "Üyeleri Excel'den İçe Aktar" : "Çalışanları Excel'den İçe Aktar"}
      yuklemeYolu={`/api/${apiYolu}/ice-aktar`}
      sablonYolu={`/api/${apiYolu}/sablon`}
      onClose={() => setIceAktarAcik(false)}
      onDone={m => { setToast(m); yenile(); }} />}
    {kind === "calisanlar" && <RolYonetimi open={rolYonetimiAcik} onClose={() => setRolYonetimiAcik(false)}
      onDone={m => { setToast(m); yenile(); }} />}
    <Toast message={toast} onClose={() => setToast("")} />
  </AppShell>;
}

/** Kullanıcı rollerini ve durumlarını tek ekrandan hızla değiştirme modalı. */
function RolYonetimi({ open, onClose, onDone }: { open: boolean; onClose: () => void; onDone: (m: string) => void }) {
  const [liste, setListe] = useState<KullaniciKaydi[]>([]);
  const [kaydediliyor, setKaydediliyor] = useState<number | null>(null);

  useEffect(() => {
    if (!open) return;
    api.get<KullaniciKaydi[]>("/api/kullanicilar").then(setListe).catch(() => setListe([]));
  }, [open]);

  async function guncelle(k: KullaniciKaydi, alan: "rol" | "durum", deger: string) {
    setKaydediliyor(k.id);
    try {
      await api.put(`/api/kullanicilar/${k.id}`, { ...k, [alan]: deger });
      setListe(l => l.map(x => x.id === k.id ? { ...x, [alan]: deger } : x));
      onDone(`${k.adSoyad} güncellendi.`);
    } catch (e) {
      onDone(e instanceof ApiError ? e.message : "Güncelleme başarısız oldu.");
    } finally {
      setKaydediliyor(null);
    }
  }

  return <Modal open={open} title="Rol ve Yetki Yönetimi" onClose={onClose}>
    <div className="action-form">
      <p style={{ color: "#5b6779" }}>Rol veya durum değişiklikleri anında kaydedilir.</p>
      <div className="table-scroll" style={{ maxHeight: 380, overflowY: "auto" }}><table>
        <thead><tr><th>Çalışan</th><th>Rol</th><th>Durum</th></tr></thead>
        <tbody>{liste.map(k => <tr key={k.id}>
          <td><strong>{k.adSoyad}</strong><small>{k.kullaniciAdi}</small></td>
          <td><select value={k.rol} disabled={kaydediliyor === k.id} onChange={e => guncelle(k, "rol", e.target.value)}>
            <option>Görevli</option><option>Yönetici</option>
          </select></td>
          <td><select value={k.durum} disabled={kaydediliyor === k.id} onChange={e => guncelle(k, "durum", e.target.value)}>
            <option>Aktif</option><option>Pasif</option>
          </select></td>
        </tr>)}</tbody>
      </table></div>
      <footer><button type="button" className="secondary-button" onClick={onClose}>Kapat</button></footer>
    </div>
  </Modal>;
}

function donutVerisi(kind: PageKind, ist: Istatistik | null): DonutSegment[] {
  if (!ist) return [];
  switch (kind) {
    case "calisanlar": return [
      { label: "Yönetici", value: ist.yonetici ?? 0, color: "blue" },
      { label: "Görevli", value: ist.gorevli ?? 0, color: "green" },
      { label: "Pasif", value: ist.pasif ?? 0, color: "gray" },
    ];
    case "gruplar": return [
      { label: "Aktif", value: ist.aktifGrup ?? 0, color: "green" },
      { label: "Pasif", value: ist.pasifGrup ?? 0, color: "gray" },
    ];
    case "onaylar": return [
      { label: "Onaylanan", value: ist.onaylanan ?? 0, color: "green" },
      { label: "Bekleyen", value: ist.bekleyen ?? 0, color: "orange" },
      { label: "Reddedilen", value: ist.reddedilen ?? 0, color: "red" },
      { label: "İptal", value: ist.iptalEdilen ?? 0, color: "gray" },
    ];
    default: return [
      { label: "Onay Veren", value: ist.onayVeren ?? 0, color: "green" },
      { label: "Onay Vermeyen", value: ist.onayVermeyen ?? 0, color: "red" },
      { label: "Kararsız", value: ist.kararsiz ?? 0, color: "orange" },
      { label: "Görüşülmemiş", value: ist.gorusulmemis ?? 0, color: "gray" },
    ];
  }
}

function Stats({ kind, ist, filtreAktif = false, seciliGrupSayisi = 0 }: {
  kind: PageKind; ist: Istatistik | null; filtreAktif?: boolean; seciliGrupSayisi?: number;
}) {
  if (!ist) return <div className="stats-grid five"><StatCard icon={Clock3} label="Yükleniyor" value="…" detail="Veriler getiriliyor" /></div>;
  if (kind === "calisanlar") return <div className="stats-grid five">
    <StatCard icon={UsersRound} label="Toplam Çalışan" value={sayiGoster(ist.toplam)} detail="Tüm kullanıcılar" />
    <StatCard icon={ShieldCheck} label="Aktif Çalışan" value={sayiGoster(ist.aktif)} detail={yuzde(ist.aktif, ist.toplam)} tone="green" />
    <StatCard icon={UserRound} label="Saha Çalışanı" value={sayiGoster(ist.gorevli)} detail={yuzde(ist.gorevli, ist.toplam)} tone="purple" />
    <StatCard icon={CircleHelp} label="Yönetici" value={sayiGoster(ist.yonetici)} detail={yuzde(ist.yonetici, ist.toplam)} tone="orange" />
    <StatCard icon={XCircle} label="Pasif Çalışan" value={sayiGoster(ist.pasif)} detail={yuzde(ist.pasif, ist.toplam)} tone="red" />
  </div>;
  if (kind === "gruplar") return <div className="stats-grid four">
    <StatCard icon={UsersRound} label="Toplam Grup" value={sayiGoster(ist.toplamGrup)} detail="Tüm gruplar" />
    <StatCard icon={Store} label="Toplam Üye" value={sayiGoster(ist.toplamEsnaf)} detail="Tüm gruplardaki üyeler" tone="green" />
    <StatCard icon={UsersRound} label="Aktif Grup" value={sayiGoster(ist.aktifGrup)} detail="Aktif gruplar" tone="purple" />
    <StatCard icon={CircleHelp} label="Pasif Grup" value={sayiGoster(ist.pasifGrup)} detail="Pasif gruplar" tone="orange" />
  </div>;
  if (kind === "onaylar") return <div className="stats-grid four">
    <StatCard icon={Clock3} label="Bekleyen Onay" value={sayiGoster(ist.bekleyen)} detail={yuzde(ist.bekleyen, ist.toplam)} />
    <StatCard icon={CheckCircle2} label="Onaylanan" value={sayiGoster(ist.onaylanan)} detail={yuzde(ist.onaylanan, ist.toplam)} tone="green" />
    <StatCard icon={XCircle} label="Reddedilen" value={sayiGoster(ist.reddedilen)} detail={yuzde(ist.reddedilen, ist.toplam)} tone="red" />
    <StatCard icon={Clock3} label="İptal Edilen" value={sayiGoster(ist.iptalEdilen)} detail={yuzde(ist.iptalEdilen, ist.toplam)} tone="gray" />
  </div>;
  const ekKapsamKarti = filtreAktif;
  // Kapsam kartı daima filtreli listenin gerçek toplamını gösterir.
  const kapsamDegeri = ist.toplam;
  const kapsamEtiketi = seciliGrupSayisi > 0 ? "Gruptaki Üye" : "Filtrelenen Üye";
  const kapsamDetayi = seciliGrupSayisi > 0
    ? (seciliGrupSayisi === 1 ? "Seçili meslek grubunun toplamı" : `${seciliGrupSayisi} seçili grubun toplamı`)
    : "Aktif filtrelerin sonucu";
  return <div className={`stats-grid ${ekKapsamKarti ? "six" : "five"}`}>
    <StatCard icon={UsersRound} label="Toplam Üye" value={sayiGoster(ist.genelToplam ?? ist.toplam)} detail="Tüm üyeler" />
    {ekKapsamKarti && <StatCard icon={Store} label={kapsamEtiketi} value={sayiGoster(kapsamDegeri)}
      detail={kapsamDetayi}
      tone="purple" />}
    <StatCard icon={CheckCircle2} label="Onay Veren" value={sayiGoster(ist.onayVeren)} detail={yuzde(ist.onayVeren, ist.toplam)} tone="green" />
    <StatCard icon={XCircle} label="Onay Vermeyen" value={sayiGoster(ist.onayVermeyen)} detail={yuzde(ist.onayVermeyen, ist.toplam)} tone="red" />
    <StatCard icon={CircleHelp} label="Kararsız" value={sayiGoster(ist.kararsiz)} detail={yuzde(ist.kararsiz, ist.toplam)} tone="orange" />
    <StatCard icon={Clock3} label="Görüşülmemiş" value={sayiGoster(ist.gorusulmemis)} detail={yuzde(ist.gorusulmemis, ist.toplam)} tone="gray" />
  </div>;
}

function SatirIslemleri({ kayit, edit = true, onGoster, onDuzenle, onSil }: {
  kayit: unknown; edit?: boolean;
  onGoster: (k: unknown) => void; onDuzenle: (k: unknown) => void; onSil: (k: unknown) => void;
}) {
  return <div className="row-actions">
    <button aria-label="Görüntüle" title="Görüntüle" onClick={() => onGoster(kayit)}><Eye size={15} /></button>
    {edit && <button aria-label="Düzenle" title="Düzenle" onClick={() => onDuzenle(kayit)}><Pencil size={15} /></button>}
    {yoneticiMi() && <button aria-label="Sil" title="Sil (yalnızca Yönetici)" onClick={() => onSil(kayit)}><Trash2 size={15} /></button>}
  </div>;
}

/** Üyenin onay durumuna göre tablo satırının rengi. */
function satirTonu(durum?: string | null): string {
  switch (durum) {
    case "Onay Verdi": return "satir-onayli";
    case "Onay Vermedi": return "satir-red";
    case "Kararsız": return "satir-kararsiz";
    default: return "";
  }
}

function Bos({ yukleniyor, sutun }: { yukleniyor: boolean; sutun: number }) {
  return <tr><td colSpan={sutun} style={{ textAlign: "center", padding: "28px 0", color: "#7a8699" }}>{yukleniyor ? "Yükleniyor..." : "Kayıt bulunamadı."}</td></tr>;
}

function ModuleTable({ kind, kayitlar, yukleniyor, onKarar, onGoster, onDuzenle, onSil }: {
  kind: PageKind; kayitlar: unknown[]; yukleniyor: boolean;
  onKarar: (id: number, durum: string) => void;
  onGoster: (k: unknown) => void; onDuzenle: (k: unknown) => void; onSil: (k: unknown) => void;
}) {
  const islemler = { onGoster, onDuzenle, onSil };
  if (kind === "gruplar") {
    const liste = kayitlar as GrupKaydi[];
    return <div className="table-scroll"><table>
      <thead><tr><th>Grup Adı</th><th>Grup Türü</th><th>Üst Grup</th><th>Üye Sayısı</th><th>Aktif Görevli</th><th>Son Güncelleme</th><th>Durum</th><th>İşlemler</th></tr></thead>
      <tbody>{!liste.length ? <Bos yukleniyor={yukleniyor} sutun={8} /> : liste.map(g => <tr key={g.id}>
        <td><strong>{g.ad}</strong><small>{g.aciklama}</small></td><td>{g.tur}</td><td>{g.ustGrup ?? "-"}</td>
        <td>{sayiGoster(g.esnafSayisi)}</td><td>{g.aktifGorevli}</td><td>{tarihGoster(g.guncellemeTarihi)}</td>
        <td><span className={`badge ${g.durum === "Aktif" ? "success" : "danger"}`}>{g.durum}</span></td>
        <td><SatirIslemleri kayit={g} {...islemler} /></td>
      </tr>)}</tbody>
    </table></div>;
  }
  if (kind === "calisanlar") {
    const liste = kayitlar as KullaniciKaydi[];
    return <div className="table-scroll"><table>
      <thead><tr><th>Çalışan</th><th>Rol</th><th>Görev</th><th>Birim</th><th>E-posta</th><th>Telefon</th><th>Durum</th><th>İşlemler</th></tr></thead>
      <tbody>{!liste.length ? <Bos yukleniyor={yukleniyor} sutun={8} /> : liste.map(k => <tr key={k.id}>
        <td><div className="user-cell"><span className="avatar small">{k.adSoyad.split(" ").map(x => x[0]).join("")}</span><div><strong>{k.adSoyad}</strong><small>{k.kullaniciAdi}</small></div></div></td>
        <td><span className="badge info">{k.rol}</span></td><td>{k.gorev ?? "-"}</td><td>{k.birim ?? "-"}</td>
        <td>{k.eposta ?? "-"}</td><td>{k.telefon ?? "-"}</td>
        <td><span className={`badge ${k.durum === "Aktif" ? "success" : "danger"}`}>{k.durum}</span></td>
        <td><SatirIslemleri kayit={k} {...islemler} /></td>
      </tr>)}</tbody>
    </table></div>;
  }
  if (kind === "onaylar") {
    const liste = kayitlar as OnayKaydi[];
    return <div className="table-scroll"><table>
      <thead><tr><th>Üye Adı / Unvan</th><th>İşlem Türü</th><th>Grup</th><th>İlçe</th><th>Telefon</th><th>Görevli</th><th>Tarih</th><th>Durum</th><th>İşlemler</th></tr></thead>
      <tbody>{!liste.length ? <Bos yukleniyor={yukleniyor} sutun={9} /> : liste.map(o => <tr key={o.id}>
        <td><strong>{o.esnaf}</strong><small>{o.isletme}</small></td><td>{o.islemTuru}</td><td>{o.grup ?? "-"}</td>
        <td>{o.ilce ?? "-"}</td><td>{o.telefon ?? "-"}</td><td>{o.gorevli ?? "-"}</td><td>{tarihGoster(o.tarih)}</td>
        <td><span className={`badge ${durumTonu(o.durum)}`}>{o.durum}</span></td>
        <td>{o.durum === "Bekliyor" && yoneticiMi()
          ? <div className="row-actions">
              <button className="approve" aria-label="Onayla" title="Onayla" onClick={() => onKarar(o.id, "Onaylandı")}>✓</button>
              <button className="reject" aria-label="Reddet" title="Reddet" onClick={() => onKarar(o.id, "Reddedildi")}>✕</button>
              <button aria-label="Görüntüle" title="Görüntüle" onClick={() => onGoster(o)}><Eye size={15} /></button>
            </div>
          : <SatirIslemleri kayit={o} edit={false} {...islemler} />}</td>
      </tr>)}</tbody>
    </table></div>;
  }
  const liste = kayitlar as EsnafKaydi[];
  return <div className="table-scroll"><table>
    <thead><tr><th>Sicil No</th><th>Unvan / Yetkili</th><th>Meslek Grubu</th><th>Üyelik Durumu</th><th>Telefon</th><th>İlçe</th><th>Görevli</th><th>Son Görüşme</th><th>Onay Durumu</th><th>İşlemler</th></tr></thead>
    {/* Satıra tıklayınca üyenin görüşme ekranı açılır; satır rengi üyenin onay durumunu gösterir. */}
    <tbody>{!liste.length ? <Bos yukleniyor={yukleniyor} sutun={10} /> : liste.map(e => <tr key={e.id}
      className={`tiklanabilir ${satirTonu(e.durum)}`} onClick={() => onGoster(e)}
      title="Görüşmeleri aç">
      <td>{e.uyeSicilNo ?? "-"}</td>
      <td><strong>{e.isletme}</strong><small>{e.adSoyad}{e.gorevi ? ` — ${e.gorevi}` : ""}</small></td>
      <td>{e.grup ? (e.grupNo ? `${e.grupNo}. ${e.grup}` : e.grup) : "-"}</td>
      <td><span className={`badge ${durumTonu(e.uyelikDurumu ?? "")}`}>{e.uyelikDurumu ?? "-"}</span>
        {e.uyelikDurumu === "Askı" && e.durumDegisimNedeni && <small>{e.durumDegisimNedeni}</small>}</td>
      <td>{e.telefon ?? e.isTelefonu ?? "-"}</td><td>{e.ilce ?? "-"}</td>
      <td>{e.gorevli ?? "-"}</td><td>{tarihGoster(e.sonGorusmeTarihi)}</td>
      <td><span className={`badge ${durumTonu(e.durum)}`}>{e.durum}</span></td>
      {/* İşlem butonları satır tıklamasını tetiklemesin. */}
      <td onClick={ev => ev.stopPropagation()}><SatirIslemleri kayit={e} {...islemler} /></td>
    </tr>)}</tbody>
  </table></div>;
}

function Pagination({ toplam, sayfa, sayfaSayisi, onSayfa, sayfaBoyutu, onSayfaBoyutu }: {
  toplam: number; sayfa: number; sayfaSayisi: number; onSayfa: (s: number) => void;
  sayfaBoyutu: number; onSayfaBoyutu: (b: number) => void;
}) {
  const sayfalar = useMemo(() => {
    const ilk = Math.max(1, Math.min(sayfa - 2, sayfaSayisi - 4));
    return Array.from({ length: Math.min(5, sayfaSayisi) }, (_, i) => ilk + i);
  }, [sayfa, sayfaSayisi]);
  return <div className="pagination">
    <span>Toplam {sayiGoster(toplam)} kayıt</span>
    <div>
      <button onClick={() => onSayfa(Math.max(1, sayfa - 1))} disabled={sayfa <= 1}>‹</button>
      {sayfalar.map(s => <button key={s} className={s === sayfa ? "active" : ""} onClick={() => onSayfa(s)}>{s}</button>)}
      <button onClick={() => onSayfa(Math.min(sayfaSayisi, sayfa + 1))} disabled={sayfa >= sayfaSayisi}>›</button>
    </div>
    <select aria-label="Sayfa başına kayıt" value={sayfaBoyutu} onChange={e => onSayfaBoyutu(Number(e.target.value))}>
      {[10, 20, 50, 100].map(b => <option key={b} value={b}>{b} / sayfa</option>)}
    </select>
  </div>;
}
