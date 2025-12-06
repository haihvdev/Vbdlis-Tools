using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services.Data;

/// <summary>
/// Service quản lý tham chiếu tờ bản đồ sử dụng EF Core
/// </summary>
public class ThamChieuToBanDoService(IDatabaseService databaseService)
{
    private readonly ILogger _logger = Log.ForContext<ThamChieuToBanDoService>();
    private readonly IDatabaseService _databaseService = databaseService;
    private bool _initialized;

    /// <summary>
    /// Khởi tạo service và load dữ liệu nếu cần
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_initialized)
            return;

        try
        {
            var dbContext = _databaseService.GetDbContext();
            var count = await dbContext.ThamChieuToBanDo.CountAsync();

            _logger.Information("ThamChieuToBanDo service initialized with {Count} records", count);
            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to initialize ThamChieuToBanDo service");
            throw;
        }
    }

    /// <summary>
    /// Tìm tờ bản đồ mới theo tờ bản đồ cũ
    /// </summary>
    public async Task<ThamChieuToBanDo?> FindByOldMapNumberAsync(string soToBanDoCu, int tinhId, int xaId)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();
            return await dbContext.ThamChieuToBanDo
                .FirstOrDefaultAsync(x =>
                    x.SoToBanDoCu == soToBanDoCu &&
                    x.TinhId == tinhId &&
                    x.XaId == xaId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to find map reference for old number {SoToBanDoCu}", soToBanDoCu);
            return null;
        }
    }

    /// <summary>
    /// Tìm tờ bản đồ cũ theo tờ bản đồ mới
    /// </summary>
    public async Task<List<ThamChieuToBanDo>> FindByNewMapNumberAsync(int soToBanDo, int tinhId, int xaId)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();
            return await dbContext.ThamChieuToBanDo
                .Where(x =>
                    x.SoToBanDo == soToBanDo &&
                    x.TinhId == tinhId &&
                    x.XaId == xaId)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to find map references for new number {SoToBanDo}", soToBanDo);
            return [];
        }
    }

    /// <summary>
    /// Thêm hoặc cập nhật tham chiếu tờ bản đồ
    /// </summary>
    public async Task<ThamChieuToBanDo> UpsertAsync(ThamChieuToBanDo thamChieu)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();

            // Tìm bản ghi hiện có
            var existing = await dbContext.ThamChieuToBanDo
                .FirstOrDefaultAsync(x =>
                    x.SoToBanDoCu == thamChieu.SoToBanDoCu &&
                    x.TinhId == thamChieu.TinhId &&
                    x.XaId == thamChieu.XaId);

            if (existing != null)
            {
                // Cập nhật
                existing.SoToBanDo = thamChieu.SoToBanDo;
                existing.TinhCuId = thamChieu.TinhCuId;
                existing.XaCuId = thamChieu.XaCuId;
                existing.Note = thamChieu.Note;
                existing.UpdatedAt = DateTime.UtcNow;

                dbContext.ThamChieuToBanDo.Update(existing);
            }
            else
            {
                // Thêm mới
                await dbContext.ThamChieuToBanDo.AddAsync(thamChieu);
            }

            await dbContext.SaveChangesAsync();
            return existing ?? thamChieu;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to upsert map reference");
            throw;
        }
    }

    /// <summary>
    /// Import nhiều tham chiếu cùng lúc
    /// </summary>
    public async Task ImportBatchAsync(List<ThamChieuToBanDo> thamChieuList)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();

            foreach (var thamChieu in thamChieuList)
            {
                await UpsertAsync(thamChieu);
            }

            _logger.Information("Imported {Count} map references", thamChieuList.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to import map references");
            throw;
        }
    }
}
