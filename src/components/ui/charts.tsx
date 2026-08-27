/** "brown", "Gelmeyecek" içindir: kırmızı "Onay Vermeyen" dilimiyle karışmaz. */
export interface DonutSegment { label: string; value: number; color: "blue" | "green" | "orange" | "red" | "brown" | "gray" }

const RENKLER: Record<DonutSegment["color"], string> = {
  blue: "#1767e8", green: "#36a657", orange: "#ff9b17", red: "#e84235", brown: "#8a5a2b", gray: "#cbd2dd",
};

export function DonutChart({ segments }: { segments: DonutSegment[] }) {
  const toplam = segments.reduce((t, s) => t + s.value, 0);
  let birikim = 0;
  const dilimler = segments.map(s => {
    const baslangic = toplam ? (birikim / toplam) * 100 : 0;
    birikim += s.value;
    const bitis = toplam ? (birikim / toplam) * 100 : 0;
    return `${RENKLER[s.color]} ${baslangic}% ${bitis}%`;
  });
  const gradient = toplam ? `conic-gradient(${dilimler.join(", ")})` : `conic-gradient(${RENKLER.gray} 0 100%)`;
  return <div className="donut-wrap">
    <div className="donut" style={{ background: gradient }}><i /></div>
    <div className="legend">{segments.map(s => <span key={s.label}>
      <i className={`dot ${s.color}`} />{s.label}
      <b>{s.value.toLocaleString("tr-TR")} (%{toplam ? ((s.value * 100) / toplam).toLocaleString("tr-TR", { maximumFractionDigits: 1 }) : "0"})</b>
    </span>)}</div>
  </div>;
}

export function LineChart({ data }: { data: { ay: string; adet: number }[] }) {
  const genislik = 600, yukseklik = 240, solPay = 20, altPay = 20, ustPay = 20;
  // Günlük seride 30-90 nokta olabiliyor; nokta ve etiketler seyreltilmezse okunmaz hale gelir.
  const yogun = data.length > 14;
  const noktaYaricap = data.length > 45 ? 0 : yogun ? 3 : 6;
  const etiketAdimi = Math.max(1, Math.ceil(data.length / 12));
  const enBuyuk = Math.max(1, ...data.map(d => d.adet));
  const noktalar = data.map((d, i) => {
    const x = data.length > 1 ? solPay + (i * (genislik - solPay * 2)) / (data.length - 1) : genislik / 2;
    const y = yukseklik - altPay - (d.adet / enBuyuk) * (yukseklik - altPay - ustPay);
    return [Math.round(x), Math.round(y)] as const;
  });
  const cizgi = noktalar.map(([x, y]) => `${x},${y}`).join(" ");
  const alan = noktalar.length
    ? `M${noktalar.map(([x, y]) => `${x} ${y}`).join(" L")} L${noktalar[noktalar.length - 1][0]} ${yukseklik - altPay} L${noktalar[0][0]} ${yukseklik - altPay} Z`
    : "";
  return <div className="line-chart">
    <div className="chart-grid" />
    <svg viewBox={`0 0 ${genislik} ${yukseklik}`} preserveAspectRatio="none" aria-label="Günlük görüşme istatistikleri">
      <defs><linearGradient id="area" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stopColor="#1671ee" stopOpacity=".24" /><stop offset="1" stopColor="#1671ee" stopOpacity="0" /></linearGradient></defs>
      {alan && <path d={alan} fill="url(#area)" />}
      {noktalar.length > 1 && <polyline points={cizgi} fill="none" stroke="#1269e8" strokeWidth="4" />}
      <g fill="#1269e8">{noktalar.map(([x, y], i) => <circle key={`${data[i].ay}-${i}`} cx={x} cy={y} r={noktaYaricap}><title>{`${data[i].ay}: ${data[i].adet}`}</title></circle>)}</g>
    </svg>
    <div className="chart-labels">{data.map((d, i) =>
      <span key={`${d.ay}-${i}`}>{i % etiketAdimi === 0 || i === data.length - 1 ? d.ay : ""}</span>)}</div>
  </div>;
}

export function BarList({ items }: { items: { name: string; value: number }[] }) {
  const enBuyuk = Math.max(1, ...items.map(i => i.value));
  if (!items.length) return <div className="bar-list"><span>Henüz veri yok</span></div>;
  return <div className="bar-list">{items.map(item => <div key={item.name}>
    <span>{item.name}</span>
    <i><b style={{ width: `${Math.round((item.value / enBuyuk) * 100)}%` }} /></i>
    <em>{item.value.toLocaleString("tr-TR")}</em>
  </div>)}</div>;
}
