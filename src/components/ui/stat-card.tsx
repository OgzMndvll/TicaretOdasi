import type { LucideIcon } from "lucide-react";

export function StatCard({ icon: Icon, label, value, detail, tone = "blue", trend }: { icon: LucideIcon; label: string; value: string; detail: string; tone?: string; trend?: string }) {
  return <article className="stat-card">
    <span className={`stat-icon ${tone}`}><Icon size={27} /></span>
    <div className="stat-content">
      <b title={label}>{label}</b>
      <strong>{value}</strong>
      <span className="stat-footer"><small title={detail}>{detail}</small>{trend && <em className={trend.startsWith("-") ? "down" : "up"}>{trend}</em>}</span>
    </div>
  </article>;
}
