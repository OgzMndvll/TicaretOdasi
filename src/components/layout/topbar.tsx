"use client";

import { useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { Bell, CalendarDays, ChevronDown, LogOut, Menu, Settings } from "lucide-react";
import { api } from "@/lib/api";
import { cikisYap, kimlik, yoneticiMi } from "@/lib/auth";

export function Topbar({ onMenu }: { onMenu: () => void }) {
  const router = useRouter();
  const [bekleyenOnay, setBekleyenOnay] = useState(0);
  const [profilAcik, setProfilAcik] = useState(false);
  const profilRef = useRef<HTMLDivElement>(null);
  const ben = kimlik();

  useEffect(() => {
    // Onaylar yalnızca Yönetici'ye açık; görevli için bildirim sorgusu yapılmaz.
    if (!yoneticiMi()) return;
    let aktif = true;
    const getir = () => api.get<{ bekleyen: number }>("/api/onaylar/istatistik")
      .then(v => { if (aktif) setBekleyenOnay(v.bekleyen); })
      .catch(() => {});
    getir();
    const zamanlayici = setInterval(getir, 60_000);
    return () => { aktif = false; clearInterval(zamanlayici); };
  }, []);

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
      <div className="topbar-actions">
        <button className="date-pill" title="Bugünün tarihi"><CalendarDays size={17} /><span>{bugun}</span></button>
        {yoneticiMi() && <button className="notification" aria-label={`Bekleyen onaylar: ${bekleyenOnay}`}
          title={bekleyenOnay > 0 ? `${bekleyenOnay} bekleyen onay` : "Bekleyen onay yok"}
          onClick={() => router.push("/onaylar")}>
          <Bell size={20} />{bekleyenOnay > 0 && <b>{bekleyenOnay > 99 ? "99+" : bekleyenOnay}</b>}
        </button>}
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
