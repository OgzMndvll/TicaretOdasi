"use client";

import { useState } from "react";
import { AlertTriangle } from "lucide-react";
import { Modal } from "./modal";

export function ConfirmModal({ open, baslik, mesaj, onaylaMetni = "Evet, Sil", onClose, onConfirm }: {
  open: boolean; baslik: string; mesaj: string; onaylaMetni?: string;
  onClose: () => void; onConfirm: () => Promise<void> | void;
}) {
  const [busy, setBusy] = useState(false);
  async function onayla() {
    setBusy(true);
    try { await onConfirm(); onClose(); } finally { setBusy(false); }
  }
  return <Modal open={open} title={baslik} onClose={onClose}>
    <div className="action-form">
      <p style={{ display: "flex", gap: 10, alignItems: "flex-start", lineHeight: 1.5 }}>
        <AlertTriangle size={22} color="#e8a13c" style={{ flexShrink: 0, marginTop: 2 }} />{mesaj}
      </p>
      <footer>
        <button type="button" className="secondary-button" onClick={onClose}>Vazgeç</button>
        <button type="button" className="primary-button" style={{ background: "#c0392b" }} disabled={busy} onClick={onayla}>
          {busy ? "Siliniyor..." : onaylaMetni}
        </button>
      </footer>
    </div>
  </Modal>;
}
