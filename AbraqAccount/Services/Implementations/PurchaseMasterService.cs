using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Implementations;

public class PurchaseMasterService : IPurchaseMasterService
{
    private readonly AppDbContext _context;

    public PurchaseMasterService(AppDbContext context)
    {
        _context = context;
    }

    // --- Purchase Item Group ---

    public async Task<List<PurchaseItemGroup>> GetPurchaseItemGroupsAsync(string? searchTerm)
    {
        var query = _context.PurchaseItemGroups.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(p => 
                p.Name.Contains(searchTerm) || 
                p.Code.Contains(searchTerm));
        }

        return await query.OrderBy(p => p.Code).ToListAsync();
    }

    public async Task<(bool success, string message)> CreatePurchaseItemGroupAsync(PurchaseItemGroup model, string username)
    {
        model.Code = await GenerateGroupCodeAsync();
        model.CreatedAt = DateTime.Now;
        model.IsActive = true;

        _context.Add(model);
        await _context.SaveChangesAsync();

        await LogGroupHistoryAsync(model.Id, "Insert", "Created", username);
        return (true, "Purchase Item Group created successfully!");
    }

    public async Task<PurchaseItemGroup?> GetPurchaseItemGroupByIdAsync(int id)
    {
        return await _context.PurchaseItemGroups.FindAsync(id);
    }

    public async Task<(bool success, string message)> UpdatePurchaseItemGroupAsync(int id, PurchaseItemGroup model, string username)
    {
        try
        {
            var existing = await _context.PurchaseItemGroups.FindAsync(id);
            if (existing == null) return (false, "Not found");

            // Preserve Code and CreatedAt
            model.Code = existing.Code;
            model.CreatedAt = existing.CreatedAt;

            _context.Entry(existing).CurrentValues.SetValues(model);
            await _context.SaveChangesAsync();

            await LogGroupHistoryAsync(model.Id, "Edit", "Edited", username);
            return (true, "Purchase Item Group updated successfully!");
        }
        catch (DbUpdateConcurrencyException)
        {
             return (false, "Concurrency error");
        }
    }

    public async Task<(bool success, string message)> DeletePurchaseItemGroupAsync(int id)
    {
        var purchaseItemGroup = await _context.PurchaseItemGroups.FindAsync(id);
        if (purchaseItemGroup != null)
        {
            _context.PurchaseItemGroups.Remove(purchaseItemGroup);
            await _context.SaveChangesAsync();
            return (true, "Purchase Item Group deleted successfully!");
        }
        return (false, "Not found");
    }
    
    public async Task<IEnumerable<object>> GetPurchaseItemGroupHistoryAsync(int id)
    {
        var histories = await _context.PurchaseItemGroupHistories
            .Where(h => h.PurchaseItemGroupId == id)
            .OrderByDescending(h => h.ActionDate)
            .ToListAsync();

        return histories.Select(h => new
        {
            action = h.Action,
            user = h.User,
            dateTime = h.ActionDate.ToString("dd/MM/yyyy HH:mm:ss"),
            remarks = h.Remarks ?? (h.Action == "Insert" ? "Created" : h.Action == "Edit" ? "Edited" : "Deleted")
        });
    }

    private async Task<string> GenerateGroupCodeAsync()
    {
        var lastGroup = await _context.PurchaseItemGroups
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        if (lastGroup == null) return "0001";

        var lastCode = lastGroup.Code;
        if (string.IsNullOrEmpty(lastCode) || !int.TryParse(lastCode, out int lastNumber)) return "0001";

        return $"{(lastNumber + 1):D4}";
    }

    private async Task LogGroupHistoryAsync(int purchaseItemGroupId, string action, string remarks, string username)
    {
        var history = new PurchaseItemGroupHistory
        {
            PurchaseItemGroupId = purchaseItemGroupId,
            Action = action,
            User = username,
            ActionDate = DateTime.Now,
            Remarks = remarks
        };
        _context.PurchaseItemGroupHistories.Add(history);
        await _context.SaveChangesAsync();
    }


    // --- Purchase Item ---

    public async Task<List<PurchaseItem>> GetPurchaseItemsAsync(string? searchTerm)
    {
        var query = _context.PurchaseItems
            .Include(p => p.PurchaseItemGroup)
            .Include(p => p.Vendor)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(p => 
                p.ItemName.Contains(searchTerm) || 
                p.Code.Contains(searchTerm) ||
                (p.PurchaseItemGroup != null && p.PurchaseItemGroup.Name.Contains(searchTerm)) ||
                (p.Vendor != null && p.Vendor.AccountName.Contains(searchTerm)) ||
                (p.BillingName != null && p.BillingName.Contains(searchTerm)));
        }

        return await query.OrderByDescending(p => p.Id).ToListAsync();
    }

    public async Task<(bool success, string message)> CreatePurchaseItemAsync(PurchaseItem model, string username)
    {
        model.Code = await GenerateItemCodeAsync();
        model.CreatedAt = DateTime.Now;
        model.IsActive = true;

        _context.Add(model);
        await _context.SaveChangesAsync();

        await LogItemHistoryAsync(model.Id, "Insert", "Created", username);
        return (true, "Purchase Item created successfully!");
    }

    public async Task<PurchaseItem?> GetPurchaseItemByIdAsync(int id)
    {
        return await _context.PurchaseItems
            .Include(p => p.PurchaseItemGroup)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<(bool success, string message)> UpdatePurchaseItemAsync(int id, PurchaseItem model, string username)
    {
        try
        {
            var existing = await _context.PurchaseItems.FindAsync(id);
            if (existing == null) return (false, "Not found");

            model.Code = existing.Code;
            model.CreatedAt = existing.CreatedAt;

            _context.Entry(existing).CurrentValues.SetValues(model);
            await _context.SaveChangesAsync();

            await LogItemHistoryAsync(model.Id, "Edit", "Edited", username);
            return (true, "Purchase Item updated successfully!");
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "Concurrency error");
        }
    }

    public async Task<(bool success, string message)> DeletePurchaseItemAsync(int id)
    {
        var purchaseItem = await _context.PurchaseItems.FindAsync(id);
        if (purchaseItem != null)
        {
            _context.PurchaseItems.Remove(purchaseItem);
            await _context.SaveChangesAsync();
            return (true, "Purchase Item deleted successfully!");
        }
        return (false, "Not found");
    }

    public async Task<IEnumerable<object>> GetPurchaseItemHistoryAsync(int id)
    {
        try
        {
            var histories = await _context.PurchaseItemHistories
                .Where(h => h.PurchaseItemId == id)
                .OrderByDescending(h => h.ActionDate)
                .ToListAsync();

            return histories.Select(h => new
            {
                action = h.Action,
                user = h.User,
                dateTime = h.ActionDate.ToString("dd/MM/yyyy HH:mm:ss"),
                remarks = h.Remarks ?? (h.Action == "Insert" ? "Created" : h.Action == "Edit" ? "Edited" : "Deleted")
            });
        }
        catch { return new object[] { }; }
    }

    private async Task<string> GenerateItemCodeAsync()
    {
        var lastItem = await _context.PurchaseItems
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync();

        if (lastItem == null) return "1001";

        var lastCode = lastItem.Code;
        if (string.IsNullOrEmpty(lastCode) || !int.TryParse(lastCode, out int lastNumber)) return "1001";

        return $"{(lastNumber + 1)}";
    }

    private async Task LogItemHistoryAsync(int purchaseItemId, string action, string remarks, string username)
    {
        try
        {
            var history = new PurchaseItemHistory
            {
                PurchaseItemId = purchaseItemId,
                Action = action,
                User = username,
                ActionDate = DateTime.Now,
                Remarks = remarks
            };
            _context.PurchaseItemHistories.Add(history);
            await _context.SaveChangesAsync();
        }
        catch { }
    }

    public async Task<List<GrowerGroup>> GetGrowerGroupsAsync()
    {
        return await _context.GrowerGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetFarmersByGroupAsync(int groupId)
    {
        try
        {
            return await _context.Farmers
                .Where(f => f.GroupId == groupId && f.IsActive)
                .OrderBy(f => f.FarmerName)
                .Select(f => new { id = f.Id, name = f.FarmerName })
                .ToListAsync();
        }
        catch { return new object[] { }; }
    }

    public async Task<(bool success, string message)> SaveSpecialRateAsync(SaveSpecialRateRequest request)
    {
        try
        {
            if (request == null) return (false, "Request data is null.");
            if (request.PurchaseItemId <= 0) return (false, "Invalid Purchase Item ID.");
            if (request.EffectiveFrom == default(DateTime)) return (false, "Effective From date is required.");
            if (!request.GrowerGroupId.HasValue && !request.FarmerId.HasValue) return (false, "Either Grower Group or Grower Name must be selected.");

            var specialRate = new SpecialRate
            {
                PurchaseItemId = request.PurchaseItemId,
                GrowerGroupId = request.GrowerGroupId.HasValue && request.GrowerGroupId.Value > 0 ? request.GrowerGroupId.Value : null,
                FarmerId = request.FarmerId.HasValue && request.FarmerId.Value > 0 ? request.FarmerId.Value : null,
                EffectiveFrom = request.EffectiveFrom,
                LabourCost = request.LabourCost,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.SpecialRates.Add(specialRate);
            await _context.SaveChangesAsync();
            return (true, "Special Rate saved successfully!");
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public async Task LoadPurchaseItemDropdownsAsync(dynamic viewBag)
    {
        var itemGroups = await _context.PurchaseItemGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.Name)
            .ToListAsync();

        viewBag.PurchaseItemGroupId = new SelectList(itemGroups, "Id", "Name");
        
        var uoms = await _context.UOMs
            .Where(u => u.IsActive && u.IsApproved)
            .OrderBy(u => u.UOMName)
            .ToListAsync();

        viewBag.UOMOptions = uoms.Select(u => new SelectListItem 
        { 
            Value = u.UOMName, 
            Text = u.UOMName 
        }).ToList();

        var vendors = await _context.BankMasters
            .Include(b => b.Group)
                .ThenInclude(g => g.MasterGroup)
            .Include(b => b.Group)
                .ThenInclude(g => g.MasterSubGroup)
            .Where(b => b.IsActive && b.Group != null && (
                b.Group.Name.Contains("Vendor") || 
                b.Group.Name.Contains("Vender") || 
                b.Group.Name.Contains("Creditor") || 
                b.Group.Name.Contains("Supplier") || 
                b.Group.Name.Contains("Sundry") ||
                (b.Group.MasterSubGroup != null && (b.Group.MasterSubGroup.Name.Contains("Vendor") || b.Group.MasterSubGroup.Name.Contains("Vender") || b.Group.MasterSubGroup.Name.Contains("Creditor"))) ||
                (b.Group.MasterGroup != null && (b.Group.MasterGroup.Name.Contains("Vendor") || b.Group.MasterGroup.Name.Contains("Vender") || b.Group.MasterGroup.Name.Contains("Creditor")))
            ))
            .OrderBy(b => b.AccountName)
            .ToListAsync();

        viewBag.VendorOptions = vendors.Select(v => new SelectListItem
        {
            Value = v.Id.ToString(),
            Text = v.AccountName
        }).ToList();

        viewBag.GSTOptions = new List<SelectListItem>
        {
            new SelectListItem { Value = "NA", Text = "NA" },
            new SelectListItem { Value = "0", Text = "0%" },
            new SelectListItem { Value = "5", Text = "5%" },
            new SelectListItem { Value = "12", Text = "12%" },
            new SelectListItem { Value = "18", Text = "18%" },
            new SelectListItem { Value = "28", Text = "28%" }
        };
    }

    // --- Purchase Order TCs ---

    public async Task<(List<PurchaseOrderTC> tcs, int totalCount, int totalPages)> GetPurchaseOrderTCsAsync(string? searchTerm, int page, int pageSize)
    {
        var query = _context.PurchaseOrderTCs.AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(p => 
                p.Caption.Contains(searchTerm) ||
                p.TCType.Contains(searchTerm) ||
                p.TermsAndConditions.Contains(searchTerm));
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var purchaseOrderTCs = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (purchaseOrderTCs, totalCount, totalPages);
    }

    public async Task<(bool success, string message)> CreatePurchaseOrderTCAsync(PurchaseOrderTC model, string username)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            _context.PurchaseOrderTCs.Add(model);
            await _context.SaveChangesAsync();
            await LogTCHistoryAsync(model.Id, "Create", "Created", username);
            return (true, "Purchase Order Terms & Conditions created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<PurchaseOrderTC?> GetPurchaseOrderTCByIdAsync(int id)
    {
        return await _context.PurchaseOrderTCs.FindAsync(id);
    }

    public async Task<(bool success, string message)> UpdatePurchaseOrderTCAsync(int id, PurchaseOrderTC model, string username)
    {
        try
        {
            var existing = await _context.PurchaseOrderTCs.FindAsync(id);
            if (existing == null) return (false, "Not found");

            existing.TCType = model.TCType;
            existing.Caption = model.Caption;
            existing.TermsAndConditions = model.TermsAndConditions;
            existing.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            await LogTCHistoryAsync(id, "Edit", "Edited", username);
            return (true, "Purchase Order Terms & Conditions updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<IEnumerable<object>> GetPurchaseOrderTCHistoryAsync(int id)
    {
        try
        {
            return await _context.PurchaseOrderTCHistories
                .Where(h => h.PurchaseOrderTCId == id)
                .OrderByDescending(h => h.ActionDate)
                .Select(h => new
                {
                    action = h.Action,
                    user = h.User,
                    dateTime = h.ActionDate.ToString("dd/MM/yyyy HH:mm:ss"),
                    remarks = h.Remarks
                })
                .ToListAsync();
        }
        catch { return new object[] { }; }
    }

    private async Task LogTCHistoryAsync(int purchaseOrderTCId, string action, string remarks, string username)
    {
        var history = new PurchaseOrderTCHistory
        {
            PurchaseOrderTCId = purchaseOrderTCId,
            Action = action,
            User = username,
            ActionDate = DateTime.Now,
            Remarks = remarks
        };
        _context.PurchaseOrderTCHistories.Add(history);
        await _context.SaveChangesAsync();
    }
}

