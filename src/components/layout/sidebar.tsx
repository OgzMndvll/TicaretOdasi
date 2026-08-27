"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { LogOut, X } from "lucide-react";
import { navigation } from "@/lib/navigation";
import { cikisYap } from "@/lib/auth";
import { varlik } from "@/lib/yol";

export function Sidebar({ open, onClose }: { open: boolean; onClose: () => void }) {
  const pathname = usePathname();
  // Panele yalnızca yönetici girdiği için menünün tamamı görünür.
  const menu = navigation;
  return (
    <>
      <button className={`sidebar-scrim ${open ? "show" : ""}`} onClick={onClose} aria-label="Menüyü kapat" />
      <aside className={`sidebar ${open ? "open" : ""}`}>
        <button className="sidebar-close" onClick={onClose} aria-label="Menüyü kapat"><X size={20} /></button>
        <Link href="/" className="brand" aria-label="Erzurum Ticaret Odası ana sayfa">
          {/* Amblemli beyaz sürüm: kenar çubuğu koyu olduğu için renk filtresi gerekmez. */}
          <Image src={varlik("/etso-sidebar.png")} alt="Erzurum Ticaret ve Sanayi Odası" width={941} height={694} priority />
        </Link>
        <nav className="side-nav" aria-label="Ana navigasyon">
          {menu.map(({ label, href, icon: Icon }) => {
            const active = href === "/" ? pathname === "/" : pathname.startsWith(href);
            return <Link key={href} href={href} className={active ? "active" : ""} onClick={onClose}><Icon size={20} /><span>{label}</span></Link>;
          })}
        </nav>
        <div className="sidebar-city" aria-hidden="true">
          <Image src={varlik("/erzurum-silueti.png")} alt="" width={768} height={512} className="sidebar-siluet" />
        </div>
        <button className="logout" onClick={cikisYap}><LogOut size={20} /> Çıkış Yap</button>
      </aside>
    </>
  );
}
