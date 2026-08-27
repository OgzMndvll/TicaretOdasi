"use client";

import { useEffect, useRef, useState } from "react";
import Image from "next/image";
import { useRouter } from "next/navigation";
import { CalendarDays, ChevronDown, LogOut, Menu, Settings } from "lucide-react";
import { cikisYap, kimlik } from "@/lib/auth";
import { varlik } from "@/lib/yol";

export function Topbar({ onMenu }: { onMenu: () => void }) {
  const router = useRouter();
  const [profilAcik, setProfilAcik] = useState(false);
  const profilRef = useRef<HTMLDivElement>(null);
  const ben = kimlik();

  useEffect(() => {
    if (!profilAcik) return;
    const kapat = (event: PointerEvent) => {
      if (!profilRef.current?.contains(event.target as Node)) setProfilAcik(false);
    };
    const klavye = (event: KeyboardEvent) => {
      if (event.key === "Escape") setProfilAcik(false);
    };
    document.addEventListener("pointerdown", kapat);
    document.addEventListener("keydown", klavye);
    return () => {
      document.removeEventListener("pointerdown", kapat);
      document.removeEventListener("keydown", klavye);
    };
  }, [profilAcik]);

  const bugun = new Date().toLocaleDateString("tr-TR", { day: "numeric", month: "long", year: "numeric" });
  const basHarfler = (ben?.adSoyad ?? "?").split(" ").map(x => x[0]).join("").slice(0, 2).toUpperCase();

  return (
    <header className="topbar">
      <button className="icon-button menu-button" onClick={onMenu} aria-label="Menüyü aç"><Menu size={24} /></button>
      {/* Sayfanın tam ortasındaki imza. Üst barın sol (menü) ve sağ (tarih + profil) blokları
          farklı genişlikte olduğu için akışa bırakılırsa ortalanmaz; mutlak konumla sayfanın
          gerçek ortasına oturtulur ve altındaki düğmelere tıklamayı engellemesin diye
          pointer-events dışarıda bırakılır (bağlantının kendisi geri açılır). */}
      <a className="topbar-brand" href="https://maydanozasist.com" target="_blank" rel="noopener noreferrer"
        aria-label="Maydanoz Asist">
        <Image src={varlik("/footer-maydanoz.png")} alt="Maydanoz Asist" width={956} height={350} priority />
      </a>
      <div className="topbar-actions">
        <button className="date-pill" title="Bugünün tarihi"><CalendarDays size={17} /><span>{bugun}</span></button>
        <div className="profile-menu-wrap" ref={profilRef}>
          <button className={`profile ${profilAcik ? "open" : ""}`} type="button"
            aria-haspopup="menu" aria-expanded={profilAcik} aria-controls="profile-menu"
            title="Kullanıcı menüsü" onClick={() => setProfilAcik(acik => !acik)}>
            <span className="avatar">{basHarfler}</span>
            <div><strong>{ben?.adSoyad ?? "-"}</strong><small>{ben?.rol ?? ""}</small></div>
            <ChevronDown className="profile-chevron" size={15} />
          </button>
          {profilAcik && <div className="profile-dropdown" id="profile-menu" role="menu">
            <div className="profile-dropdown-head">
              <span className="avatar large">{basHarfler}</span>
              <div><strong>{ben?.adSoyad ?? "-"}</strong><small>{ben?.kullaniciAdi ?? ""} · {ben?.rol ?? ""}</small></div>
            </div>
            <div className="profile-dropdown-actions">
              <button type="button" role="menuitem" onClick={() => { setProfilAcik(false); router.push("/ayarlar"); }}>
                <span><Settings size={17} /></span><div><b>Profil ve Ayarlar</b><small>Hesap ve sistem tercihleri</small></div>
              </button>
              <button type="button" role="menuitem" onClick={() => { setProfilAcik(false); cikisYap(); }} className="profile-logout">
                <span><LogOut size={17} /></span><div><b>Çıkış Yap</b><small>Oturumu güvenli şekilde kapat</small></div>
              </button>
            </div>
          </div>}
        </div>
      </div>
    </header>
  );
}
