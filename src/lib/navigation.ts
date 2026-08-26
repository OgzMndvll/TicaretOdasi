import {
  ChartNoAxesCombined, LayoutDashboard,
  Settings, Store, UserRound, UsersRound,
} from "lucide-react";

export const navigation = [
  { label: "Dashboard", href: "/", icon: LayoutDashboard },
  { label: "Üyeler", href: "/esnaflar", icon: Store },
  { label: "Aktif Çalışanlar", href: "/calisanlar", icon: UserRound },
  { label: "Raporlar", href: "/raporlar", icon: ChartNoAxesCombined },
  { label: "Gruplar", href: "/gruplar", icon: UsersRound },
  { label: "Ayarlar", href: "/ayarlar", icon: Settings },
];
