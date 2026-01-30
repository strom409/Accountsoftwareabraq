using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;

namespace AbraqAccount.Services.Implementations;

public class UserPermissionService : IUserPermissionService
{
    private readonly AppDbContext _context;

    public UserPermissionService(AppDbContext context)
    {
        _context = context;
    }

    #region Data Seeding
    public async Task SeedMenusAsync()
    {
        try
        {
            // Check if menus exist
            if (await _context.Menus.AnyAsync()) return;

            // 1. Dashboard
            var db = new Menu { Name = "DashBoard", ControllerName = "Dashboard", ActionName = "Index", IconClass = "bi bi-speedometer2", DisplayOrder = 1 };
            _context.Menus.Add(db);

            // 2. Account Master
            var am = new Menu { Name = "Account Master", IconClass = "bi bi-wallet2", DisplayOrder = 2 };
            _context.Menus.Add(am);
            await _context.SaveChangesAsync(); // Save to get ID

            _context.Menus.AddRange(new List<Menu>
            {
                new Menu { Name = "Master Group", ControllerName = "AccountMaster", ActionName = "MasterGroup", ParentId = am.Id, DisplayOrder = 1 },
                new Menu { Name = "Master Sub Group", ControllerName = "AccountMaster", ActionName = "MasterSubGroup", ParentId = am.Id, DisplayOrder = 2 },
                new Menu { Name = "Sub Group Ledger", ControllerName = "AccountMaster", ActionName = "SubGroupLedger", ParentId = am.Id, DisplayOrder = 3 }
            });

            // 3. Transactions
            var tr = new Menu { Name = "Transactions", IconClass = "bi bi-receipt", DisplayOrder = 3 };
            _context.Menus.Add(tr);
            await _context.SaveChangesAsync();

            _context.Menus.AddRange(new List<Menu>
            {
                new Menu { Name = "Ledger Creation", ControllerName = "BankMaster", ActionName = "Index", ParentId = tr.Id, DisplayOrder = 1 },
                new Menu { Name = "Transaction Entries", ControllerName = "Note", ActionName = "Index", ParentId = tr.Id, DisplayOrder = 2 },
                new Menu { Name = "Expense Entry", ControllerName = "TransactionEntries", ActionName = "ExpensesIncurred", ParentId = tr.Id, DisplayOrder = 3 }
            });

            // 4. Ledger Report
            var lr = new Menu { Name = "Ledger Report", ControllerName = "LedgerReport", ActionName = "Index", IconClass = "bi bi-file-earmark-spreadsheet", DisplayOrder = 4 };
            _context.Menus.Add(lr);

            // 5. Inventory
            var inv = new Menu { Name = "Inventory", IconClass = "bi bi-box-seam", DisplayOrder = 5 };
            _context.Menus.Add(inv);
            await _context.SaveChangesAsync();

            _context.Menus.AddRange(new List<Menu>
            {
                new Menu { Name = "Purchase Item Group", ControllerName = "PurchaseItemGroup", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 1 },
                new Menu { Name = "Purchase Item", ControllerName = "PurchaseItem", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 2 },
                new Menu { Name = "Packing Special Rate", ControllerName = "PackingSpecialRate", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 3 },
                new Menu { Name = "Purchase Order T & C", ControllerName = "PurchaseOrderTC", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 4 },
                new Menu { Name = "Packing Recipe", ControllerName = "PackingRecipe", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 5 },
                new Menu { Name = "Purchase Order", ControllerName = "PurchaseOrder", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 6 },
                new Menu { Name = "Purchase Request", ControllerName = "PurchaseRequest", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 7 },
                new Menu { Name = "Purchase Receive", ControllerName = "PurchaseReceive", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 8 },
                new Menu { Name = "Purchase Order Report", ControllerName = "PurchaseOrderReport", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 9 },
                new Menu { Name = "Material Issue", ControllerName = "MaterialIssue", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 10 },
                new Menu { Name = "Material Stock Ledger", ControllerName = "MaterialStockLedger", ActionName = "Index", ParentId = inv.Id, DisplayOrder = 11 }
            });

            // 6. Settings
            var set = new Menu { Name = "Settings", IconClass = "bi bi-gear", DisplayOrder = 6 };
            _context.Menus.Add(set);
            await _context.SaveChangesAsync();

            _context.Menus.AddRange(new List<Menu>
            {
                new Menu { Name = "Transaction Rules", ControllerName = "Rules", ActionName = "Index", ParentId = set.Id, DisplayOrder = 1 },
                new Menu { Name = "Entry For", ControllerName = "EntryFor", ActionName = "Index", ParentId = set.Id, DisplayOrder = 2 },
                // Add Permission Menu
                new Menu { Name = "User Permissions", ControllerName = "UserPermission", ActionName = "Index", ParentId = set.Id, DisplayOrder = 3 }
            });

            await _context.SaveChangesAsync();

            // Auto-assign all permissions to the first user (Super Admin) if no permissions exist
            if (!await _context.UserPermissions.AnyAsync())
            {
                var adminUser = await _context.Users.OrderBy(u => u.Id).FirstOrDefaultAsync();
                if (adminUser != null)
                {
                    var allMenus = await _context.Menus.ToListAsync();
                    var perms = allMenus.Select(m => new UserPermission
                    {
                        UserId = adminUser.Id,
                        MenuId = m.Id,
                        CanView = true,
                        CanCreate = true,
                        CanEdit = true,
                        CanDelete = true
                    }).ToList();
                    
                    _context.UserPermissions.AddRange(perms);
                    await _context.SaveChangesAsync();
                }
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region Menu Management
    public async Task<List<Menu>> GetAllMenusAsync()
    {
        try
        {
            return await _context.Menus
                .Include(m => m.Children)
                .Where(m => m.IsActive && m.ParentId == null) // Root menus
                .OrderBy(m => m.DisplayOrder)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
    
    #region Permission Management
    public async Task<List<UserPermission>> GetPermissionsForUserAsync(int userId)
    {
        try
        {
            return await _context.UserPermissions
                .Include(p => p.Menu)
                .Where(p => p.UserId == userId)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdatePermissionsAsync(int userId, List<UserPermission> permissions)
    {
        try
        {
            // Remove existing permissions for this user (simplest way to update)
            var existing = await _context.UserPermissions.Where(p => p.UserId == userId).ToListAsync();
            _context.UserPermissions.RemoveRange(existing);
            
            // Add new permissions (only ones that are actually granted something, to save space?)
            // Or just add all submitted.
            // Let's filter to only save if at least one permission is true
            var toAdd = permissions.Where(p => p.CanView || p.CanCreate || p.CanEdit || p.CanDelete).ToList();
            
            foreach(var p in toAdd)
            {
                p.Id = 0; // Reset ID to ensure insert
                p.UserId = userId; // Ensure User ID
                p.LastUpdated = DateTime.Now;
            }
            
            _context.UserPermissions.AddRange(toAdd);
            await _context.SaveChangesAsync();
            
            return (true, "Permissions updated successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error updating permissions: " + ex.Message);
        }
    }

    public async Task<(bool success, string message)> SavePermissionAsync(UserPermission permission)
    {
        try
        {
            var existing = await _context.UserPermissions
                .FirstOrDefaultAsync(p => p.UserId == permission.UserId && p.MenuId == permission.MenuId);

            if (existing != null)
            {
                // Update existing
                existing.CanView = permission.CanView;
                existing.CanCreate = permission.CanCreate;
                existing.CanEdit = permission.CanEdit;
                existing.CanDelete = permission.CanDelete;
                existing.LastUpdated = DateTime.Now;
                _context.UserPermissions.Update(existing);
            }
            else
            {
                // Insert new
                permission.Id = 0;
                permission.LastUpdated = DateTime.Now;
                _context.UserPermissions.Add(permission);
            }

            await _context.SaveChangesAsync();
            return (true, "Permission updated.");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<bool> HasPermissionAsync(int userId, string controllerName, string actionName, string permissionType = "View")
    {
        try
        {
            if (string.IsNullOrEmpty(controllerName)) return true; // No protection on simple links?

            // Find the menu item
            var menu = await _context.Menus
                .FirstOrDefaultAsync(m => m.ControllerName == controllerName && (string.IsNullOrEmpty(actionName) || m.ActionName == actionName));

            if (menu == null) return true; // If not in Menu Master, assume public/protected by login only

            var permission = await _context.UserPermissions
                .FirstOrDefaultAsync(p => p.UserId == userId && p.MenuId == menu.Id);

            if (permission == null) return false; // Default to deny if no permission record found

            switch (permissionType)
            {
                case "View": return permission.CanView;
                case "Create": return permission.CanCreate;
                case "Edit": return permission.CanEdit;
                case "Delete": return permission.CanDelete;
                default: return false;
            }
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion

    #region User Management
    public async Task<List<User>> GetAllUsersAsync()
    {
        try
        {
            return await _context.Users.OrderBy(u => u.Username).ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

