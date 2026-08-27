"use client";

import { FormEvent, useEffect, useState } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { Eye, EyeOff, KeyRound, Lock, ShieldCheck, UserRound } from "lucide-react";
import { api, ApiError } from "@/lib/api";
import { girisliMi, tokenKaydet } from "@/lib/auth";
import { varlik } from "@/lib/yol";

interface GirisCevabi {
  token: string;
  kullanici: { id: number; adSoyad: string; kullaniciAdi: string; rol: string };
}

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
    {/* Arka plan CSS'te url(...) ile yazılsaydı basePath ön eki eklenmez, alt yolda 404 olurdu. */}
    <aside className="giris-tanitim" style={{ ["--giris-arkaplan" as string]: `url("${varlik("/giris-arkaplan.png")}")` }}>
      {/* Logo ve metin tek blok halinde panelin ortasında durur. Başlık kurum adını
          tekrarlamaz; o bilgi zaten logonun içinde yazıyor. */}
      <div className="giris-tanitim-icerik">
        <div className="giris-marka">
          {/* Amblemli beyaz sürüm: sol panel koyu olduğu için renk filtresi gerekmez. */}
          <Image src={varlik("/etso-sidebar.png")} alt="Erzurum Ticaret ve Sanayi Odası" width={941} height={694} priority />
        </div>
        <div className="giris-tanitim-govde">
          <h1>Dijital Yönetim Sistemi</h1>
          <p className="giris-alt-baslik">
            Üye kayıtları, saha görüşmeleri ve raporlama<br />tek panelde.
          </p>
          <span className="giris-tanitim-not">1885&apos;ten bugüne Erzurum ticaretinin hizmetinde</span>
        </div>
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
        {/* Telif metninin yerine projeyi hazırlayan şirketlerin logoları. Form sütunu açık
            zeminli olduğu için logoların lacivert sürümleri kullanılır. */}
        <span className="giris-hazirlayan">
          <a href="https://maydanozasist.com" target="_blank" rel="noopener noreferrer">
            <Image src={varlik("/footer-maydanoz.png")} alt="Maydanoz Asist"
              width={956} height={350} className="hazirlayan-logo hazirlayan-logo-maydanoz" />
          </a>
          <i aria-hidden="true" />
          <a href="https://ajansorkestra.com.tr" target="_blank" rel="noopener noreferrer">
            <Image src={varlik("/footer-ajans.png")} alt="Ajans Orkestra"
              width={940} height={94} className="hazirlayan-logo hazirlayan-logo-ajans" />
          </a>
        </span>
      </footer>
    </main>
  </div>;
}
