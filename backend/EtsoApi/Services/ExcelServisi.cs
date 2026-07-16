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
                degerler[basliklar[i]] = satir.Cell(i + 1).GetString().Trim();
            }
            if (degerler.Values.All(string.IsNullOrWhiteSpace)) continue;
            sonuc.Add((satir.RowNumber(), degerler));
        }
        return sonuc;
    }

    /// <summary>Başlıkları Türkçe karakter/boşluk/büyük-küçük farklarına dayanıklı hale getirir.</summary>
    public static string Normalize(string baslik) => baslik.Trim().ToLowerInvariant()
        .Replace("ç", "c").Replace("ğ", "g").Replace("ı", "i").Replace("i̇", "i")
        .Replace("ö", "o").Replace("ş", "s").Replace("ü", "u")
        .Replace(" ", "").Replace("/", "").Replace("-", "").Replace("_", "").Replace(".", "");
}
