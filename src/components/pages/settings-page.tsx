"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import Image from "next/image";
import { Bell, Building2, CloudUpload, DatabaseBackup, FileText, KeyRound, Save, Settings2, ShieldCheck } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { Toast } from "@/components/ui/toast";
import { api, ApiError } from "@/lib/api";
import { tokenKaydet, yoneticiMi } from "@/lib/auth";
import { varlik } from "@/lib/yol";

const VARSAYILANLAR: Record<string, string> = {
  dil: "Türkçe", saatDilimi: "(UTC+03:00) İstanbul", tarihFormati: "GG.AA.YYYY", saatFormati: "24 Saat", sayfaBoyutu: "20",
  kurumAdi: "Erzurum Ticaret Odası", kurumKisaAd: "ETO", kurumVergiNo: "381 004 8045",
  kurumAdres: "Muratpaşa Mah. Yakutiye / Erzurum", kurumTelefon: "0442 234 00 00", kurumEposta: "info@erzto.org.tr",
  bildirimEposta: "1", bildirimSistem: "1", bildirimGorevlendirme: "1", bildirimGorusme: "1",
  oturumZamanAsimi: "30 dakika", sifreGecerlilik: "90 gün", ikiAsamali: "Zorunlu", girisDenemeLimiti: "5 deneme",
};

const BOLUMLER = [
  { id: "genel", ad: "Genel Ayarlar", yalnizYonetici: true },
  { id: "kurum", ad: "Kurum Bilgileri", yalnizYonetici: true },
  { id: "sifre", ad: "Şifre Değiştir", yalnizYonetici: false },
  { id: "bildirim", ad: "Bildirim Ayarları", yalnizYonetici: true },
  { id: "guvenlik", ad: "Sistem Güvenliği", yalnizYonetici: true },
  { id: "yedekleme", ad: "Yedekleme ve Dışa Aktarım", yalnizYonetici: true },
];

