"use client";

import { useEffect, useState } from "react";
import Image from "next/image";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { girisliMi, kimlik } from "@/lib/auth";
import { varlik } from "@/lib/yol";

export function AppShell({ title, children }: { title: string; children: React.ReactNode }) {
  const router = useRouter();
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [hazir, setHazir] = useState(false);

  useEffect(() => {
    if (!girisliMi()) {
      router.replace("/giris");
      return;
    }
    setHazir(true);
  }, [router, pathname]);

  if (!hazir) return null;

  return (
    <div className="app-shell">
      <Sidebar open={mobileOpen} onClose={() => setMobileOpen(false)} />
      <main className="main-content">
        <Topbar onMenu={() => setMobileOpen(true)} />
        <div className="page-heading">
          <h1>{title}</h1>
          {title === "Dashboard" ? <p>Hoş geldiniz, {kimlik()?.adSoyad ?? ""}</p> : <p><Link href="/">Dashboard</Link><span>›</span>{title}</p>}
        </div>
        {children}
        {/* Her panel sayfasının altında görünen imza şeridi. Logoların koyu sürümleri
            kullanıldığı için sayfanın açık zeminine doğrudan oturur, ayrı bir bant gerekmez. */}
        <footer className="site-footer">
          <a href="https://maydanozasist.com" target="_blank" rel="noopener noreferrer">
            <Image src={varlik("/footer-maydanoz.png")} alt="Maydanoz Asist"
              width={956} height={350} className="footer-logo footer-logo-maydanoz" />
          </a>
          <i aria-hidden="true" />
          <a href="https://ajansorkestra.com.tr" target="_blank" rel="noopener noreferrer">
            <Image src={varlik("/footer-ajans.png")} alt="Ajans Orkestra"
              width={940} height={94} className="footer-logo footer-logo-ajans" />
          </a>
        </footer>
      </main>
    </div>
  );
}
