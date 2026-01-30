using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;

namespace AbraqAccount.Services.Implementations;

public class BankMasterService : IBankMasterService
{
    private readonly AppDbContext _context;

    public BankMasterService(AppDbContext context)
    {
        _context = context;
    }

    #region BankMaster CRUD
    public async Task<IEnumerable<BankMaster>> GetAllActiveBankMastersAsync()
    {
        try
        {
            await FixNullAccountNamesAsync();

            return await _context.BankMasters
                .Include(b => b.Group)
                .Where(b => b.IsActive)
                .OrderBy(b => b.AccountName)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<BankMaster?> GetBankMasterByIdAsync(int id)
    {
        try
        {
            return await _context.BankMasters
                .Include(b => b.Group)
                .FirstOrDefaultAsync(b => b.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<BankMaster> CreateBankMasterAsync(BankMaster bankMaster, string username)
    {
        try
        {
            // Make old columns nullable
            await MakeOldColumnsNullableAsync();

            bankMaster.CreatedDate = DateTime.Now;
            bankMaster.IsActive = true;
            if (string.IsNullOrEmpty(bankMaster.CreatedBy))
            {
                bankMaster.CreatedBy = username;
            }

            _context.Add(bankMaster);
            await _context.SaveChangesAsync();
            return bankMaster;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<BankMaster> UpdateBankMasterAsync(int id, BankMaster bankMaster)
    {
        try
        {
            var existingBankMaster = await _context.BankMasters.FindAsync(id);
            if (existingBankMaster == null)
            {
                throw new InvalidOperationException("Bank master not found");
            }

            // Update properties
            existingBankMaster.AccountName = bankMaster.AccountName;
            existingBankMaster.GroupId = bankMaster.GroupId;
            existingBankMaster.Status = bankMaster.Status;
            existingBankMaster.Address = bankMaster.Address;
            existingBankMaster.Phone = bankMaster.Phone;
            existingBankMaster.Email = bankMaster.Email;
            existingBankMaster.AccountNumber = bankMaster.AccountNumber;
            existingBankMaster.IfscCode = bankMaster.IfscCode;
            existingBankMaster.BranchName = bankMaster.BranchName;

            _context.Update(existingBankMaster);
            await _context.SaveChangesAsync();
            return existingBankMaster;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> DeleteBankMasterAsync(int id)
    {
        try
        {
            var bankMaster = await _context.BankMasters.FindAsync(id);
            if (bankMaster == null)
            {
                return false;
            }

            bankMaster.IsActive = false;
            _context.Update(bankMaster);
            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> BankMasterExistsAsync(int id)
    {
        try
        {
            return await _context.BankMasters.AnyAsync(e => e.Id == id);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> AccountNameExistsAsync(string accountName, int? excludeId = null)
    {
        try
        {
            if (excludeId.HasValue)
            {
                return await _context.BankMasters.AnyAsync(b => b.AccountName == accountName && b.Id != excludeId.Value && b.IsActive);
            }
            return await _context.BankMasters.AnyAsync(b => b.AccountName == accountName && b.IsActive);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<bool> AccountNumberExistsAsync(string accountNumber, int? excludeId = null)
    {
        try
        {
            if (string.IsNullOrEmpty(accountNumber))
            {
                return false;
            }

            if (excludeId.HasValue)
            {
                return await _context.BankMasters.AnyAsync(b => b.AccountNumber == accountNumber && b.Id != excludeId.Value && b.IsActive);
            }
            return await _context.BankMasters.AnyAsync(b => b.AccountNumber == accountNumber && b.IsActive);
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Dropdowns
    public async Task<SelectList> GetGroupsSelectListAsync(int? selectedValue = null)
    {
        try
        {
            var groups = await _context.SubGroupLedgers
                .Include(sgl => sgl.MasterGroup)
                .Include(sgl => sgl.MasterSubGroup)
                .Where(l => l.IsActive)
                .OrderBy(l => l.Name)
                .ToListAsync();

            return new SelectList(groups, "Id", "Name", selectedValue);
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Helpers
    public async Task FixNullAccountNamesAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                UPDATE [dbo].[BankMasters]
                SET [AccountName] = COALESCE(
                    (SELECT [BankName] FROM [dbo].[BankMasters] b2 WHERE b2.[Id] = [BankMasters].[Id]),
                    'Account ' + CAST([Id] AS NVARCHAR(10))
                )
                WHERE [AccountName] IS NULL OR [AccountName] = '';
            ");
        }
        catch
        {
            // Ignore errors - might not have BankName column
        }
    }

    private async Task MakeOldColumnsNullableAsync()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync(@"
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'BankName' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [BankName] NVARCHAR(255) NULL;
                END
                
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'AccountType' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [AccountType] NVARCHAR(20) NULL;
                END
                
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'OpeningBalance' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [OpeningBalance] DECIMAL(18,2) NULL;
                END
                
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'OpeningBalanceDate' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [OpeningBalanceDate] DATETIME NULL;
                END
                
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'BalanceMode' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [BalanceMode] NVARCHAR(10) NULL;
                END
                
                IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'LedgerAccountMapping' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [LedgerAccountMapping] NVARCHAR(255) NULL;
                END
            ");
        }
        catch { }
    }
    #endregion

    #region Migration
    public async Task<(bool success, string message, List<string> steps)> MigrateDatabaseAsync()
    {
        var results = new List<string>();
        try
        {
            // Step 1: Add AccountName column
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'AccountName')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ADD [AccountName] NVARCHAR(255) NULL;
                    END
                ");
                results.Add("Step 1: AccountName column check completed");
            }
            catch (Exception ex)
            {
                results.Add($"Step 1 Error: {ex.Message}");
            }

            // Step 2: Copy data from BankName
            try
            {
                var bankNameExists = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'BankName'"
                ).FirstOrDefaultAsync() > 0;

                var accountNameExists = await _context.Database.SqlQueryRaw<int>(
                    "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'AccountName'"
                ).FirstOrDefaultAsync() > 0;

                if (bankNameExists && accountNameExists)
                {
                    await _context.Database.ExecuteSqlRawAsync(@"
                        UPDATE [dbo].[BankMasters] 
                        SET [AccountName] = [BankName] 
                        WHERE [AccountName] IS NULL OR [AccountName] = '';
                    ");
                    results.Add("Step 2: Data copied from BankName to AccountName");
                }
                else if (accountNameExists)
                {
                    await _context.Database.ExecuteSqlRawAsync(@"
                        UPDATE [dbo].[BankMasters] 
                        SET [AccountName] = 'Account ' + CAST([Id] AS NVARCHAR(10))
                        WHERE [AccountName] IS NULL OR [AccountName] = '';
                    ");
                    results.Add("Step 2: Default values set for AccountName");
                }
                else
                {
                    results.Add("Step 2: Skipped (AccountName doesn't exist)");
                }
            }
            catch (Exception ex)
            {
                results.Add($"Step 2 Error: {ex.Message}");
            }

            // Step 3: Make AccountName NOT NULL if it exists and is still nullable
            try
            {
                var isNullable = await _context.Database.SqlQueryRaw<string>(
                    "SELECT IS_NULLABLE FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'AccountName'"
                ).FirstOrDefaultAsync();

                if (isNullable == "YES")
                {
                    // First ensure no NULL values
                    await _context.Database.ExecuteSqlRawAsync(@"
                        UPDATE [dbo].[BankMasters] 
                        SET [AccountName] = COALESCE([AccountName], 'Account ' + CAST([Id] AS NVARCHAR(10)))
                        WHERE [AccountName] IS NULL;
                    ");
                    // Then make it NOT NULL
                    await _context.Database.ExecuteSqlRawAsync("ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [AccountName] NVARCHAR(255) NOT NULL");
                    results.Add("Step 3: AccountName set to NOT NULL");
                }
                else
                {
                    results.Add("Step 3: AccountName already NOT NULL or doesn't exist");
                }
            }
            catch (Exception ex)
            {
                results.Add($"Step 3 Error: {ex.Message}");
            }

            // Step 4: Add GroupId column
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'GroupId')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ADD [GroupId] INT NOT NULL DEFAULT 1;
                    END
                ");
                results.Add("Step 4: GroupId column added");
            }
            catch (Exception ex)
            {
                results.Add($"Step 4 Error: {ex.Message}");
            }

            // Step 5: Add Address column
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'Address')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ADD [Address] NVARCHAR(500) NULL;
                    END
                ");
                results.Add("Step 5: Address column added");
            }
            catch (Exception ex)
            {
                results.Add($"Step 5 Error: {ex.Message}");
            }

            // Step 6: Add Phone column
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'Phone')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ADD [Phone] NVARCHAR(20) NULL;
                    END
                ");
                results.Add("Step 6: Phone column added");
            }
            catch (Exception ex)
            {
                results.Add($"Step 6 Error: {ex.Message}");
            }

            // Step 7: Add Email column
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'Email')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ADD [Email] NVARCHAR(255) NULL;
                    END
                ");
                results.Add("Step 7: Email column added");
            }
            catch (Exception ex)
            {
                results.Add($"Step 7 Error: {ex.Message}");
            }

            // Step 8: Make old columns nullable if they exist (to avoid insert errors)
            try
            {
                await _context.Database.ExecuteSqlRawAsync(@"
                    -- Make BankName nullable
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'BankName' AND IS_NULLABLE = 'NO')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [BankName] NVARCHAR(255) NULL;
                    END
                    
                    -- Make AccountType nullable
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'AccountType' AND IS_NULLABLE = 'NO')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [AccountType] NVARCHAR(20) NULL;
                    END
                    
                    -- Make OpeningBalance nullable
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'OpeningBalance' AND IS_NULLABLE = 'NO')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [OpeningBalance] DECIMAL(18,2) NULL;
                    END
                    
                    -- Make OpeningBalanceDate nullable
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'OpeningBalanceDate' AND IS_NULLABLE = 'NO')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [OpeningBalanceDate] DATETIME NULL;
                    END
                    
                    -- Make BalanceMode nullable
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'BalanceMode' AND IS_NULLABLE = 'NO')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [BalanceMode] NVARCHAR(10) NULL;
                    END
                    
                    -- Make LedgerAccountMapping nullable
                    IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'BankMasters' AND COLUMN_NAME = 'LedgerAccountMapping' AND IS_NULLABLE = 'NO')
                    BEGIN
                        ALTER TABLE [dbo].[BankMasters] ALTER COLUMN [LedgerAccountMapping] NVARCHAR(255) NULL;
                    END
                ");
                results.Add("Step 8: Old columns made nullable");
            }
            catch (Exception ex)
            {
                results.Add($"Step 8 Error: {ex.Message}");
            }

            return (true, "Database migration completed!", results);
        }
        catch (Exception ex)
        {
            results.Add($"Fatal Error: {ex.Message}");
            return (false, $"Migration failed: {ex.Message}", results);
        }
    }
    #endregion
}

