using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;

namespace AbraqAccount.Services.Implementations;

public class AccountMasterService : IAccountMasterService
{
    private readonly AppDbContext _context;

    public AccountMasterService(AppDbContext context)
    {
        _context = context;
    }

    #region Master Group

    public async Task<List<MasterGroup>> GetMasterGroupsAsync()
    {
        try
        {
            return await _context.MasterGroups
                .OrderBy(m => string.IsNullOrEmpty(m.Code) ? "ZZZ" : m.Code)
                .ThenByDescending(m => m.Id)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateMasterGroupAsync(MasterGroup model)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            _context.MasterGroups.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Master Group created successfully.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<MasterGroup?> GetMasterGroupByIdAsync(int id)
    {
        try
        {
            return await _context.MasterGroups.FindAsync(id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateMasterGroupAsync(int id, MasterGroup model)
    {
        try
        {
            var masterGroup = await _context.MasterGroups.FindAsync(id);
            if (masterGroup != null)
            {
                masterGroup.Code = string.IsNullOrWhiteSpace(model.Code) ? null : model.Code;
                masterGroup.Name = model.Name;
                masterGroup.Description = model.Description;
                await _context.SaveChangesAsync();
                return (true, "Master Group updated successfully.");
            }
            return (false, "Master Group not found.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task DeleteMasterGroupAsync(int id)
    {
        try
        {
            var masterGroup = await _context.MasterGroups.FindAsync(id);
            if (masterGroup != null)
            {
                _context.MasterGroups.Remove(masterGroup);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message, int imported, int updated, List<string> errors)> ImportMasterGroupsAsync(List<MasterGroupImportModel> importData)
    {
        if (importData == null || !importData.Any())
        {
            return (false, "No data to import.", 0, 0, new List<string>());
        }

        try
        {
            int importedCount = 0;
            int updatedCount = 0;
            var errors = new List<string>();

            foreach (var item in importData)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add($"Row with code '{item.Code}': Name is required.");
                    continue;
                }

                var existingByCode = await _context.MasterGroups
                    .FirstOrDefaultAsync(m => m.Code == item.Code && !string.IsNullOrEmpty(item.Code));

                var existingByName = await _context.MasterGroups
                    .FirstOrDefaultAsync(m => m.Name == item.Name);

                if (existingByCode != null)
                {
                    existingByCode.Name = item.Name;
                    existingByCode.Description = item.Description;
                    existingByCode.CreatedAt = existingByCode.CreatedAt ?? DateTime.Now;
                    updatedCount++;
                }
                else if (existingByName != null)
                {
                    if (string.IsNullOrEmpty(existingByName.Code) && !string.IsNullOrEmpty(item.Code))
                    {
                        existingByName.Code = item.Code;
                    }
                    existingByName.Description = item.Description;
                    existingByName.CreatedAt = existingByName.CreatedAt ?? DateTime.Now;
                    updatedCount++;
                }
                else
                {
                    var newGroup = new MasterGroup
                    {
                        Code = string.IsNullOrWhiteSpace(item.Code) ? null : item.Code,
                        Name = item.Name,
                        Description = item.Description,
                        CreatedAt = DateTime.Now
                    };
                    _context.MasterGroups.Add(newGroup);
                    importedCount++;
                }
            }

            await _context.SaveChangesAsync();

            return (true, $"Import completed. {importedCount} new groups imported, {updatedCount} groups updated.", importedCount, updatedCount, errors);
        }
        catch (Exception ex)
        {
            return (false, "Error importing data: " + ex.Message, 0, 0, new List<string> { ex.Message });
        }
    }

    #endregion
    
    #region Master Sub Group

    public async Task<(List<MasterSubGroup> subGroups, List<MasterGroup> masterGroups)> GetMasterSubGroupsWithParentsAsync()
    {
        try
        {
            var masterSubGroups = await _context.MasterSubGroups
                .Include(m => m.MasterGroup)
                .OrderByDescending(m => m.Id)
                .ToListAsync();
            
            var masterGroups = await _context.MasterGroups
                .OrderBy(m => m.Name)
                .ToListAsync();

            return (masterSubGroups, masterGroups);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateMasterSubGroupAsync(MasterSubGroup model)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            _context.MasterSubGroups.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Master Sub Group created successfully.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<MasterSubGroup?> GetMasterSubGroupByIdAsync(int id)
    {
        try
        {
            return await _context.MasterSubGroups
                .Include(m => m.MasterGroup)
                .FirstOrDefaultAsync(m => m.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateMasterSubGroupAsync(int id, MasterSubGroup model)
    {
        try
        {
            var masterSubGroup = await _context.MasterSubGroups.FindAsync(id);
            if (masterSubGroup != null)
            {
                masterSubGroup.MasterGroupId = model.MasterGroupId;
                masterSubGroup.Name = model.Name;
                masterSubGroup.Description = model.Description;
                masterSubGroup.IsActive = model.IsActive;
                await _context.SaveChangesAsync();
                return (true, "Master Sub Group updated successfully.");
            }
            return (false, "Master Sub Group not found.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task DeleteMasterSubGroupAsync(int id)
    {
        try
        {
            var masterSubGroup = await _context.MasterSubGroups.FindAsync(id);
            if (masterSubGroup != null)
            {
                _context.MasterSubGroups.Remove(masterSubGroup);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message, int imported, int updated, List<string> errors)> ImportMasterSubGroupsAsync(List<MasterSubGroupImportModel> importData)
    {
        if (importData == null || !importData.Any())
        {
            return (false, "No data to import.", 0, 0, new List<string>());
        }

        try
        {
            int importedCount = 0;
            int updatedCount = 0;
            var errors = new List<string>();

            var allMasterGroups = await _context.MasterGroups.ToListAsync();
            var masterGroupsDict = allMasterGroups.ToDictionary(
                mg => mg.Name.Trim().ToLowerInvariant(), 
                mg => mg, 
                StringComparer.OrdinalIgnoreCase
            );

            var masterGroupIds = masterGroupsDict.Values.Select(mg => mg.Id).ToList();
            var existingSubGroups = await _context.MasterSubGroups
                .Where(msg => masterGroupIds.Contains(msg.MasterGroupId))
                .ToListAsync();

            foreach (var item in importData)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add($"Row with Master Group '{item.MasterGroupName}': Name is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.MasterGroupName))
                {
                    errors.Add($"Row '{item.Name}': Master Group Name is required.");
                    continue;
                }

                var masterGroupKey = item.MasterGroupName.Trim().ToLowerInvariant();
                if (!masterGroupsDict.TryGetValue(masterGroupKey, out var masterGroup))
                {
                    errors.Add($"Row '{item.Name}': Master Group '{item.MasterGroupName}' not found. Please create it first.");
                    continue;
                }

                var subGroupNameTrimmed = item.Name.Trim();
                var existing = existingSubGroups
                    .FirstOrDefault(msg => msg.Name.Trim().Equals(subGroupNameTrimmed, StringComparison.OrdinalIgnoreCase) 
                        && msg.MasterGroupId == masterGroup.Id);

                if (existing != null)
                {
                    existing.Description = item.Description;
                    existing.IsActive = true;
                    existing.CreatedAt = existing.CreatedAt ?? DateTime.Now;
                    updatedCount++;
                }
                else
                {
                    var newSubGroup = new MasterSubGroup
                    {
                        MasterGroupId = masterGroup.Id,
                        Name = item.Name.Trim(),
                        Description = item.Description,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.MasterSubGroups.Add(newSubGroup);
                    importedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return (true, $"Import completed. {importedCount} new sub groups imported, {updatedCount} sub groups updated.", importedCount, updatedCount, errors);
        }
        catch (Exception ex)
        {
            return (false, "Error importing data: " + ex.Message, 0, 0, new List<string> { ex.Message });
        }
    }

    public async Task<IEnumerable<object>> GetMasterSubGroupsForDropdownAsync(int masterGroupId)
    {
        try
        {
            return await _context.MasterSubGroups
                .Where(m => m.MasterGroupId == masterGroupId)
                .OrderBy(m => m.Name)
                .Select(m => new { m.Id, m.Name })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    #endregion
    
    #region Sub Group Ledger

    public async Task<(List<SubGroupLedger> ledgers, List<MasterSubGroup> masterSubGroups)> GetSubGroupLedgersWithParentsAsync()
    {
        try
        {
            var subGroupLedgers = await _context.SubGroupLedgers
                .Include(s => s.MasterGroup)
                .Include(s => s.MasterSubGroup)
                .OrderByDescending(s => s.Id)
                .ToListAsync();
            
            var masterSubGroups = await _context.MasterSubGroups
                .Include(msg => msg.MasterGroup)
                .Where(msg => msg.IsActive)
                .OrderBy(msg => msg.MasterGroup != null ? msg.MasterGroup.Name : "")
                .ThenBy(msg => msg.Name)
                .ToListAsync();

            return (subGroupLedgers, masterSubGroups);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateSubGroupLedgerAsync(SubGroupLedger model)
    {
        // Automatically set MasterGroupId based on selected MasterSubGroupId
        if (model.MasterSubGroupId > 0)
        {
            var masterSubGroup = await _context.MasterSubGroups
                .Include(msg => msg.MasterGroup)
                .FirstOrDefaultAsync(msg => msg.Id == model.MasterSubGroupId);
            
            if (masterSubGroup != null && masterSubGroup.MasterGroup != null)
            {
                model.MasterGroupId = masterSubGroup.MasterGroupId;
            }
        }

        try
        {
            model.CreatedAt = DateTime.Now;
            _context.SubGroupLedgers.Add(model);
            await _context.SaveChangesAsync();
            return (true, "Sub Group Ledger created successfully.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<SubGroupLedger?> GetSubGroupLedgerByIdAsync(int id)
    {
        try
        {
            return await _context.SubGroupLedgers
                .Include(s => s.MasterGroup)
                .Include(s => s.MasterSubGroup)
                .FirstOrDefaultAsync(s => s.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdateSubGroupLedgerAsync(int id, SubGroupLedger model)
    {
        try
        {
            var subGroupLedger = await _context.SubGroupLedgers.FindAsync(id);
            if (subGroupLedger != null)
            {
                // Auto-set MasterGroupId if needed
                if (model.MasterSubGroupId > 0)
                {
                    var masterSubGroup = await _context.MasterSubGroups
                        .Include(msg => msg.MasterGroup)
                        .FirstOrDefaultAsync(msg => msg.Id == model.MasterSubGroupId);
                    
                    if (masterSubGroup != null && masterSubGroup.MasterGroup != null)
                    {
                        model.MasterGroupId = masterSubGroup.MasterGroupId;
                    }
                }

                subGroupLedger.MasterGroupId = model.MasterGroupId;
                subGroupLedger.MasterSubGroupId = model.MasterSubGroupId;
                subGroupLedger.Name = model.Name;
                subGroupLedger.Description = model.Description;
                subGroupLedger.IsActive = model.IsActive;
                await _context.SaveChangesAsync();
                return (true, "Sub Group Ledger updated successfully.");
            }
            return (false, "Sub Group Ledger not found.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task DeleteSubGroupLedgerAsync(int id)
    {
        try
        {
            var subGroupLedger = await _context.SubGroupLedgers.FindAsync(id);
            if (subGroupLedger != null)
            {
                _context.SubGroupLedgers.Remove(subGroupLedger);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message, int imported, int updated, List<string> errors)> ImportSubGroupLedgersAsync(List<SubGroupLedgerImportModel> importData)
    {
        if (importData == null || !importData.Any())
        {
            return (false, "No data to import.", 0, 0, new List<string>());
        }

        try
        {
            int importedCount = 0;
            int updatedCount = 0;
            var errors = new List<string>();

            var allMasterGroups = await _context.MasterGroups.ToListAsync();
            var masterGroupsDict = allMasterGroups.ToDictionary(
                mg => mg.Name.Trim().ToLowerInvariant(), 
                mg => mg, 
                StringComparer.OrdinalIgnoreCase
            );

            var allMasterSubGroups = await _context.MasterSubGroups
                .Include(msg => msg.MasterGroup)
                .ToListAsync();
            var masterSubGroupsDict = allMasterSubGroups
                .GroupBy(msg => $"{msg.MasterGroup?.Name?.Trim().ToLowerInvariant() ?? ""}|{msg.Name.Trim().ToLowerInvariant()}")
                .ToDictionary(
                    g => g.Key,
                    g => g.First()
                );

            var existingLedgers = await _context.SubGroupLedgers
                .Include(sgl => sgl.MasterGroup)
                .Include(sgl => sgl.MasterSubGroup)
                .ToListAsync();

            foreach (var item in importData)
            {
                if (string.IsNullOrWhiteSpace(item.Name))
                {
                    errors.Add($"Row with Master Group '{item.MasterGroupName}', Master Sub Group '{item.MasterSubGroupName}': Ledger Name is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.MasterGroupName))
                {
                    errors.Add($"Row '{item.Name}': Master Group Name is required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(item.MasterSubGroupName))
                {
                    errors.Add($"Row '{item.Name}': Master Sub Group Name is required.");
                    continue;
                }

                var masterGroupKey = item.MasterGroupName.Trim().ToLowerInvariant();
                if (!masterGroupsDict.TryGetValue(masterGroupKey, out var masterGroup))
                {
                    errors.Add($"Row '{item.Name}': Master Group '{item.MasterGroupName}' not found. Please create it first.");
                    continue;
                }

                var masterSubGroupKey = $"{masterGroupKey}|{item.MasterSubGroupName.Trim().ToLowerInvariant()}";
                if (!masterSubGroupsDict.TryGetValue(masterSubGroupKey, out var masterSubGroup))
                {
                    errors.Add($"Row '{item.Name}': Master Sub Group '{item.MasterSubGroupName}' under '{item.MasterGroupName}' not found. Please create it first.");
                    continue;
                }

                var ledgerNameTrimmed = item.Name.Trim();
                var existing = existingLedgers
                    .FirstOrDefault(sgl => sgl.Name.Trim().Equals(ledgerNameTrimmed, StringComparison.OrdinalIgnoreCase) 
                        && sgl.MasterSubGroupId == masterSubGroup.Id);

                if (existing != null)
                {
                    existing.Description = item.Description;
                    existing.IsActive = true;
                    existing.CreatedAt = existing.CreatedAt ?? DateTime.Now;
                    updatedCount++;
                }
                else
                {
                    var newLedger = new SubGroupLedger
                    {
                        MasterGroupId = masterGroup.Id,
                        MasterSubGroupId = masterSubGroup.Id,
                        Name = ledgerNameTrimmed,
                        Description = item.Description,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    _context.SubGroupLedgers.Add(newLedger);
                    existingLedgers.Add(newLedger);
                    importedCount++;
                }
            }

            await _context.SaveChangesAsync();
            return (true, $"Import completed. {importedCount} new ledgers imported, {updatedCount} ledgers updated.", importedCount, updatedCount, errors);
        }
        catch (Exception ex)
        {
            return (false, "Error importing data: " + ex.Message, 0, 0, new List<string> { ex.Message });
        }
    }

    public async Task EnsureCodeColumnExistsAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                IF NOT EXISTS (
                    SELECT * 
                    FROM INFORMATION_SCHEMA.COLUMNS 
                    WHERE TABLE_NAME = 'MasterGroups' 
                    AND COLUMN_NAME = 'Code'
                )
                BEGIN
                    ALTER TABLE [dbo].[MasterGroups]
                    ADD [Code] NVARCHAR(50) NULL;
                END
            ");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not add Code column: {ex.Message}");
        }
    }
    #endregion
}

