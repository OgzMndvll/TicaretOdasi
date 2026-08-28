"use client";

import { FormEvent, useCallback, useEffect, useState } from "react";
import { CheckCircle2, KeyRound, Plus, ShieldCheck, Trash2, UserRound, UsersRound } from "lucide-react";
import { AppShell } from "@/components/layout/app-shell";
import { ConfirmModal } from "@/components/ui/confirm-modal";
import { FormField, Modal } from "@/components/ui/modal";
import { StatCard } from "@/components/ui/stat-card";
import { Toast } from "@/components/ui/toast";
import { api, ApiError, API_ERISIM_HATASI, KullaniciKaydi, sayiGoster } from "@/lib/api";
import { kimlik } from "@/lib/auth";

/** Şifre politikası sunucuda da doğrulanır; buradaki ipucu yalnızca kullanıcıya yol gösterir. */
const SIFRE_IPUCU = "En az 10 karakter; büyük harf, küçük harf ve rakam içermeli.";

type SifreIstegi = { id: number; adSoyad: string };

/**
 * Panele giriş yapabilen hesapların yönetimi. Yalnızca sistem yöneticisine açıktır:
 * hesap açmak ve şifre belirlemek tek elde toplanır. Şifresiz görevli kayıtları
 * (görüşme listelerinde seçilenler) "Aktif Çalışanlar" ekranından yönetilir.
 */
