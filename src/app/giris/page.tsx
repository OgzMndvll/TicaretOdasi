"use client";

import { FormEvent, useEffect, useState } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { BarChart3, Eye, EyeOff, KeyRound, Lock, LogIn, ShieldCheck, Users, UserRound } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { girisliMi, tokenKaydet } from "@/lib/auth";

interface GirisCevabi {
  token: string;
  kullanici: { id: number; adSoyad: string; kullaniciAdi: string; rol: string };
}

const OZELLIKLER = [
  { ikon: Users, baslik: "Etkin Üye Yönetimi", metin: "Tüm üye bilgilerini düzenli ve güncel tutun." },
  { ikon: BarChart3, baslik: "Görüşme & Raporlama", metin: "Görüşmeleri kaydedin, raporlarla kararlarınıza yön verin." },
  { ikon: ShieldCheck, baslik: "Güvenli & Yetkili Erişim", metin: "Rol bazlı yetkilendirme ile güvenli kullanım sağlayın." },
];

export default function GirisSayfasi() {
  const router = useRouter();
  const [kullaniciAdi, setKullaniciAdi] = useState("");
  const [sifre, setSifre] = useState("");
  const [sifreGorunur, setSifreGorunur] = useState(false);
  const [hatirla, setHatirla] = useState(true);
  const [hata, setHata] = useState("");
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (girisliMi()) router.replace("/");
  }, [router]);

  async function giris(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setBusy(true);
    setHata("");
    try {
      const cevap = await api.post<GirisCevabi>("/api/auth/giris", { kullaniciAdi, sifre });
      tokenKaydet(cevap.token, hatirla);
      router.replace("/");
    } catch (e) {
      setHata(e instanceof ApiError ? e.message : "Sunucuya ulaşılamadı. API'nin çalıştığından emin olun.");
      setBusy(false);
    }
  }

  return <div className="giris-ekran">
    <aside className="giris-tanitim">
      <div className="giris-marka">
        <Image src="/etso.png" alt="Erzurum Ticaret Odası" width={300} height={60} priority />
      </div>
      <div className="giris-tanitim-govde">
        <h1>Erzurum Ticaret Odası</h1>
        <p className="giris-alt-baslik">Dijital Yönetim Sistemi</p>
        <span className="giris-cizgi" />
        <p className="giris-slogan">Üyelerimizle daha güçlü bir iletişim,<br />daha şeffaf bir yönetim.</p>
        <ul className="giris-ozellikler">
          {OZELLIKLER.map(({ ikon: Ikon, baslik, metin }) => <li key={baslik}>
            <span><Ikon size={19} /></span>
            <div>
              <strong>{baslik}</strong>
              <small>{metin}</small>
            </div>
          </li>)}
        </ul>
      </div>
    </aside>

    <main className="giris-form-alani">
      <section className="giris-kart">
        <header>
          <h2>Hoş Geldiniz</h2>
          <p>Hesabınıza giriş yaparak devam edin.</p>
        </header>

        <form onSubmit={giris} autoComplete="off">
          <label className="giris-alan">
            <span className="giris-etiket">Kullanıcı Adı</span>
            <div className="giris-girdi">
              <UserRound size={16} />
              <input value={kullaniciAdi} onChange={e => setKullaniciAdi(e.target.value)} required maxLength={60}
                placeholder="ad.soyad" autoFocus autoComplete="username" />
            </div>
          </label>

          <label className="giris-alan">
            <span className="giris-etiket">Şifre</span>
            <div className="giris-girdi">
              <KeyRound size={16} />
              <input type={sifreGorunur ? "text" : "password"} value={sifre} onChange={e => setSifre(e.target.value)}
                required maxLength={128} placeholder="Şifrenizi giriniz" autoComplete="current-password" />
              <button type="button" className="giris-goz" onClick={() => setSifreGorunur(g => !g)}
                aria-label={sifreGorunur ? "Şifreyi gizle" : "Şifreyi göster"}>
                {sifreGorunur ? <EyeOff size={16} /> : <Eye size={16} />}
              </button>
            </div>
          </label>

          <label className="giris-hatirla">
            <input type="checkbox" checked={hatirla} onChange={e => setHatirla(e.target.checked)} />
            <i />
            Beni Hatırla
          </label>

          {hata && <p className="giris-hata" role="alert">{hata}</p>}

          <button type="submit" className="giris-buton" disabled={busy}>
            <Lock size={16} />{busy ? "Giriş yapılıyor..." : "Giriş Yap"}
          </button>
        </form>

        <p className="giris-yardim">
          Şifrenizi unuttuysanız veya hesabınız yoksa <strong>sistem yöneticinize</strong> başvurun.
          5 hatalı denemede hesap 10 dakika kilitlenir.
        </p>
      </section>

      <footer className="giris-footer">
        <span className="giris-ssl">
          <ShieldCheck size={17} />
          <div>
            <strong>Güvenli Bağlantı</strong>
            <small>Oturumunuz şifreli olarak korunmaktadır.</small>
          </div>
        </span>
        <span className="giris-telif">
          © {new Date().getFullYear()} Erzurum Ticaret Odası<br />
          <small>Tüm hakları saklıdır.</small>
        </span>
      </footer>
    </main>
  </div>;
}
