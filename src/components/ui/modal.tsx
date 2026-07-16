"use client";

import { X } from "lucide-react";
import { useEffect } from "react";

export function Modal({ open, title, children, onClose }: { open: boolean; title: string; children: React.ReactNode; onClose: () => void }) {
  useEffect(() => {
    if (!open) return;
    const close = (event: KeyboardEvent) => event.key === "Escape" && onClose();
    document.addEventListener("keydown", close);
    document.body.style.overflow = "hidden";
    return () => { document.removeEventListener("keydown", close); document.body.style.overflow = ""; };
  }, [open, onClose]);
  if (!open) return null;
  return <div className="modal-backdrop" role="presentation" onMouseDown={(e) => e.target === e.currentTarget && onClose()}>
    <section className="modal" role="dialog" aria-modal="true" aria-labelledby="modal-title">
      <header><div><span>Yeni kayıt</span><h2 id="modal-title">{title}</h2></div><button className="icon-button" onClick={onClose} aria-label="Pencereyi kapat"><X size={20} /></button></header>
      {children}
    </section>
  </div>;
}

export function FormField({ label, children, genis = false }: { label: string; children: React.ReactNode; genis?: boolean }) {
  return <label className="form-field" style={genis ? { gridColumn: "1 / -1" } : undefined}><span>{label}</span>{children}</label>;
}