export function KullanicilarPage() {
  const [liste, setListe] = useState<KullaniciKaydi[]>([]);
  const [yukleniyor, setYukleniyor] = useState(true);
  const [hata, setHata] = useState("");
  const [toast, setToast] = useState("");
  const [ekleAcik, setEkleAcik] = useState(false);
  const [sifreIstegi, setSifreIstegi] = useState<SifreIstegi | null>(null);
  const [silme, setSilme] = useState<KullaniciKaydi | null>(null);
  const ben = kimlik();

  const yukle = useCallback(() => {
    setYukleniyor(true);
    api.get<KullaniciKaydi[]>("/api/kullanicilar?rol=Yönetici")
      .then(v => { setListe(v); setHata(""); })
      .catch(() => setHata(API_ERISIM_HATASI))
      .finally(() => setYukleniyor(false));
  }, []);

  useEffect(() => { yukle(); }, [yukle]);

  async function yetkiDegistir(k: KullaniciKaydi, sistemYoneticisi: boolean) {
    try {
      await api.put(`/api/kullanicilar/${k.id}`, { ...k, sistemYoneticisi });
      setToast(sistemYoneticisi
        ? `${k.adSoyad} artık sistem yöneticisi.`
        : `${k.adSoyad} kullanıcısının sistem yöneticiliği kaldırıldı.`);
      yukle();
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : "Yetki değiştirilemedi.");
    }
  }

  async function durumDegistir(k: KullaniciKaydi, durum: string) {
    try {
      await api.put(`/api/kullanicilar/${k.id}`, { ...k, durum });
      setToast(`${k.adSoyad} ${durum.toLocaleLowerCase("tr")} duruma alındı.`);
      yukle();
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : "Durum değiştirilemedi.");
    }
  }

  async function sil() {
    if (!silme) return;
    try {
      await api.delete(`/api/kullanicilar/${silme.id}`);
      setToast("Hesap silindi.");
      yukle();
    } catch (e) {
      setToast(e instanceof ApiError ? e.message : "Hesap silinemedi.");
    }
  }

  const toplam = liste.length;
  const aktif = liste.filter(k => k.durum === "Aktif").length;
  const sistemYoneticisi = liste.filter(k => k.sistemYoneticisi).length;

  return <AppShell title="Kullanıcılar" sistemYonetimi>
    <section className="panel" style={{ marginBottom: 14 }}>
      <div style={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 14, flexWrap: "wrap" }}>
        <div>
          <h2 style={{ marginBottom: 6 }}>Panele Giriş Yapabilen Hesaplar</h2>
          <p style={{ margin: 0, color: "#5b6779", fontSize: 12, lineHeight: 1.6, maxWidth: 620 }}>
            Buradaki hesaplar kullanıcı adı ve şifreyle panele girer. Hepsi aynı ekranları görür;
            <b> Kullanıcılar</b>, <b>İşlem Kayıtları</b> ve <b>Ayarlar</b> yalnızca sistem
            yöneticisi işaretli hesaplarda görünür. Görüşme listelerinde seçilen şifresiz görevli
            kayıtları <b>Aktif Çalışanlar</b> ekranından yönetilir.
          </p>
        </div>
        <button className="dark-button" onClick={() => setEkleAcik(true)}><Plus size={16} /> Yeni Kullanıcı</button>
      </div>
    </section>

    <div className="stats-grid four">
      <StatCard icon={UsersRound} label="Toplam Hesap" value={sayiGoster(toplam)} detail="Giriş yapabilen" />
      <StatCard icon={CheckCircle2} label="Aktif" value={sayiGoster(aktif)} detail="Giriş yapabilir durumda" tone="green" />
      <StatCard icon={ShieldCheck} label="Sistem Yöneticisi" value={sayiGoster(sistemYoneticisi)} detail="Hesap ve şifre yetkisi" tone="purple" />
      <StatCard icon={UserRound} label="Pasif" value={sayiGoster(toplam - aktif)} detail="Girişi kapalı" tone="red" />
    </div>

    <section className="panel table-panel">
      {hata ? <p className="security-note" role="alert">{hata}</p>
        : <div className="table-scroll"><table>
          <thead><tr><th>Kullanıcı</th><th>Görev / Birim</th><th>E-posta</th><th>Sistem Yöneticisi</th><th>Durum</th><th>İşlemler</th></tr></thead>
          <tbody>
            {!liste.length && <tr><td colSpan={6} style={{ textAlign: "center", padding: "28px 0", color: "#7a8699" }}>
              {yukleniyor ? "Yükleniyor..." : "Giriş yapabilen hesap yok."}
            </td></tr>}
            {liste.map(k => {
              const kendisi = k.id === ben?.id;
              return <tr key={k.id}>
                <td><div className="user-cell">
                  <span className="avatar small">{k.adSoyad.split(" ").map(x => x[0]).join("").slice(0, 2)}</span>
                  <div><strong>{k.adSoyad}{kendisi && " (siz)"}</strong><small>{k.kullaniciAdi}</small></div>
                </div></td>
                <td>{[k.gorev, k.birim].filter(Boolean).join(" · ") || "-"}</td>
                <td>{k.eposta ?? "-"}</td>
                <td>
                  {/* Son sistem yöneticisinin yetkisi sunucuda da korunur; burada yalnızca
                      kendi yetkisini elinden almaya karşı ek bir engel var. */}
                  <label className="yetki-anahtari" title={kendisi ? "Kendi yetkinizi buradan kaldıramazsınız." : "Sistem yöneticisi yetkisi"}>
                    <input type="checkbox" checked={!!k.sistemYoneticisi} disabled={kendisi}
                      onChange={e => yetkiDegistir(k, e.target.checked)} />
                    <i />
                  </label>
                </td>
                <td><span className={`badge ${k.durum === "Aktif" ? "success" : "danger"}`}>{k.durum}</span></td>
                <td><div className="row-actions">
                  <button title="Şifre belirle / sıfırla" aria-label="Şifre belirle"
                    onClick={() => setSifreIstegi({ id: k.id, adSoyad: k.adSoyad })}><KeyRound size={15} /></button>
                  <button title={k.durum === "Aktif" ? "Pasife al" : "Aktife al"} aria-label="Durum değiştir"
                    disabled={kendisi}
                    onClick={() => durumDegistir(k, k.durum === "Aktif" ? "Pasif" : "Aktif")}>
                    {k.durum === "Aktif" ? "⏸" : "▶"}
                  </button>
                  <button title="Hesabı sil" aria-label="Sil" disabled={kendisi}
                    onClick={() => setSilme(k)}><Trash2 size={15} /></button>
                </div></td>
              </tr>;
            })}
          </tbody>
        </table></div>}
    </section>

    <KullaniciEkleModal open={ekleAcik} onClose={() => setEkleAcik(false)}
      onDone={m => { setToast(m); setEkleAcik(false); yukle(); }} />
    <SifreModal istek={sifreIstegi} onClose={() => setSifreIstegi(null)}
      onDone={m => { setToast(m); setSifreIstegi(null); }} />
    <ConfirmModal open={!!silme} baslik="Hesabı Sil"
      mesaj={`"${silme?.adSoyad}" hesabı kalıcı olarak silinecek ve bu kişi panele giremeyecek. Devam edilsin mi?`}
      onClose={() => setSilme(null)} onConfirm={sil} />
    <Toast message={toast} onClose={() => setToast("")} />
  </AppShell>;
}

