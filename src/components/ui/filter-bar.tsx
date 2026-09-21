"use client";

import { Plus, RotateCcw, Search, UserSearch } from "lucide-react";
import { CokluSecim } from "./coklu-secim";

export interface FiltreSecim {
  label: string;
  options: { deger: string; etiket: string }[];
  /** Tek seçimde değerin kendisi, çoklu seçimde virgülle ayrılmış değerler. */
  value: string;
  onChange: (deger: string) => void;
  /** true: aynı süzgeçte birden çok değer seçilebilir. */
  coklu?: boolean;
}

export function FilterBar({ action, onAction, filters = [], searchValue, onSearch, yetkiliValue, onYetkili, onReset, extra, ekAlanlar }: {
  action?: string;
  onAction?: () => void;
  filters?: FiltreSecim[];
  searchValue?: string;
  onSearch?: (deger: string) => void;
  /** Yalnızca yetkili kişinin adı soyadıyla arama (şirket unvanına bakmaz). */
  yetkiliValue?: string;
  onYetkili?: (deger: string) => void;
  onReset?: () => void;
  extra?: React.ReactNode;
  /** Açılır listelerin yanına giren serbest alanlar (ör. tarih aralığı girdileri). */
  ekAlanlar?: React.ReactNode;
}) {
  return <section className="filter-card">
    {onSearch && <label className="filter-search"><span>Arama</span><div>
      <input maxLength={100} placeholder="Üye adı, unvan, telefon..." aria-label="Ara"
        value={searchValue ?? ""} onChange={e => onSearch(e.target.value)} />
      <Search size={18} />
    </div></label>}
    {onYetkili && <label className="filter-search"><span>Yetkili Adı Soyadı</span><div>
      <input maxLength={100} placeholder="Örn. Ahmet Yılmaz" aria-label="Yetkili adı soyadı"
        value={yetkiliValue ?? ""} onChange={e => onYetkili(e.target.value)} />
      <UserSearch size={18} />
    </div></label>}
    {filters.map(filtre => filtre.coklu
      ? <div key={filtre.label} className="filter-select"><span>{filtre.label}</span>
          <CokluSecim label={filtre.label} options={filtre.options} value={filtre.value} onChange={filtre.onChange} />
        </div>
      : <label key={filtre.label} className="filter-select"><span>{filtre.label}</span>
          <select aria-label={filtre.label} value={filtre.value} onChange={e => filtre.onChange(e.target.value)}>
            <option value="">Tümü</option>
            {filtre.options.map(o => <option key={o.deger} value={o.deger}>{o.etiket}</option>)}
          </select>
        </label>)}
    {ekAlanlar}
    <div className="filter-actions">
      {action && <button className="dark-button" onClick={onAction}><Plus size={18} />{action}</button>}
      {extra}
      {onReset && <button className="reset-button" onClick={onReset}><RotateCcw size={16} />Filtreleri Temizle</button>}
    </div>
  </section>;
}
