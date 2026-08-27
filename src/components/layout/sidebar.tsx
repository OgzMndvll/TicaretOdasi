"use client";

import Image from "next/image";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { X } from "lucide-react";
import { navigation } from "@/lib/navigation";
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
        {/* Kenar çubuğunun dibindeki imza. "Çıkış Yap" düğmesinin yerini aldı; oturum kapatma
            üst bardaki profil menüsünde duruyor. Logonun kendisi siyah olduğu için koyu lacivert
            zeminde görünmez; CSS'te brightness/invert ile beyaza çevrilir ve alttaki Erzurum
            silüetine karışmasın diye kendi koyu levhasının üstüne oturtulur. */}
        <a className="sidebar-ajans" href="https://ajansorkestra.com.tr" target="_blank" rel="noopener noreferrer"
          aria-label="Ajans Orkestra — yeni sekmede açılır">
          <Image src={varlik("/sidebar-ajans.png")} alt="Ajans Orkestra" width={940} height={94} />
        </a>
      </aside>
    </>
  );
}
