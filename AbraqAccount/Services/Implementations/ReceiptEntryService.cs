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

public class ReceiptEntryService : IReceiptEntryService
{
    private readonly AppDbContext _context;
    private readonly ITransactionEntriesService _transactionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ReceiptEntryService(AppDbContext context, ITransactionEntriesService transactionService, IHttpContextAccessor httpContextAccessor)
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

    public async Task<(List<ReceiptEntryGroupViewModel> groups, int totalCount, int totalPages)> GetReceiptEntriesAsync(
        string? voucherNo, 
        string? growerGroup, 
        string? growerName, 
        string? unit,
        string? status, 
        DateTime? fromDate, 
        DateTime? toDate, 
        int page, 
        int pageSize)
    {
        var query = _context.ReceiptEntries.Where(r => r.IsActive).AsQueryable();
        
        if (!string.IsNullOrEmpty(unit) && unit != "ALL")
        {
            query = query.Where(r => r.Unit == unit);
        }

        if (!string.IsNullOrEmpty(voucherNo))
        {
            query = query.Where(r => r.VoucherNo.Contains(voucherNo));
        }

        if (!string.IsNullOrEmpty(growerGroup))
        {
            // Filter by account name - now includes BankMasters
            var bankMasterIdsGroup = await _context.BankMasters
                .Where(bm => bm.IsActive && bm.AccountName.Contains(growerGroup))
                .Select(bm => bm.Id)
                .ToListAsync();
            
            var masterGroupIds = await _context.MasterGroups
                .Where(mg => mg.Name.Contains(growerGroup))
                .Select(mg => mg.Id)
                .ToListAsync();
            
            var masterSubGroupIds = await _context.MasterSubGroups
                .Where(msg => msg.Name.Contains(growerGroup))
                .Select(msg => msg.Id)
                .ToListAsync();
            
            var subGroupLedgerIds = await _context.SubGroupLedgers
                .Where(sgl => sgl.Name.Contains(growerGroup))
                .Select(sgl => sgl.Id)
                .ToListAsync();
            
            query = query.Where(r => 
                (r.AccountType == "BankMaster" && bankMasterIdsGroup.Contains(r.AccountId)) ||
                (r.AccountType == "MasterGroup" && masterGroupIds.Contains(r.AccountId)) ||
                (r.AccountType == "MasterSubGroup" && masterSubGroupIds.Contains(r.AccountId)) ||
                (r.AccountType == "SubGroupLedger" && subGroupLedgerIds.Contains(r.AccountId)));
        }

        if (!string.IsNullOrEmpty(growerName))
        {
            var bankMasterIdsName = await _context.BankMasters
                .Where(bm => bm.IsActive && bm.AccountName.Contains(growerName))
                .Select(bm => bm.Id)
                .ToListAsync();
            
            var masterGroupIds = await _context.MasterGroups
                .Where(mg => mg.Name.Contains(growerName))
                .Select(mg => mg.Id)
                .ToListAsync();
            
            var masterSubGroupIds = await _context.MasterSubGroups
                .Where(msg => msg.Name.Contains(growerName))
                .Select(msg => msg.Id)
                .ToListAsync();
            
            var subGroupLedgerIds = await _context.SubGroupLedgers
                .Where(sgl => sgl.Name.Contains(growerName))
                .Select(sgl => sgl.Id)
                .ToListAsync();
            
            query = query.Where(r => 
                (r.AccountType == "BankMaster" && bankMasterIdsName.Contains(r.AccountId)) ||
                (r.AccountType == "MasterGroup" && masterGroupIds.Contains(r.AccountId)) ||
                (r.AccountType == "MasterSubGroup" && masterSubGroupIds.Contains(r.AccountId)) ||
                (r.AccountType == "SubGroupLedger" && subGroupLedgerIds.Contains(r.AccountId)));
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(r => r.ReceiptDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(r => r.ReceiptDate <= toDate.Value);
        }

        // Get matching voucher numbers first
        var matchingVoucherNos = await query
            .Select(r => r.VoucherNo)
            .Distinct()
            .ToListAsync();

        // Now fetch ALL entries for these vouchers to ensure we have complete data for summation
        var allReceiptEntries = await _context.ReceiptEntries
            .Where(r => r.IsActive && matchingVoucherNos.Contains(r.VoucherNo))
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();
        
        // Group entries by VoucherNo
        var groupedEntries = allReceiptEntries
            .GroupBy(r => r.VoucherNo)
            .Select(g => new
            {
                VoucherNo = g.Key,
                Entries = g.OrderBy(e => e.CreatedAt).ToList()
            })
            .OrderByDescending(g => g.Entries.First().CreatedAt)
            .ToList();
        
        // Calculate total count for pagination
        var totalGroups = groupedEntries.Count;
        var totalPages = (int)Math.Ceiling(totalGroups / (double)pageSize);
        
        // Apply pagination to groups
        var paginatedGroups = groupedEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        
        // Create view model for grouped entries
        var groupedViewModels = new List<ReceiptEntryGroupViewModel>();
        
        foreach (var group in paginatedGroups)
        {
            var creditEntries = group.Entries.Where(e => e.Type == "Credit").ToList();
            var debitEntries = group.Entries.Where(e => e.Type == "Debit").ToList();
            
            var creditEntry = creditEntries.FirstOrDefault();
            var debitEntry = debitEntries.FirstOrDefault();
            
            if (creditEntry != null)
            {
                var names = new List<string>();
                
                // Populate Account Name for Credit Entry
                var creditAccountName = await GetAccountNameAsync(creditEntry.AccountId, creditEntry.AccountType);
                creditEntry.AccountName = creditAccountName; // Explicitly set for UI
                if (!string.IsNullOrEmpty(creditAccountName)) names.Add(creditAccountName);

                // Populate Account Name for Debit Entry if exists
                if (debitEntry != null)
                {
                    var debitAccountName = await GetAccountNameAsync(debitEntry.AccountId, debitEntry.AccountType);
                    debitEntry.AccountName = debitAccountName; // Explicitly set for UI
                    if (!string.IsNullOrEmpty(debitAccountName)) names.Add(debitAccountName);
                }
                else 
                {
                     // Fallback check if there are other credit entries that might be relevant for the aggregated name
                     foreach (var ce in creditEntries.Skip(1))
                     {
                        var name = await GetAccountNameAsync(ce.AccountId, ce.AccountType);
                        if (!string.IsNullOrEmpty(name)) names.Add(name);
                     }
                }

                var viewModel = new ReceiptEntryGroupViewModel
                {
                    CreditEntry = creditEntry,
                    DebitEntry = debitEntry,
                    VoucherNo = creditEntry.VoucherNo,
                    ReceiptDate = creditEntry.ReceiptDate,
                    AccountName = string.Join(", ", names.Distinct()),
                    ReceiptAmount = creditEntries.Sum(e => e.Amount),
                    Status = creditEntry.Status,
                    CreditEntryId = creditEntry.Id,
                    DebitEntryId = debitEntry?.Id ?? 0,
                    Unit = creditEntry.Unit
                };
                
                groupedViewModels.Add(viewModel);
            }
        }

        return (groupedViewModels, totalGroups, totalPages);
    }

    public async Task<(bool success, string message)> CreateMultipleReceiptsAsync(ReceiptEntryBatchModel model)
    {
        var currentUser = GetCurrentUsername();
        if (model == null || model.Entries == null || model.Entries.Count == 0)
        {
            return (false, "No entries to save.");
        }

        try
        {
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

            // Validate Payment Type and Ref No for multi-entry transactions
            if (model.Entries.Count > 1)
            {
                var firstPaymentType = model.Entries.First().PaymentType;
                var firstRefNo = model.Entries.First().RefNoChequeUTR ?? "";

                if (model.Entries.Any(e => e.PaymentType != firstPaymentType || (e.RefNoChequeUTR ?? "") != firstRefNo))
                {
                    return (false, "PAYMENT TYPES OR REF. NO'S NOT MATCHED FOR ALL ENTRIES");
                }
            }

            // Generate Voucher Number (e.g., RCPT/A/26-27/0006)
            var lastEntry = await _context.ReceiptEntries
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();
            
            int nextNumber = 1;
            if (lastEntry != null && !string.IsNullOrEmpty(lastEntry.VoucherNo))
            {
                var parts = lastEntry.VoucherNo.Split('/');
                if (parts.Length > 0)
                {
                    var numberPart = parts[parts.Length - 1];
                    if (int.TryParse(numberPart, out int lastNum))
                    {
                        nextNumber = lastNum + 1;
                    }
                }
            }
            
            var currentYear = DateTime.Now.Year;
            var yearShort = currentYear.ToString().Substring(2);
            var nextYear = (currentYear + 1).ToString().Substring(2);
            var voucherNo = $"RCPT/A/{yearShort}-{nextYear}/{nextNumber:D4}";

            // Save all entries with the same voucher number
            foreach (var entryData in model.Entries)
            {
                var receiptEntry = new ReceiptEntry
                {
                    VoucherNo = voucherNo,
                    ReceiptDate = model.ReceiptDate,
                    MobileNo = model.MobileNo,
                    Type = entryData.Type,
                    AccountId = entryData.AccountId,
                    AccountType = entryData.AccountType,
                    PaymentType = entryData.PaymentType,
                    Amount = entryData.Amount,
                    RefNoChequeUTR = entryData.RefNoChequeUTR,
                    Narration = entryData.Narration,
                    Status = "Unapproved",
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser,
                    IsActive = true,
                    PaymentFromSubGroupId = entryData.PaymentFromSubGroupId,
                    Unit = entryData.Unit,
                    EntryAccountId = entryData.EntryAccountId,
                    EntryForId = entryData.EntryForId,
                    EntryForName = entryData.EntryForName
                };

                _context.ReceiptEntries.Add(receiptEntry);
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    voucherNo, "Receipt", "Insert", currentUser, 
                    remarks: "Voucher Created", 
                    newValues: JsonSerializer.Serialize(model));
            }
            catch { /* Ignore logging errors to not block transaction */ }

            return (true, "Receipt Entry created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    public async Task<(bool success, object? data, string? error)> GetVoucherDetailsAsync(string voucherNo)
    {
        try
        {
            // Find all entries with this voucher number
            var entries = await _context.ReceiptEntries
                .Where(r => r.VoucherNo == voucherNo && r.IsActive)
                .ToListAsync();
            
            if (entries == null || entries.Count == 0)
            {
                return (false, null, "Voucher not found");
            }

            var creditEntries = entries.Where(e => e.Type == "Credit").ToList();
            var debitEntries = entries.Where(e => e.Type == "Debit").ToList();

            var firstCredit = creditEntries.FirstOrDefault() ?? entries.First();
            var firstDebit = debitEntries.FirstOrDefault();

            // Aggregate Credit Names
            var creditNames = new List<string>();
            foreach (var ce in creditEntries)
            {
                var name = await GetAccountNameAsync(ce.AccountId, ce.AccountType);
                if (!string.IsNullOrEmpty(name)) creditNames.Add(name);
            }
            var creditAccountName = string.Join(", ", creditNames.Distinct());

            // Aggregate Debit Names
            var debitNames = new List<string>();
            foreach (var de in debitEntries)
            {
                var name = await GetAccountNameAsync(de.AccountId, de.AccountType);
                if (!string.IsNullOrEmpty(name)) debitNames.Add(name);
            }
            var debitAccountName = string.Join(", ", debitNames.Distinct());

            var result = new
            {
                success = true,
                credit = new
                {
                    voucherNo = firstCredit.VoucherNo,
                    accountName = creditAccountName,
                    amount = creditEntries.Sum(e => e.Amount),
                    paymentType = firstCredit.PaymentType,
                    refNo = firstCredit.RefNoChequeUTR,
                    narration = firstCredit.Narration,
                    date = firstCredit.ReceiptDate.ToString("dd/MM/yyyy"),
                    unit = firstCredit.Unit
                },
                debit = firstDebit != null ? new
                {
                    voucherNo = firstDebit.VoucherNo,
                    accountName = debitAccountName,
                    amount = debitEntries.Sum(e => e.Amount),
                    paymentType = firstDebit.PaymentType,
                    refNo = firstDebit.RefNoChequeUTR,
                    narration = firstDebit.Narration,
                    date = firstDebit.ReceiptDate.ToString("dd/MM/yyyy"),
                    unit = firstDebit.Unit
                } : null
            };

            return (true, result, null);
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }

    public async Task<(bool success, string message)> DeleteReceiptEntryAsync(int id)
    {
        var entry = await _context.ReceiptEntries.FindAsync(id);
        if (entry == null)
        {
            return (false, "Entry not found.");
        }

        var currentUser = GetCurrentUsername();
        try
        {
            // Delete all entries with same VoucherNo
            var relatedEntries = await _context.ReceiptEntries
                .Where(r => r.VoucherNo == entry.VoucherNo)
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
                    entry.VoucherNo, "Receipt", "Delete", currentUser, 
                    remarks: "Voucher Deleted");
            }
            catch { /* Ignore */ }

            return (true, "Receipt entry deleted successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error deleting entry: " + ex.Message);
        }
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
            .Where(e => e.TransactionType == "ReceiptEntry")
            .OrderBy(e => e.AccountName)
            .Select(e => new LookupItem { Id = e.Id, Name = e.AccountName })
            .ToListAsync();
    }

    public Task LoadDropdownsAsync(dynamic viewBag)
    {
        var typeList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Credit", Text = "Credit" },
            new SelectListItem { Value = "Debit", Text = "Debit" }
        };
        viewBag.TypeList = new SelectList(typeList, "Value", "Text");

        var paymentTypeList = new List<SelectListItem>
        {
            new SelectListItem { Value = "Mobile Pay", Text = "Mobile Pay" },
            new SelectListItem { Value = "Cash", Text = "Cash" },
            new SelectListItem { Value = "Cheque", Text = "Cheque" },
            new SelectListItem { Value = "UTR", Text = "UTR" },
            new SelectListItem { Value = "NEFT", Text = "NEFT" },
            new SelectListItem { Value = "RTGS", Text = "RTGS" }
        };
        viewBag.PaymentTypeList = new SelectList(paymentTypeList, "Value", "Text");

        viewBag.Units = new List<string> { "Unit 1", "Unit 2" };

        var entryAccounts = _context.EntryForAccounts
            .Where(e => e.TransactionType == "ReceiptEntry")
            .OrderBy(e => e.AccountName)
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = e.AccountName
            })
            .ToList();
        viewBag.EntryProfiles = new SelectList(entryAccounts, "Value", "Text");
        
