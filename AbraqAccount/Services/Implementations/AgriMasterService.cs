using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Implementations;

public class AgriMasterService : IAgriMasterService
{
    private readonly AppDbContext _context;

    public AgriMasterService(AppDbContext context)
    {
        _context = context;
    }

    #region Grower Group

    public async Task<List<GrowerGroup>> GetGrowerGroupsAsync()
    {
        try
        {
            return await _context.GrowerGroups
                .Where(g => g.IsActive)
                .OrderBy(g => g.GroupName)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateGrowerGroupAsync(GrowerGroup model)
    {
        try
        {
            model.GroupCode = await GenerateGroupCodeAsync();
            model.CreatedAt = DateTime.Now;
            model.IsActive = true;

            _context.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Grower Group created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<GrowerGroup?> GetGrowerGroupByIdAsync(int id)
    {
        try
        {
            return await _context.GrowerGroups.FindAsync(id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateGrowerGroupAsync(int id, GrowerGroup model)
    {
        try
        {
            var existing = await _context.GrowerGroups.FindAsync(id);
            if (existing == null) return (false, "Not found");

            existing.GroupName = model.GroupName;
            existing.GroupCode = model.GroupCode;
            // Add other fields updates as necessary... keeping safe with explicit mapping or Attach
            // Logic is basically direct update but we should be careful not to overwrite Id, CreatedAt if handled by EF.
            // Using logic from controller:
             _context.Entry(existing).CurrentValues.SetValues(model);
            
            await _context.SaveChangesAsync();
            return (true, "Grower Group updated successfully!");
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> DeleteGrowerGroupAsync(int id)
    {
        try
        {
            var group = await _context.GrowerGroups.FindAsync(id);
            if (group != null)
            {
                group.IsActive = false;
                _context.Update(group);
                await _context.SaveChangesAsync();
                return (true, "Deleted successfully");
            }
            return (false, "Not found");
        }
        catch (Exception ex) 
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<string> GenerateGroupCodeAsync()
    {
        try
        {
            var lastGroup = await _context.GrowerGroups
                .OrderByDescending(g => g.Id)
                .FirstOrDefaultAsync();

            if (lastGroup == null) return "GG001";

            var lastCode = lastGroup.GroupCode;
            if (string.IsNullOrEmpty(lastCode) || !lastCode.StartsWith("GG")) return "GG001";

            if (int.TryParse(lastCode.Substring(2), out int lastNumber))
                return $"GG{(lastNumber + 1):D3}";

            return "GG001";
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<List<Farmer>> GetGroupFarmersAsync(int groupId)
    {
         try
         {
             return await _context.Farmers
                .Where(f => f.GroupId == groupId && f.IsActive)
                .ToListAsync();
         }
         catch (Exception)
         {
             throw;
         }
    }

    public async Task<List<Lot>> GetGroupLotsAsync(int groupId)
    {
        try
        {
            return await _context.Lots
                .Include(l => l.GrowerGroup)
                .Include(l => l.Farmer)
                .Where(l => l.GroupId == groupId && l.IsActive)
                .OrderByDescending(l => l.ArrivalDate)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }


    #endregion

    #region Farmer

    public async Task<List<Farmer>> GetAllFarmersAsync()
    {
        try
        {
            return await _context.Farmers
                .Include(f => f.GrowerGroup)
                .Where(f => f.IsActive)
                .OrderBy(f => f.FarmerName)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateFarmerAsync(Farmer model)
    {
        try
        {
            model.FarmerCode = await GenerateFarmerCodeAsync(model.GroupId);
            model.CreatedAt = DateTime.Now;
            model.IsActive = true;

            _context.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Farmer created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<Farmer?> GetFarmerByIdAsync(int id)
    {
        try
        {
            return await _context.Farmers
                .Include(f => f.GrowerGroup)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateFarmerAsync(int id, Farmer model)
    {
        try
        {
            var existing = await _context.Farmers.FindAsync(id);
            if (existing == null) return (false, "Not found");
            
            // Retain created logic
            model.CreatedAt = existing.CreatedAt;
            model.FarmerCode = existing.FarmerCode;
            
             _context.Entry(existing).CurrentValues.SetValues(model);
            await _context.SaveChangesAsync();
            return (true, "Farmer updated successfully!");
        }
        catch (Exception ex) { return (false, "Error: " + ex.Message); }
    }

    public async Task<(bool success, string message)> DeleteFarmerAsync(int id)
    {
        try
        {
            var farmer = await _context.Farmers.FindAsync(id);
            if (farmer != null)
            {
                farmer.IsActive = false;
                _context.Update(farmer);
                await _context.SaveChangesAsync();
                return (true, "Deleted successfully");
            }
            return (false, "Not found");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<string> GenerateFarmerCodeAsync(int groupId)
    {
        try
        {
            var lastFarmer = await _context.Farmers
                .Where(f => f.GroupId == groupId)
                .OrderByDescending(f => f.Id)
                .FirstOrDefaultAsync();

            var group = await _context.GrowerGroups.FindAsync(groupId);
            var groupCode = group?.GroupCode ?? "GG";

            if (lastFarmer == null) return $"{groupCode}F001";

            var lastCode = lastFarmer.FarmerCode;
            if (string.IsNullOrEmpty(lastCode) || !lastCode.StartsWith(groupCode)) return $"{groupCode}F001";

            var suffix = lastCode.Substring(groupCode.Length);
            if (suffix.StartsWith("F") && int.TryParse(suffix.Substring(1), out int lastNumber))
            {
                return $"{groupCode}F{(lastNumber + 1):D3}";
            }

            return $"{groupCode}F001";
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<string> GetGroupNameAsync(int groupId)
    {
        try
        {
            var g = await _context.GrowerGroups.FindAsync(groupId);
            return g?.GroupName ?? "Unknown";
        }
        catch (Exception)
        {
            throw;
        }
    }

    #endregion

    #region Lot

    public async Task<(bool success, string message)> CreateLotAsync(Lot model)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            model.IsActive = true;
            _context.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Lot created successfully!");
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }

    public async Task<Lot?> GetLotByIdAsync(int id)
    {
        try
        {
            return await _context.Lots
                .Include(l => l.GrowerGroup)
                .Include(l => l.Farmer)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateLotAsync(int id, Lot model)
    {
        try
        {
            var existing = await _context.Lots.FindAsync(id);
            if (existing == null) return (false, "Not found");

            _context.Entry(existing).CurrentValues.SetValues(model);
             await _context.SaveChangesAsync();
            return (true, "Lot updated successfully!");
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> DeleteLotAsync(int id)
    {
        try
        {
            var lot = await _context.Lots.FindAsync(id);
            if (lot != null)
            {
                lot.IsActive = false;
                _context.Update(lot);
                await _context.SaveChangesAsync();
                return (true, "Deleted successfully");
            }
             return (false, "Not found");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task LoadLotDropdownsAsync(dynamic viewBag, int? groupId, int? farmerId)
    {
        try
        {
            if (groupId.HasValue)
            {
                viewBag.Farmers = new SelectList(
                    await _context.Farmers
                        .Where(f => f.GroupId == groupId.Value && f.IsActive)
                        .OrderBy(f => f.FarmerName)
                        .ToListAsync(),
                    "Id", "FarmerName", farmerId
                );
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

