using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace AbraqAccount.Services.Implementations;

public class SettingsService : ISettingsService
{
    private readonly AppDbContext _context;

    public SettingsService(AppDbContext context)
    {
        _context = context;
    }

    #region Entry For

    public async Task<List<EntryForAccount>> GetEntryForAccountsAsync()
    {
        try
        {
            return await _context.EntryForAccounts
                .OrderBy(e => e.TransactionType)
                .ThenBy(e => e.AccountName)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateEntryForAccountAsync(EntryForAccount model)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            _context.EntryForAccounts.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Entry For account created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<EntryForAccount?> GetEntryForAccountByIdAsync(int id)
    {
        try
        {
            return await _context.EntryForAccounts.FindAsync(id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateEntryForAccountAsync(int id, EntryForAccount model)
    {
        try
        {
            var existing = await _context.EntryForAccounts.FindAsync(id);
            if (existing == null) return (false, "Entry For account not found.");

            existing.TransactionType = model.TransactionType;
            existing.AccountName = model.AccountName;
            
            await _context.SaveChangesAsync();
            return (true, "Entry For account updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task DeleteEntryForAccountAsync(int id)
    {
        try
        {
            var entry = await _context.EntryForAccounts.FindAsync(id);
            if (entry != null)
            {
                _context.EntryForAccounts.Remove(entry);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Transaction Rules

    public async Task<List<AccountRule>> GetAccountRulesAsync()
    {
        try
        {
            return await _context.AccountRules
                .OrderBy(r => r.AccountType)
                .ThenBy(r => r.RuleType)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateAccountRuleAsync(AccountRule model)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            _context.AccountRules.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Account rule created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<AccountRule?> GetAccountRuleByIdAsync(int id)
    {
        try
        {
            return await _context.AccountRules.FindAsync(id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateAccountRuleAsync(int id, AccountRule model)
    {
        try
        {
            var existing = await _context.AccountRules.FindAsync(id);
            if (existing == null) return (false, "Account rule not found.");

            existing.AccountType = model.AccountType;
            existing.AccountId = model.AccountId;
            existing.RuleType = model.RuleType;
            existing.Value = model.Value;
            existing.EntryAccountId = model.EntryAccountId;
            existing.UpdatedAt = DateTime.Now;
            
            await _context.SaveChangesAsync();
            return (true, "Account rule updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task DeleteAccountRuleAsync(int id)
    {
        try
        {
            var rule = await _context.AccountRules.FindAsync(id);
            if (rule != null)
            {
                _context.AccountRules.Remove(rule);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Lookups

    public async Task<IEnumerable<LookupItem>> GetAvailableAccountsForRulesAsync(string accountType)
    {
        try
        {
            return accountType switch
            {
                "MasterGroup" => await _context.MasterGroups.OrderBy(g => g.Name).Select(g => new LookupItem { Id = g.Id, Name = g.Name }).ToListAsync(),
                "MasterSubGroup" => await _context.MasterSubGroups.OrderBy(g => g.Name).Select(g => new LookupItem { Id = g.Id, Name = g.Name }).ToListAsync(),
                "SubGroupLedger" => await _context.SubGroupLedgers.OrderBy(g => g.Name).Select(g => new LookupItem { Id = g.Id, Name = g.Name }).ToListAsync(),
                "BankMaster" => await _context.BankMasters.OrderBy(b => b.AccountName).Select(b => new LookupItem { Id = b.Id, Name = b.AccountName }).ToListAsync(),
                "GrowerGroup" => await _context.GrowerGroups.OrderBy(g => g.GroupName).Select(g => new LookupItem { Id = g.Id, Name = g.GroupName }).ToListAsync(),
                _ => new List<LookupItem>()
            };
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Dashboard

    public async Task<List<RulesItemViewModel>> GetRulesDashboardDataAsync(int? entryForId)
    {
        try
        {
            var result = new List<RulesItemViewModel>();

            // 1. Fetch only Sub Group Ledgers (Owners)
            var subGroupLedgers = await _context.SubGroupLedgers
                .Include(s => s.MasterGroup)
                .Include(s => s.MasterSubGroup)
                .OrderBy(s => s.Name)
                .ToListAsync();

            // 2. Fetch existing rules for this profile
            var existingRules = await _context.AccountRules
                .Where(r => r.EntryAccountId == entryForId && r.RuleType == "AllowedNature")
                .ToListAsync();

            // 3. Map to View Model
            foreach (var sgl in subGroupLedgers)
            {
                result.Add(new RulesItemViewModel {
                    AccountType = "SubGroupLedger",
                    AccountId = sgl.Id,
                    Name = sgl.Name,
                    Hierarchy = $"{(sgl.MasterGroup?.Name ?? "")} > {(sgl.MasterSubGroup?.Name ?? "")}",
                    GroupName = "Sub Group Ledgers",
                    RuleValue = existingRules.FirstOrDefault(r => r.AccountType == "SubGroupLedger" && r.AccountId == sgl.Id)?.Value ?? "Cancel"
                });
            }

            return result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> SaveAccountRuleByValueAsync(string accountType, int accountId, string value, int? entryForId)
    {
        try
        {
            var rule = await _context.AccountRules
                .FirstOrDefaultAsync(r => r.AccountType == accountType && r.AccountId == accountId && r.EntryAccountId == entryForId && r.RuleType == "AllowedNature");

            if (rule == null)
            {
                // Create new
                rule = new AccountRule
                {
                    AccountType = accountType,
                    AccountId = accountId,
                    EntryAccountId = entryForId,
                    RuleType = "AllowedNature",
                    Value = value,
                    CreatedAt = DateTime.Now
                };
                _context.AccountRules.Add(rule);
            }
            else
            {
                // Update existing
                rule.Value = value;
                rule.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return (true, "Rule saved successfully.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<List<EntryForAccount>> GetFormattedEntryProfilesAsync(string transactionType)
    {
        try
        {
            var allEntries = await _context.EntryForAccounts
                .Where(e => e.TransactionType == "Global" || e.TransactionType == transactionType)
                .OrderBy(e => e.TransactionType)
                .ThenBy(e => e.AccountName)
                .ToListAsync();

            var result = new List<EntryForAccount>();
            var grouped = allEntries.GroupBy(e => e.TransactionType);

            foreach (var group in grouped)
            {
                var hasValidAccount = group.Any(e => !string.IsNullOrWhiteSpace(e.AccountName) && e.AccountName.Trim().ToLower() != "null");
                if (hasValidAccount)
                {
                    result.AddRange(group.Where(e => !string.IsNullOrWhiteSpace(e.AccountName) && e.AccountName.Trim().ToLower() != "null"));
                }
                else
                {
                    var first = group.FirstOrDefault();
                    if (first != null) result.Add(first);
                }
            }
            return result.OrderBy(e => e.TransactionType).ThenBy(e => e.AccountName).ToList();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}
