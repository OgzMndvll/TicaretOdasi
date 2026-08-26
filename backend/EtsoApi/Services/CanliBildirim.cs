using EtsoApi.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace EtsoApi.Services;

/// <summary>
/// Değişiklik bildirimlerini tek yerden yayımlar. Controller'lar <see cref="IHubContext{THub}"/>'i
/// doğrudan kullanmak yerine bunu çağırır: olay adı ve gövdesi tek noktada durur.
/// </summary>
public class CanliBildirim(IHubContext<PanelHub> hub, ILogger<CanliBildirim> gunluk)
{
    /// <summary>İstemcinin dinlediği olay adı. Değişirse src/lib/canli.ts de güncellenmeli.</summary>
    public const string Olay = "veriDegisti";

    /// <summary>
    /// Bağlı tüm panellere bir kaydın değiştiğini bildirir. Gövde kasıtlı olarak minimum tutulur:
    /// istemci haberi alınca veriyi kendi yetkisiyle API'den yeniden okur.
    /// </summary>
    /// <param name="tur">Değişen kayıt türü: "gorusme", "esnaf", "kullanici", "grup".</param>
    /// <param name="id">Değişen kaydın kimliği (bilinmiyorsa null).</param>
    public async Task DegistiAsync(string tur, int? id = null)
    {
        try
        {
            await hub.Clients.All.SendAsync(Olay, new { tur, id, zaman = DateTime.UtcNow });
        }
        catch (Exception hata)
        {
            // Canlı bildirim yan işlevdir: gönderilemezse asıl kayıt işlemi başarısız sayılmamalı.
            gunluk.LogWarning(hata, "Canlı bildirim gönderilemedi ({Tur}).", tur);
        }
    }
}
