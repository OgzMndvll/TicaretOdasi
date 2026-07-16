"use client";

import { CheckCircle2, X } from "lucide-react";

export function Toast({ message, onClose }: { message: string; onClose: () => void }) {
  if (!message) return null;
  return <div className="toast" role="status"><CheckCircle2 size={20} /><span>{message}</span><button onClick={onClose} aria-label="Bildirimi kapat"><X size={17} /></button></div>;
}
