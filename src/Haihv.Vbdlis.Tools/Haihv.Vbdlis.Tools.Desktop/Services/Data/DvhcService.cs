using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Haihv.Vbdlis.Tools.Desktop.Entities;
using Serilog;

namespace Haihv.Vbdlis.Tools.Desktop.Services.Data;

/// <summary>
/// Service quản lý cache Đơn vị hành chính (ĐVHC) sử dụng EF Core
/// </summary>
public class DvhcService(IDatabaseService databaseService)
{
    private readonly ILogger _logger = Log.ForContext<DvhcService>();
    private readonly IDatabaseService _databaseService = databaseService;

    #region DvhcCapHuyen

    /// <summary>
    /// Lưu danh sách huyện vào database
    /// </summary>
    public async Task SaveCapHuyenListAsync(int tinhId, List<DvhcCapHuyen> capHuyenList)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();

            // Xóa dữ liệu cũ của tỉnh này
            var existingRecords = await dbContext.DvhcCapHuyen
                .Where(x => x.CapTinhId == tinhId)
                .ToListAsync();

            if (existingRecords.Count != 0)
            {
                dbContext.DvhcCapHuyen.RemoveRange(existingRecords);
            }

            // Thêm dữ liệu mới
            await dbContext.DvhcCapHuyen.AddRangeAsync(capHuyenList);
            await dbContext.SaveChangesAsync();

            _logger.Information("Đã lưu {Count} quận/huyện cho tỉnh {TinhId}", capHuyenList.Count, tinhId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lưu danh sách quận/huyện cho tỉnh {TinhId} thất bại", tinhId);
            throw;
        }
    }

    /// <summary>
    /// Lấy danh sách huyện từ database
    /// Mặc định tỉnh Id = 24 (Tỉnh Bắc Ninh mới - Tỉnh Bắc Giang cũ)
    /// </summary>
    public async Task<List<DvhcCapHuyen>> GetCapHuyenListAsync(int tinhId = 24)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();
            var result = await dbContext.DvhcCapHuyen
                .Where(x => x.CapTinhId == tinhId)
                .OrderBy(x => x.Name)
                .ToListAsync();

            _logger.Debug("Đã lấy {Count} quận/huyện cho tỉnh {TinhId}", result.Count, tinhId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lấy danh sách quận/huyện cho tỉnh {TinhId} thất bại", tinhId);
            return [];
        }
    }

    #endregion

    #region DvhcCapXa

    /// <summary>
    /// Lưu danh sách xã vào database
    /// </summary>
    public async Task SaveCapXaListAsync(int tinhId, List<DvhcCapXa> capXaList)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();

            // Xóa dữ liệu cũ của tỉnh này
            var existingRecords = await dbContext.DvhcCapXa
                .Where(x => x.CapTinhId == tinhId)
                .ToListAsync();

            if (existingRecords.Any())
            {
                dbContext.DvhcCapXa.RemoveRange(existingRecords);
            }

            // Thêm dữ liệu mới
            await dbContext.DvhcCapXa.AddRangeAsync(capXaList);
            await dbContext.SaveChangesAsync();

            _logger.Information("Đã lưu {Count} xã/phường cho tỉnh {TinhId}", capXaList.Count, tinhId);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lưu danh sách xã/phường cho tỉnh {TinhId} thất bại", tinhId);
            throw;
        }
    }

    /// <summary>
    /// Lấy danh sách xã theo tỉnh từ database
    /// Mặc định tỉnh Id = 24 (Tỉnh Bắc Ninh mới - Tỉnh Bắc Giang cũ)
    /// </summary>
    public async Task<List<DvhcCapXa>> GetCapXaListByTinhAsync(int tinhId = 24)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();
            var result = await dbContext.DvhcCapXa
                .Where(x => x.CapTinhId == tinhId)
                .OrderBy(x => x.Name)
                .ToListAsync();

            _logger.Debug("Đã lấy {Count} xã/phường cho tỉnh {TinhId}", result.Count, tinhId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lấy danh sách xã/phường cho tỉnh {TinhId} thất bại", tinhId);
            return [];
        }
    }

    /// <summary>
    /// Lấy danh sách xã theo huyện từ database
    /// Mặc định tỉnh Id = 24 (Tỉnh Bắc Ninh mới - Tỉnh Bắc Giang cũ)
    /// huyệnId = 0 để lấy tất cả xã của tỉnh
    /// </summary>
    public async Task<List<DvhcCapXa>> GetCapXaListByHuyenAsync(int tinhId = 24, int huyenId = 0)
    {
        try
        {
            var dbContext = _databaseService.GetDbContext();
            var result = await dbContext.DvhcCapXa
                .Where(x => x.CapTinhId == tinhId && (huyenId == 0 || x.CapHuyenId == huyenId))
                .OrderBy(x => x.Name)
                .ToListAsync();

            _logger.Debug("Đã lấy {Count} xã theo huyện {HuyenId}", result.Count, huyenId);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Lấy danh sách xã theo huyện {HuyenId} thất bại", huyenId);
            return [];
        }
    }

    #endregion
}
