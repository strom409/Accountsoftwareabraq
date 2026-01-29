using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using AbraqAccount.Models.Common;
using AbraqAccount.Extensions;
using Microsoft.AspNetCore.Http;

namespace AbraqAccount.Services.Implementations;

public class PaymentSettlementService : IPaymentSettlementService
{
    private readonly AppDbContext _context;
    private readonly ITransactionEntriesService _transactionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PaymentSettlementService(AppDbContext context, ITransactionEntriesService transactionService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _transactionService = transactionService;
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

    public async Task<(List<PaymentSettlementGroupViewModel> groups, int totalCount, int totalPages)> GetSettlementsAsync(
        string? paNumber,
        DateTime? fromDate,
        DateTime? toDate,
        string? vendorGroup,
        string? vendorName,
        string? unit,
        string? approvalStatus,
        string? paymentStatus,
        int page,
        int pageSize)
    {
        var query = _context.PaymentSettlements.Where(s => s.IsActive)
            .AsQueryable();

        if (!string.IsNullOrEmpty(unit) && unit != "ALL")
        {
            query = query.Where(s => s.Unit == unit);
        }

        if (!string.IsNullOrEmpty(paNumber))
        {
            query = query.Where(p => p.PANumber.Contains(paNumber));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(p => p.SettlementDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(p => p.SettlementDate <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(vendorGroup))
        {
            query = query.Where(p => p.AccountName.Contains(vendorGroup));
        }

        if (!string.IsNullOrEmpty(vendorName))
        {
            query = query.Where(p => p.AccountName.Contains(vendorName));
        }

        if (!string.IsNullOrEmpty(approvalStatus))
        {
            query = query.Where(p => p.ApprovalStatus == approvalStatus);
        }

        if (!string.IsNullOrEmpty(paymentStatus))
        {
            query = query.Where(p => p.PaymentStatus == paymentStatus);
        }

        // Get all entries first (before pagination) - group by PANumber
        var allSettlements = await query
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
        
        // Group entries by PANumber (entries created together)
        var groupedEntries = allSettlements
            .GroupBy(p => p.PANumber)
            .Select(g => new
            {
                PANumber = g.Key,
                Entries = g.OrderBy(e => e.CreatedAt).ToList()
            })
            .OrderByDescending(g => g.Entries.First().CreatedAt)
            .ToList();
        
        // Calculate total count for pagination (count groups, not individual entries)
        var totalGroups = groupedEntries.Count;
        var totalPages = (int)Math.Ceiling(totalGroups / (double)pageSize);
        
        // Apply pagination to groups
        var paginatedGroups = groupedEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        // Create view model for grouped entries
        var groupedViewModels = new List<PaymentSettlementGroupViewModel>();
        
        foreach (var group in paginatedGroups)
        {
            var creditEntry = group.Entries.FirstOrDefault(e => e.Type == "Credit");
            var debitEntry = group.Entries.FirstOrDefault(e => e.Type == "Debit");
            
            if (creditEntry != null || debitEntry != null)
            {
                var viewModel = new PaymentSettlementGroupViewModel
                {
                    CreditEntry = creditEntry,
                    DebitEntry = debitEntry,
                    PANumber = group.PANumber,
                    SettlementDate = creditEntry?.SettlementDate ?? debitEntry?.SettlementDate ?? DateTime.Now,
                    VendorName = creditEntry?.AccountName ?? debitEntry?.AccountName ?? "",
                    PaymentAmount = creditEntry?.Amount ?? debitEntry?.Amount ?? 0,
                    ApprovalStatus = creditEntry?.ApprovalStatus ?? debitEntry?.ApprovalStatus ?? "Unapproved",
                    PaymentStatus = creditEntry?.PaymentStatus ?? debitEntry?.PaymentStatus ?? "Pending",
                    CreditEntryId = creditEntry?.Id ?? 0,
                    DebitEntryId = debitEntry?.Id ?? 0,
                    ClosingBal = 0, // Removed from model
                    NEFTRTGSCashForm = creditEntry?.RefNo ?? debitEntry?.RefNo,
                    Unit = creditEntry?.Unit ?? debitEntry?.Unit
                };
                
                groupedViewModels.Add(viewModel);
            }
        }

        return (groupedViewModels, totalGroups, totalPages);
    }

    public async Task<(bool success, string message)> CreateSettlementAsync(PaymentSettlement paymentSettlement)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            // Generate PA Number (e.g., PA000001)
            var lastSettlement = await _context.PaymentSettlements
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            
            int nextNumber = 1;
            if (lastSettlement != null && !string.IsNullOrEmpty(lastSettlement.PANumber))
            {
                var numberPart = lastSettlement.PANumber.Replace("PA", "");
                if (int.TryParse(numberPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }
            paymentSettlement.PANumber = $"PA{nextNumber:D6}";

            paymentSettlement.CreatedAt = DateTime.Now;
            paymentSettlement.CreatedBy = currentUser;
            paymentSettlement.ApprovalStatus = paymentSettlement.ApprovalStatus ?? "Unapproved";
            paymentSettlement.PaymentStatus = paymentSettlement.PaymentStatus ?? "Pending";
            paymentSettlement.IsActive = true;

            _context.PaymentSettlements.Add(paymentSettlement);
            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    paymentSettlement.PANumber, "Payment", "Insert", currentUser, 
                    remarks: "Settlement Created",
                    newValues: JsonSerializer.Serialize(paymentSettlement));
            }
            catch { /* Ignore */ }

            return (true, "Payment Settlement created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> CreateMultipleSettlementsAsync(PaymentSettlementBatchModel model)
    {
        if (model == null || model.Entries == null || model.Entries.Count == 0)
        {
            return (false, "No entries to save.");
        }

        try
        {
            var currentUser = GetCurrentUsername();
            // Validate that total debit equals total credit
            decimal totalDebit = 0;
            decimal totalCredit = 0;
            foreach (var entry in model.Entries)
            {
                if (entry.Type == "Debit")
                {
                    totalDebit += entry.Amount;
                }
                else if (entry.Type == "Credit")
                {
                    totalCredit += entry.Amount;
                }
            }

            if (Math.Abs(totalDebit - totalCredit) >= 0.01m)
            {
                return (false, $"Total Debit ({totalDebit:F2}) does not equal Total Credit ({totalCredit:F2}). Difference: {Math.Abs(totalDebit - totalCredit):F2}");
            }

            // Generate PA Number (e.g., PA000001)
            var lastSettlement = await _context.PaymentSettlements
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            
            int nextNumber = 1;
            if (lastSettlement != null && !string.IsNullOrEmpty(lastSettlement.PANumber))
            {
                var numberPart = lastSettlement.PANumber.Replace("PA", "");
                if (int.TryParse(numberPart, out int lastNum))
                {
                    nextNumber = lastNum + 1;
                }
            }

            var paNumber = $"PA{nextNumber:D6}";

            // Save all entries with the same PA number
            foreach (var entryData in model.Entries)
            {
                // Fetch Account Name if not provided in model but ID is there (safety check)
                string accountName = "Unknown Account";
                // Ideally account name is passed from UI or we fetch it. 
                // Since user wants "AccountName" stored, and UI has it in dropdown but sends ID...
                // Actually the PaymentSettlementItemModel doesn't have AccountName! 
                // We need to fetch it based on AccountType/ID or trust what we can get.
                // Let's modify this to fetch.
                
                if (!string.IsNullOrEmpty(entryData.AccountType)) {
                     if (entryData.AccountType == "BankMaster") {
                         var acc = await _context.BankMasters.FindAsync(entryData.AccountId);
                         if (acc != null) accountName = acc.AccountName;
                     } 
                     else if (entryData.AccountType == "SubGroupLedger") {
                         var acc = await _context.SubGroupLedgers.FindAsync(entryData.AccountId);
                         if (acc != null) accountName = acc.Name;
                     }
                     else if (entryData.AccountType == "Farmer") {
                         var acc = await _context.Farmers.FindAsync(entryData.AccountId);
                         if (acc != null) accountName = acc.FarmerName;
                     }
                     else if (entryData.AccountType == "Vendor") {
                          var acc = await _context.Vendors.FindAsync(entryData.AccountId);
                          if (acc != null) accountName = acc.VendorName;
                     }
                     else if (entryData.AccountType == "MasterGroup") {
                         var acc = await _context.MasterGroups.FindAsync(entryData.AccountId);
                         if (acc != null) accountName = acc.Name;
                     }
                     else if (entryData.AccountType == "MasterSubGroup") {
                         var acc = await _context.MasterSubGroups.FindAsync(entryData.AccountId);
                         if (acc != null) accountName = acc.Name;
                     }
                }

                var paymentSettlement = new PaymentSettlement
                {
                    PANumber = paNumber,
                    SettlementDate = model.SettlementDate,
                    Type = entryData.Type,
                    AccountId = entryData.AccountId,
                    AccountType = entryData.AccountType ?? "Main", 
                    AccountName = accountName, 
                    PaymentType = entryData.PaymentType,
                    Amount = entryData.Amount,
                    RefNo = entryData.RefNo,
                    Narration = entryData.Narration,
                    PaymentStatus = "Pending",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser,
                    Unit = entryData.Unit,
                    EntryAccountId = entryData.EntryAccountId,
                    EntryForId = entryData.EntryForId,
                    EntryForName = entryData.EntryForName
                };

                _context.PaymentSettlements.Add(paymentSettlement);
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    paNumber, "Payment", "Insert", currentUser, 
                    remarks: "Settlement Created",
                    newValues: JsonSerializer.Serialize(model));
            }
            catch { /* Ignore */ }

            return (true, "Payment Settlement created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> DeleteSettlementAsync(int id)
    {
        var entry = await _context.PaymentSettlements.FindAsync(id);
        if (entry == null)
        {
            return (false, "Entry not found.");
        }

        try
        {
            var currentUser = GetCurrentUsername();
            // Delete all entries with same PANumber
            var relatedEntries = await _context.PaymentSettlements
                .Where(p => p.PANumber == entry.PANumber)
                .ToListAsync();

            foreach (var rel in relatedEntries)
            {
                rel.IsActive = false;
                rel.UpdatedAt = DateTime.Now;
                rel.UpdatedBy = currentUser;
                _context.Update(rel);
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    entry.PANumber, "Payment", "Delete", currentUser, 
                    remarks: "Settlement Deleted");
            }
            catch { /* Ignore */ }

            return (true, "Payment settlement deleted successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error deleting settlement: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> ApproveSettlementAsync(int id)
    {
        var entry = await _context.PaymentSettlements.FindAsync(id);
        if (entry == null)
        {
            return (false, "Entry not found.");
        }

        try
        {
            var currentUser = GetCurrentUsername();
            // Approve all entries with same PANumber
            var relatedEntries = await _context.PaymentSettlements
                .Where(p => p.PANumber == entry.PANumber && p.IsActive)
                .ToListAsync();

            foreach (var rel in relatedEntries)
            {
                rel.ApprovalStatus = "Approved";
                rel.UpdatedAt = DateTime.Now;
                rel.UpdatedBy = currentUser;
                _context.Update(rel);
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    entry.PANumber, "Payment", "Approve", currentUser, 
                    remarks: "Settlement Approved");
            }
            catch { /* Ignore */ }

            return (true, "Payment settlement approved successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error approving settlement: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> UnapproveSettlementAsync(int id)
    {
        var entry = await _context.PaymentSettlements.FindAsync(id);
        if (entry == null)
        {
            return (false, "Entry not found.");
        }

        try
        {
            var currentUser = GetCurrentUsername();
            // Unapprove all entries with same PANumber
            var relatedEntries = await _context.PaymentSettlements
                .Where(p => p.PANumber == entry.PANumber && p.IsActive)
                .ToListAsync();

            foreach (var rel in relatedEntries)
            {
                rel.ApprovalStatus = "Unapproved";
                rel.UpdatedAt = DateTime.Now;
                rel.UpdatedBy = currentUser;
                _context.Update(rel);
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    entry.PANumber, "Payment", "Unapprove", currentUser, 
                    remarks: "Settlement Unapproved");
            }
            catch { /* Ignore */ }

            return (true, "Payment settlement unapproved successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error unapproving settlement: " + ex.Message);
        }
    }

    public async Task<IEnumerable<object>> GetVendorsAsync(string? searchTerm)
    {
        var query = _context.Vendors.Where(v => v.IsActive).AsQueryable();
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(v => v.VendorName.Contains(searchTerm) || v.VendorCode.Contains(searchTerm));
        }
        
        return await query
            .OrderBy(v => v.VendorName)
            .Take(50)
            .Select(v => new { id = v.Id, name = $"{v.VendorCode} - {v.VendorName}" })
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetBillsAsync(string? searchTerm, int? vendorId)
    {
        // Placeholder implementation as in controller
        return await Task.FromResult(new List<object>());
    }

    public async Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? paymentFromId = null, string? type = null)
    {
        var rules = await _context.AccountRules
            .Where(r => r.RuleType == "AllowedNature")
            .ToListAsync();
        
        if (paymentFromId.HasValue)
        {
             var profileRules = rules.Where(r => r.EntryAccountId == paymentFromId.Value).ToList();

             var allowedSubGroupIds = profileRules
                 .Where(r => r.AccountType == "SubGroupLedger" && CheckRule(r.Value, type))
                 .Select(r => r.AccountId)
                 .ToHashSet();

             var allowedGrowerGroupIds = profileRules
                 .Where(r => r.AccountType == "GrowerGroup" && CheckRule(r.Value, type))
                 .Select(r => r.AccountId)
                 .ToHashSet();

             var allowedBankMasterIds = profileRules
                 .Where(r => r.AccountType == "BankMaster" && CheckRule(r.Value, type))
                 .Select(r => r.AccountId)
                 .ToHashSet();

             var allowedFarmerIds = profileRules
                 .Where(r => r.AccountType == "Farmer" && CheckRule(r.Value, type))
                 .Select(r => r.AccountId)
                 .ToHashSet();

             var bankMastersQuery = _context.BankMasters.Where(bm => bm.IsActive);
             var farmersQuery = _context.Farmers.Where(f => f.IsActive);

             if (!string.IsNullOrEmpty(searchTerm))
             {
                 bankMastersQuery = bankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
                 farmersQuery = farmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
             }

             var bankMasters = await bankMastersQuery
                 .Where(bm => allowedBankMasterIds.Contains(bm.Id) || allowedSubGroupIds.Contains(bm.GroupId))
                 .OrderBy(bm => bm.AccountName)
                 .Take(50)
                 .ToListAsync();

             var farmers = await farmersQuery
                 .Where(f => allowedFarmerIds.Contains(f.Id) || allowedGrowerGroupIds.Contains(f.GroupId))
                 .OrderBy(f => f.FarmerName)
                 .Take(50)
                 .ToListAsync();

             var allAccounts = new List<LookupItem>();
             allAccounts.AddRange(bankMasters.Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = "BankMaster" }));
             allAccounts.AddRange(farmers.Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = "Farmer" }));

             return allAccounts.OrderBy(a => a.Name).Take(100).ToList();
        }

        string? GetRuleValue(string accountType, int accountId, int? entryId)
        {
            if (entryId.HasValue)
            {
                var specificRule = rules.FirstOrDefault(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == entryId);
                if (specificRule != null) return specificRule.Value;
            }
            var defaultRule = rules.FirstOrDefault(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == null);
            if (defaultRule != null) return defaultRule.Value;
            return null; 
        }

        bool IsAllowed(string accountType, int accountId, string? fallbackType = null, int? fallbackId = null)
        {
             string? ruleValue = GetRuleValue(accountType, accountId, paymentFromId);
             if (ruleValue != null) return CheckRule(ruleValue, type);

             if (fallbackType != null && fallbackId.HasValue)
             {
                 string? fallbackRuleValue = GetRuleValue(fallbackType, fallbackId.Value, paymentFromId);
                 if (fallbackRuleValue != null) return CheckRule(fallbackRuleValue, type);
             }
             return true; 
        }

        bool CheckRule(string ruleValue, string? filterType)
        {
            if (string.IsNullOrWhiteSpace(ruleValue)) return false;
            if (ruleValue.Equals("Both", StringComparison.OrdinalIgnoreCase)) return true;
            if (ruleValue.Equals("Cancel", StringComparison.OrdinalIgnoreCase)) return false;
            if (string.IsNullOrWhiteSpace(filterType)) return true; 

            if (ruleValue.Equals("Debit", StringComparison.OrdinalIgnoreCase) && filterType.Equals("Debit", StringComparison.OrdinalIgnoreCase)) return true;
            if (ruleValue.Equals("Credit", StringComparison.OrdinalIgnoreCase) && filterType.Equals("Credit", StringComparison.OrdinalIgnoreCase)) return true;
            
            return false;
        }

        var globalBankMastersQuery = _context.BankMasters.Where(bm => bm.IsActive);
        var globalFarmersQuery = _context.Farmers.Where(f => f.IsActive);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            globalBankMastersQuery = globalBankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
            globalFarmersQuery = globalFarmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
        }

        var globalBankMasters = await globalBankMastersQuery.OrderBy(bm => bm.AccountName).Take(50).ToListAsync();
        var globalFarmers = await globalFarmersQuery.OrderBy(f => f.FarmerName).Take(50).ToListAsync();

        var globalAccounts = new List<LookupItem>();
        
        globalAccounts.AddRange(globalBankMasters
            .Where(bm => IsAllowed("BankMaster", bm.Id, "SubGroupLedger", bm.GroupId))
            .Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = "BankMaster" }));
            
        globalAccounts.AddRange(globalFarmers
            .Where(f => IsAllowed("Farmer", f.Id, "GrowerGroup", f.GroupId))
            .Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = "Farmer" }));

