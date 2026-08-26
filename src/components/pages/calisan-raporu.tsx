"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import { CheckCircle2, ChevronDown, ChevronRight, CloudDownload, Filter, MessageSquareText, Percent, UsersRound } from "lucide-react";
import { FilterBar, FiltreSecim } from "@/components/ui/filter-bar";
import { StatCard } from "@/components/ui/stat-card";
import {
  api, API_ERISIM_HATASI, durumTonu, sayiGoster, tarihGoster, yuzde,
  CalisanGorusmeSatiri, CalisanRaporSatiri, CalisanRaporu as CalisanRaporuVerisi, GrupKaydi,
} from "@/lib/api";
import { useCanliYenileme } from "@/lib/canli";

const GORUSME_SONUCLARI = ["Onay Verdi", "Onay Vermedi", "Kararsız"];

function gunOnce(gun: number): string {
  const t = new Date();
  t.setDate(t.getDate() - gun);
  return t.toISOString().slice(0, 10);
}

/**
 * Çalışan (görüşen görevli) bazlı görüşme raporu. Bir görüşme yalnızca birincil görevliye
 * sayılır; ikinci çalışan olarak katılım ayrı sütunda durur, aksi halde sütun toplamları
 * gerçek görüşme sayısını aşardı. Tablodaki tüm süzgeçler Excel çıktısına da geçer.
 */
