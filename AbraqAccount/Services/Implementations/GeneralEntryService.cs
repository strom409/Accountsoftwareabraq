using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting; // For IWebHostEnvironment
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using AbraqAccount.Models.Common;
using AbraqAccount.Extensions;

namespace AbraqAccount.Services.Implementations;

public class GeneralEntryService : IGeneralEntryService
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _webHostEnvironment;
    private readonly ITransactionEntriesService _transactionService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GeneralEntryService(AppDbContext context, IWebHostEnvironment webHostEnvironment, ITransactionEntriesService transactionService, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _webHostEnvironment = webHostEnvironment;
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

    public async Task<(List<GeneralEntry> entries, int totalCount, int totalPages)> GetGeneralEntriesAsync(
        string? voucherNo,
        DateTime? fromDate,
        DateTime? toDate,
        string? debitAccount,
        string? creditAccount,
        string? type,
        string? unit,
        string? status,
        int page,
        int pageSize)
    {
        var query = _context.GeneralEntries.Where(g => g.IsActive).AsQueryable();

        if (!string.IsNullOrEmpty(unit) && unit != "ALL")
        {
            query = query.Where(g => g.Unit == unit);
        }

        if (!string.IsNullOrEmpty(voucherNo))
        {
            query = query.Where(g => g.VoucherNo.Contains(voucherNo));
        }

        if (fromDate.HasValue)
        {
            query = query.Where(g => g.EntryDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(g => g.EntryDate <= toDate.Value);
        }

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(g => g.Type == type);
        }

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(g => g.Status == status);
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var entries = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        // Load navigation properties manually
        foreach (var entry in entries)
        {
            await LoadAccountNamesAsync(entry);

        }

        return (entries, totalCount, totalPages);
    }

    public async Task<(List<GeneralEntryGroupViewModel> groups, int totalCount, int totalPages)> GetJournalGroupsAsync(
        string? unit,
        string? noteNo,
        string? status,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize)
    {
        var query = _context.GeneralEntries.Where(g => g.IsActive).AsQueryable();

        if (!string.IsNullOrEmpty(unit) && unit != "ALL") query = query.Where(g => g.Unit == unit);
        if (!string.IsNullOrEmpty(noteNo)) query = query.Where(g => g.VoucherNo.Contains(noteNo));
        if (!string.IsNullOrEmpty(status)) query = query.Where(g => g.Status == status);
        if (fromDate.HasValue) query = query.Where(g => g.EntryDate >= fromDate.Value);
        if (fromDate.HasValue) query = query.Where(g => g.EntryDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(g => g.EntryDate <= toDate.Value);

        // Exclude 'Grower Book' entries (which start with GBK/) as they have their own tab
        // VoucherType is NotMapped, so we must filter by VoucherNo pattern or other DB column
        query = query.Where(g => !g.VoucherNo.StartsWith("GBK/"));

        // Fetch all matching entries to group effectively (pagination is tricky with grouping, so filtering first is key)
        var allEntries = await query.OrderByDescending(g => g.CreatedAt).ToListAsync();

        var groupedEntries = allEntries
            .GroupBy(g => g.VoucherNo)
            .Select(g => new GeneralEntryGroupViewModel
            {
                VoucherNo = g.Key,
                Date = g.First().EntryDate,
                Entries = g.ToList(),
                TotalDebit = g.Sum(e => e.Amount),
                Status = g.First().Status,
                Unit = g.First().Unit,
                Id = g.First().Id
            })
            .ToList();

        var totalCount = groupedEntries.Count;
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var pagedGroups = groupedEntries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        // Load account names
        foreach (var group in pagedGroups)
        {
            foreach (var entry in group.Entries)
            {
                await LoadAccountDetailsAsync(entry);
                await LoadAccountDetailsAsync(entry, isCredit: true);
            }
        }

        return (pagedGroups, totalCount, totalPages);
    }

    private async Task LoadAccountDetailsAsync(GeneralEntry entry, bool isCredit = false)
    {
        string type = isCredit ? entry.CreditAccountType : entry.DebitAccountType;
        int id = isCredit ? entry.CreditAccountId : entry.DebitAccountId;

        if (type == AccountTypes.MasterGroup)
        {
            var mg = await _context.MasterGroups.FindAsync(id);
            if(isCredit) entry.CreditMasterGroup = mg; else entry.DebitMasterGroup = mg;
        }
        else if (type == AccountTypes.MasterSubGroup)
        {
            var msg = await _context.MasterSubGroups.Include(m => m.MasterGroup).FirstOrDefaultAsync(m => m.Id == id);
            if(isCredit) entry.CreditMasterSubGroup = msg; else entry.DebitMasterSubGroup = msg;
        }
        else if (type == AccountTypes.SubGroupLedger)
        {
            var sgl = await _context.SubGroupLedgers.Include(s => s.MasterGroup).Include(s => s.MasterSubGroup).FirstOrDefaultAsync(s => s.Id == id);
             if(isCredit) entry.CreditSubGroupLedger = sgl; else entry.DebitSubGroupLedger = sgl;
        }
        else if (type == AccountTypes.BankMaster)
        {
            var bm = await _context.BankMasters.FindAsync(id);
            if (isCredit) entry.CreditBankMasterInfo = bm; else entry.DebitBankMasterInfo = bm;
        }
        else if (type == AccountTypes.Farmer)
        {
            var farmer = await _context.Farmers.FindAsync(id);
            if (isCredit) entry.CreditFarmer = farmer; else entry.DebitFarmer = farmer;
        }
    }

    public async Task<(bool success, string message)> CreateGeneralEntryAsync(GeneralEntry generalEntry, IFormFile? imageFile)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            // Handle image upload
            string? imagePath = null;
            if (imageFile != null && imageFile.Length > 0)
            {
                // Validate file type
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var fileExtension = Path.GetExtension(imageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return (false, "Invalid image file type. Allowed types: JPG, JPEG, PNG, GIF, BMP, WEBP");
                }

                // Validate file size (max 5MB)
                if (imageFile.Length > 5 * 1024 * 1024)
                {
                    return (false, "Image size should be less than 5MB.");
                }

                // Create uploads directory if it doesn't exist
                var uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "uploads", "generalentries");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                // Generate unique filename
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                // Store relative path
                imagePath = $"/uploads/generalentries/{fileName}";
            }

            // Generate Voucher Number (e.g., JBK/A/24-25/00320)
            var lastEntry = await _context.GeneralEntries
                .OrderByDescending(g => g.Id)
                .FirstOrDefaultAsync();
            
            int nextNumber = 1;
            if (lastEntry != null && !string.IsNullOrEmpty(lastEntry.VoucherNo))
            {
                // Extract number from format JBK/A/24-25/00320
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
            generalEntry.VoucherNo = $"JBK/A/{yearShort}-{nextYear}/{nextNumber:D5}";

            generalEntry.CreatedAt = DateTime.Now;
            generalEntry.CreatedBy = GetCurrentUsername();
            generalEntry.Status = generalEntry.Status ?? "Unapproved";
            generalEntry.IsActive = true;

            _context.GeneralEntries.Add(generalEntry);
            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    generalEntry.VoucherNo, "Journal", "Insert", currentUser, 
                    remarks: "Voucher Created",
                    newValues: JsonSerializer.Serialize(generalEntry));
            }
            catch { /* Ignore */ }

            // Update ImagePath using raw SQL if column exists and image was uploaded
            if (!string.IsNullOrEmpty(imagePath))
            {
                try
                {
                    await _context.Database.ExecuteSqlRawAsync(
                        "UPDATE [dbo].[GeneralEntries] SET [ImagePath] = {0} WHERE [Id] = {1}",
                        imagePath, generalEntry.Id);
                }
                catch (Exception ex)
                {
                    // Column might not exist yet - log but don't fail
                    Console.WriteLine($"Warning: Could not save ImagePath. Please run ADD_IMAGEPATH_TO_GENERAL_ENTRIES.sql: {ex.Message}");
                }
            }

            return (true, "Journal Entry Book created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> CreateMultipleEntriesAsync(GeneralEntryBatchModel model)
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

            if (totalDebit != totalCredit)
            {
                return (false, $"Entry is not balanced. Total Debit ({totalDebit:F2}) must be equal to Total Credit ({totalCredit:F2}). Difference: {Math.Abs(totalDebit - totalCredit):F2}");
            }

            // Validate Payment Type and Ref No for 2-account transactions
            if (model.Entries.Count == 2)
            {
                var debitEntry = model.Entries.FirstOrDefault(e => e.Type == "Debit");
                var creditEntry = model.Entries.FirstOrDefault(e => e.Type == "Credit");
                
                if (debitEntry != null && creditEntry != null)
                {
                    if (debitEntry.PaymentType != creditEntry.PaymentType || debitEntry.RefNoChequeUTR != creditEntry.RefNoChequeUTR)
                    {
                        return (false, "PAYMENT TYPES OR REF. NO'S NOT MATCHED");
                    }
                }
            }

            // Get default account for opposite side (the mediator)
            var mediatorAccount = await _context.MasterGroups
                .OrderBy(mg => mg.Id)
                .FirstOrDefaultAsync();
            
            int mediatorAccountId = mediatorAccount?.Id ?? 1;
            string mediatorAccountType = AccountTypes.MasterGroup;
            
            // Detection: Is this a 1-to-1 transaction?
            bool isSimpleTransaction = model.Entries.Count == 2 && 
                                      model.Entries.Any(e => e.Type == "Debit") && 
                                      model.Entries.Any(e => e.Type == "Credit");

            // Generate Voucher Number (e.g., JBK/A/24-25/00320)
            var lastEntry = await _context.GeneralEntries
                .OrderByDescending(g => g.Id)
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
            var voucherNo = $"JBK/A/{yearShort}-{nextYear}/{nextNumber:D5}";

            // Save entries
            if (isSimpleTransaction)
            {
                // MERGE: Handle single row for 1-to-1 transaction
                var debitData = model.Entries.First(e => e.Type == "Debit");
                var creditData = model.Entries.First(e => e.Type == "Credit");

                // Map accounts
                int debitAccountId = debitData.AccountId;
                string debitAccountType = debitData.AccountType;

                int creditAccountId = creditData.AccountId;
                string creditAccountType = creditData.AccountType;

                var mergedEntry = new GeneralEntry
                {
                    VoucherNo = voucherNo,
                    EntryDate = model.EntryDate,
                    DebitAccountId = debitAccountId,
                    DebitAccountType = debitAccountType,
                    CreditAccountId = creditAccountId,
                    CreditAccountType = creditAccountType,
                    Amount = debitData.Amount, // Same for both in 1-to-1
                    Type = debitData.PaymentType, 
                    Narration = (!string.IsNullOrEmpty(debitData.RefNoChequeUTR) ? $"Ref: {debitData.RefNoChequeUTR}. " : "") + (debitData.Narration ?? ""),
                    ReferenceNo = debitData.RefNoChequeUTR,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser,
                    Status = "Unapproved",
                    IsActive = true,
                    Unit = debitData.Unit,
                    EntryAccountId = debitData.EntryAccountId,
                    EntryForId = debitData.EntryForId,
                    EntryForName = debitData.EntryForName,
                    PaymentFromSubGroupId = debitData.PaymentFromSubGroupId,
                    PaymentFromSubGroupName = debitData.PaymentFromSubGroupName
                };
                _context.GeneralEntries.Add(mergedEntry);
            }
            else
            {
                // STANDARD: Save multiple entries with mediator
                foreach (var entryData in model.Entries)
                {
                    int debitAccountId, creditAccountId;
                    string debitAccountType, creditAccountType;

                    int mappedAccountId = entryData.AccountId;
                    string mappedAccountType = entryData.AccountType;
                    
                    if (entryData.Type == "Debit")
                    {
                        debitAccountId = mappedAccountId;
                        debitAccountType = mappedAccountType;
                        creditAccountId = mediatorAccountId;
                        creditAccountType = mediatorAccountType;
                    }
                    else // Credit
                    {
                        creditAccountId = mappedAccountId;
                        creditAccountType = mappedAccountType;
                        debitAccountId = mediatorAccountId;
                        debitAccountType = mediatorAccountType;
                    }

                    var generalEntry = new GeneralEntry
                    {
                        VoucherNo = voucherNo,
                        EntryDate = model.EntryDate,
                        DebitAccountId = debitAccountId,
                        DebitAccountType = debitAccountType,
                        CreditAccountId = creditAccountId,
                        CreditAccountType = creditAccountType,
                        Amount = entryData.Amount,
                        Type = entryData.PaymentType,
                        Narration = (!string.IsNullOrEmpty(entryData.RefNoChequeUTR) ? $"Ref: {entryData.RefNoChequeUTR}. " : "") + (entryData.Narration ?? ""),
                        ReferenceNo = entryData.RefNoChequeUTR,
                        CreatedAt = DateTime.Now,
                        CreatedBy = currentUser,
                        Status = "Unapproved",
                        IsActive = true,
                        Unit = entryData.Unit,
                        PaymentFromSubGroupId = entryData.PaymentFromSubGroupId,
                        PaymentFromSubGroupName = entryData.PaymentFromSubGroupName,
                        EntryAccountId = entryData.EntryAccountId,
                        EntryForId = entryData.EntryForId,
                        EntryForName = entryData.EntryForName
                    };

                    _context.GeneralEntries.Add(generalEntry);
                }
            }

            await _context.SaveChangesAsync();

            // History Logging
            try
            {
                await _transactionService.LogTransactionHistoryAsync(
                    voucherNo, "Journal", "Insert", currentUser, 
                    remarks: "Voucher Created",
                    newValues: JsonSerializer.Serialize(model));
            }
            catch { /* Ignore */ }

            return (true, "Journal Entry Book created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "An error occurred while saving: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> ApproveEntryAsync(int id)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var entry = await _context.GeneralEntries.FindAsync(id);
            if (entry != null)
            {
                // Update all entries with the same VoucherNo to Approved
                var relatedEntries = await _context.GeneralEntries
                    .Where(g => g.VoucherNo == entry.VoucherNo && g.IsActive)
                    .ToListAsync();

                foreach (var rel in relatedEntries)
                {
                    rel.Status = "Approved";
                    rel.UpdatedAt = DateTime.Now;
                    rel.UpdatedBy = currentUser;
                }

                await _context.SaveChangesAsync();

                // History Logging
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        entry.VoucherNo, "Journal", "Approve", currentUser, 
                        remarks: "Voucher Approved");
                }
                catch { /* Ignore */ }

                return (true, "Journal Entry Book approved successfully!");
            }
            return (false, "Journal Entry Book not found.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> UnapproveEntryAsync(int id)
    {
        try
        {
            var currentUser = GetCurrentUsername();
            var entry = await _context.GeneralEntries.FindAsync(id);
            if (entry != null)
            {
                // Update all entries with the same VoucherNo to Unapproved
                var relatedEntries = await _context.GeneralEntries
                    .Where(g => g.VoucherNo == entry.VoucherNo && g.IsActive)
                    .ToListAsync();

                foreach (var rel in relatedEntries)
                {
                    rel.Status = "Unapproved";
                    rel.UpdatedAt = DateTime.Now;
                    rel.UpdatedBy = currentUser;
                }

                await _context.SaveChangesAsync();

                // History Logging
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        entry.VoucherNo, "Journal", "Unapprove", currentUser, 
                        remarks: "Voucher Unapproved");
                }
                catch { /* Ignore */ }

                return (true, "Journal Entry Book unapproved successfully!");
            }
            return (false, "Journal Entry Book not found.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> DeleteEntryAsync(int id)
    {
        var currentUser = GetCurrentUsername();
        var entry = await _context.GeneralEntries.FindAsync(id);
        if (entry == null)
        {
            return (false, "Entry not found.");
        }

        try
        {
            // Delete all entries with same VoucherNo
            var relatedEntries = await _context.GeneralEntries
                .Where(g => g.VoucherNo == entry.VoucherNo)
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
                    entry.VoucherNo, "Journal", "Delete", currentUser, 
                    remarks: "Voucher Deleted");
            }
            catch { /* Ignore */ }

            return (true, "Journal entry deleted successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error deleting entry: " + ex.Message);
        }
    }

    public async Task<GeneralEntry?> GetEntryByIdAsync(int id)
    {
         var entry = await _context.GeneralEntries.FindAsync(id);
         if (entry != null)
         {
            await LoadAccountNamesAsync(entry);
         }
         return entry;
    }

    public async Task<List<GeneralEntry>> GetVoucherEntriesAsync(string voucherNo)
    {
        var entries = await _context.GeneralEntries
            .Where(g => g.VoucherNo == voucherNo)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync();
            
        foreach(var entry in entries)
        {
            await LoadAccountNamesAsync(entry);
        }
        return entries;
    }

    public async Task<IEnumerable<LookupItem>> GetAccountsAsync(string? searchTerm, int? paymentFromId = null, string? type = null)
    {
        var rules = await _context.AccountRules
            .Where(r => r.RuleType == "AllowedNature")
            .ToListAsync();

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

        if (paymentFromId.HasValue)
        {
            var profileRules = rules.Where(r => r.EntryAccountId == paymentFromId.Value).ToList();

            var allowedSubGroupIds = profileRules
                .Where(r => r.AccountType == "SubGroupLedger" && CheckRule(r.Value, type))
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
                .Where(f => allowedFarmerIds.Contains(f.Id))
                .OrderBy(f => f.FarmerName)
                .Take(50)
                .ToListAsync();

            var allAccounts = new List<LookupItem>();
            allAccounts.AddRange(bankMasters.Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = "BankMaster" }));
            allAccounts.AddRange(farmers.Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = "Farmer" }));

            return allAccounts.OrderBy(a => a.Name).Take(100).ToList();
        }

        var globalBankMastersQuery = _context.BankMasters.Where(bm => bm.IsActive);
        var globalSubGroupLedgersQuery = _context.SubGroupLedgers.Where(s => s.IsActive);
        var globalFarmersQuery = _context.Farmers.Where(f => f.IsActive);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            globalBankMastersQuery = globalBankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
            globalSubGroupLedgersQuery = globalSubGroupLedgersQuery.Where(s => s.Name.Contains(searchTerm));
            globalFarmersQuery = globalFarmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
        }

        var globalBankMasters = await globalBankMastersQuery.OrderBy(bm => bm.AccountName).Take(50).ToListAsync();
        var globalSubGroupLedgers = await globalSubGroupLedgersQuery.OrderBy(s => s.Name).Take(50).ToListAsync();
        var globalFarmers = await globalFarmersQuery.OrderBy(f => f.FarmerName).Take(50).ToListAsync();

        var globalAccounts = new List<LookupItem>();
        globalAccounts.AddRange(globalBankMasters
            .Where(bm => IsAllowed(AccountTypes.BankMaster, bm.Id, AccountTypes.SubGroupLedger, bm.GroupId))
            .Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = AccountTypes.BankMaster }));
        globalAccounts.AddRange(globalSubGroupLedgers
            .Where(sgl => IsAllowed(AccountTypes.SubGroupLedger, sgl.Id))
            .Select(sgl => new LookupItem { Id = sgl.Id, Name = sgl.Name, Type = AccountTypes.SubGroupLedger }));
        globalAccounts.AddRange(globalFarmers
            .Where(f => IsAllowed(AccountTypes.Farmer, f.Id, AccountTypes.GrowerGroup, f.GroupId))
            .Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = AccountTypes.Farmer }));

        return globalAccounts.OrderBy(a => a.Name).Take(100).ToList();
    }
    

    public async Task<IEnumerable<object>> GetExpenseGroupsAsync(string? searchTerm)
    {
        var query = _context.MasterGroups.AsQueryable();
        
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(mg => mg.Name.Contains(searchTerm));
        }
        
        return await query
            .OrderBy(mg => mg.Name)
            .Take(50)
            .Select(mg => new { id = mg.Id, name = mg.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetExpenseSubGroupsAsync(int? groupId, string? searchTerm)
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
            .Select(msg => new { 
                id = msg.Id, 
                name = msg.MasterGroup != null ? $"{msg.MasterGroup.Name} - {msg.Name}" : msg.Name 
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetVendorGroupsAsync(string? searchTerm)
    {
        return await GetExpenseGroupsAsync(searchTerm);
    }
    
    public async Task<List<string>> GetUniqueTypesAsync()
    {
        return await _context.GeneralEntries
            .Where(g => !string.IsNullOrEmpty(g.Type))
            .Select(g => g.Type)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetSubGroupLedgersAsync()
    {
        return await _context.SubGroupLedgers
            .Where(s => s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new { Id = s.Id, Name = s.Name })
            .ToListAsync();
    }

    public async Task<IEnumerable<LookupItem>> GetEntryProfilesAsync(string transactionType)
    {
        return await _context.EntryForAccounts
            .Where(e => e.TransactionType == transactionType)
            .OrderBy(e => e.AccountName)
            .Select(e => new LookupItem { Id = e.Id, Name = e.AccountName })
            .ToListAsync();
    }

    public async Task<(bool success, string message)> AddTypeColumnAsync()
    {
        try
        {
            var sql = @"
                IF NOT EXISTS (
                    SELECT * 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'GeneralEntries' 
                    AND COLUMN_NAME = 'Type'
                )
                BEGIN
                    ALTER TABLE [dbo].[GeneralEntries]
                    ADD [Type] NVARCHAR(100) NULL;
                END
            ";
            
            await _context.Database.ExecuteSqlRawAsync(sql);
            return (true, "Type column added successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> UpdateVoucherAsync(string voucherNo, GeneralEntryBatchModel model)
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
                
                if (totalDebit != totalCredit)
                {
                    return (false, $"Entry is not balanced. Total Debit ({totalDebit:F2}) must be equal to Total Credit ({totalCredit:F2}). Details: Diff {Math.Abs(totalDebit - totalCredit):F2}");
                }

                // 2. Validate Payment Type and Ref No for 2-account transactions
                if (model.Entries.Count == 2)
                {
                    var debitEntry = model.Entries.FirstOrDefault(e => e.Type == "Debit");
                    var creditEntry = model.Entries.FirstOrDefault(e => e.Type == "Credit");
                    
                    if (debitEntry != null && creditEntry != null)
                    {
                        if (debitEntry.PaymentType != creditEntry.PaymentType || debitEntry.RefNoChequeUTR != creditEntry.RefNoChequeUTR)
                        {
                            return (false, "PAYMENT TYPES OR REF. NO'S NOT MATCHED");
                        }
                    }
                }

                // 3. Delete existing active entries for this VoucherNo
                var existingEntries = await _context.GeneralEntries
                    .Where(g => g.VoucherNo == voucherNo)
                    .ToListAsync();

                if (!existingEntries.Any())
                {
                    return (false, "Journal Entry not found.");
                }

                // Capture old state for history
                var oldState = new {
                    EntryDate = existingEntries.First().EntryDate,
                    Entries = existingEntries.Select(e => new {
                        e.DebitAccountId, e.DebitAccountType, e.CreditAccountId, e.CreditAccountType, e.Amount, e.Type, e.Narration, e.ReferenceNo, e.PaymentFromSubGroupId
                    }).ToList()
                };

                // Capture existing Unit to preserve it if not provided
                var existingUnit = existingEntries.First().Unit;

                // Hard delete or Soft delete? Using Hard delete for update to keep history clean or Soft Delete then Insert?
                _context.GeneralEntries.RemoveRange(existingEntries);
                await _context.SaveChangesAsync();

                // 4. Create new entries (Using logic similar to CreateMultiple, but preserving VoucherNo)
                
                // Get default account (the mediator)
                var mediatorAccount = await _context.MasterGroups.OrderBy(mg => mg.Id).FirstOrDefaultAsync();
                int mediatorAccountId = mediatorAccount?.Id ?? 1;
                string mediatorAccountType = AccountTypes.MasterGroup;

                // Detection: Is this a 1-to-1 transaction?
                bool isSimpleTransaction = model.Entries.Count == 2 && 
                                          model.Entries.Any(e => e.Type == "Debit") && 
                                          model.Entries.Any(e => e.Type == "Credit");

                if (isSimpleTransaction)
                {
                    // MERGE: Handle single row for 1-to-1 transaction
                    var debitData = model.Entries.First(e => e.Type == "Debit");
                    var creditData = model.Entries.First(e => e.Type == "Credit");

                    int debitAccountId = debitData.AccountId;
                    string debitAccountType = debitData.AccountType;

                    int creditAccountId = creditData.AccountId;
                    string creditAccountType = creditData.AccountType;

                    var mergedEntry = new GeneralEntry
                    {
                        VoucherNo = voucherNo,
                        EntryDate = model.EntryDate,
                        DebitAccountId = debitAccountId,
                        DebitAccountType = debitAccountType,
                        CreditAccountId = creditAccountId,
                        CreditAccountType = creditAccountType,
                        Amount = debitData.Amount,
                        Type = debitData.PaymentType, 
                        Narration = (!string.IsNullOrEmpty(debitData.RefNoChequeUTR) ? $"Ref: {debitData.RefNoChequeUTR}. " : "") + (debitData.Narration ?? ""),
                        ReferenceNo = debitData.RefNoChequeUTR,
                        CreatedAt = DateTime.Now,
                        CreatedBy = currentUser,
                        Status = existingEntries.FirstOrDefault()?.Status ?? "Unapproved",
                        IsActive = true,
                        Unit = debitData.Unit ?? existingUnit, // Preserve unit
                        PaymentFromSubGroupId = debitData.PaymentFromSubGroupId,
                        PaymentFromSubGroupName = debitData.PaymentFromSubGroupName,
                        EntryAccountId = debitData.EntryAccountId,
                        EntryForId = debitData.EntryForId,
                        EntryForName = debitData.EntryForName
                    };
                    _context.GeneralEntries.Add(mergedEntry);
                }
                else
                {
                    // STANDARD: Save multiple entries with mediator
                    foreach (var entryData in model.Entries)
                    {
                        int debitAccountId, creditAccountId;
                        string debitAccountType, creditAccountType;

                        int mappedAccountId = entryData.AccountId;
                        string mappedAccountType = entryData.AccountType;

                        if (entryData.Type == "Debit")
                        {
                            debitAccountId = mappedAccountId;
                            debitAccountType = mappedAccountType;
                            creditAccountId = mediatorAccountId;
                            creditAccountType = mediatorAccountType;
                        }
                        else
                        {
                            creditAccountId = mappedAccountId;
                            creditAccountType = mappedAccountType;
                            debitAccountId = mediatorAccountId;
                            debitAccountType = mediatorAccountType;
                        }

                        var generalEntry = new GeneralEntry
                        {
                            VoucherNo = voucherNo,
                            EntryDate = model.EntryDate,
                            DebitAccountId = debitAccountId,
                            DebitAccountType = debitAccountType,
                            CreditAccountId = creditAccountId,
                            CreditAccountType = creditAccountType,
                            Amount = entryData.Amount,
                            Type = entryData.PaymentType,
                            Narration = (!string.IsNullOrEmpty(entryData.RefNoChequeUTR) ? $"Ref: {entryData.RefNoChequeUTR}. " : "") + (entryData.Narration ?? ""),
                            ReferenceNo = entryData.RefNoChequeUTR,
                            CreatedAt = DateTime.Now,
                            Status = existingEntries.FirstOrDefault()?.Status ?? "Unapproved",
                            IsActive = true,
                            Unit = entryData.Unit ?? existingUnit, // Preserve unit
                            PaymentFromSubGroupId = entryData.PaymentFromSubGroupId,
                            PaymentFromSubGroupName = entryData.PaymentFromSubGroupName,
                            EntryAccountId = entryData.EntryAccountId,
                            EntryForId = entryData.EntryForId,
                            EntryForName = entryData.EntryForName
                        };

                        _context.GeneralEntries.Add(generalEntry);
                    }
                }

                await _context.SaveChangesAsync();

                // History Logging
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        voucherNo, "Journal", "Edit", currentUser, 
                        remarks: "Voucher Updated",
                        oldValues: JsonSerializer.Serialize(oldState),
                        newValues: JsonSerializer.Serialize(model));
                }
                catch { /* Ignore */ }

                await transaction.CommitAsync();

                return (true, "Journal Entry updated successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Error updating voucher: " + ex.Message);
            }
        });
    }

    private async Task LoadAccountNamesAsync(GeneralEntry entry)
    {
        // Load Debit Account
        if (entry.DebitAccountType == AccountTypes.MasterGroup)
        {
            entry.DebitMasterGroup = await _context.MasterGroups.FindAsync(entry.DebitAccountId);
        }
        else if (entry.DebitAccountType == AccountTypes.MasterSubGroup)
        {
            entry.DebitMasterSubGroup = await _context.MasterSubGroups
                .Include(msg => msg.MasterGroup)
                .FirstOrDefaultAsync(msg => msg.Id == entry.DebitAccountId);
        }
        else if (entry.DebitAccountType == AccountTypes.SubGroupLedger)
        {
            entry.DebitSubGroupLedger = await _context.SubGroupLedgers
                .Include(sgl => sgl.MasterGroup)
                .Include(sgl => sgl.MasterSubGroup)
                .FirstOrDefaultAsync(sgl => sgl.Id == entry.DebitAccountId);
        }
        else if (entry.DebitAccountType == AccountTypes.BankMaster)
        {
            entry.DebitBankMasterInfo = await _context.BankMasters
                .Include(b => b.Group)
                .FirstOrDefaultAsync(b => b.Id == entry.DebitAccountId);
        }
        else if (entry.DebitAccountType == AccountTypes.Farmer)
        {
            entry.DebitFarmer = await _context.Farmers
                .Include(f => f.GrowerGroup)
                .FirstOrDefaultAsync(f => f.Id == entry.DebitAccountId);
        }

        // Load Credit Account
        if (entry.CreditAccountType == AccountTypes.MasterGroup)
        {
            entry.CreditMasterGroup = await _context.MasterGroups.FindAsync(entry.CreditAccountId);
        }
        else if (entry.CreditAccountType == AccountTypes.MasterSubGroup)
        {
            entry.CreditMasterSubGroup = await _context.MasterSubGroups
                .Include(msg => msg.MasterGroup)
                .FirstOrDefaultAsync(msg => msg.Id == entry.CreditAccountId);
        }
        else if (entry.CreditAccountType == AccountTypes.SubGroupLedger)
        {
            entry.CreditSubGroupLedger = await _context.SubGroupLedgers
                .Include(sgl => sgl.MasterGroup)
                .Include(sgl => sgl.MasterSubGroup)
                .FirstOrDefaultAsync(sgl => sgl.Id == entry.CreditAccountId);
        }
        else if (entry.CreditAccountType == AccountTypes.BankMaster)
        {
            entry.CreditBankMasterInfo = await _context.BankMasters
                .Include(b => b.Group)
                .FirstOrDefaultAsync(b => b.Id == entry.CreditAccountId);
        }
        else if (entry.CreditAccountType == AccountTypes.Farmer)
        {
             entry.CreditFarmer = await _context.Farmers
                .Include(f => f.GrowerGroup)
                .FirstOrDefaultAsync(f => f.Id == entry.CreditAccountId);
        }

        // Load EntryForName if missing
    if (string.IsNullOrEmpty(entry.EntryForName))
    {
        int? profileId = entry.EntryForId ?? entry.EntryAccountId;
        if (profileId.HasValue && profileId > 0)
        {
            var profile = await _context.EntryForAccounts.FindAsync(profileId.Value);
            if (profile != null)
            {
                entry.EntryForName = profile.AccountName;
            }
        }
    }
    }


    public async Task<IEnumerable<object>> GetSubGroupLedgersAsync(int? masterSubGroupId, string? searchTerm)
    {
        var query = _context.SubGroupLedgers
            .Include(sgl => sgl.MasterGroup)
            .Include(sgl => sgl.MasterSubGroup)
            .Where(sgl => sgl.IsActive)
            .AsQueryable();

        if (masterSubGroupId.HasValue)
        {
            query = query.Where(sgl => sgl.MasterSubGroupId == masterSubGroupId.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(sgl => 
                sgl.Name.Contains(searchTerm) ||
                (sgl.MasterGroup != null && sgl.MasterGroup.Name.Contains(searchTerm)) ||
                (sgl.MasterSubGroup != null && sgl.MasterSubGroup.Name.Contains(searchTerm))
            );
        }

        return await query
            .OrderBy(sgl => sgl.Name)
            .Take(50)
            .Select(sgl => new { 
                id = sgl.Id, 
                name = sgl.Name // Just Name as per user request? Or including group? "Sub Group Ledger & Account".
                // User wants "Account" dropdown.
            })
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetAccountsByGroupIdAsync(int groupId)
    {
        // Fetch BankMaster accounts filtered by SubGroupLedger ID (GroupId)
        var accounts = await _context.BankMasters
            .Where(bm => bm.GroupId == groupId && bm.IsActive)
            .OrderBy(bm => bm.AccountName)
            .Select(bm => new { 
                id = bm.Id, 
                name = bm.AccountName,
                type = AccountTypes.BankMaster
            })
            .ToListAsync();
            
        return accounts;
    }

    public async Task<LedgerReportResult> GetLedgerReportAsync(int accountId, string accountType, DateTime fromDate, DateTime toDate)
    {
        var result = new LedgerReportResult();
        
        // 0. Calculate Opening Balance
        result.OpeningBalance = await CalculateBalanceUntilAsync(accountId, accountType, fromDate);
        
        var allEntries = new List<GeneralEntry>();
        
        // 1. Fetch GeneralEntries
        var query = _context.GeneralEntries
            .Where(g => 
                (g.EntryDate >= fromDate && g.EntryDate <= toDate)
            );
            
        if (accountType == "BankMaster")
        {
            // Filter by BankMaster on either debit or credit side
            query = query.Where(g =>
                (g.DebitAccountId == accountId && g.DebitAccountType == AccountTypes.BankMaster) ||
                (g.CreditAccountId == accountId && g.CreditAccountType == AccountTypes.BankMaster)
            );
        }
        else
        {
            // Dynamic filtering based on the passed accountType
            query = query.Where(g =>
                (g.DebitAccountId == accountId && g.DebitAccountType == accountType) ||
                (g.CreditAccountId == accountId && g.CreditAccountType == accountType)
            );
        }
        
        // 1. Journal Entries (GeneralEntries)
        var generalEntries = await query.ToListAsync();
        foreach (var entry in generalEntries)
        {
            await LoadAccountNamesAsync(entry);
            
            // Each GeneralEntry row is a balanced pair (1 Debit, 1 Credit).
            // We just need to determine which side our account is on and show the other side as Particulars.
            bool isOurDebit = (entry.DebitAccountId == accountId && entry.DebitAccountType == accountType);
            
            // Create a ledger entry representing this row's contribution to the selected account
            var ledgerEntry = CreateLedgerEntry(entry, entry, entry.Amount, isOurDebit);
            allEntries.Add(ledgerEntry);
        }
        
        // 2. Fetch ReceiptEntries (All Accounts)
        // Group by VoucherNo to handle breakdown in receipts
        var allReceiptEntriesForReport = await _context.ReceiptEntries
            .Where(r => 
                r.IsActive &&
                r.Status == "Approved" && 
                r.ReceiptDate >= fromDate && 
                r.ReceiptDate <= toDate
            )
            .ToListAsync();

        var matchingVouchers = allReceiptEntriesForReport
            .Where(r => r.AccountId == accountId && r.AccountType == accountType)
            .Select(r => r.VoucherNo)
            .Distinct()
            .ToList();

        foreach (var voucherNo in matchingVouchers)
        {
            // Get ALL entries for this specific voucher
            var allVoucherReceipts = await _context.ReceiptEntries
                .Where(r => r.VoucherNo == voucherNo && r.IsActive)
                .ToListAsync();

            var currentSideReceipts = allVoucherReceipts.Where(r => r.AccountId == accountId && r.AccountType == accountType).ToList();
            
            foreach (var entry in currentSideReceipts)
            {
                // Identify the actual opposite side entries based on the current side's Type (Debit vs Credit)
                var actualOppositeEntries = allVoucherReceipts.Where(r => r.Type != entry.Type).ToList();

                // Rule for Breakdown/Split:
                // If we have ONE entry on OUR side and MULTIPLE on the opposite side, split it.
                if (currentSideReceipts.Count(r => r.Type == entry.Type) == 1 && actualOppositeEntries.Count > 1)
                {
                    // Split logic for 1-to-Many
                    foreach (var opEntry in actualOppositeEntries)
                    {
                        var pairedAccountName = await GetAccountNameAsync(opEntry.AccountId, opEntry.AccountType);
                        var generalEntry = CreateReceiptLedgerEntry(entry, opEntry, opEntry.Amount, pairedAccountName);
                        allEntries.Add(generalEntry);
                    }
                }
                else
                {
                    // No split (1-to-1 or Many-to-1, or Many-to-Many fallback)
                    // We'll show the current entry against the first available opposite account
                    var opEntry = actualOppositeEntries.FirstOrDefault();
                    var pairedAccountName = opEntry != null ? await GetAccountNameAsync(opEntry.AccountId, opEntry.AccountType) : "N/A";
                    var generalEntry = CreateReceiptLedgerEntry(entry, opEntry, entry.Amount, pairedAccountName);
                    allEntries.Add(generalEntry);
                }
            }
        }

        // 3. Fetch Debit Notes if filtering by BankMaster (Vendor side)
        if (accountType == AccountTypes.BankMaster)
        {
            var debitNotes = await _context.DebitNotes
                .Where(d =>
                    d.IsActive &&
                    d.Status == "Approved" &&
                    d.DebitNoteDate >= fromDate &&
                    d.DebitNoteDate <= toDate
                )
                .ToListAsync();

            var mappings = await LoadDebitNoteBankMasterIdMappingsAsync();

            foreach (var note in debitNotes)
            {
                int noteBankMasterId = note.BankMasterId ?? 0;
                if (mappings.TryGetValue(note.Id, out int mappedId)) noteBankMasterId = mappedId;

                if (noteBankMasterId == accountId)
                {
                    // For each detail in the debit note, create a separate entry
                    var noteDetails = await _context.DebitNoteDetails
                        .Where(d => d.DebitNoteId == note.Id)
                        .ToListAsync();

                    if (noteDetails.Any())
                    {
                        foreach (var detail in noteDetails)
                        {
                            var generalEntry = new GeneralEntry
                            {
                                Id = note.Id * -100 - detail.Id, // Unique pseudo ID
                                VoucherNo = note.DebitNoteNo,
                                EntryDate = note.DebitNoteDate,
                                Amount = detail.Amount,
                                Narration = note.Narration,
                                Status = note.Status,
                                CreatedAt = note.CreatedAt,
                                DebitAccountId = noteBankMasterId,
                                DebitAccountType = AccountTypes.BankMaster,
                                VoucherType = "Debit Note",
                                Unit = note.Unit
                            };

                            // Set individual detail info as the "opposite" account info
                            generalEntry.CreditBankMasterInfo = new BankMaster { AccountName = detail.AccountType };
                            allEntries.Add(generalEntry);
                        }
                    }
                    else
                    {
                        // Fallback if no details
                        allEntries.Add(new GeneralEntry
                        {
                            Id = note.Id * -100,
                            VoucherNo = note.DebitNoteNo,
                            EntryDate = note.DebitNoteDate,
                            Amount = note.Amount ?? 0,
                            Narration = note.Narration,
                            Status = note.Status,
                            CreatedAt = note.CreatedAt,
                            DebitAccountId = noteBankMasterId,
                            DebitAccountType = AccountTypes.BankMaster,
                            VoucherType = "Debit Note",
                            CreditBankMasterInfo = new BankMaster { AccountName = "Items" }, // Default fallback name
                            Unit = note.Unit
                        });
                    }
                }
            }
        }

        // 4. Fetch Credit Notes if filtering by Farmer (Grower side)
        if (accountType == AccountTypes.Farmer)
        {
            var creditNotes = await _context.CreditNotes
                .Where(c =>
                    c.IsActive &&
                    c.Status == "Approved" &&
                    c.CreditNoteDate >= fromDate &&
                    c.CreditNoteDate <= toDate &&
                    c.FarmerId == accountId
                )
                .ToListAsync();

            foreach (var note in creditNotes)
            {
                var noteDetails = await _context.CreditNoteDetails
                    .Where(d => d.CreditNoteId == note.Id)
                    .ToListAsync();

                if (noteDetails.Any())
                {
                    foreach (var detail in noteDetails)
                    {
                        allEntries.Add(new GeneralEntry
                        {
                            Id = note.Id * -1000 - detail.Id, // Unique pseudo ID
                            VoucherNo = note.CreditNoteNo,
                            EntryDate = note.CreditNoteDate,
                            Amount = detail.Amount,
                            Narration = note.Narration,
                            Status = note.Status,
                            CreatedAt = note.CreatedAt,
                            CreditAccountId = note.FarmerId ?? 0,
                            CreditAccountType = AccountTypes.Farmer,

                            VoucherType = "Credit Note",
                            Unit = note.Unit,
                            // Set individual detail info as the "opposite" account info
                            DebitBankMasterInfo = new BankMaster { AccountName = detail.AccountType }
                        });
                    }
                }
                else
                {
                    allEntries.Add(new GeneralEntry
                    {
                        Id = note.Id * -1000, // Unique pseudo ID
                        VoucherNo = note.CreditNoteNo,
                        EntryDate = note.CreditNoteDate,
                        Amount = note.Amount ?? 0,
                        Narration = note.Narration,
                        Status = note.Status,
                        CreatedAt = note.CreatedAt,
                        CreditAccountId = note.FarmerId ?? 0,
                        CreditAccountType = AccountTypes.Farmer,
                        VoucherType = "Credit Note",
                        DebitBankMasterInfo = new BankMaster { AccountName = "Items" }, // Default fallback name
                        Unit = note.Unit
                    });
                }
            }
        }

        // 5. Payment Settlements
        // Fetch all matching PANumbers first to avoid N+1 queries later if possible, 
        // but for simplicity and correctness, we'll fetch them in a way that allows finding the opposite side.
        var matchedSettlements = await _context.PaymentSettlements
            .Where(s => 
                s.IsActive && 
                s.ApprovalStatus == "Approved" &&
                s.SettlementDate >= fromDate && s.SettlementDate <= toDate &&
                (
                    (s.AccountId == accountId && s.AccountType == accountType) ||
                    (s.EntryForId == accountId && accountType == AccountTypes.SubGroupLedger)
                )
            )
            .ToListAsync();

        var paNumbers = matchedSettlements.Select(s => s.PANumber).Distinct().ToList();
        var allRelatedSettlements = await _context.PaymentSettlements
            .Where(s => paNumbers.Contains(s.PANumber) && s.IsActive)
            .ToListAsync();

        foreach (var s in matchedSettlements)
        {
            // Find the opposite side in the same batch
            var oppositeEntries = allRelatedSettlements
                .Where(r => r.PANumber == s.PANumber && r.Type != s.Type)
                .ToList();

            string oppositeName = "N/A";
            if (oppositeEntries.Any())
            {
                // Join names if multiple (though usually it's 1-to-1)
                oppositeName = string.Join(", ", oppositeEntries.Select(oe => oe.AccountName).Distinct());
            }

            var ge = new GeneralEntry
            {
                Id = s.Id * -5000, 
                VoucherNo = s.PANumber,
                EntryDate = s.SettlementDate,
                Amount = s.Amount,
                Narration = s.Narration,
                Status = s.ApprovalStatus,
                CreatedAt = s.CreatedAt,
                VoucherType = "Payment Settlement",
                PaymentType = s.PaymentType,
                Unit = s.Unit
            };

            // Setup the side we are viewing
            bool isDebit = (s.Type == "Debit" || s.Type == "Payment");
            if (isDebit)
            {
                ge.DebitAccountId = s.AccountId;
                ge.DebitAccountType = s.AccountType;
                ge.CreditBankMasterInfo = new BankMaster { AccountName = oppositeName };
            }
            else
            {
                ge.CreditAccountId = s.AccountId;
                ge.CreditAccountType = s.AccountType;
                ge.DebitBankMasterInfo = new BankMaster { AccountName = oppositeName };
            }

            allEntries.Add(ge);
        }
        
        // 6. Sort all entries by date
        result.Entries = allEntries
            .OrderBy(e => e.EntryDate)
            .ThenBy(e => e.Id)
            .ToList();

        // Calculate Closing Balance
        decimal bal = result.OpeningBalance;
        foreach (var entry in result.Entries)
        {
            bool isOurDebit = (entry.DebitAccountId == accountId && entry.DebitAccountType == accountType);
            decimal d = isOurDebit ? entry.Amount : 0;
            decimal c = isOurDebit ? 0 : entry.Amount;
            bal += (c - d);
        }
        result.ClosingBalance = bal;

        return result;
    }

    private async Task<decimal> CalculateBalanceUntilAsync(int accountId, string accountType, DateTime untilDate)
    {
        // 1. GeneralEntries (Journal)
        var geDebit = await _context.GeneralEntries
            .Where(g => g.DebitAccountId == accountId && g.DebitAccountType == accountType && g.EntryDate < untilDate)
            .SumAsync(g => (decimal?)g.Amount) ?? 0;
        var geCredit = await _context.GeneralEntries
            .Where(g => g.CreditAccountId == accountId && g.CreditAccountType == accountType && g.EntryDate < untilDate)
            .SumAsync(g => (decimal?)g.Amount) ?? 0;

        // 2. ReceiptEntries
        var reDebit = await _context.ReceiptEntries
            .Where(r => r.IsActive && r.Status == "Approved" && r.AccountId == accountId && r.AccountType == accountType && (r.Type == "Debit" || r.Type == "Payment") && r.ReceiptDate < untilDate)
            .SumAsync(r => (decimal?)r.Amount) ?? 0;
        var reCredit = await _context.ReceiptEntries
            .Where(r => r.IsActive && r.Status == "Approved" && r.AccountId == accountId && r.AccountType == accountType && (r.Type == "Credit" || r.Type == "Receipt") && r.ReceiptDate < untilDate)
            .SumAsync(r => (decimal?)r.Amount) ?? 0;

        // 3. PaymentSettlements
        var psDebit = await _context.PaymentSettlements
            .Where(s => s.IsActive && s.ApprovalStatus == "Approved" && s.AccountId == accountId && s.AccountType == accountType && (s.Type == "Debit" || s.Type == "Payment") && s.SettlementDate < untilDate)
            .SumAsync(s => (decimal?)s.Amount) ?? 0;
        var psCredit = await _context.PaymentSettlements
            .Where(s => s.IsActive && s.ApprovalStatus == "Approved" && s.AccountId == accountId && s.AccountType == accountType && (s.Type == "Credit" || s.Type == "Receipt") && s.SettlementDate < untilDate)
            .SumAsync(s => (decimal?)s.Amount) ?? 0;

        // 4. Debit Notes
        decimal dnAmount = 0;
        if (accountType == AccountTypes.BankMaster)
        {
             var mappings = await LoadDebitNoteBankMasterIdMappingsAsync();
             var debitNotes = await _context.DebitNotes
                .Where(d => d.IsActive && d.Status == "Approved" && d.DebitNoteDate < untilDate)
                .ToListAsync();
             foreach(var note in debitNotes)
             {
                 int noteBankMasterId = note.BankMasterId ?? 0;
                 if (mappings.TryGetValue(note.Id, out int mappedId)) noteBankMasterId = mappedId;
                 if(noteBankMasterId == accountId) dnAmount += (note.Amount ?? 0);
             }
        }

        // 5. Credit Notes
        decimal cnAmount = 0;
        if (accountType == AccountTypes.Farmer)
        {
             cnAmount = await _context.CreditNotes
                .Where(c => c.IsActive && c.Status == "Approved" && c.FarmerId == accountId && c.CreditNoteDate < untilDate)
                .SumAsync(c => c.Amount ?? 0);
        }

        return (geCredit + reCredit + psCredit + cnAmount) - (geDebit + reDebit + psDebit + dnAmount);
    }

        private async Task<string> GetAccountNameAsync(int accountId, string accountType)
    {
        return accountType switch
        {
            AccountTypes.BankMaster => (await _context.BankMasters.FindAsync(accountId))?.AccountName ?? "",
            AccountTypes.MasterGroup => (await _context.MasterGroups.FindAsync(accountId))?.Name ?? "",
            AccountTypes.MasterSubGroup => (await _context.MasterSubGroups.FindAsync(accountId))?.Name ?? "",
            AccountTypes.SubGroupLedger => (await _context.SubGroupLedgers.FindAsync(accountId))?.Name ?? "",
            AccountTypes.Farmer => (await _context.Farmers.FindAsync(accountId))?.FarmerName ?? "",
            _ => ""
        };
    }

    public async Task<string> GetMediatorAccountNameAsync()
    {
        var mediator = await _context.MasterGroups
            .OrderBy(mg => mg.Id)
            .FirstOrDefaultAsync();
        return mediator?.Name ?? "ASSETS";
    }

    private async Task<Dictionary<int, int>> LoadDebitNoteBankMasterIdMappingsAsync()
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var projectRoot = Path.GetFullPath(Path.Combine(basePath, "..", "..", "..", ".."));
            var mappingPath = Path.Combine(projectRoot, "Data", "DebitNoteBankMasterMapping.json");
            if (System.IO.File.Exists(mappingPath))
            {
                var json = await System.IO.File.ReadAllTextAsync(mappingPath);
                if (!string.IsNullOrEmpty(json)) return JsonSerializer.Deserialize<Dictionary<int, int>>(json) ?? new Dictionary<int, int>();
            }
        }
        catch { }
        return new Dictionary<int, int>();
    }
    private GeneralEntry CreateLedgerEntry(GeneralEntry original, GeneralEntry? opposite, decimal amount, bool isDebit)
    {
        var ledgerEntry = new GeneralEntry
        {
            Id = original.Id,
            VoucherNo = original.VoucherNo,
            EntryDate = original.EntryDate,
            Amount = amount,
            Narration = original.Narration,
            Status = original.Status,
            Type = original.Type,
            CreatedAt = original.CreatedAt,
            DebitAccountId = original.DebitAccountId,
            DebitAccountType = original.DebitAccountType,
            CreditAccountId = original.CreditAccountId,
            CreditAccountType = original.CreditAccountType,
            VoucherType = "Journal Entry Book",
            PaymentType = original.EntryForName ?? "Journal",
            Unit = original.Unit
        };

        if (opposite != null)
        {
            if (isDebit)
            {
                ledgerEntry.CreditMasterGroup = opposite.CreditMasterGroup;
                ledgerEntry.CreditMasterSubGroup = opposite.CreditMasterSubGroup;
                ledgerEntry.CreditSubGroupLedger = opposite.CreditSubGroupLedger;
                ledgerEntry.CreditBankMasterInfo = opposite.CreditBankMasterInfo;
                ledgerEntry.DebitBankMasterInfo = original.DebitBankMasterInfo;
            }
            else
            {
                ledgerEntry.DebitMasterGroup = opposite.DebitMasterGroup;
                ledgerEntry.DebitMasterSubGroup = opposite.DebitMasterSubGroup;
                ledgerEntry.DebitSubGroupLedger = opposite.DebitSubGroupLedger;
                ledgerEntry.DebitBankMasterInfo = opposite.DebitBankMasterInfo;
                ledgerEntry.CreditBankMasterInfo = original.CreditBankMasterInfo;
            }
        }

        return ledgerEntry;
    }

    private GeneralEntry CreateReceiptLedgerEntry(ReceiptEntry original, ReceiptEntry? opposite, decimal amount, string oppositeName)
    {
        var generalEntry = new GeneralEntry
        {
            Id = original.Id * -1,
            VoucherNo = original.VoucherNo,
            EntryDate = original.ReceiptDate,
            Amount = amount,
            Narration = original.Narration,
            Status = original.Status,
            Type = original.PaymentType,
            CreatedAt = original.CreatedAt,
            VoucherType = "Receipt Entry",
            PaymentType = original.PaymentType,
            Unit = original.Unit
        };

        if (original.Type == "Debit")
        {
            generalEntry.DebitAccountId = original.AccountId;
            generalEntry.DebitAccountType = original.AccountType;
            generalEntry.DebitBankMasterInfo = new BankMaster { AccountName = "Self" }; // Handled by display logic
            
            if (opposite != null)
            {
                generalEntry.CreditAccountId = opposite.AccountId;
                generalEntry.CreditAccountType = opposite.AccountType;
                generalEntry.CreditBankMasterInfo = new BankMaster { Id = 0, AccountName = oppositeName };
            }
        }
        else
        {
            generalEntry.CreditAccountId = original.AccountId;
            generalEntry.CreditAccountType = original.AccountType;
            generalEntry.CreditBankMasterInfo = new BankMaster { AccountName = "Self" };

            if (opposite != null)
            {
                generalEntry.DebitAccountId = opposite.AccountId;
                generalEntry.DebitAccountType = opposite.AccountType;
                generalEntry.DebitBankMasterInfo = new BankMaster { Id = 0, AccountName = oppositeName };
            }
        }

        return generalEntry;
    }
    
    public async Task<(bool success, string message)> CreateGrowerBookEntryAsync(GeneralEntry entry)
    {
        var currentUser = GetCurrentUsername();
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. Generate Voucher Number (e.g., GBK/24-25/00001)
                var currentYear = DateTime.Now.Year;
                var yearShort = currentYear.ToString().Substring(2);
                var nextYear = (currentYear + 1).ToString().Substring(2);
                var prefix = $"GBK/{yearShort}-{nextYear}/";

                var lastEntry = await _context.GeneralEntries
                    .Where(g => g.VoucherNo.StartsWith("GBK/"))
                    .OrderByDescending(g => g.Id)
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

                var voucherNo = $"{prefix}{nextNumber:D5}";

                // 2. Create entry with both Credit and Debit accounts from user selection
                var growerEntry = new GeneralEntry
                {
                    VoucherNo = voucherNo,
                    EntryDate = entry.EntryDate,
                    Amount = entry.Amount,
                    Narration = entry.Narration,
                    Status = "Unapproved",
                    IsActive = true,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser,
                    VoucherType = "Grower Book",
                    Type = entry.Type,
                    Unit = entry.Unit,
                    PaymentFromSubGroupId = entry.PaymentFromSubGroupId,
                    EntryAccountId = entry.EntryAccountId,
                    EntryForId = entry.EntryForId,
                    EntryForName = entry.EntryForName,
                    // Use the accounts directly from the entry (set by controller)
                    DebitAccountId = entry.DebitAccountId,
                    DebitAccountType = entry.DebitAccountType,
                    CreditAccountId = entry.CreditAccountId,
                    CreditAccountType = entry.CreditAccountType
                };

                _context.GeneralEntries.Add(growerEntry);
                await _context.SaveChangesAsync();

                // History
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        voucherNo, "Grower Book", "Insert", currentUser, 
                        remarks: "Grower Book Entry Created",
                        newValues: JsonSerializer.Serialize(growerEntry));
                }
                catch { /* Ignore */ }

                await transaction.CommitAsync();
                return (true, "Grower Book entry created successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var fullMessage = ex.InnerException != null 
                    ? $"{ex.Message} -> {ex.InnerException.Message}" 
                    : ex.Message;
                return (false, "Error creating entry: " + fullMessage);
            }
        });
    }

    public async Task<(bool success, string message)> UpdateGrowerBookEntryAsync(GeneralEntry entry)
    {
        var currentUser = GetCurrentUsername();
        var strategy = _context.Database.CreateExecutionStrategy();
        
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existing = await _context.GeneralEntries
                    .FirstOrDefaultAsync(e => e.Id == entry.Id);

                if (existing == null) return (false, "Entry not found.");
                if (existing.Status == "Approved") return (false, "Approved entries cannot be modified.");

                // Update Fields
                existing.EntryDate = entry.EntryDate;
                existing.Amount = entry.Amount;
                existing.Narration = entry.Narration;
                existing.Type = entry.Type;
                existing.Unit = entry.Unit; // Added Unit update
                existing.PaymentFromSubGroupId = entry.PaymentFromSubGroupId;
                existing.EntryForId = entry.EntryForId;
                existing.EntryForName = entry.EntryForName;

                // Update accounts directly from the entry (set by controller)
                existing.DebitAccountId = entry.DebitAccountId;
                existing.DebitAccountType = entry.DebitAccountType;
                existing.CreditAccountId = entry.CreditAccountId;
                existing.CreditAccountType = entry.CreditAccountType;

                _context.GeneralEntries.Update(existing);
                await _context.SaveChangesAsync();

                // History
                try
                {
                    await _transactionService.LogTransactionHistoryAsync(
                        existing.VoucherNo, "Grower Book", "Update", currentUser, 
                        remarks: "Grower Book Entry Updated",
                        newValues: JsonSerializer.Serialize(existing));
                }
                catch { /* Ignore */ }

                await transaction.CommitAsync();
                return (true, "Entry updated successfully!");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "Error updating entry: " + ex.Message);
            }
        });
    }

    public async Task<(List<GeneralEntry> entries, int totalCount, int totalPages)> GetGrowerBookEntriesAsync(
        DateTime? fromDate, DateTime? toDate, string? bookNo, string? fromGrower, string? toGrower, string? status, string? unit, int page, int pageSize)
    {
        var query = _context.GeneralEntries
            .Where(g => g.IsActive && g.VoucherNo.StartsWith("GBK/"))
            .AsQueryable();

        if (fromDate.HasValue) query = query.Where(g => g.EntryDate >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(g => g.EntryDate <= toDate.Value);
        if (!string.IsNullOrEmpty(status) && status != "All") query = query.Where(g => g.Status == status);
        if (!string.IsNullOrEmpty(bookNo)) query = query.Where(g => g.VoucherNo.Contains(bookNo));
        if (!string.IsNullOrEmpty(unit) && unit != "All") query = query.Where(g => g.Unit == unit);

        if (!string.IsNullOrEmpty(fromGrower))
        {
            // Search BankMasters now
            var farmerIds = await _context.BankMasters
                .Where(b => b.AccountName.Contains(fromGrower))
                .Select(b => b.Id)
                .ToListAsync();
            
            if (farmerIds.Any())
            {
                 query = query.Where(g => 
                    (g.DebitAccountType == "BankMaster" && farmerIds.Contains(g.DebitAccountId)) ||
                    (g.CreditAccountType == "BankMaster" && farmerIds.Contains(g.CreditAccountId))
                );
            }
            else
            {
                return (new List<GeneralEntry>(), 0, 0); 
            }
        }

        if (!string.IsNullOrEmpty(toGrower))
        {
             // Search BankMasters now
             var farmerIds = await _context.BankMasters
                .Where(b => b.AccountName.Contains(toGrower))
                .Select(b => b.Id)
                .ToListAsync();
            
             if (farmerIds.Any())
            {
                 query = query.Where(g => 
                    (g.DebitAccountType == "BankMaster" && farmerIds.Contains(g.DebitAccountId)) ||
                    (g.CreditAccountType == "BankMaster" && farmerIds.Contains(g.CreditAccountId))
                );
            }
             else { return (new List<GeneralEntry>(), 0, 0); }
        }

        var totalCount = await query.CountAsync();
        var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

        var entries = await query
            .OrderByDescending(g => g.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        foreach (var entry in entries)
        {
            await LoadAccountNamesAsync(entry);
        }

        return (entries, totalCount, totalPages);
    
    }

    public async Task<IEnumerable<object>> GetGrowerGroupsAsync(string? searchTerm)
    {
        var query = _context.SubGroupLedgers.AsNoTracking()
             .Include(s => s.MasterGroup)
             .Where(g => g.IsActive && (g.Name.Contains("Grower") || g.MasterGroup.Name.Contains("Grower")));

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(g => g.Name.Contains(searchTerm));
        }

        return await query
            .OrderBy(g => g.Name)
            .Take(20)
            .Select(g => new { id = g.Id, name = $"{g.Name} ({g.MasterGroup.Name})" })
            .ToListAsync();
    }

    public async Task<IEnumerable<object>> GetFarmersByGroupAsync(int? groupId, string? searchTerm, string? type = null)
    {
        // 1. Fetch grower-related SubGroupLedger IDs (Grower Groups)
        var growerGroupIds = await _context.SubGroupLedgers
            .Where(s => s.IsActive && (s.Name.Contains("Grower") || s.MasterGroup.Name.Contains("Grower")))
            .Select(s => s.Id)
            .ToListAsync();

        if (!growerGroupIds.Any())
        {
            return new List<object>();
        }

        // 2. Fetch Rules dictionary for fast lookup
        var rules = await _context.AccountRules
            .Where(r => r.RuleType == "AllowedNature" && r.AccountType == "BankMaster")
            .ToListAsync();
        
        var rulesDict = rules.ToDictionary(r => r.AccountId, r => r.Value);

        // Helper to check if account is allowed
        bool IsAllowed(int bankMasterId)
        {
            if (string.IsNullOrEmpty(type)) return true;

            if (!rulesDict.ContainsKey(bankMasterId)) return true; // No rule = Allowed (Both)
            var ruleValue = rulesDict[bankMasterId];
            if (ruleValue == "Both") return true;

            // Map GrowerBook Type to Debit/Credit
            // Payment -> Debit Farmer
            // Receipt/Journal -> Credit Farmer
            string neededType = (type == "Payment") ? "Debit" : "Credit";

            return ruleValue == neededType;
        }

        // 3. Query BankMasters that belong to Grower groups
        var query = _context.BankMasters
            .AsNoTracking()
            .Where(b => b.IsActive && growerGroupIds.Contains(b.GroupId));

        // 4. Apply specific group filter if provided
        if (groupId.HasValue && groupId.Value > 0)
        {
            query = query.Where(b => b.GroupId == groupId.Value);
        }

        // 5. Apply search term filter
        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(b => b.AccountName.Contains(searchTerm));
        }

        var list = await query
            .OrderBy(b => b.AccountName)
            .Take(50)
            .ToListAsync();

        return list
            .Where(b => IsAllowed(b.Id))
            .Select(b => new { id = b.Id, name = b.AccountName, type = AccountTypes.BankMaster });
    }

    public async Task<IEnumerable<LookupItem>> GetGrowerAccountsAsync(string? searchTerm, string? transactionType, string? accountSide, int? entryForId = null)
    {
        // Require EntryForId for strict filtering as per request (or handle null reasonably)
        if (!entryForId.HasValue || entryForId == 0)
        {
            return new List<LookupItem>();
        }

        // 1. Fetch Rules dictionary for fast lookup
        var rules = await _context.AccountRules
            .Where(r => r.RuleType == "AllowedNature" && r.EntryAccountId == entryForId)
            .ToListAsync();

        bool CheckRule(string ruleValue)
        {
            if (string.IsNullOrWhiteSpace(ruleValue)) return false;
            if (string.Equals(ruleValue, "Both", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(ruleValue, "Cancel", StringComparison.OrdinalIgnoreCase)) return false;

            if (string.IsNullOrWhiteSpace(accountSide)) return true; 

            if (string.Equals(ruleValue, "Debit", StringComparison.OrdinalIgnoreCase) && string.Equals(accountSide, "Debit", StringComparison.OrdinalIgnoreCase)) return true;
            if (string.Equals(ruleValue, "Credit", StringComparison.OrdinalIgnoreCase) && string.Equals(accountSide, "Credit", StringComparison.OrdinalIgnoreCase)) return true;

            return false;
        }

        // 2. Build Allowed ID Sets from Rules
        var allowedSubGroupIds = rules
            .Where(r => r.AccountType == "SubGroupLedger" && CheckRule(r.Value))
            .Select(r => r.AccountId)
            .ToHashSet();

        var allowedGrowerGroupIds = rules
            .Where(r => r.AccountType == "GrowerGroup" && CheckRule(r.Value))
            .Select(r => r.AccountId)
            .ToHashSet();

        var allowedBankMasterIds = rules
            .Where(r => r.AccountType == "BankMaster" && CheckRule(r.Value))
            .Select(r => r.AccountId)
            .ToHashSet();

        var allowedFarmerIds = rules
            .Where(r => r.AccountType == "Farmer" && CheckRule(r.Value))
            .Select(r => r.AccountId)
            .ToHashSet();

        // 3. Query DB with Allowed List
        var bankMastersQuery = _context.BankMasters.Where(bm => bm.IsActive);
        var farmersQuery = _context.Farmers.Where(f => f.IsActive);

        if (!string.IsNullOrEmpty(searchTerm))
        {
            bankMastersQuery = bankMastersQuery.Where(bm => bm.AccountName.Contains(searchTerm));
            farmersQuery = farmersQuery.Where(f => f.FarmerName.Contains(searchTerm));
        }

        // Execute Queries - Filtering by ID sets
        // For BankMasters: Match ID OR GroupId (SubGroupLedger)
        var bankMasters = await bankMastersQuery
            .Where(bm => allowedBankMasterIds.Contains(bm.Id) || allowedSubGroupIds.Contains(bm.GroupId))
            .OrderBy(bm => bm.AccountName)
            .Take(50)
            .ToListAsync();

        // For Farmers: Match ID OR GroupId (GrowerGroup)
        var farmers = await farmersQuery
            .Where(f => allowedFarmerIds.Contains(f.Id) || allowedGrowerGroupIds.Contains(f.GroupId))
            .OrderBy(f => f.FarmerName)
            .Take(50)
            .ToListAsync();

        var results = new List<LookupItem>();
        results.AddRange(bankMasters.Select(bm => new LookupItem { Id = bm.Id, Name = bm.AccountName, Type = AccountTypes.BankMaster, AccountNumber = bm.AccountNumber }));
        results.AddRange(farmers.Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName, Type = AccountTypes.Farmer, AccountNumber = f.FarmerCode }));

        return results.OrderBy(r => r.Name).Take(100).ToList();
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
