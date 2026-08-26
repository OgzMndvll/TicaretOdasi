import type { LucideIcon } from "lucide-react";

export function StatCard({ icon: Icon, label, value, detail, detailTitle, tone = "blue", trend }: {
  icon: LucideIcon; label: string; value: string; detail: string;
  /** Alt metin kısaltıldığında tam hâli (ör. "Son 30 gün" → tarih aralığı). */
  detailTitle?: string;
  tone?: string; trend?: string;
}) {
  return <article className={`stat-card ton-${tone}`}>
    <span className={`stat-icon ${tone}`}><Icon size={27} /></span>
    <div className="stat-content">
      <b title={label}>{label}</b>
      <strong>{value}</strong>
      <span className="stat-footer"><small title={detailTitle ?? detail}>{detail}</small>{trend && <em className={trend.startsWith("-") ? "down" : "up"}>{trend}</em>}</span>
    </div>
  </article>;
}