export function CalisanRaporu({ onToast }: { onToast: (mesaj: string) => void }) {
  const [durum, setDurum] = useState("Aktif");
  const [sonuc, setSonuc] = useState("");
  const [grupId, setGrupId] = useState("");
  const [baslangic, setBaslangic] = useState("");
  const [bitis, setBitis] = useState("");

  const [gruplar, setGruplar] = useState<GrupKaydi[]>([]);
  const [veri, setVeri] = useState<CalisanRaporuVerisi | null>(null);
  const [hata, setHata] = useState("");
  const [indiriliyor, setIndiriliyor] = useState(false);

  // Açılan satırın görüşme dökümü ("kimle görüşmüş"); süzgeç değişince tazelenir.
  const [acikId, setAcikId] = useState<number | null>(null);
  const [detay, setDetay] = useState<CalisanGorusmeSatiri[] | null>(null);

  const sorgu = useMemo(() => {
    const params = new URLSearchParams();
    // Boş süzgeç parametre olarak hiç gönderilmez: sunucu tarafında boş bir değer, kaydın
    // varsayılanına düşüyor. "Tümü" seçimi bu yüzden parametreyi tamamen atlar.
    if (durum) params.set("durum", durum);
    if (sonuc) params.set("sonuc", sonuc);
    if (grupId) params.set("grupId", grupId);
    if (baslangic) params.set("baslangic", baslangic);
    if (bitis) params.set("bitis", bitis);
    return params.toString();
  }, [durum, sonuc, grupId, baslangic, bitis]);

  useEffect(() => {
    api.get<GrupKaydi[]>("/api/gruplar").then(setGruplar).catch(() => setGruplar([]));
  }, []);

  const yukle = useCallback(() => {
    api.get<CalisanRaporuVerisi>(`/api/raporlar/calisanlar?${sorgu}`)
      .then(v => { setVeri(v); setHata(""); })
      .catch(() => setHata(API_ERISIM_HATASI));
  }, [sorgu]);

  useEffect(() => { yukle(); }, [yukle]);
  useCanliYenileme(yukle);

  // Süzgeç değişince açık dökümün içeriği de eskir; aynı satır yeni süzgeçle yeniden çekilir.
  useEffect(() => {
    if (acikId === null) { setDetay(null); return; }
    let iptal = false;
    setDetay(null);
    api.get<CalisanGorusmeSatiri[]>(`/api/raporlar/calisanlar/${acikId}/gorusmeler?${sorgu}`)
      .then(v => { if (!iptal) setDetay(v); })
      .catch(() => { if (!iptal) setDetay([]); });
    return () => { iptal = true; };
  }, [acikId, sorgu]);

  const filtreler = useMemo<FiltreSecim[]>(() => [
    {
      label: "Çalışan Durumu", value: durum, onChange: setDurum,
      options: [{ deger: "Aktif", etiket: "Aktif çalışanlar" }, { deger: "Pasif", etiket: "Pasif çalışanlar" }],
    },
    {
      label: "Görüşme Sonucu", value: sonuc, onChange: setSonuc,
      options: GORUSME_SONUCLARI.map(s => ({ deger: s, etiket: s === "Onay Verdi" ? "Yalnızca onay alınanlar" : s })),
    },
    {
      label: "Meslek Grubu", value: grupId, onChange: setGrupId,
      options: gruplar.map(g => ({ deger: String(g.id), etiket: g.no ? `${g.no}. ${g.ad}` : g.ad })),
    },
  ], [durum, sonuc, grupId, gruplar]);

  const satirlar = veri?.satirlar ?? [];
  const toplam = veri?.toplam;

  function indir() {
    setIndiriliyor(true);
    api.indir(`/api/raporlar/calisanlar/disa-aktar?${sorgu}`)
      .then(() => onToast("Çalışan raporu indirildi (Özet + Görüşme Detayı sayfaları)."))
      .catch(() => onToast("Rapor indirilemedi. API'nin çalıştığından emin olun."))
      .finally(() => setIndiriliyor(false));
  }

  return <section className="calisan-raporu">
    <div className="bolum-basligi">
      <h2>Çalışan Bazlı Görüşme Raporu</h2>
      <p>
        Her görüşme yalnızca <strong>görüşen çalışana</strong> sayılır; ikinci kişi olarak katılım ayrı sütundadır.
        Onay sayıları çalışanın kendi görüşme sonucundan gelir, üyenin güncel durumundan değil.
      </p>
    </div>
    <FilterBar
      filters={filtreler}
      onReset={() => { setDurum("Aktif"); setSonuc(""); setGrupId(""); setBaslangic(""); setBitis(""); setAcikId(null); }}
      ekAlanlar={
        <div className="filter-select filter-range">
          <span id="calisan-tarih-araligi">Görüşme Tarihi Aralığı</span>
          <div className="date-range" role="group" aria-labelledby="calisan-tarih-araligi">
            <input type="date" aria-label="Başlangıç tarihi" value={baslangic} max={bitis || undefined}
              onChange={e => setBaslangic(e.target.value)} />
            <i aria-hidden="true">–</i>
            <input type="date" aria-label="Bitiş tarihi" value={bitis} min={baslangic || undefined}
              onChange={e => setBitis(e.target.value)} />
          </div>
        </div>
      }
      extra={<button className="dark-button" onClick={indir} disabled={indiriliyor}>
        <CloudDownload size={16} />{indiriliyor ? "Hazırlanıyor..." : "Excel'e Aktar"}
      </button>}
    />
    {hata && <section className="panel"><p role="alert">{hata}</p></section>}
    <div className="stats-grid four">
      <StatCard icon={UsersRound} label="Raporlanan Çalışan" value={toplam ? sayiGoster(toplam.calisan) : "…"}
        detail={durum ? `${durum} kayıtlar` : "Tüm çalışanlar"} />
      <StatCard icon={MessageSquareText} label="Toplam Görüşme" value={toplam ? sayiGoster(toplam.gorusme) : "…"}
        detail="Seçili süzgeçlerle" tone="purple" />
      <StatCard icon={CheckCircle2} label="Onay Verdi" value={toplam ? sayiGoster(toplam.onayVerdi) : "…"}
        detail={toplam ? `${sayiGoster(toplam.onayVermedi)} ret · ${sayiGoster(toplam.kararsiz)} kararsız` : ""} tone="green" />
      {sonuc
        // Sonuç süzgeci açıkken her satırda pay = payda olur; oran %100'e sabitlenip yanıltır.
        ? <StatCard icon={Filter} label="Sonuç Süzgeci" value={sonuc}
            detail="Oran, süzgeç açıkken gösterilmez" tone="orange" />
        : <StatCard icon={Percent} label="Onay Oranı" value={toplam ? yuzde(toplam.onayVerdi, toplam.gorusme) : "…"}
            detail="Görüşme başına" tone="orange" />}
    </div>
    <div className="panel table-panel">
      <div className="table-scroll"><table className="calisan-tablosu">
        <thead><tr>
          <th>Çalışan</th><th>Görüşme</th><th>Görüşülen Üye</th>
          <th>Onay Verdi</th><th>Onay Vermedi</th><th>Kararsız</th>
          <th>Onay Alınan Üye</th><th>2. Kişi Katılımı</th><th>Onay Oranı</th><th>Son Görüşme</th><th>Kimlerle</th>
        </tr></thead>
        <tbody>
          {satirlar.length === 0 && <tr><td colSpan={11} className="bos-satir">
            Seçili süzgeçlere uyan çalışan yok.
          </td></tr>}
          {satirlar.map(s => <CalisanSatiri key={s.id} satir={s} acik={acikId === s.id} detay={detay}
            oraniGizle={!!sonuc} onAc={() => setAcikId(acikId === s.id ? null : s.id)} />)}
        </tbody>
      </table></div>
    </div>
  </section>;
}