/** Yeni giriş hesabı: kullanıcı adı ve şifre burada belirlenir. */
function KullaniciEkleModal({ open, onClose, onDone }: {
  open: boolean; onClose: () => void; onDone: (mesaj: string) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [hata, setHata] = useState("");

  useEffect(() => { if (open) setHata(""); }, [open]);

  async function gonder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!event.currentTarget.checkValidity()) return;
    const v = Object.fromEntries(new FormData(event.currentTarget).entries()) as Record<string, string>;
    setBusy(true);
    setHata("");
    try {
      const olusan = await api.post<{ id: number; kullaniciAdi: string }>("/api/kullanicilar", {
        adSoyad: v.adSoyad, kullaniciAdi: v.kullaniciAdi || "", rol: "Yönetici",
        gorev: v.gorev || null, birim: v.birim || null, eposta: v.eposta || null, telefon: v.telefon || null,
        durum: "Aktif", sifre: v.sifre, sistemYoneticisi: v.sistemYoneticisi === "on",
      });
      onDone(`Hesap oluşturuldu. Kullanıcı adı: ${olusan.kullaniciAdi}`);
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı.");
    } finally {
      setBusy(false);
    }
  }

  return <Modal open={open} title="Yeni Kullanıcı" ustBaslik="Giriş hesabı" onClose={onClose}>
    <form className="action-form" onSubmit={gonder} autoComplete="off">
      <div className="form-grid">
        <FormField label="Ad Soyad"><input name="adSoyad" required maxLength={120} autoFocus placeholder="Ad Soyad giriniz" /></FormField>
        <FormField label="Kullanıcı Adı (boş bırakılırsa addan türetilir)">
          <input name="kullaniciAdi" maxLength={55} placeholder="ornek.kullanici" />
        </FormField>
        <FormField label="Görev"><input name="gorev" maxLength={120} placeholder="Görev giriniz" /></FormField>
        <FormField label="Birim"><input name="birim" maxLength={120} placeholder="Birim giriniz" /></FormField>
        <FormField label="E-posta"><input name="eposta" type="email" maxLength={160} placeholder="ornek@erzto.org.tr" /></FormField>
        <FormField label="Telefon"><input name="telefon" type="tel" maxLength={11} placeholder="05321234567" /></FormField>
        <FormField label={`Şifre — ${SIFRE_IPUCU}`} genis>
          <input name="sifre" type="password" required minLength={10} maxLength={128}
            autoComplete="new-password" placeholder="••••••••••" />
        </FormField>
      </div>
      <label className="yetki-secimi">
        <input type="checkbox" name="sistemYoneticisi" />
        <div>
          <b>Sistem yöneticisi yetkisi ver</b>
          <small>Kullanıcılar, İşlem Kayıtları ve Ayarlar ekranlarını açar; hesap ve şifre oluşturabilir.</small>
        </div>
      </label>
      {hata && <p className="security-note" role="alert" style={{ color: "#c0392b" }}>{hata}</p>}
      <footer>
        <button type="button" className="secondary-button" onClick={onClose}>Vazgeç</button>
        <button type="submit" className="primary-button" disabled={busy}>
          <CheckCircle2 size={17} />{busy ? "Kaydediliyor..." : "Hesabı Oluştur"}
        </button>
      </footer>
    </form>
  </Modal>;
}

/** Şifre belirleme/sıfırlama. Kaydedildiğinde o kullanıcının açık oturumları düşer. */
function SifreModal({ istek, onClose, onDone }: {
  istek: SifreIstegi | null; onClose: () => void; onDone: (mesaj: string) => void;
}) {
  const [busy, setBusy] = useState(false);
  const [hata, setHata] = useState("");

  useEffect(() => { if (istek) setHata(""); }, [istek]);

  async function gonder(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!istek || !event.currentTarget.checkValidity()) return;
    const v = Object.fromEntries(new FormData(event.currentTarget).entries()) as Record<string, string>;
    if (v.yeniSifre !== v.tekrar) { setHata("Şifreler birbirini tutmuyor."); return; }
    setBusy(true);
    setHata("");
    try {
      await api.put(`/api/kullanicilar/${istek.id}/sifre`, { yeniSifre: v.yeniSifre });
      onDone(`${istek.adSoyad} için yeni şifre belirlendi; açık oturumları kapatıldı.`);
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı.");
    } finally {
      setBusy(false);
    }
  }

  return <Modal open={!!istek} title={`Şifre Belirle — ${istek?.adSoyad ?? ""}`} ustBaslik="Giriş hesabı" onClose={onClose}>
    <form className="action-form" onSubmit={gonder} autoComplete="off" key={istek?.id}>
      <div className="form-grid">
        <FormField label="Yeni Şifre">
          <input name="yeniSifre" type="password" required minLength={10} maxLength={128}
            autoComplete="new-password" autoFocus placeholder="••••••••••" />
        </FormField>
        <FormField label="Yeni Şifre (tekrar)">
          <input name="tekrar" type="password" required minLength={10} maxLength={128}
            autoComplete="new-password" placeholder="••••••••••" />
        </FormField>
      </div>
      <p className="security-note">{SIFRE_IPUCU} Şifre değiştirildiğinde bu kullanıcının açık tüm oturumları kapanır.</p>
      {hata && <p className="security-note" role="alert" style={{ color: "#c0392b" }}>{hata}</p>}
      <footer>
        <button type="button" className="secondary-button" onClick={onClose}>Vazgeç</button>
        <button type="submit" className="primary-button" disabled={busy}>
          <CheckCircle2 size={17} />{busy ? "Kaydediliyor..." : "Şifreyi Kaydet"}
        </button>
      </footer>
    </form>
  </Modal>;
}
