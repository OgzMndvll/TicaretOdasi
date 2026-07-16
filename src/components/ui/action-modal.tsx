"use client";

import { FormEvent, useEffect, useState } from "react";
import { CheckCircle2 } from "lucide-react";
import { EsnafSecici } from "./esnaf-secici";
import { FormField, Modal } from "./modal";
import { api, ApiError, GrupKaydi, KullaniciKaydi } from "@/lib/api";
import { yoneticiMi } from "@/lib/auth";
import { ILLER, ilceleriGetir } from "@/lib/il-ilce";

type Alan = {
  name: string;
  label: string;
  tip: "text" | "email" | "date" | "textarea" | "select" | "hidden" | "password" | "tel";
  zorunlu?: boolean;
  secenekKaynagi?: "gruplar" | "gorevliler" | "esnaflar" | "sabit" | "iller" | "ilceler";
  sabitSecenekler?: string[];
};

/** Telefon: 0 ile başlayan 11 hane (05XX XXX XX XX). Rakam dışı her şey atılır, fazlası kesilir. */
const TELEFON_UZUNLUK = 11;
const telefonTemizle = (deger: string) => deger.replace(/\D/g, "").slice(0, TELEFON_UZUNLUK);

/** Yeni kullanıcı oluşturulduğunda yöneticiye bir kez gösterilecek giriş bilgileri. */
type KimlikBilgisi = { kullaniciAdi: string; sifre: string };

const ESNAF_DURUMLARI = ["Onay Verdi", "Onay Vermedi", "Kararsız", "Görüşülmedi"];

type FormTanimi = { alanlar: Alan[]; buton: string; gonder: (v: Record<string, string>) => Promise<KimlikBilgisi | void> };
type DuzenlemeTanimi = { alanlar: Alan[]; buton: string; gonder: (id: number, v: Record<string, string>) => Promise<void> };

const esnafAlanlari: Alan[] = [
  { name: "adSoyad", label: "Üye / Yetkili Adı", tip: "text", zorunlu: true },
  { name: "isletme", label: "İş Yeri / Unvan", tip: "text", zorunlu: true },
  { name: "telefon", label: "Telefon", tip: "tel" },
  { name: "grupId", label: "Grup / Meslek Grubu", tip: "select", secenekKaynagi: "gruplar", zorunlu: true },
  { name: "gorevliId", label: "Görevli", tip: "select", secenekKaynagi: "gorevliler" },
  { name: "il", label: "İl", tip: "select", secenekKaynagi: "iller", zorunlu: true },
  { name: "ilce", label: "İlçe", tip: "select", secenekKaynagi: "ilceler", zorunlu: true },
  { name: "mahalle", label: "Mahalle", tip: "text" },
  { name: "vergiNo", label: "Vergi Numarası", tip: "text" },
  { name: "adres", label: "Adres", tip: "textarea" },
];

function esnafGovdesi(v: Record<string, string>) {
  return {
    adSoyad: v.adSoyad, isletme: v.isletme, telefon: v.telefon || null,
    grupId: v.grupId ? Number(v.grupId) : null, gorevliId: v.gorevliId ? Number(v.gorevliId) : null,
    il: v.il || null, ilce: v.ilce || null, mahalle: v.mahalle || null, adres: v.adres || null, vergiNo: v.vergiNo || null,
    durum: v.durum || null,
  };
}