        return Task.CompletedTask;
    }
    public async Task<ReceiptEntry?> GetReceiptEntryByIdAsync(int id)
    {
        return await _context.ReceiptEntries
            .FirstOrDefaultAsync(r => r.Id == id && r.IsActive);
    }

    public async Task<List<ReceiptEntry>> GetReceiptEntriesByVoucherNoAsync(string voucherNo)
    {
        return await _context.ReceiptEntries
            .Where(r => r.VoucherNo == voucherNo && r.IsActive)
            .ToListAsync();
    }



    public async Task<(bool success, string message)> UpdateReceiptEntryAsync(ReceiptEntry model)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var existing = await _context.ReceiptEntries.FindAsync(model.Id);
            if (existing == null) return (false, "Entry not found");

            existing.ReceiptDate = model.ReceiptDate;
            existing.MobileNo = model.MobileNo;
            existing.Type = model.Type;
            existing.AccountId = model.AccountId;
            existing.AccountType = model.AccountType;
            existing.PaymentType = model.PaymentType;
            existing.Amount = model.Amount;
            existing.RefNoChequeUTR = model.RefNoChequeUTR;
            existing.Narration = model.Narration;
            existing.Status = model.Status;
            existing.UpdatedAt = DateTime.Now;
            existing.UpdatedBy = currentUser;
            
            _context.Update(existing);
            await _context.SaveChangesAsync();
            return (true, "Receipt Entry updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error updating entry: " + ex.Message);
        }
    }



    public async Task<(bool success, string message)> UnapproveReceiptEntryAsync(int id)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var entry = await _context.ReceiptEntries.FindAsync(id);
            if (entry == null) return (false, "Entry not found");

            // Unapprove all entries with same VoucherNo
            var entries = await _context.ReceiptEntries.Where(r => r.VoucherNo == entry.VoucherNo).ToListAsync();
            foreach(var e in entries) 
            {
                e.Status = "Unapproved";
                e.UpdatedAt = DateTime.Now;
                e.UpdatedBy = currentUser;
            }
            await _context.SaveChangesAsync();

             // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    entry.VoucherNo, "Receipt", "Unapprove", currentUser, 
                    remarks: "Voucher Unapproved");
            }
            catch { /* Ignore */ }

            return (true, "Receipt Unapproved Successfully");
        }
        catch (Exception ex)
        {
            return (false, "Error unapproving entry: " + ex.Message);
        }
    }

    public async Task<string> GetAccountNameAsync(int accountId, string accountType)
    {
        if (string.IsNullOrEmpty(accountType)) return "";

        if (string.Equals(accountType, "BankMaster", StringComparison.OrdinalIgnoreCase))
        {
            var bank = await _context.BankMasters.FindAsync(accountId);
            return bank?.AccountName ?? "";
        }
        else if (string.Equals(accountType, "MasterGroup", StringComparison.OrdinalIgnoreCase))
        {
            var group = await _context.MasterGroups.FindAsync(accountId);
            return group?.Name ?? "";
        }
        else if (string.Equals(accountType, "MasterSubGroup", StringComparison.OrdinalIgnoreCase))
        {
            var subGroup = await _context.MasterSubGroups
                .Include(msg => msg.MasterGroup)
                .FirstOrDefaultAsync(msg => msg.Id == accountId);
            return subGroup != null ? $"{subGroup.MasterGroup?.Name ?? ""} - {subGroup.Name}" : "";
        }
        else if (string.Equals(accountType, "SubGroupLedger", StringComparison.OrdinalIgnoreCase))
        {
            var ledger = await _context.SubGroupLedgers
                .Include(sgl => sgl.MasterGroup)
                .Include(sgl => sgl.MasterSubGroup)
                .FirstOrDefaultAsync(sgl => sgl.Id == accountId);
            return ledger != null ? $"{ledger.MasterGroup?.Name ?? ""} - {ledger.MasterSubGroup?.Name ?? ""} - {ledger.Name}" : "";
        }
        else if (string.Equals(accountType, "Farmer", StringComparison.OrdinalIgnoreCase))
        {
            var farmer = await _context.Farmers.FindAsync(accountId);
            return farmer?.FarmerName ?? "";
        }

        return "";
    }

    public async Task<(bool success, string message)> UpdateReceiptVoucherAsync(string voucherNo, ReceiptEntryBatchModel model)
    {
        if (model == null || model.Entries == null || model.Entries.Count == 0)
        {
            return (false, "No entries to save.");
        }

        var currentUser = GetCurrentUsername();

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Validate Balance
                decimal totalDebit = model.Entries.Where(e => e.Type == "Debit").Sum(e => e.Amount);
                decimal totalCredit = model.Entries.Where(e => e.Type == "Credit").Sum(e => e.Amount);

                if (Math.Abs(totalDebit - totalCredit) >= 0.01m)
                {
                    return (false, $"Entry is not balanced. Total Debit ({totalDebit:F2}) must be equal to Total Credit ({totalCredit:F2}). Difference: {Math.Abs(totalDebit - totalCredit):F2}");
                }

                // 2. Validate Payment Type and Ref No for multi-entry transactions
                if (model.Entries.Count > 1)
                {
                    var firstPaymentType = model.Entries.First().PaymentType;
                    var firstRefNo = model.Entries.First().RefNoChequeUTR ?? "";

                    if (model.Entries.Any(e => e.PaymentType != firstPaymentType || (e.RefNoChequeUTR ?? "") != firstRefNo))
                    {
                        return (false, "PAYMENT TYPES OR REF. NO'S NOT MATCHED FOR ALL ENTRIES");
                    }
                }

                // 3. Delete existing active entries for this VoucherNo
                var existingEntries = await _context.ReceiptEntries
                    .Where(r => r.VoucherNo == voucherNo)
                    .ToListAsync();

                if (!existingEntries.Any())
                {
                    return (false, "Receipt Entry not found.");
                }

                // Capture old state for history
                var oldState = new {
                    ReceiptDate = existingEntries.First().ReceiptDate,
                    MobileNo = existingEntries.First().MobileNo,
                    Entries = existingEntries.Select(e => new {
                        e.Type, e.AccountId, e.AccountType, e.Amount, e.PaymentType, e.RefNoChequeUTR, e.Narration, e.PaymentFromSubGroupId
                    }).ToList()
                };

                // Capture existing Unit to preserve it if not provided
                var existingUnit = existingEntries.First().Unit;

                _context.ReceiptEntries.RemoveRange(existingEntries);
                await _context.SaveChangesAsync();

                // 4. Create new entries (preserving VoucherNo)
                foreach (var entryData in model.Entries)
                {
                    var receiptEntry = new ReceiptEntry
                    {
                        VoucherNo = voucherNo,
                        ReceiptDate = model.ReceiptDate,
                        MobileNo = model.MobileNo,
                        Type = entryData.Type,
                        AccountId = entryData.AccountId,
                        AccountType = entryData.AccountType,
                        PaymentType = entryData.PaymentType,
                        Amount = entryData.Amount,
                        RefNoChequeUTR = entryData.RefNoChequeUTR,
                        Narration = entryData.Narration,
                        Status = existingEntries.FirstOrDefault()?.Status ?? "Unapproved",
                        CreatedAt = DateTime.Now,
                        CreatedBy = currentUser,
                        IsActive = true,
                        PaymentFromSubGroupId = entryData.PaymentFromSubGroupId,
                        Unit = entryData.Unit ?? existingUnit, // Preserve Unit
                        EntryAccountId = entryData.EntryAccountId,
                        EntryForId = entryData.EntryForId,
                        EntryForName = entryData.EntryForName
                    };

                    _context.ReceiptEntries.Add(receiptEntry);
                }

                await _context.SaveChangesAsync();

                // History Logging
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        voucherNo, "Receipt", "Edit", currentUser, 
                        remarks: "Voucher Updated",
                        oldValues: JsonSerializer.Serialize(oldState),
                        newValues: JsonSerializer.Serialize(model));
                }
                catch { /* Ignore logging errors */ }
                await transaction.CommitAsync();

                return (true, "Receipt Entry updated successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Error updating voucher: " + ex.Message);
            }
        });
    }

    public async Task<(bool success, string message)> ApproveReceiptEntryAsync(int id)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var entry = await _context.ReceiptEntries.FindAsync(id);
            if (entry == null)
            {
                return (false, "Receipt entry not found.");
            }

            // Update all entries with the same VoucherNo to Approved
            var relatedEntries = await _context.ReceiptEntries
                .Where(r => r.VoucherNo == entry.VoucherNo && r.IsActive)
                .ToListAsync();

            foreach (var relatedEntry in relatedEntries)
            {
                relatedEntry.Status = "Approved";
                relatedEntry.UpdatedAt = DateTime.Now;
                relatedEntry.UpdatedBy = currentUser;
                _context.Update(relatedEntry);
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    entry.VoucherNo, "Receipt", "Approve", currentUser, 
                    remarks: "Voucher Approved");
            }
            catch { /* Ignore */ }

            return (true, "Receipt entry approved successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error approving entry: " + ex.Message);
        }
    }

}

