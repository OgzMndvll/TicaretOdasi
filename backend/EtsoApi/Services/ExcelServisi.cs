using ClosedXML.Excel;

namespace EtsoApi.Services;

public record IceAktarmaSonucu(int Eklenen, int Atlanan, List<string> Hatalar);

public static class ExcelServisi
{
    public const string IcerikTipi = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Başlık satırı + satır verilerinden xlsx üretir.</summary>
    public static byte[] Olustur(string sayfaAdi, string[] basliklar, IEnumerable<object?[]> satirlar)
    {
        using var kitap = new XLWorkbook();
        var sayfa = kitap.Worksheets.Add(sayfaAdi);
        for (var i = 0; i < basliklar.Length; i++)
        {
            var hucre = sayfa.Cell(1, i + 1);
            hucre.Value = basliklar[i];
            hucre.Style.Font.Bold = true;
            hucre.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F0FE");
        }
        var satirNo = 2;
        foreach (var satir in satirlar)
        {
            for (var i = 0; i < satir.Length; i++)
            {
                var deger = satir[i];
                var hucre = sayfa.Cell(satirNo, i + 1);
                if (deger is DateTime tarih) { hucre.Value = tarih; hucre.Style.DateFormat.Format = "dd.mm.yyyy hh:mm"; }
                else if (deger is int sayi) hucre.Value = sayi;
                else if (deger is double ondalik) hucre.Value = ondalik;
                else hucre.Value = deger?.ToString() ?? "";
            }
            satirNo++;
        }
        sayfa.Columns().AdjustToContents(1, Math.Min(satirNo, 50));
        using var akis = new MemoryStream();
        kitap.SaveAs(akis);
        return akis.ToArray();
    }

    /// <summary>
    /// Yüklenen xlsx dosyasının ilk sayfasını okur; başlıkları normalize edip
    /// her veri satırını başlık→değer sözlüğü olarak döndürür (satır numarasıyla birlikte).
    /// </summary>
    /// <summary>
    /// Tek dosyadan okunacak en fazla veri satırı. Yüklenen dosya 10 MB ile sınırlı olsa da
    /// sıkıştırılmış bir çalışma kitabı bellekte çok daha fazla yer kaplayabilir; tavan bunu keser.
    /// </summary>
    public const int EnFazlaSatir = 50_000;

    public static List<(int SatirNo, Dictionary<string, string> Degerler)> Oku(Stream dosya)
    {
        using var kitap = new XLWorkbook(dosya);
        var sayfa = kitap.Worksheets.First();
        var kullanilanAlan = sayfa.RangeUsed();
        var sonuc = new List<(int, Dictionary<string, string>)>();
        if (kullanilanAlan is null) return sonuc;

        var basliklar = kullanilanAlan.FirstRow().Cells()
            .Select(c => Normalize(c.GetString()))
            .ToList();

        foreach (var satir in kullanilanAlan.RowsUsed().Skip(1))
        {
            var degerler = new Dictionary<string, string>();
            for (var i = 0; i < basliklar.Count; i++)
            {
                if (string.IsNullOrEmpty(basliklar[i])) continue;
                degerler[basliklar[i]] = HucreMetni(satir.Cell(i + 1));
            }
            if (degerler.Values.All(string.IsNullOrWhiteSpace)) continue;
            sonuc.Add((satir.RowNumber(), degerler));
            if (sonuc.Count >= EnFazlaSatir)
                throw new InvalidOperationException($"Dosyada {EnFazlaSatir:N0} satırdan fazla veri var; dosyayı bölerek yükleyin.");
        }
        return sonuc;
    }

    /// <summary>
    /// Hücreyi metne çevirir. Gerçek tarih hücreleri makinenin kültürüne göre biçimlenirse
    /// (12/20/1978 mi 20/12/1978 mi?) geri okuma bozulur; bu yüzden tarihler sabit ISO biçimine çevrilir.
    /// </summary>
    private static string HucreMetni(IXLCell hucre) =>
        hucre.DataType == XLDataType.DateTime && hucre.TryGetValue<DateTime>(out var tarih)
            ? tarih.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)
            : hucre.GetString().Trim();

    /// <summary>Başlıkları Türkçe karakter/boşluk/büyük-küçük farklarına dayanıklı hale getirir.</summary>
    public static string Normalize(string baslik)
    {
        // 'İ' (U+0130) ToLowerInvariant ile küçülmediği için Türkçe harfler önce tek tek sadeleştirilir;
        // aksi halde "YETKİLİ ADI SOYADI" gibi başlıklar hiçbir arama anahtarıyla eşleşmez.
        var sade = baslik.Trim()
            .Replace('İ', 'i').Replace('I', 'i').Replace('ı', 'i')
            .Replace('Ç', 'c').Replace('Ğ', 'g').Replace('Ö', 'o').Replace('Ş', 's').Replace('Ü', 'u');
        return sade.ToLowerInvariant()
            .Replace("ç", "c").Replace("ğ", "g").Replace("\u0307", "")
            .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u")
            .Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "").Replace(".", "");
    }
}
