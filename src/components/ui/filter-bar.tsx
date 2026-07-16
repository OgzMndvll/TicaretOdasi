"use client";

import { Plus, RotateCcw, Search } from "lucide-react";

export interface FiltreSecim {
  label: string;
  options: { deger: string; etiket: string }[];
  value: string;
  onChange: (deger: string) => void;
}

export function FilterBar({ action, onAction, filters = [], searchValue, onSearch, onReset, extra }: {
  action?: string;
  onAction?: () => void;
  filters?: FiltreSecim[];
  searchValue?: string;
  onSearch?: (deger: string) => void;
  onReset?: () => void;
  extra?: React.ReactNode;
}) {
  return <section className="filter-card">
    {onSearch && <label className="filter-search"><span>Arama</span><div>
      <input maxLength={100} placeholder="Üye adı, unvan, telefon..." aria-label="Ara"
        value={searchValue ?? ""} onChange={e => onSearch(e.target.value)} />
      <Search size={18} />
    </div></label>}
    {filters.map(filtre => <label key={filtre.label} className="filter-select"><span>{filtre.label}</span>
      <select aria-label={filtre.label} value={filtre.value} onChange={e => filtre.onChange(e.target.value)}>
        <option value="">Tümü</option>
        {filtre.options.map(o => <option key={o.deger} value={o.deger}>{o.etiket}</option>)}
      </select>
    </label>)}
    <div className="filter-actions">
      {action && <button className="dark-button" onClick={onAction}><Plus size={18} />{action}</button>}
      {extra}
      {onReset && <button className="reset-button" onClick={onReset}><RotateCcw size={16} />Filtreleri Temizle</button>}
    </div>
  </section>;
}