const kullaniciAlanlari: Alan[] = [
  { name: "adSoyad", label: "Ad Soyad", tip: "text", zorunlu: true },
  { name: "eposta", label: "E-posta", tip: "email" },
  { name: "telefon", label: "Telefon", tip: "tel" },
  { name: "rol", label: "Rol", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Görevli", "Yönetici"], zorunlu: true },
  { name: "gorev", label: "Görev", tip: "text" },
  { name: "birim", label: "Birim", tip: "text" },
];

function kullaniciGovdesi(v: Record<string, string>) {
  return {
    adSoyad: v.adSoyad, kullaniciAdi: v.kullaniciAdi ?? "", rol: v.rol,
    gorev: v.gorev || null, birim: v.birim || null, eposta: v.eposta || null, telefon: v.telefon || null,
    durum: v.durum || null, sifre: v.sifre || null,
  };
}

export const formlar: Record<string, FormTanimi> = {
  "Yeni Üye Ekle": {
    alanlar: esnafAlanlari,
    buton: "Üye Kaydını Oluştur",
    gonder: v => api.post("/api/esnaflar", esnafGovdesi(v)),
  },
  "Yeni Görevlendirme": {
    alanlar: [
      { name: "esnafId", label: "Üye", tip: "select", secenekKaynagi: "esnaflar", zorunlu: true },
      { name: "gorevliId", label: "Görevli", tip: "select", secenekKaynagi: "gorevliler", zorunlu: true },
      { name: "grupId", label: "Grup / Meslek Grubu", tip: "select", secenekKaynagi: "gruplar" },
      { name: "tarih", label: "Görevlendirme Tarihi", tip: "date", zorunlu: true },
      { name: "not", label: "Not", tip: "textarea" },
    ],
    buton: "Görevlendir",
    gonder: v => api.post("/api/gorevlendirmeler", {
      esnafId: Number(v.esnafId), gorevliId: Number(v.gorevliId),
      grupId: v.grupId ? Number(v.grupId) : null, tarih: v.tarih, not: v.not || null, durum: "Aktif",
    }),
  },
  "Yeni Görüşme": {
    alanlar: [
      { name: "esnafId", label: "Üye", tip: "select", secenekKaynagi: "esnaflar", zorunlu: true },
      { name: "gorevliId", label: "Görevli", tip: "select", secenekKaynagi: "gorevliler", zorunlu: true },
      { name: "tarih", label: "Görüşme Tarihi", tip: "date", zorunlu: true },
      { name: "sonuc", label: "Görüşme Sonucu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Onay Verdi", "Onay Vermedi", "Kararsız"], zorunlu: true },
      { name: "takipGerekli", label: "Takip Gerekli mi?", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Hayır", "Evet"] },
      { name: "not", label: "Not / Yorum", tip: "textarea" },
    ],
    buton: "Görüşmeyi Kaydet",
    gonder: v => api.post("/api/gorusmeler", {
      // Görevli rolünde görevli alanı formda yoktur; sunucu oturum sahibini atar (0 yer tutucudur).
      esnafId: Number(v.esnafId), gorevliId: v.gorevliId ? Number(v.gorevliId) : 0,
      tarih: v.tarih, sonuc: v.sonuc, not: v.not || null, takipGerekli: v.takipGerekli === "Evet",
    }),
  },
  "Yeni Grup Ekle": {
    alanlar: [
      { name: "ad", label: "Grup Adı", tip: "text", zorunlu: true },
      { name: "tur", label: "Grup Türü", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Sektörel", "Bölgesel", "Özel"], zorunlu: true },
      { name: "ustGrupId", label: "Üst Grup", tip: "select", secenekKaynagi: "gruplar" },
      { name: "aciklama", label: "Açıklama", tip: "textarea" },
    ],
    buton: "Grubu Oluştur",
    gonder: v => api.post("/api/gruplar", {
      ad: v.ad, tur: v.tur, ustGrupId: v.ustGrupId ? Number(v.ustGrupId) : null, aciklama: v.aciklama || null,
    }),
  },
  "Yeni Kullanıcı Ekle": {
    alanlar: [...kullaniciAlanlari, { name: "sifre", label: "Geçici Şifre (en az 10 karakter; büyük/küçük harf ve rakam)", tip: "password", zorunlu: true }],
    buton: "Kullanıcıyı Oluştur",
    // Kullanıcı adını sunucu Ad Soyad'dan türetebildiği için oluşan kaydı geri okuyup
    // giriş bilgilerini yöneticiye gösteriyoruz; şifre bir daha hiçbir yerden okunamaz.
    gonder: async v => {
      const olusan = await api.post<{ id: number; kullaniciAdi: string }>("/api/kullanicilar", kullaniciGovdesi(v));
      return { kullaniciAdi: olusan.kullaniciAdi, sifre: v.sifre };
    },
  },
};

export const duzenlemeFormlari: Record<string, DuzenlemeTanimi> = {
  esnaf: {
    alanlar: [...esnafAlanlari, { name: "durum", label: "Onay Durumu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ESNAF_DURUMLARI, zorunlu: true }],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/esnaflar/${id}`, esnafGovdesi(v)),
  },
  grup: {
    alanlar: [
      { name: "ad", label: "Grup Adı", tip: "text", zorunlu: true },
      { name: "tur", label: "Grup Türü", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Sektörel", "Bölgesel", "Özel"], zorunlu: true },
      { name: "ustGrupId", label: "Üst Grup", tip: "select", secenekKaynagi: "gruplar" },
      { name: "durum", label: "Durum", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Aktif", "Pasif"], zorunlu: true },
      { name: "aciklama", label: "Açıklama", tip: "textarea" },
    ],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/gruplar/${id}`, {
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
      { name: "gorevliId", label: "Görevli", tip: "select", secenekKaynagi: "gorevliler", zorunlu: true },
      { name: "tarih", label: "Görüşme Tarihi", tip: "date", zorunlu: true },
      { name: "sonuc", label: "Görüşme Sonucu", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Onay Verdi", "Onay Vermedi", "Kararsız"], zorunlu: true },
      { name: "takipGerekli", label: "Takip Gerekli mi?", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Hayır", "Evet"], zorunlu: true },
      { name: "not", label: "Not / Yorum", tip: "textarea" },
      { name: "esnafId", label: "", tip: "hidden" },
    ],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/gorusmeler/${id}`, {
      esnafId: Number(v.esnafId), gorevliId: v.gorevliId ? Number(v.gorevliId) : 0,
      tarih: v.tarih, sonuc: v.sonuc, not: v.not || null, takipGerekli: v.takipGerekli === "Evet",
    }),
  },
  gorevlendirme: {
    alanlar: [
      { name: "esnafId", label: "Üye", tip: "select", secenekKaynagi: "esnaflar", zorunlu: true },
      { name: "gorevliId", label: "Görevli", tip: "select", secenekKaynagi: "gorevliler", zorunlu: true },
      { name: "grupId", label: "Grup / Meslek Grubu", tip: "select", secenekKaynagi: "gruplar" },
      { name: "tarih", label: "Tarih", tip: "date", zorunlu: true },
      { name: "durum", label: "Durum", tip: "select", secenekKaynagi: "sabit", sabitSecenekler: ["Aktif", "Tamamlandı", "İptal Edildi"], zorunlu: true },
      { name: "not", label: "Not", tip: "textarea" },
    ],
    buton: "Değişiklikleri Kaydet",
    gonder: (id, v) => api.put(`/api/gorevlendirmeler/${id}`, {
      esnafId: v.esnafId ? Number(v.esnafId) : null, gorevliId: Number(v.gorevliId),
      grupId: v.grupId ? Number(v.grupId) : null, tarih: v.tarih, not: v.not || null, durum: v.durum,
    }),
  },
};

export interface DuzenlemeIstegi { form: keyof typeof duzenlemeFormlari; id: number; degerler: Record<string, string>; baslik: string }

export function ActionModal({ action, duzenleme, open, onClose, onSuccess, onSaved }: {
  action: string; duzenleme?: DuzenlemeIstegi | null; open: boolean; onClose: () => void;
  onSuccess: (message: string) => void; onSaved?: () => void;
}) {
  const [busy, setBusy] = useState(false);
  const [hata, setHata] = useState("");
  const [gruplar, setGruplar] = useState<GrupKaydi[]>([]);
  const [gorevliler, setGorevliler] = useState<KullaniciKaydi[]>([]);
  const [kimlik, setKimlik] = useState<KimlikBilgisi | null>(null);
  const [kopyalandi, setKopyalandi] = useState(false);
  // İlçe listesi seçili ile bağlıdır; il değişince ilçe seçimi sıfırlanır.
  const [secilenIl, setSecilenIl] = useState("");

  const form: FormTanimi | DuzenlemeTanimi | undefined = duzenleme ? duzenlemeFormlari[duzenleme.form] : formlar[action];
  const baslik = duzenleme ? duzenleme.baslik : action;
  const degerler = duzenleme?.degerler ?? {};

  useEffect(() => {
    if (!open || !form) return;
    setHata("");
    setKimlik(null);
    setKopyalandi(false);
    // Düzenlemede kayıtlı il ile açılır; yeni kayıtta boş başlar.
    setSecilenIl(duzenleme?.degerler.il ?? "");
    const kaynaklar = new Set(form.alanlar.map(a => a.secenekKaynagi).filter(Boolean));
    if (kaynaklar.has("gruplar")) api.get<GrupKaydi[]>("/api/gruplar").then(setGruplar).catch(() => setGruplar([]));
    if (kaynaklar.has("gorevliler")) api.get<KullaniciKaydi[]>("/api/kullanicilar?durum=Aktif").then(setGorevliler).catch(() => setGorevliler([]));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [open, action, duzenleme?.form, duzenleme?.id]);

  if (!form) return null;

  function secenekler(alan: Alan): { deger: string; etiket: string }[] {
    switch (alan.secenekKaynagi) {
      case "gruplar": return gruplar.map(g => ({ deger: String(g.id), etiket: g.ad }));
      case "gorevliler": return gorevliler.map(k => ({ deger: String(k.id), etiket: k.adSoyad }));
      case "iller": return ILLER.map(i => ({ deger: i, etiket: i }));
      case "ilceler": return ilceleriGetir(secilenIl).map(i => ({ deger: i, etiket: i }));
      default: return (alan.sabitSecenekler ?? []).map(s => ({ deger: s, etiket: s }));
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) return;
    const veri = Object.fromEntries(new FormData(event.currentTarget).entries()) as Record<string, string>;
    setBusy(true);
    setHata("");
    try {
      if (duzenleme) {
        await (form as DuzenlemeTanimi).gonder(duzenleme.id, veri);
      } else {
        const sonuc = await (form as FormTanimi).gonder(veri);
        // Giriş bilgisi dönen formlarda (yeni kullanıcı) modal açık kalır; yönetici
        // bilgileri not edip kapattığında normal başarı akışı işler.
        if (sonuc) {
          setKimlik(sonuc);
          onSaved?.();
          return;
        }
      }
      onClose();
      onSuccess(duzenleme ? "Kayıt güncellendi." : `${action} işlemi başarıyla kaydedildi.`);
      onSaved?.();
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı. API'nin çalıştığından emin olun.");
    } finally {
      setBusy(false);
    }
  }

  function kimlikKapat() {
    setKimlik(null);
    setKopyalandi(false);
    onClose();
    onSuccess("Kullanıcı oluşturuldu. Giriş bilgilerini kullanıcıya iletin.");
  }

  async function kimlikKopyala() {
    if (!kimlik) return;
    try {
      await navigator.clipboard.writeText(`Kullanıcı adı: ${kimlik.kullaniciAdi}\nŞifre: ${kimlik.sifre}`);
      setKopyalandi(true);
    } catch {
      setKopyalandi(false);
    }
  }

  if (kimlik) return <Modal open={open} title="Kullanıcı Oluşturuldu" onClose={kimlikKapat}>
    <div className="action-form">
      <p className="security-note">
        Aşağıdaki giriş bilgilerini kullanıcıya iletin. <strong>Şifre bu ekrandan sonra bir daha görüntülenemez</strong> —
        kaybolursa yönetici olarak yeni bir şifre atamanız gerekir.
      </p>
      <dl className="kimlik-kutusu">
        <dt>Kullanıcı adı</dt><dd><code>{kimlik.kullaniciAdi}</code></dd>
        <dt>Geçici şifre</dt><dd><code>{kimlik.sifre}</code></dd>
      </dl>
      <footer>
        <button type="button" className="secondary-button" onClick={kimlikKopyala}>
          {kopyalandi ? "Kopyalandı" : "Bilgileri Kopyala"}
        </button>
        <button type="button" className="primary-button" onClick={kimlikKapat}><CheckCircle2 size={17} />Not Aldım, Kapat</button>
      </footer>
    </div>
  </Modal>;

  // Görevli rolü görüşmeyi yalnızca kendi adına kaydeder: görevli seçimi gizlenir (sunucu kendisini atar),
  // esnaf seçici de yalnızca kabul ettiği görevlendirmelerdeki esnafları listeler.
  const gorusmeFormu = action === "Yeni Görüşme" || duzenleme?.form === "gorusme";
  const kisitli = gorusmeFormu && !yoneticiMi();
  const gosterilecekAlanlar = kisitli ? form.alanlar.filter(a => a.name !== "gorevliId") : form.alanlar;

  return <Modal open={open} title={baslik} onClose={onClose}>
    <form className="action-form" onSubmit={submit} autoComplete="off" key={duzenleme ? `d-${duzenleme.form}-${duzenleme.id}` : action}>
      <div className="form-grid">
        {gosterilecekAlanlar.map((alan, index) => alan.tip === "hidden"
          ? <input key={alan.name} type="hidden" name={alan.name} defaultValue={degerler[alan.name] ?? ""} />
          : <FormField key={alan.name} label={alan.label} genis={alan.secenekKaynagi === "esnaflar"}>
            {alan.secenekKaynagi === "esnaflar"
              ? <EsnafSecici name={alan.name} required={alan.zorunlu} sadeceGorevlendirilmis={kisitli}
                  defaultId={degerler[alan.name] || undefined} defaultEtiket={degerler[`${alan.name}Etiket`] || undefined} />
              : alan.tip === "textarea" ? <textarea name={alan.name} required={alan.zorunlu} maxLength={500} placeholder={`${alan.label} giriniz`} defaultValue={degerler[alan.name] ?? ""} />
              : alan.secenekKaynagi === "ilceler"
                // İlçe listesi seçili ile bağlı: il değişince key değişir, seçim sıfırdan başlar.
                ? <select key={`ilce-${secilenIl}`} name={alan.name} required={alan.zorunlu} disabled={!secilenIl}
                    defaultValue={secilenIl && secilenIl === degerler.il ? degerler[alan.name] ?? "" : ""}>
                    <option value="" disabled={alan.zorunlu}>{secilenIl ? "Seçiniz" : "Önce il seçiniz"}</option>
                    {secenekler(alan).map(s => <option key={s.deger} value={s.deger}>{s.etiket}</option>)}
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
