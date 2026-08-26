"use client";

import { useEffect, useState } from "react";
import { usePathname, useRouter } from "next/navigation";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { girisliMi, kimlik } from "@/lib/auth";

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
          {title === "Dashboard" ? <p>Hoş geldiniz, {kimlik()?.adSoyad ?? ""}</p> : <p><a href="/">Dashboard</a><span>›</span>{title}</p>}
        </div>
        {children}
      </main>
    </div>
  );
}