function CalisanSatiri({ satir, acik, detay, oraniGizle, onAc }: {
  satir: CalisanRaporSatiri; acik: boolean; detay: CalisanGorusmeSatiri[] | null;
  /** Sonuç süzgeci açıkken oran anlamını yitirir (bkz. üstteki kart). */
  oraniGizle: boolean;
  onAc: () => void;
}) {
  return <>
    <tr className={acik ? "acik-satir" : undefined}>
      <td>
        <strong>{satir.adSoyad}</strong>
        <small>{[satir.gorev, satir.birim].filter(Boolean).join(" · ") || satir.rol}</small>
      </td>
      <td>{sayiGoster(satir.gorusme)}</td>
      <td>{sayiGoster(satir.uyeSayisi)}</td>
      <td>{sayiGoster(satir.onayVerdi)}</td>
      <td>{sayiGoster(satir.onayVermedi)}</td>
      <td>{sayiGoster(satir.kararsiz)}</td>
      <td>{sayiGoster(satir.onayliUyeSayisi)}</td>
      <td>{satir.ikinciKatilim ? sayiGoster(satir.ikinciKatilim) : "-"}</td>
      <td>{oraniGizle
        ? <span title="Sonuç süzgeci açıkken oran her satırda %100 çıkar; süzgeci kaldırın.">—</span>
        : <span className="ratio"><i style={{ width: `${Math.min(100, satir.onayOrani)}%` }} />%{satir.onayOrani.toLocaleString("tr-TR")}</span>}</td>
      <td>{satir.sonGorusme ? tarihGoster(satir.sonGorusme) : "-"}</td>
      <td>
        <button type="button" className="detay-ac" onClick={onAc} disabled={satir.gorusme === 0}
          aria-expanded={acik} aria-label={`${satir.adSoyad} görüşme dökümü`}>
          {acik ? <ChevronDown size={15} /> : <ChevronRight size={15} />}
          {satir.gorusme === 0 ? "Görüşme yok" : `${sayiGoster(satir.gorusme)} görüşme`}
        </button>
      </td>
    </tr>
    {acik && <tr className="detay-satiri"><td colSpan={11}>
      {detay === null
        ? <p className="bos-satir">Görüşme dökümü yükleniyor…</p>
        : detay.length === 0
          ? <p className="bos-satir">Seçili süzgeçlerde görüşme kaydı yok.</p>
          : <div className="table-scroll"><table>
            <thead><tr>
              <th>Üye Yetkilisi</th><th>Unvan</th><th>Meslek Grubu</th><th>İlçe</th>
              <th>Kaçıncı</th><th>Tarih</th><th>Sonuç</th><th>Görüşecek Kişi</th><th>Not</th>
            </tr></thead>
            <tbody>{detay.map(g => <tr key={g.id}>
              <td><strong>{g.esnaf}</strong></td>
              <td>{g.isletme}</td>
              <td>{g.grup ? `${g.grupNo ? `${g.grupNo}. ` : ""}${g.grup}` : "-"}</td>
              <td>{g.ilce ?? "-"}</td>
              <td>{g.sira}. Görüşme</td>
              <td>{tarihGoster(g.tarih)}</td>
              <td><span className={`badge ${durumTonu(g.sonuc)}`}>{g.sonuc}</span></td>
              <td>{g.ikinciGorevli ?? "-"}</td>
              <td className="gorusme-not">{g.not || "-"}</td>
            </tr>)}</tbody>
          </table></div>}
    </td></tr>}
  </>;
}