        return globalAccounts.OrderBy(a => a.Name).Take(100).ToList();
    }

    public async Task<IEnumerable<LookupItem>> GetEntryProfilesAsync()
    {
        return await _context.EntryForAccounts
            .Where(e => e.TransactionType == "PaymentSettlement")
            .OrderBy(e => e.AccountName)
            .Select(e => new LookupItem { Id = e.Id, Name = e.AccountName })
            .ToListAsync();
    }

    public async Task<PaymentSettlement?> GetSettlementByIdAsync(int id)
    {
        return await _context.PaymentSettlements
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<PaymentSettlement>> GetSettlementEntriesByPANumberAsync(string paNumber)
    {
        return await _context.PaymentSettlements
            .Where(p => p.PANumber == paNumber && p.IsActive)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<(bool success, string message)> UpdateSettlementAsync(PaymentSettlementBatchModel model, string paNumber)
    {
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var currentUser = GetCurrentUsername();
                // Find existing entries for this PANumber
                var existingEntries = await _context.PaymentSettlements
                    .Where(p => p.PANumber == paNumber)
                    .ToListAsync();

                if (existingEntries.Count == 0)
                {
                    return (false, "Payment settlement not found.");
                }

                // Check if any existing entry is approved
                if (existingEntries.Any(e => e.ApprovalStatus == "Approved"))
                {
                    return (false, "Cannot edit an approved settlement.");
                }

                // Capture old state for history
                var oldState = new {
                    SettlementDate = existingEntries.First().SettlementDate,
                    Entries = existingEntries.Select(e => new {
                        e.Type, e.AccountId, e.AccountName, e.Amount, e.PaymentType, e.RefNo, e.Narration
                    }).ToList()
                };

                var firstEntryCreatedAt = existingEntries.OrderBy(e => e.CreatedAt).First().CreatedAt;

                _context.PaymentSettlements.RemoveRange(existingEntries);
                await _context.SaveChangesAsync();

                // Reuse CreateMultiple logic but with fixed PA Number
                foreach (var entryData in model.Entries)
                {
                    // Fetch Account Name if not provided in model but ID is there
                    string accountName = "Unknown Account";
                    if (!string.IsNullOrEmpty(entryData.AccountType)) {
                         if (entryData.AccountType == "BankMaster") {
                             var acc = await _context.BankMasters.FindAsync(entryData.AccountId);
                             if (acc != null) accountName = acc.AccountName;
                         } 
                         else if (entryData.AccountType == "SubGroupLedger") {
                             var acc = await _context.SubGroupLedgers.FindAsync(entryData.AccountId);
                             if (acc != null) accountName = acc.Name;
                         }
                         else if (entryData.AccountType == "Farmer") {
                             var acc = await _context.Farmers.FindAsync(entryData.AccountId);
                             if (acc != null) accountName = acc.FarmerName;
                         }
                         else if (entryData.AccountType == "Vendor") {
                              var acc = await _context.Vendors.FindAsync(entryData.AccountId);
                              if (acc != null) accountName = acc.VendorName;
                         }
                         else if (entryData.AccountType == "MasterGroup") {
                             var acc = await _context.MasterGroups.FindAsync(entryData.AccountId);
                             if (acc != null) accountName = acc.Name;
                         }
                         else if (entryData.AccountType == "MasterSubGroup") {
                             var acc = await _context.MasterSubGroups.FindAsync(entryData.AccountId);
                             if (acc != null) accountName = acc.Name;
                         }
                    }

                    var paymentSettlement = new PaymentSettlement
                    {
                        PANumber = paNumber,
                        SettlementDate = model.SettlementDate,
                        Type = entryData.Type,
                        AccountId = entryData.AccountId,
                        AccountType = entryData.AccountType ?? "Main",
                        AccountName = accountName,
                        PaymentType = entryData.PaymentType,
                        Amount = entryData.Amount,
                        RefNo = entryData.RefNo,
                        Narration = entryData.Narration,
                        CreatedAt = firstEntryCreatedAt,
                        ApprovalStatus = "Unapproved",
                        PaymentStatus = "Pending",
                        IsActive = true,
                        CreatedBy = currentUser,
                        Unit = entryData.Unit,
                        EntryForId = entryData.EntryForId,
                        EntryForName = entryData.EntryForName,
                        EntryAccountId = entryData.EntryAccountId
                    };

                    _context.PaymentSettlements.Add(paymentSettlement);
                }

                await _context.SaveChangesAsync();
                
                // History Logging
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        paNumber, "Payment", "Edit", currentUser, 
                        remarks: "Settlement Updated",
                        oldValues: JsonSerializer.Serialize(oldState),
                        newValues: JsonSerializer.Serialize(model));
                }
                catch { /* Ignore */ }

                await transaction.CommitAsync();

                return (true, "Payment Settlement updated successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "An error occurred while updating: " + ex.Message);
            }
        });
    }

    public async Task<object?> GetPADetailsAsync(string paNumber)
    {
        // Find all entries with this PA number
        var entries = await _context.PaymentSettlements
            .Where(p => p.PANumber == paNumber && p.IsActive)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
        
        if (entries == null || entries.Count == 0)
        {
            return new { success = false, error = "PA Number not found" };
        }

        return new
        {
            success = true,
            entries = entries.Select(e => new
            {
                paNumber = e.PANumber,
                vendorName = e.AccountName,
                vendorId = e.AccountId, // Mapping to format expected by UI
                amount = e.Amount,
                type = e.Type,
                paymentType = e.PaymentType,
                refNo = e.RefNo ?? "-",
                narration = e.Narration ?? "-",
                date = e.SettlementDate.ToString("dd/MM/yyyy"),
                unit = e.Unit,
                accountType = e.AccountType,
                entryForId = e.EntryForId,
                entryForName = e.EntryForName
            }).ToList()
        };
    }

    public async Task LoadDropdownsAsync(dynamic viewBag)
    {
        var paymentForList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Vendor", Text = "Vendor" }
        };
        viewBag.PaymentForList = new SelectList(paymentForList, "Value", "Text", "Vendor");

        var paymentModeList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Cash", Text = "Cash" },
            new SelectListItem { Value = "Cheque", Text = "Cheque" },
            new SelectListItem { Value = "NEFT", Text = "NEFT" },
            new SelectListItem { Value = "RTGS", Text = "RTGS" },
            new SelectListItem { Value = "UPI", Text = "UPI" }
        };
        viewBag.PaymentModeList = new SelectList(paymentModeList, "Value", "Text");

        var paymentTypeList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Advance Debit", Text = "Advance Debit" },
            new SelectListItem { Value = "Vide Bills Debit", Text = "Vide Bills Debit" }
        };
        viewBag.PaymentTypeList = new SelectList(paymentTypeList, "Value", "Text");

        viewBag.Units = new List<string> { "Unit 1", "Unit 2" };

        var entryAccounts = await _context.EntryForAccounts
            .Where(e => e.TransactionType == "PaymentSettlement")
            .OrderBy(e => e.AccountName)
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = e.AccountName
            })
            .ToListAsync();
        viewBag.EntryProfiles = new SelectList(entryAccounts, "Value", "Text");
    }
    public async Task<List<string>> GetUnitNamesAsync()
    {
        return await _context.UnitMasters
            .OrderBy(u => u.UnitName)
            .Select(u => u.UnitName ?? "")
            .Where(u => !string.IsNullOrEmpty(u))
            .Distinct()
            .ToListAsync();
    }
}