export function SettingsPage() {
  const [ayarlar, setAyarlar] = useState<Record<string, string>>(VARSAYILANLAR);
  const [toast, setToast] = useState("");
  const [busy, setBusy] = useState("");
  const [aktifBolum, setAktifBolum] = useState("genel");
  const icerikRef = useRef<HTMLDivElement>(null);
  const yonetici = yoneticiMi();

  // Ayarlar yalnızca Yönetici'ye açıktır (API'de de kilitli); Görevli yalnızca şifre bölümünü görür.
  useEffect(() => {
    if (!yonetici) return;
    api.get<Record<string, string>>("/api/ayarlar")
      .then(kayitli => setAyarlar({ ...VARSAYILANLAR, ...kayitli }))
      .catch(() => setToast("Kayıtlı ayarlar alınamadı; varsayılanlar gösteriliyor."));
  }, [yonetici]);

  function deger(anahtar: string) { return ayarlar[anahtar] ?? VARSAYILANLAR[anahtar] ?? ""; }
  function degistir(anahtar: string, yeni: string) { setAyarlar(a => ({ ...a, [anahtar]: yeni })); }

  async function kaydet(anahtarlar: string[], etiket: string) {
    setBusy(etiket);
    try {
      await api.put("/api/ayarlar", Object.fromEntries(anahtarlar.map(k => [k, deger(k)])));
      setToast(`${etiket} kaydedildi.`);
    } catch {
      setToast("Kaydetme başarısız. API'nin çalıştığından emin olun.");
    } finally {
      setBusy("");
    }
  }

  async function bildirimKaydet(anahtar: string, acik: boolean) {
    degistir(anahtar, acik ? "1" : "0");
    try {
      await api.put("/api/ayarlar", { [anahtar]: acik ? "1" : "0" });
      setToast("Bildirim tercihi kaydedildi.");
    } catch {
      setToast("Kaydetme başarısız oldu.");
    }
  }

  function indir(yol: string, etiket: string) {
    setBusy(etiket);
    api.indir(yol)
      .then(() => setToast(`${etiket} indirildi.`))
      .catch(() => setToast(`${etiket} indirilemedi.`))
      .finally(() => setBusy(""));
  }

  function bolumeGit(id: string) {
    setAktifBolum(id);
    document.getElementById(`ayar-${id}`)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  const Secim = ({ anahtar, label, secenekler }: { anahtar: string; label: string; secenekler: string[] }) =>
    <label className="setting-field"><span>{label}</span>
      <select value={deger(anahtar)} onChange={e => degistir(anahtar, e.target.value)}>
        {secenekler.map(s => <option key={s}>{s}</option>)}
      </select>
    </label>;

  return <AppShell title="Ayarlar" sistemYonetimi>
    <div className="settings-layout">
      <aside className="panel settings-menu"><h2>Ayarlar</h2>
        {BOLUMLER.filter(b => yonetici || !b.yalnizYonetici).map(b =>
          <button className={aktifBolum === b.id ? "active" : ""} key={b.id} onClick={() => bolumeGit(b.id)}>
            <Settings2 size={16} />{b.ad}
          </button>)}
      </aside>
      <div className="settings-content" ref={icerikRef}>
        {yonetici && <section className="panel settings-card" id="ayar-genel">
          <header><div><h2>Genel Ayarlar</h2><p>Sistem genelinde kullanılan temel ayarları düzenleyin.</p></div><span className="stat-icon blue"><Settings2 size={24} /></span></header>
          <div className="settings-grid">
            <Secim anahtar="dil" label="Dil" secenekler={["Türkçe", "English"]} />
            <Secim anahtar="saatDilimi" label="Saat Dilimi" secenekler={["(UTC+03:00) İstanbul"]} />
            <Secim anahtar="tarihFormati" label="Tarih Formatı" secenekler={["GG.AA.YYYY", "YYYY-AA-GG"]} />
            <Secim anahtar="saatFormati" label="Saat Formatı" secenekler={["24 Saat", "12 Saat"]} />
            <Secim anahtar="sayfaBoyutu" label="Sayfa Başına Kayıt" secenekler={["10", "20", "50", "100"]} />
          </div>
          <button className="primary-button" disabled={busy === "Genel ayarlar"}
            onClick={() => kaydet(["dil", "saatDilimi", "tarihFormati", "saatFormati", "sayfaBoyutu"], "Genel ayarlar")}>
            <Save size={16} />{busy === "Genel ayarlar" ? "Kaydediliyor..." : "Değişiklikleri Kaydet"}
          </button>
        </section>}

        {yonetici && <section className="panel institution-card" id="ayar-kurum">
          <header><div><h2>Kurum Bilgileri</h2><p>Oda/kurum bilgilerinizi güncelleyin.</p></div><span className="stat-icon green"><Building2 size={24} /></span></header>
          <div className="institution-grid">
            <div className="logo-preview"><Image src={varlik("/etso.png")} alt="ETSO logosu" width={450} height={90} /></div>
            {[["kurumAdi", "Kurum Adı"], ["kurumKisaAd", "Kısa Ad"], ["kurumVergiNo", "Vergi No"], ["kurumAdres", "Adres"], ["kurumTelefon", "Telefon"], ["kurumEposta", "E-posta"]].map(([anahtar, etiket]) =>
              <label key={anahtar}><span>{etiket}</span>
                <input value={deger(anahtar)} maxLength={200} onChange={e => degistir(anahtar, e.target.value)} />
              </label>)}
          </div>
          <button className="primary-button" disabled={busy === "Kurum bilgileri"}
            onClick={() => kaydet(["kurumAdi", "kurumKisaAd", "kurumVergiNo", "kurumAdres", "kurumTelefon", "kurumEposta"], "Kurum bilgileri")}>
            <Save size={16} />{busy === "Kurum bilgileri" ? "Kaydediliyor..." : "Güncelle"}
          </button>
        </section>}

        <SifreDegistirKarti onMesaj={setToast} />

        {yonetici && <section className="panel settings-card" id="ayar-bildirim">
          <header><div><h2>Bildirim Ayarları</h2><p>Sistem içi ve e-posta bildirim tercihlerini yönetin. Değişiklikler anında kaydedilir.</p></div><span className="stat-icon purple"><Bell size={24} /></span></header>
          <div className="toggle-list">
            {[["bildirimEposta", "E-posta Bildirimleri"], ["bildirimSistem", "Sistem İçi Bildirimler"], ["bildirimGorusme", "Görüşme Hatırlatmaları"]].map(([anahtar, etiket]) =>
              <label key={anahtar}><span>{etiket}</span>
                <input type="checkbox" checked={deger(anahtar) === "1"} onChange={e => bildirimKaydet(anahtar, e.target.checked)} /><i />
              </label>)}
          </div>
        </section>}

        {yonetici && <section className="panel settings-card" id="ayar-guvenlik">
          <header><div><h2>Sistem Güvenliği</h2><p>Oturum ve güvenlik ayarlarını yapılandırın. (Kimlik doğrulama devreye alındığında uygulanır.)</p></div><span className="stat-icon red"><ShieldCheck size={24} /></span></header>
          <div className="settings-grid one">
            <Secim anahtar="oturumZamanAsimi" label="Oturum Zaman Aşımı" secenekler={["15 dakika", "30 dakika", "60 dakika"]} />
            <Secim anahtar="sifreGecerlilik" label="Şifre Geçerlilik Süresi" secenekler={["30 gün", "90 gün", "180 gün"]} />
            <Secim anahtar="ikiAsamali" label="İki Aşamalı Doğrulama" secenekler={["Zorunlu", "İsteğe Bağlı", "Kapalı"]} />
            <Secim anahtar="girisDenemeLimiti" label="Başarısız Giriş Denemesi Limiti" secenekler={["3 deneme", "5 deneme", "10 deneme"]} />
          </div>
          <button className="primary-button" disabled={busy === "Güvenlik ayarları"}
            onClick={() => kaydet(["oturumZamanAsimi", "sifreGecerlilik", "ikiAsamali", "girisDenemeLimiti"], "Güvenlik ayarları")}>
            <Save size={16} />{busy === "Güvenlik ayarları" ? "Kaydediliyor..." : "Değişiklikleri Kaydet"}
          </button>
        </section>}

        {yonetici && <section className="panel backup-card" id="ayar-yedekleme">
          <header><div><h2>Yedekleme ve Dışa Aktarım</h2><p>Verilerinizi yedekleyin veya dışa aktarın.</p></div><span className="stat-icon blue"><CloudUpload size={24} /></span></header>
          <div className="backup-row"><span><DatabaseBackup size={18} /><b>Veritabanı Yedeği (JSON)</b></span>
            <button disabled={busy === "Veritabanı yedeği"} onClick={() => indir("/api/sistem/yedek", "Veritabanı yedeği")}>{busy === "Veritabanı yedeği" ? "Hazırlanıyor..." : "Yedekle"}</button></div>
          <div className="backup-row"><span><CloudUpload size={18} /><b>Tüm Veriler (Excel)</b></span>
            <button disabled={busy === "Excel dışa aktarımı"} onClick={() => indir("/api/sistem/disa-aktar", "Excel dışa aktarımı")}>{busy === "Excel dışa aktarımı" ? "Hazırlanıyor..." : "Dışa Aktar"}</button></div>
          <div className="backup-row"><span><FileText size={18} /><b>Sistem Logları</b></span>
            <button disabled={busy === "Sistem logları"} onClick={() => indir("/api/sistem/loglar", "Sistem logları")}>{busy === "Sistem logları" ? "Hazırlanıyor..." : "Logları İndir"}</button></div>
        </section>}
      </div>
    </div>
    <Toast message={toast} onClose={() => setToast("")} />
  </AppShell>;
}

function SifreDegistirKarti({ onMesaj }: { onMesaj: (m: string) => void }) {
  const [busy, setBusy] = useState(false);
  const [hata, setHata] = useState("");

  async function gonder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const form = event.currentTarget;
    const veri = new FormData(form);
    const yeni = String(veri.get("yeniSifre") ?? "");
    if (yeni !== String(veri.get("yeniSifreTekrar") ?? "")) {
      setHata("Yeni şifreler birbiriyle uyuşmuyor.");
      return;
    }
    setBusy(true);
    setHata("");
    try {
      // Şifre değişince sunucu eski token'ları geçersiz kılar; oturumun düşmemesi için taze token saklanır.
      const yanit = await api.post<{ token: string }>("/api/auth/sifre-degistir",
        { mevcutSifre: veri.get("mevcutSifre"), yeniSifre: yeni });
      if (yanit?.token) tokenKaydet(yanit.token);
      onMesaj("Şifreniz değiştirildi.");
      form.reset();
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı.");
    } finally {
      setBusy(false);
    }
  }

  return <section className="panel settings-card" id="ayar-sifre">
    <header><div><h2>Şifre Değiştir</h2><p>Hesabınızın şifresini güncelleyin. En az 10 karakter; büyük/küçük harf ve rakam içermelidir.</p></div><span className="stat-icon orange"><KeyRound size={24} /></span></header>
    <form onSubmit={gonder} className="settings-grid" autoComplete="off">
      <label className="setting-field"><span>Mevcut Şifre</span>
        <input type="password" name="mevcutSifre" required maxLength={128} autoComplete="current-password" /></label>
      <label className="setting-field"><span>Yeni Şifre</span>
        <input type="password" name="yeniSifre" required minLength={10} maxLength={128} autoComplete="new-password" /></label>
      <label className="setting-field"><span>Yeni Şifre (Tekrar)</span>
        <input type="password" name="yeniSifreTekrar" required minLength={10} maxLength={128} autoComplete="new-password" /></label>
      <div style={{ display: "flex", alignItems: "flex-end", gap: 12 }}>
        <button type="submit" className="primary-button" disabled={busy}><Save size={16} />{busy ? "Kaydediliyor..." : "Şifreyi Değiştir"}</button>
      </div>
      {hata && <p role="alert" style={{ color: "#c0392b", gridColumn: "1 / -1" }}>{hata}</p>}
    </form>
  </section>;
}
