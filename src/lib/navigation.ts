import {
  ChartNoAxesCombined, ClipboardList, KeyRound, LayoutDashboard,
  Settings, Store, UserRound, UsersRound,
} from "lucide-react";

/**
 * Kenar çubuğu menüsü. `sistemYonetimi: true` olan sayfalar yalnızca sistem yöneticisinde
 * görünür; geri kalanını panele giren herkes görür. Menüyü gizlemek görünüm içindir —
 * sayfaların kendisi de, arkasındaki uçlar da sunucuda ayrıca kilitlidir.
 */
export const navigation = [
  { label: "Dashboard", href: "/", icon: LayoutDashboard },
  { label: "Üyeler", href: "/esnaflar", icon: Store },
  { label: "Aktif Çalışanlar", href: "/calisanlar", icon: UserRound },
  { label: "Raporlar", href: "/raporlar", icon: ChartNoAxesCombined },
  { label: "Gruplar", href: "/gruplar", icon: UsersRound },
  { label: "İşlem Kayıtları", href: "/islem-kayitlari", icon: ClipboardList, sistemYonetimi: true },
  { label: "Kullanıcılar", href: "/kullanicilar", icon: KeyRound, sistemYonetimi: true },
  { label: "Ayarlar", href: "/ayarlar", icon: Settings, sistemYonetimi: true },
];
