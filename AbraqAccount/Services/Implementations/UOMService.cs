using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AbraqAccount.Services.Implementations;

public class UOMService : IUOMService
{
    private readonly AppDbContext _context;

    public UOMService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<UOM>> GetUOMsAsync(string? searchTerm)
    {
        IQueryable<UOM> query = _context.UOMs;

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(u => u.UOMName.Contains(searchTerm) || u.UOMCode.Contains(searchTerm));
        }

        return await query.OrderByDescending(u => u.CreatedAt).ToListAsync();
    }

    public async Task<UOM?> GetUOMByIdAsync(int id)
    {
        return await _context.UOMs.FindAsync(id);
    }

    public async Task<bool> CreateUOMAsync(UOM uom, string username)
    {
        try
        {
            uom.CreatedAt = DateTime.Now;
            _context.UOMs.Add(uom);
            await _context.SaveChangesAsync();

            _context.UOMHistories.Add(new UOMHistory
            {
                UOMId = uom.Id,
                Action = "Create",
                User = username,
                Remarks = $"Created UOM: {uom.UOMName}"
            });
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> UpdateUOMAsync(UOM uom, string username)
    {
        try
        {
            _context.UOMs.Update(uom);
            await _context.SaveChangesAsync();

            _context.UOMHistories.Add(new UOMHistory
            {
                UOMId = uom.Id,
                Action = "Update",
                User = username,
                Remarks = $"Updated UOM: {uom.UOMName}"
            });
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> ApproveUOMAsync(int id, string username)
    {
        try
        {
            var uom = await _context.UOMs.FindAsync(id);
            if (uom == null) return false;

            uom.IsApproved = true;
            await _context.SaveChangesAsync();

            _context.UOMHistories.Add(new UOMHistory
            {
                UOMId = uom.Id,
                Action = "Approve",
                User = username,
                Remarks = $"Approved UOM: {uom.UOMName}"
            });
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> DeleteUOMAsync(int id)
    {
        try
        {
            var uom = await _context.UOMs.FindAsync(id);
            if (uom == null) return false;

            _context.UOMs.Remove(uom);
            await _context.SaveChangesAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<List<UOMHistory>> GetUOMHistoryAsync(int uomId)
    {
        return await _context.UOMHistories
            .Where(h => h.UOMId == uomId)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync();
    }
}
