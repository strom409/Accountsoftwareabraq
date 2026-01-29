using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using AbraqAccount.Models.Common;
using AbraqAccount.Extensions;
using Microsoft.AspNetCore.Http;

namespace AbraqAccount.Services.Implementations;

public class ExpensesIncurredService : IExpensesIncurredService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExpensesIncurredService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    private string GetCurrentUsername()
    {
        try
        {
            var session = _httpContextAccessor.HttpContext?.Session;
            if (session == null) return "Admin";
            var userSession = session.GetObject<UserSession>(SessionKeys.UserSession);
            return userSession?.Username ?? "Admin";
        }
        catch
        {
            return "Admin";
        }
    }

    public async Task<(IEnumerable<ExpensesIncurred> expenses, int totalCount, int totalPages)> GetExpensesAsync(
        string? voucherNo,
        DateTime? fromDate,
        DateTime? toDate,
        string? expenseGroup,
        string? expenseSubGroup,
        string? expenseLedger,
        string? vendorName,
        string? status,
        int page,
        int pageSize)
    {
        var query = _context.ExpensesIncurreds.AsQueryable();

        if (!string.IsNullOrEmpty(voucherNo))
        {
            query = query.Where(e => e.VoucherNo.Contains(voucherNo));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.ExpenseDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.ExpenseDate <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(vendorName))
        {
            query = query.Where(e => e.VendorName != null && e.VendorName.Contains(vendorName));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(e => e.Status == status);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var expenses = await query
            .Include(e => e.Items)
            .Include(e => e.MiscCharges)
            .OrderByDescending(e => e.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load navigation properties manually for display
        foreach (var expense in expenses)
        {
            await LoadAccountNamesAsync(expense);
        }

        return (expenses, totalCount, totalPages);
    }

    public async Task<ExpensesIncurred?> GetExpenseByIdAsync(int id, bool includeItems = false, bool includeMiscCharges = false)
    {
        var query = _context.ExpensesIncurreds.AsQueryable();

        if (includeItems)
        {
            query = query
                .Include(e => e.Items)
                    .ThenInclude(i => i.ItemGroup)
                .Include(e => e.Items)
                    .ThenInclude(i => i.Item);
        }

        if (includeMiscCharges)
        {
            query = query.Include(e => e.MiscCharges);
        }

        var expense = await query.FirstOrDefaultAsync(m => m.Id == id);
        
        if (expense != null)
        {
            await LoadAccountNamesAsync(expense);
        }

        return expense;
    }

    public async Task<ExpensesIncurred> CreateExpenseAsync(ExpensesIncurred expensesIncurred)
    {
        // Generate Voucher Number
        var lastExpense = await _context.ExpensesIncurreds
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();
        
        int nextNumber = 1;
        if (lastExpense != null && !string.IsNullOrEmpty(lastExpense.VoucherNo))
        {
            var numberPart = lastExpense.VoucherNo.Replace("EXP", "");
            if (int.TryParse(numberPart, out int lastNum))
            {
                nextNumber = lastNum + 1;
            }
        }
        // Populate Group, SubGroup and DebitAccountName from Ledger if not provided
        if (expensesIncurred.ExpenseLedgerId.HasValue)
        {
            var ledger = await _context.SubGroupLedgers
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == expensesIncurred.ExpenseLedgerId.Value);
            
            if (ledger != null)
            {
                expensesIncurred.ExpenseGroupId = ledger.MasterGroupId;
                expensesIncurred.ExpenseSubGroupId = ledger.MasterSubGroupId;
            }
        }

        if (expensesIncurred.DebitAccountId.HasValue && (string.IsNullOrEmpty(expensesIncurred.DebitAccountName) || expensesIncurred.DebitAccountName == "Venders"))
        {
            var debitLedger = await _context.SubGroupLedgers
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == expensesIncurred.DebitAccountId.Value);
            if (debitLedger != null)
            {
                expensesIncurred.DebitAccountName = debitLedger.Name;
            }
        }

        if (expensesIncurred.VendorId.HasValue && (string.IsNullOrEmpty(expensesIncurred.VendorName) || expensesIncurred.VendorName == "Venders"))
        {
            var ledger = await _context.SubGroupLedgers
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == expensesIncurred.VendorId.Value);
            if (ledger != null)
            {
                expensesIncurred.VendorName = ledger.Name;
            }
            else
            {
                var farmer = await _context.Farmers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(f => f.Id == expensesIncurred.VendorId.Value);
                if (farmer != null)
                {
                    expensesIncurred.VendorName = farmer.FarmerName;
                }
            }
        }

        expensesIncurred.VoucherNo = $"EXP{nextNumber:D6}";
        expensesIncurred.CreatedAt = DateTime.Now;
        expensesIncurred.CreatedBy = GetCurrentUsername();
        expensesIncurred.Status = expensesIncurred.Status ?? "Unapproved";
        expensesIncurred.IsActive = true;

        _context.ExpensesIncurreds.Add(expensesIncurred);
        await _context.SaveChangesAsync();

        return expensesIncurred;
    }

    public async Task<ExpensesIncurred> UpdateExpenseAsync(int id, ExpensesIncurred expensesIncurred)
    {
        var existingExpense = await _context.ExpensesIncurreds
            .Include(e => e.Items)
            .Include(e => e.MiscCharges)
            .FirstOrDefaultAsync(e => e.Id == id);

        if (existingExpense == null)
        {
            throw new InvalidOperationException("Expense not found");
        }

        // Update basic properties
        existingExpense.ExpenseDate = expensesIncurred.ExpenseDate;
        existingExpense.VendorId = expensesIncurred.VendorId;
        existingExpense.VendorName = expensesIncurred.VendorName;
        existingExpense.POType = expensesIncurred.POType;
        existingExpense.PANNo = expensesIncurred.PANNo;
        existingExpense.FirmType = expensesIncurred.FirmType;
        existingExpense.BillDate = expensesIncurred.BillDate;
        existingExpense.BillNo = expensesIncurred.BillNo;
        existingExpense.VehicleNo = expensesIncurred.VehicleNo;
        existingExpense.Remarks = expensesIncurred.Remarks;
        existingExpense.TotalDiscount = expensesIncurred.TotalDiscount;
        existingExpense.ExpenseGroupId = expensesIncurred.ExpenseGroupId;
        existingExpense.ExpenseSubGroupId = expensesIncurred.ExpenseSubGroupId;
        existingExpense.ExpenseLedgerId = expensesIncurred.ExpenseLedgerId;

        // Populate Group and SubGroup from Ledger if changed or missing
        if (expensesIncurred.ExpenseLedgerId.HasValue)
        {
            var ledger = await _context.SubGroupLedgers
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.Id == expensesIncurred.ExpenseLedgerId.Value);
            
            if (ledger != null)
            {
                existingExpense.ExpenseGroupId = ledger.MasterGroupId;
                existingExpense.ExpenseSubGroupId = ledger.MasterSubGroupId;
            }
        }
        else
        {
            existingExpense.ExpenseGroupId = expensesIncurred.ExpenseGroupId;
            existingExpense.ExpenseSubGroupId = expensesIncurred.ExpenseSubGroupId;
        }

        existingExpense.DebitAccountId = expensesIncurred.DebitAccountId;
        existingExpense.UpdatedAt = DateTime.Now;
        existingExpense.UpdatedBy = GetCurrentUsername();

        // Populate DebitAccountName from Ledger
        if (expensesIncurred.DebitAccountId.HasValue)
        {
            if (string.IsNullOrEmpty(expensesIncurred.DebitAccountName) || expensesIncurred.DebitAccountName == "Venders")
            {
                var debitLedger = await _context.SubGroupLedgers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == expensesIncurred.DebitAccountId.Value);
                if (debitLedger != null)
                {
                    existingExpense.DebitAccountName = debitLedger.Name;
                }
                else 
                {
                    existingExpense.DebitAccountName = expensesIncurred.DebitAccountName;
                }
            }
            else
            {
                existingExpense.DebitAccountName = expensesIncurred.DebitAccountName;
            }
        }
        else
        {
            existingExpense.DebitAccountName = expensesIncurred.DebitAccountName;
        }

        existingExpense.VendorId = expensesIncurred.VendorId;
        
        // Populate VendorName from ID
        if (expensesIncurred.VendorId.HasValue)
        {
            if (string.IsNullOrEmpty(expensesIncurred.VendorName) || expensesIncurred.VendorName == "Venders")
            {
                var ledger = await _context.SubGroupLedgers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(l => l.Id == expensesIncurred.VendorId.Value);
                if (ledger != null)
                {
                    existingExpense.VendorName = ledger.Name;
                }
                else
                {
                    var farmer = await _context.Farmers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(f => f.Id == expensesIncurred.VendorId.Value);
                    if (farmer != null)
                    {
                        existingExpense.VendorName = farmer.FarmerName;
                    }
                    else
                    {
                        existingExpense.VendorName = expensesIncurred.VendorName;
                    }
                }
            }
            else
            {
                existingExpense.VendorName = expensesIncurred.VendorName;
            }
        }
        else
        {
            existingExpense.VendorName = expensesIncurred.VendorName;
        }

        existingExpense.Unit = expensesIncurred.Unit;
        existingExpense.Amount = expensesIncurred.Amount;
        existingExpense.Status = expensesIncurred.Status;

        // Update Items
        _context.ExpenseItems.RemoveRange(existingExpense.Items);
        existingExpense.Items = expensesIncurred.Items;

        // Update MiscCharges
        _context.ExpenseMiscCharges.RemoveRange(existingExpense.MiscCharges);
        existingExpense.MiscCharges = expensesIncurred.MiscCharges;

        _context.Update(existingExpense);
        await _context.SaveChangesAsync();

        return existingExpense;
    }

    public async Task<bool> DeleteExpenseAsync(int id)
    {
        var expense = await _context.ExpensesIncurreds.FindAsync(id);
        if (expense == null)
        {
            return false;
        }

        var currentUser = GetCurrentUsername();
        expense.IsActive = false;
        expense.UpdatedAt = DateTime.Now;
        expense.UpdatedBy = currentUser;
        _context.Update(expense); // Use Update instead of Remove for soft delete
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> ApproveExpenseAsync(int id)
    {
        var expense = await _context.ExpensesIncurreds.FindAsync(id);
        if (expense == null)
        {
            return false;
        }

        var currentUser = GetCurrentUsername();
        expense.Status = "Approved";
        expense.UpdatedAt = DateTime.Now;
        expense.UpdatedBy = currentUser;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<IEnumerable<LookupItem>> GetExpenseGroupsAsync(string? searchTerm)
    {
        var query = _context.MasterGroups.AsQueryable();
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(mg => mg.Name.Contains(searchTerm));
        }
        
        return await query
            .OrderBy(mg => mg.Name)
            .Take(100)
            .Select(mg => new LookupItem { Id = mg.Id, Name = mg.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetExpenseSubGroupsAsync(int? groupId, string? searchTerm)
    {
        var query = _context.MasterSubGroups
            .Include(msg => msg.MasterGroup)
            .Where(msg => msg.IsActive)
            .AsQueryable();
        
        if (groupId.HasValue)
        {
            query = query.Where(msg => msg.MasterGroupId == groupId.Value);
        }
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(msg => 
                msg.Name.Contains(searchTerm) ||
                (msg.MasterGroup != null && msg.MasterGroup.Name.Contains(searchTerm)));
        }
        
        return await query
            .OrderBy(msg => msg.Name)
            .Take(50)
            .Select(msg => new LookupItem { 
                Id = msg.Id, 
                Name = msg.MasterGroup != null ? $"{msg.MasterGroup.Name} - {msg.Name}" : msg.Name 
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, string? type = null)
    {
        var profile = await _context.EntryForAccounts
            .Where(e => e.TransactionType == "ExpenseEntry" && 
                       (e.AccountName == "null" || string.IsNullOrEmpty(e.AccountName)))
            .OrderBy(e => e.Id)
            .FirstOrDefaultAsync();
        int? profileId = profile?.Id;

        var rules = await _context.AccountRules
            .Where(r => r.RuleType == "AllowedNature")
            .ToListAsync();

        bool profileHasRules = profileId.HasValue && rules.Any(r => r.EntryAccountId == profileId.Value);

        if (!profileHasRules)
        {
             return new List<LookupItem> { new LookupItem { Id = 0, Name = "Please select transaction rule first", Type = "Message" } };
        }

        bool CheckRule(string ruleValue)
        {
            if (string.IsNullOrWhiteSpace(ruleValue)) return false;
            if (ruleValue.Equals("Both", StringComparison.OrdinalIgnoreCase)) return true;
            if (ruleValue.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrEmpty(type)) return true;
            if (ruleValue.Equals("Debit", StringComparison.OrdinalIgnoreCase) && type.Equals("Debit", StringComparison.OrdinalIgnoreCase)) return true;
            if (ruleValue.Equals("Credit", StringComparison.OrdinalIgnoreCase) && type.Equals("Credit", StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        string? GetRuleValue(string accountType, int accountId)
        {
            if (profileId.HasValue)
            {
                var pRule = rules.FirstOrDefault(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == profileId.Value);
                if (pRule != null) return pRule.Value;
            }
            var gRule = rules.FirstOrDefault(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == null);
            if (gRule != null) return gRule.Value;
            return null;
        }

        bool IsAllowed(string accountType, int accountId, string? fallbackGroupType = null, int? fallbackGroupId = null)
        {
            if (string.IsNullOrEmpty(type)) return true;
            string? ruleValue = GetRuleValue(accountType, accountId);
            if (ruleValue != null) return CheckRule(ruleValue);
            if (fallbackGroupType != null && fallbackGroupId.HasValue)
            {
                string? fallbackValue = GetRuleValue(fallbackGroupType, fallbackGroupId.Value);
                if (fallbackValue != null) return CheckRule(fallbackValue);
            }
            return false;
        }

        var bankMastersQuery = _context.BankMasters.Where(bm => bm.IsActive).AsNoTracking();
        var subGroupLedgersQuery = _context.SubGroupLedgers.Where(s => s.IsActive).AsNoTracking();
        var farmersQuery = _context.Farmers.Where(f => f.IsActive).AsNoTracking();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            bankMastersQuery = bankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
            subGroupLedgersQuery = subGroupLedgersQuery.Where(s => s.Name.Contains(searchTerm));
            farmersQuery = farmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
        }

        var bankMasters = await bankMastersQuery.ToListAsync();
        var subGroupLedgers = await subGroupLedgersQuery.ToListAsync();
        var farmers = await farmersQuery.ToListAsync();
        var bankGroupIds = bankMasters.Select(bm => bm.GroupId).Distinct().ToHashSet();
        
        var results = new List<LookupItem>();
        results.AddRange(bankMasters
            .Where(bm => IsAllowed("BankMaster", bm.Id, "SubGroupLedger", bm.GroupId))
            .Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = "BankMaster" }));
        results.AddRange(subGroupLedgers
            .Where(sgl => IsAllowed("SubGroupLedger", sgl.Id) && !bankGroupIds.Contains(sgl.Id))
            .Select(sgl => new LookupItem { Id = sgl.Id, Name = sgl.Name, Type = "SubGroupLedger" }));
        results.AddRange(farmers
            .Where(f => IsAllowed("Farmer", f.Id, "GrowerGroup", f.GroupId))
            .Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = "Farmer" }));

        return results.OrderBy(a => a.Name).Take(100).ToList();
    }

    public async Task<IEnumerable<LookupItem>> GetItemGroupsAsync(string? searchTerm)
    {
        var query = _context.PurchaseItemGroups
            .Where(pig => pig.IsActive)
            .AsQueryable();
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(pig => pig.Name.Contains(searchTerm));
        }
        
        return await query
            .OrderBy(pig => pig.Name)
            .Take(50)
            .Select(pig => new LookupItem { 
                Id = pig.Id, 
                Name = pig.Name 
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetItemNamesAsync(int? itemGroupId, string? searchTerm)
    {
        var query = _context.PurchaseItems
            .Include(pi => pi.PurchaseItemGroup)
            .Where(pi => pi.IsActive)
            .AsQueryable();
        
        if (itemGroupId.HasValue)
        {
            query = query.Where(pi => pi.PurchaseItemGroupId == itemGroupId.Value);
        }
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(pi => pi.ItemName.Contains(searchTerm));
        }
        
        return await query
            .OrderBy(pi => pi.ItemName)
            .Take(50)
            .Select(pi => new LookupItem { 
                Id = pi.Id, 
                Name = pi.ItemName,
                UOM = pi.UOM,
                GroupId = pi.PurchaseItemGroupId
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetExpenseLedgersAsync(int? subGroupId, string? searchTerm)
    {
        var profile = await _context.EntryForAccounts
            .Where(e => e.TransactionType == "ExpenseEntry" && 
                       (e.AccountName == "null" || string.IsNullOrEmpty(e.AccountName)))
            .FirstOrDefaultAsync();
        
        var profileHasRules = profile != null && await _context.AccountRules.AnyAsync(r => r.EntryAccountId == profile.Id && r.RuleType == "AllowedNature");

        if (!profileHasRules)
        {
            return new List<LookupItem> { new LookupItem { Id = 0, Name = "Please select transaction rule first", Type = "Message" } };
        }

        var query = _context.SubGroupLedgers
            .Include(sgl => sgl.MasterGroup)
            .Include(sgl => sgl.MasterSubGroup)
            .Where(sgl => sgl.IsActive)
            .AsQueryable();
        
        if (subGroupId.HasValue)
        {
            query = query.Where(sgl => sgl.MasterSubGroupId == subGroupId.Value);
        }
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(sgl =>
                sgl.Name.Contains(searchTerm) ||
                (sgl.MasterGroup != null && sgl.MasterGroup.Name.Contains(searchTerm)) ||
                (sgl.MasterSubGroup != null && sgl.MasterSubGroup.Name.Contains(searchTerm)));
        }
        
        return await query
            .OrderBy(sgl => sgl.Name)
            .Take(50)
            .Select(sgl => new LookupItem {
                Id = sgl.Id,
                Name = sgl.MasterGroup != null && sgl.MasterSubGroup != null
                    ? $"{sgl.MasterGroup.Name} - {sgl.MasterSubGroup.Name} - {sgl.Name}"
                    : sgl.Name
            })
            .ToListAsync();
    }

    public async Task LoadAccountNamesAsync(ExpensesIncurred expense)
    {
        expense.ExpenseGroup = await _context.MasterGroups.FindAsync(expense.ExpenseGroupId);
        
        expense.ExpenseSubGroup = await _context.MasterSubGroups
            .Include(msg => msg.MasterGroup)
            .FirstOrDefaultAsync(msg => msg.Id == expense.ExpenseSubGroupId);
        
        expense.ExpenseLedger = await _context.SubGroupLedgers
            .Include(sgl => sgl.MasterGroup)
            .Include(sgl => sgl.MasterSubGroup)
            .FirstOrDefaultAsync(sgl => sgl.Id == expense.ExpenseLedgerId);

        // Load Debit Account name if needed (assuming it's a Ledger)
        if (expense.DebitAccountId.HasValue && string.IsNullOrEmpty(expense.DebitAccountName))
        {
            var ledger = await _context.SubGroupLedgers.FindAsync(expense.DebitAccountId.Value);
            expense.DebitAccountName = ledger?.Name;
        }
    }

    public Task<List<SelectListItem>> GetPaymentModeListAsync()
    {
        var paymentModeList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Cash", Text = "Cash" },
            new SelectListItem { Value = "Cheque", Text = "Cheque" },
            new SelectListItem { Value = "NEFT", Text = "NEFT" },
            new SelectListItem { Value = "RTGS", Text = "RTGS" },
            new SelectListItem { Value = "UPI", Text = "UPI" }
        };
        return Task.FromResult(paymentModeList);
    }

    public async Task<IEnumerable<string>> GetVehiclesAsync(string? searchTerm)
    {
        var query1 = _context.ExpensesIncurreds
            .AsNoTracking()
            .Where(e => !string.IsNullOrEmpty(e.VehicleNo))
            .Select(e => e.VehicleNo);

        var query2 = _context.PurchaseReceives
            .AsNoTracking()
            .Where(p => !string.IsNullOrEmpty(p.VehicleNo))
            .Select(p => p.VehicleNo);

        var vehicles = await query1.Union(query2).ToListAsync();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            vehicles = vehicles
                .Where(v => v.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return vehicles.Distinct().OrderBy(v => v).Take(20);
    }
}

