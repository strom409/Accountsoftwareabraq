using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;

namespace AbraqAccount.Services.Implementations;

public class VendorService : IVendorService
{
    private readonly AppDbContext _context;

    public VendorService(AppDbContext context)
    {
        _context = context;
    }

    #region Retrieval
    public async Task<IEnumerable<Vendor>> GetAllActiveVendorsAsync()
    {
        try
        {
            return await _context.Vendors
                .Where(v => v.IsActive)
                .OrderBy(v => v.VendorName)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Vendor?> GetVendorByIdAsync(int id)
    {
        try
        {
            return await _context.Vendors.FindAsync(id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<object>> SearchVendorsAsync(string? searchTerm)
    {
        try
        {
            var query = _context.Vendors.Where(v => v.IsActive).AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm) && searchTerm.Trim().Length > 0)
            {
                query = query.Where(v => v.VendorName.Contains(searchTerm.Trim()) || 
                                         v.VendorCode.Contains(searchTerm.Trim()));
            }

            return await query
                .OrderBy(v => v.VendorName)
                .Select(v => new { id = v.Id, name = v.VendorName, code = v.VendorCode })
                .Take(100)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Management
    public async Task<Vendor> CreateVendorAsync(Vendor vendor)
    {
        try
        {
            vendor.VendorCode = await GenerateVendorCodeAsync();
            vendor.CreatedAt = DateTime.Now;
            vendor.IsActive = true;

            _context.Add(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<Vendor> UpdateVendorAsync(Vendor vendor)
    {
        try
        {
            _context.Update(vendor);
            await _context.SaveChangesAsync();
            return vendor;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> DeleteVendorAsync(int id)
    {
        try
        {
            var vendor = await _context.Vendors.FindAsync(id);
            if (vendor != null)
            {
                vendor.IsActive = false;
                _context.Update(vendor);
                await _context.SaveChangesAsync();
                return true;
            }
            return false;
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Helpers
    public async Task<bool> VendorExistsAsync(int id)
    {
        try
        {
            return await _context.Vendors.AnyAsync(e => e.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<string> GenerateVendorCodeAsync()
    {
        try
        {
            var lastVendor = await _context.Vendors
                .OrderByDescending(v => v.Id)
                .FirstOrDefaultAsync();

            if (lastVendor == null)
            {
                return "V001";
            }

            var lastCode = lastVendor.VendorCode;
            if (string.IsNullOrEmpty(lastCode) || !lastCode.StartsWith("V"))
            {
                return "V001";
            }

            if (int.TryParse(lastCode.Substring(1), out int lastNumber))
            {
                return $"V{(lastNumber + 1):D3}";
            }

            return "V001";
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

