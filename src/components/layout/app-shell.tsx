"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { girisliMi, kimlik, sistemYoneticisiMi } from "@/lib/auth";

export function AppShell({ title, children, sistemYonetimi = false }: {
  title: string; children: React.ReactNode;
  /** true: sayfa yalnızca sistem yöneticisine açıktır; başkası adres çubuğundan
   *  girerse Dashboard'a döner. Asıl kilit sunucudadır, bu yalnızca yönlendirmedir. */
  sistemYonetimi?: boolean;
}) {
  const router = useRouter();
  const pathname = usePathname();
  const [mobileOpen, setMobileOpen] = useState(false);
  const [hazir, setHazir] = useState(false);

  useEffect(() => {
    if (!girisliMi()) {
      router.replace("/giris");
      return;
    }
    if (sistemYonetimi && !sistemYoneticisiMi()) {
      router.replace("/");
      return;
    }
    setHazir(true);
  }, [router, pathname, sistemYonetimi]);

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
      </main>
    </div>
  );
}
