using EtsoApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace EtsoApi.Hubs;

/// <summary>
/// Panelin canlı veri kanalı. Yalnızca sunucudan istemciye tek yönlü "veri değişti" haberi taşır;
/// istemciden çağrılabilen hiçbir metodu yoktur. Bu kasıtlıdır: kanal veri taşımaz, ekranlar haberi
/// alınca veriyi her zamanki yetkili API uçlarından yeniden okur, böylece yetki denetimi tek yerde kalır.
///
/// Yetki, Program.cs'teki genel kuralla aynı: yalnızca kimliği doğrulanmış Yönetici bağlanabilir.
/// Kural burada açıkça yazılır; yalnızca FallbackPolicy'ye güvenilseydi ileride oraya eklenecek bir
/// [Authorize] metadata'sı bu ucu sessizce zayıflatabilirdi.
/// </summary>
[Authorize(Roles = Kullanici.YoneticiRolu)]
public class PanelHub : Hub
{
}
