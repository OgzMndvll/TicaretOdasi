using EtsoApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EtsoApi.Data;

public class EtsoDbContext(DbContextOptions<EtsoDbContext> options) : DbContext(options)
{
    public DbSet<Grup> Gruplar => Set<Grup>();
    public DbSet<Kullanici> Kullanicilar => Set<Kullanici>();
    public DbSet<Esnaf> Esnaflar => Set<Esnaf>();
    public DbSet<EsnafYetkili> EsnafYetkilileri => Set<EsnafYetkili>();
    public DbSet<Gorusme> Gorusmeler => Set<Gorusme>();
    public DbSet<Gorevlendirme> Gorevlendirmeler => Set<Gorevlendirme>();
    public DbSet<Onay> Onaylar => Set<Onay>();
    public DbSet<Ayar> Ayarlar => Set<Ayar>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Grup>(e =>
        {
            // Meslek grubu adları uzun olabildiği için 120 yetmiyor (kaynak raporda en uzunu 150 karakter).
            e.Property(x => x.Ad).HasMaxLength(180);
            e.Property(x => x.Aciklama).HasMaxLength(500);
            e.Property(x => x.Tur).HasMaxLength(40);
            e.Property(x => x.Durum).HasMaxLength(20);
            e.HasIndex(x => x.Ad).IsUnique();
            e.HasIndex(x => x.No).IsUnique();
            e.HasOne(x => x.UstGrup).WithMany().HasForeignKey(x => x.UstGrupId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Kullanici>(e =>
        {
            e.Property(x => x.AdSoyad).HasMaxLength(120);
            e.Property(x => x.KullaniciAdi).HasMaxLength(60);
            e.Property(x => x.Rol).HasMaxLength(20);
            e.Property(x => x.Gorev).HasMaxLength(120);
            e.Property(x => x.Birim).HasMaxLength(120);
            e.Property(x => x.Eposta).HasMaxLength(160);
            e.Property(x => x.Telefon).HasMaxLength(30);
            e.Property(x => x.Durum).HasMaxLength(20);
            e.HasIndex(x => x.KullaniciAdi).IsUnique();
        });

        modelBuilder.Entity<Esnaf>(e =>
        {
            e.Property(x => x.AdSoyad).HasMaxLength(120);
            // Kaynak rapordaki en uzun unvan 223 karakter.
            e.Property(x => x.Isletme).HasMaxLength(250);
            e.Property(x => x.VergiNo).HasMaxLength(20);
            e.Property(x => x.Ilce).HasMaxLength(60);
            e.Property(x => x.Mahalle).HasMaxLength(80);
            e.Property(x => x.Adres).HasMaxLength(500);
            e.Property(x => x.Telefon).HasMaxLength(30);
            e.Property(x => x.IsTelefonu).HasMaxLength(30);
            e.Property(x => x.Durum).HasMaxLength(20);
            e.Property(x => x.UyeSicilNo).HasMaxLength(20);
            e.Property(x => x.TicaretSicilNo).HasMaxLength(30);
            e.Property(x => x.SirketTipi).HasMaxLength(60);
            e.Property(x => x.TabelaUnvani).HasMaxLength(160);
            e.Property(x => x.Uyruk).HasMaxLength(40);
            e.Property(x => x.Sermaye).HasMaxLength(30);
            e.Property(x => x.Derece).HasMaxLength(20);
            e.Property(x => x.VergiDairesi).HasMaxLength(80);
            e.Property(x => x.Gorevi).HasMaxLength(80);
            e.Property(x => x.UyelikDurumu).HasMaxLength(20);
            e.Property(x => x.DurumDegisimNedeni).HasMaxLength(200);
            e.Property(x => x.FaaliyetDetayi).HasMaxLength(2000);
            e.Property(x => x.NaceKodu).HasMaxLength(20);
            e.Property(x => x.NaceAdi).HasMaxLength(500);
            e.HasIndex(x => x.Durum);
            e.HasIndex(x => x.UyelikDurumu);
            // Üye sicil no oda kayıtlarında tekildir; içe aktarma tekrarını da bu anahtar engeller.
            e.HasIndex(x => x.UyeSicilNo).IsUnique();
            e.HasOne(x => x.Grup).WithMany(g => g.Esnaflar).HasForeignKey(x => x.GrupId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne(x => x.Gorevli).WithMany().HasForeignKey(x => x.GorevliId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EsnafYetkili>(e =>
        {
            e.Property(x => x.AdSoyad).HasMaxLength(120);
            e.Property(x => x.Gorevi).HasMaxLength(80);
            e.HasOne(x => x.Esnaf).WithMany(m => m.Yetkililer).HasForeignKey(x => x.EsnafId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Gorusme>(e =>
        {
            e.Property(x => x.Sonuc).HasMaxLength(20);
            e.Property(x => x.Not).HasMaxLength(1000);
            e.HasIndex(x => x.Tarih);
            e.HasOne(x => x.Esnaf).WithMany(m => m.Gorusmeler).HasForeignKey(x => x.EsnafId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Gorevli).WithMany().HasForeignKey(x => x.GorevliId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Gorevlendirme>(e =>
        {
            e.Property(x => x.Not).HasMaxLength(1000);
            e.Property(x => x.RedMazereti).HasMaxLength(1000);
            e.Property(x => x.Durum).HasMaxLength(20);
            e.HasOne(x => x.Gorevli).WithMany().HasForeignKey(x => x.GorevliId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Esnaf).WithMany().HasForeignKey(x => x.EsnafId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Grup).WithMany().HasForeignKey(x => x.GrupId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<Ayar>(e =>
        {
            e.Property(x => x.Anahtar).HasMaxLength(80);
            e.Property(x => x.Deger).HasMaxLength(2000);
        });

        modelBuilder.Entity<Onay>(e =>
        {
            e.Property(x => x.IslemTuru).HasMaxLength(80);
            e.Property(x => x.Durum).HasMaxLength(20);
            e.HasIndex(x => x.Durum);
            e.HasOne(x => x.Esnaf).WithMany().HasForeignKey(x => x.EsnafId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Gorevli).WithMany().HasForeignKey(x => x.GorevliId).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
