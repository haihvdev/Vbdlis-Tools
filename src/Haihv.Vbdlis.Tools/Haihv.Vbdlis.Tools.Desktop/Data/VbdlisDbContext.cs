using Microsoft.EntityFrameworkCore;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Haihv.Vbdlis.Tools.Desktop.Data;

/// <summary>
/// DbContext cho VBDLIS Tools sử dụng Entity Framework Core với SQLite
/// </summary>
public class VbdlisDbContext(DbContextOptions<VbdlisDbContext> options) : DbContext(options)
{
    /// <summary>
    /// Bảng Đơn vị hành chính cấp tỉnh
    /// </summary>
    public DbSet<DvhcCapTinh> DvhcCapTinh { get; set; } = null!;

    /// <summary>
    /// Bảng Đơn vị hành chính cấp huyện
    /// </summary>
    public DbSet<DvhcCapHuyen> DvhcCapHuyen { get; set; } = null!;

    /// <summary>
    /// Bảng Đơn vị hành chính cấp xã
    /// </summary>
    public DbSet<DvhcCapXa> DvhcCapXa { get; set; } = null!;

    /// <summary>
    /// Bảng tham chiếu tờ bản đồ
    /// </summary>
    public DbSet<ThamChieuToBanDo> ThamChieuToBanDo { get; set; } = null!;

    /// <summary>
    /// Bảng lịch sử tìm kiếm
    /// </summary>
    public DbSet<SearchHistoryEntry> SearchHistoryEntries { get; set; } = null!;

    /// <summary>
    /// Bảng cache kết quả tìm kiếm
    /// </summary>
    public DbSet<SearchCacheEntry> SearchCacheEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Cấu hình cho DvhcCapTinh
        modelBuilder.Entity<DvhcCapTinh>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Cấu hình cho DvhcCapHuyen
        modelBuilder.Entity<DvhcCapHuyen>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CapTinhId);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Cấu hình cho DvhcCapXa
        modelBuilder.Entity<DvhcCapXa>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.CapTinhId, e.CapHuyenId });
            entity.Property(e => e.Name).IsRequired().HasMaxLength(255);
            entity.Property(e => e.Dvhc2Cap).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Cấu hình cho ThamChieuToBanDo
        modelBuilder.Entity<ThamChieuToBanDo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TinhId, e.XaId });
            entity.HasIndex(e => e.SoToBanDo);
            entity.Property(e => e.SoToBanDoCu).HasMaxLength(100);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("datetime('now')");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("datetime('now')");
        });

        // Cấu hình cho SearchHistoryEntry
        modelBuilder.Entity<SearchHistoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SearchType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SearchQuery).IsRequired();
            entity.Property(e => e.ResultCount).HasDefaultValue(0);
            entity.Property(e => e.SearchItemCount).HasDefaultValue(0);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.SearchedAt).IsRequired();
            entity.HasIndex(e => new { e.SearchType, e.SearchQuery }).IsUnique();
            entity.HasIndex(e => new { e.SearchType, e.SearchedAt });
        });

        // Cấu hình cho SearchCacheEntry
        modelBuilder.Entity<SearchCacheEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SearchType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.SearchKey).IsRequired();
            entity.Property(e => e.ResponseJson).IsRequired();
            entity.Property(e => e.CachedAt).IsRequired();
            entity.HasIndex(e => new { e.SearchType, e.SearchKey }).IsUnique();
            entity.HasIndex(e => e.CachedAt);
        });
    }

    /// <summary>
    /// Override SaveChangesAsync để tự động cập nhật UpdatedAt
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case DvhcCapTinh tinh:
                    tinh.UpdatedAt = DateTime.UtcNow;
                    break;
                case DvhcCapHuyen huyen:
                    huyen.UpdatedAt = DateTime.UtcNow;
                    break;
                case DvhcCapXa xa:
                    xa.UpdatedAt = DateTime.UtcNow;
                    break;
                case ThamChieuToBanDo thamChieu:
                    thamChieu.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}