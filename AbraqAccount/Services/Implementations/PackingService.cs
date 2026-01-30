using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;
using AbraqAccount.Models.Common;
using AbraqAccount.Extensions;

namespace AbraqAccount.Services.Implementations;

public class PackingService : IPackingService
{
    private readonly AppDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public PackingService(AppDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }
    #region Packing Recipe
    public async Task<List<PackingRecipe>> GetPackingRecipesAsync(string? searchTerm)
    {
        var query = _context.PackingRecipes
            .AsQueryable();
        try
        {

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => 
                    (p.RecipeCode != null && p.RecipeCode.Contains(searchTerm)) ||
                    (p.recipename != null && p.recipename.Contains(searchTerm)));
            }

            return await query.OrderByDescending(p => p.createddate).ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreatePackingRecipeAsync(PackingRecipe model, IFormCollection form)
    {
        try
        {
             var materials = GetMaterialsFromForm(form);

            // Generate Recipe Code and ID
            var lastRecipe = await _context.PackingRecipes.OrderByDescending(r => r.Recipeid).FirstOrDefaultAsync();
            long nextId = (lastRecipe?.Recipeid ?? 0) + 1;
            int nextCode = 1;
            if (lastRecipe != null && !string.IsNullOrEmpty(lastRecipe.RecipeCode))
            {
                if (int.TryParse(lastRecipe.RecipeCode, out int lastCode)) nextCode = lastCode + 1;
            }
            
            model.Recipeid = nextId;
            model.RecipeCode = nextCode.ToString("D4");
            model.createddate = DateTime.Now;
            model.flagdeleted = false;
            model.status = true;

            if (materials.Any()) model.unitcost = materials.Sum(m => m.Value);
            else model.unitcost = 0;

            _context.PackingRecipes.Add(model);
            
            // Get last item ID for sequence
            var lastItem = await _context.PackingRecipeMaterials
                .AsNoTracking()
                .OrderByDescending(m => m.RecipeItemId)
                .FirstOrDefaultAsync();
            long nextItemId = (lastItem?.RecipeItemId ?? 0) + 1;

            foreach (var material in materials)
            {
                material.RecipeItemId = nextItemId++;
                material.RecipeId = model.Recipeid;
                material.createddate = DateTime.Now;
                _context.PackingRecipeMaterials.Add(material);
            }
            await _context.SaveChangesAsync();

            return (true, "Packing Recipe created successfully!");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += " Inner Error: " + ex.InnerException.Message;
                if (ex.InnerException.InnerException != null)
                {
                    msg += " Details: " + ex.InnerException.InnerException.Message;
                }
            }
            return (false, "Error: " + msg);
        }
    }

    public async Task<PackingRecipe?> GetPackingRecipeByIdAsync(long id)
    {
        var recipe = await _context.PackingRecipes
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.Recipeid == id);

        if (recipe != null)
        {
            // 1. Get raw materials first (without Include) to avoid dropping rows with broken FKs
            var materials = await _context.PackingRecipeMaterials
                .AsNoTracking()
                .Where(m => m.RecipeId == id && !m.flagdeleted)
                .ToListAsync();
                
            if (materials.Any())
            {
                // 2. Get distinct Item IDs
                var itemIds = materials.Select(m => m.packingitemid).Distinct().ToList();
                
                // 3. Fetch valid items
                var items = await _context.PurchaseItems
                    .AsNoTracking()
                    .Where(p => itemIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);
                    
                // 4. Stitch manually
                foreach (var mat in materials)
                {
                    if (items.TryGetValue(mat.packingitemid, out var item))
                    {
                        mat.PurchaseItem = item;
                    }
                    else
                    {
                        // Handle missing item (Broken FK)
                        mat.PurchaseItem = new PurchaseItem { ItemName = "Unknown (Missing)", Code = "N/A", UOM = "" };
                        mat.MaterialName = "Unknown (Missing)"; 
                    }
                }
                
                recipe.Materials = materials;
            }
        }

        return recipe;
    }


    public async Task<(bool success, string message)> SavePackingRecipeAsync(PackingRecipe model, List<PackingRecipeMaterial> materials)
    {
        try
        {
            PackingRecipe? existing;
            
            if (model.Recipeid == 0)
            {
                // Creation Logic - Use AsNoTracking() for the lookup to avoid tracking conflicts
                var lastRecipe = await _context.PackingRecipes
                    .AsNoTracking()
                    .OrderByDescending(r => r.Recipeid)
                    .FirstOrDefaultAsync();

                long nextId = (lastRecipe?.Recipeid ?? 0) + 1;
                int nextCode = 1;

                if (lastRecipe != null && !string.IsNullOrEmpty(lastRecipe.RecipeCode))
                {
                    if (int.TryParse(lastRecipe.RecipeCode, out int lastCode)) nextCode = lastCode + 1;
                }
                
                existing = new PackingRecipe
                {
                    Recipeid = nextId,
                    RecipeCode = nextCode.ToString("D4"),
                    createddate = DateTime.Now,
                    flagdeleted = false,
                    status = true
                };
                _context.PackingRecipes.Add(existing);
            }
            else
            {
                existing = await _context.PackingRecipes
                    .Include(p => p.Materials)
                    .FirstOrDefaultAsync(m => m.Recipeid == model.Recipeid);
                
                if (existing == null) return (false, "Recipe not found.");
                
                existing.updateddate = DateTime.Now;
                
                // Clear old materials for update
                if (existing.Materials != null && existing.Materials.Any())
                {
                    _context.PackingRecipeMaterials.RemoveRange(existing.Materials);
                }
            }

            // Sync properties from model to existing
            existing.recipename = model.RecipeName;
            existing.RecipePackageId = model.RecipePackageId;
            existing.ItemWeight = (double)model.CostUnit; 
            existing.labourcost = model.LabourCost;
            existing.HighDensityRate = (double)model.HighDensityRate;
            existing.status = model.IsActive;
            existing.flagdeleted = false;

            if (materials != null && materials.Any())
            {
                existing.unitcost = materials.Sum(m => m.Value);
                
                // Get last item ID for sequence
                var lastItem = await _context.PackingRecipeMaterials
                    .AsNoTracking()
                    .OrderByDescending(m => m.RecipeItemId)
                    .FirstOrDefaultAsync();
                long nextItemId = (lastItem?.RecipeItemId ?? 0) + 1;

                foreach (var material in materials)
                {
                    if (material.PurchaseItemId > 0)
                    {
                        var newMat = new PackingRecipeMaterial
                        {
                            RecipeItemId = nextItemId++,
                            RecipeId = existing.Recipeid,
                            packingitemid = material.PurchaseItemId,
                            qty = (double)material.Qty,
                            avgCost = material.Value,
                            flagdeleted = false,
                            createddate = DateTime.Now
                        };
                        _context.PackingRecipeMaterials.Add(newMat);
                    }
                }
            }
            else
            {
                existing.unitcost = 0;
            }

            await _context.SaveChangesAsync();

            // Log History
            try
            {
                var userSession = _httpContextAccessor.HttpContext?.Session.GetObject<UserSession>(SessionKeys.UserSession);
                var username = userSession?.Username ?? "Unknown";
                var history = new TransactionHistory
                {
                    VoucherNo = existing.RecipeCode ?? existing.Recipeid.ToString("D4"),
                    VoucherType = "PackingRecipe",
                    Action = model.Recipeid == 0 ? "Insert" : "Edit",
                    User = username,
                    ActionDate = DateTime.Now,
                    Remarks = $"Recipe saved: {existing.recipename}"
                };
                _context.TransactionHistories.Add(history);
                await _context.SaveChangesAsync();
            }
            catch { /* Ignore logging errors */ }

            return (true, model.Recipeid == 0 ? "Created successfully" : "Updated successfully");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null) msg += " Inner: " + ex.InnerException.Message;
            return (false, "Error: " + msg);
        }
    }

    public async Task<(bool success, string message)> UpdatePackingRecipeAsync(long id, PackingRecipe model, List<PackingRecipeMaterial> materials)
    {
        try
        {
            model.Recipeid = id;
            return await SavePackingRecipeAsync(model, materials);
        }
        catch (Exception ex)
        {
             return (false, "Error: " + ex.Message);
        }
    }

    public async Task<IEnumerable<LookupItem>> GetPackingMaterialsAsync(string? searchTerm)
    {
        try
        {
            var query = _context.PurchaseItems
                .Where(p => p.InventoryType == "Packing Inventory" && p.IsActive)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.ItemName.Contains(searchTerm) || p.Code.Contains(searchTerm));
            }

            return await query
                .OrderBy(p => p.ItemName)
                .Select(p => new LookupItem { 
                    Id = p.Id, 
                    Name = p.ItemName,
                    UOM = p.UOM,
                    Rate = p.PurchaseCostingPerNos
                })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<string> GetMaterialUOMAsync(int id)
    {
        try
        {
            var material = await _context.PurchaseItems.FindAsync(id);
            return material?.UOM ?? "";
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<object?> GetSpecialRateFormDataAsync(long id)
    {
        try
        {
            var recipe = await _context.PackingRecipes
                .Include(p => p.Materials)
                    .ThenInclude(m => m.PurchaseItem)
                .FirstOrDefaultAsync(r => r.Recipeid == id);

            if (recipe == null) return null;

            var mainGrowers = await _context.BankMasters
                .Where(b => b.IsActive && b.SourceType == "C")
                .OrderBy(b => b.AccountName)
                .Select(b => new { id = b.PartyId ?? 0, name = b.AccountName, code = "" })
                .ToListAsync();

            return new
            {
                recipeId = recipe.Recipeid,
                recipeName = recipe.RecipeName,
                materials = recipe.Materials.Select(m => new
                {
                    id = m.PurchaseItemId,
                    name = m.PurchaseItem?.ItemName ?? "",
                    code = m.PurchaseItem?.Code ?? ""
                }).ToList(),
                growerGroups = mainGrowers
            };
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> SaveSpecialRateAsync(SavePackingRateRequest request)
    {
        try
        {
            var specialRate = new PackingRecipeSpecialRate
            {
                PackingRecipeId = request.RecipeId,
                GrowerGroupId = request.GrowerGroupId,
                EffectiveFrom = request.EffectiveFrom,
                HighDensityRate = request.HighDensityRate,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.PackingRecipeSpecialRates.Add(specialRate);
            await _context.SaveChangesAsync();

            if (request.Details != null && request.Details.Any())
            {
                foreach (var detail in request.Details)
                {
                    if (detail.PurchaseItemId > 0)
                    {
                        var rateDetail = new PackingRecipeSpecialRateDetail
                        {
                            PackingRecipeSpecialRateId = specialRate.Id,
                            PurchaseItemId = detail.PurchaseItemId,
                            Rate = detail.Rate,
                            CreatedAt = DateTime.Now
                        };
                        _context.PackingRecipeSpecialRateDetails.Add(rateDetail);
                    }
                }
                await _context.SaveChangesAsync();
            }
            return (true, "Special Rate saved successfully!");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += " Inner Error: " + ex.InnerException.Message;
                if (ex.InnerException.InnerException != null)
                {
                    msg += " Details: " + ex.InnerException.InnerException.Message;
                }
            }
            return (false, "Error: " + msg);
        }
    }

    public async Task LoadRecipeDropdownsAsync(dynamic viewBag)
    {
        try
        {
            // Get approved UOMs from Master table (points to RecipePackageId)
            var units = await _context.UOMs
                .Where(u => u.IsActive && u.IsApproved)
                .OrderBy(u => u.UOMName)
                .Select(u => new { Value = u.Id.ToString(), Text = u.UOMName })
                .ToListAsync();

            viewBag.RecipeUOMName = new SelectList(units, "Value", "Text");
        }
        catch (Exception)
        {
            throw;
        }
    }

    private List<PackingRecipeMaterial> GetMaterialsFromForm(IFormCollection form)
    {
        try
        {
            var materials = new List<PackingRecipeMaterial>();
            var materialIndex = 0;
            
            while (form.ContainsKey($"materials[{materialIndex}].PurchaseItemId"))
            {
                var purchaseItemIdStr = form[$"materials[{materialIndex}].PurchaseItemId"].ToString();
                var qtyStr = form[$"materials[{materialIndex}].Qty"].ToString();
                var uomStr = form[$"materials[{materialIndex}].UOM"].ToString();
                var valueStr = form[$"materials[{materialIndex}].Value"].ToString();

                if (int.TryParse(purchaseItemIdStr, out int purchaseItemId) && purchaseItemId > 0)
                {
                    if (decimal.TryParse(qtyStr, out decimal qty) && decimal.TryParse(valueStr, out decimal value))
                    {
                        var material = new PackingRecipeMaterial
                        {
                            PurchaseItemId = purchaseItemId,
                            Qty = qty,
                            UOM = uomStr ?? "",
                            Value = value,
                            CreatedAt = DateTime.Now
                        };
                        materials.Add(material);
                    }
                }
                materialIndex++;
            }
            return materials;
        }
        catch (Exception)
        {
             throw;
        }
    }

    #endregion

    #region Packing Special Rate Implementation

    public async Task<List<PackingSpecialRate>> GetPackingSpecialRatesAsync(string? growerGroupSearch, string? growerNameSearch, string? status)
    {
        try
        {
            var query = _context.PackingSpecialRates.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(status) && !status.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            {
                bool isActive = status.Equals("Active", StringComparison.OrdinalIgnoreCase);
                query = query.Where(p => p.IsActive == isActive);
            }

            var list = await query.OrderByDescending(p => p.EffectiveDate).ToListAsync();

            // Collect IDs to fetch names from new sources
            var mainIds = list.Where(p => p.GrowerGroupId.HasValue).Select(p => p.GrowerGroupId.Value).Distinct().ToList();
            var subIds = list.Where(p => p.FarmerId.HasValue).Select(p => (long)p.FarmerId.Value).Distinct().ToList();

            var mainGrowers = await _context.BankMasters
                .Where(b => b.PartyId.HasValue && mainIds.Contains((long)b.PartyId.Value))
                .ToDictionaryAsync(b => (long)b.PartyId!.Value, b => b.AccountName);
            var subGrowers = await _context.PartySubs.Where(s => subIds.Contains(s.PartyId)).ToDictionaryAsync(s => s.PartyId, s => s.PartyName);

            foreach (var item in list)
            {
                if (item.GrowerGroupId.HasValue && mainGrowers.TryGetValue(item.GrowerGroupId.Value, out var mName))
                {
                    item.GrowerGroup = new BankMaster { AccountName = mName };
                }
                if (item.FarmerId.HasValue && subGrowers.TryGetValue(item.FarmerId.Value, out var sName))
                {
                    item.Farmer = new PartySub { PartyName = sName };
                }
            }

            // Apply text filters in memory
            if (!string.IsNullOrEmpty(growerGroupSearch))
            {
                list = list.Where(p => p.GrowerGroup != null && p.GrowerGroup.AccountName.Contains(growerGroupSearch, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            if (!string.IsNullOrEmpty(growerNameSearch))
            {
                list = list.Where(p => p.Farmer != null && p.Farmer.PartyName.Contains(growerNameSearch, StringComparison.OrdinalIgnoreCase)).ToList();
            }

            return list;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreatePackingSpecialRateAsync(PackingSpecialRate model, IFormCollection form)
    {
        try
        {
            model.CreatedAt = DateTime.Now;
            if (!form.ContainsKey("IsActive")) model.IsActive = false;

            _context.PackingSpecialRates.Add(model);
            await _context.SaveChangesAsync();

            var details = GetSpecialRateDetailsFromForm(form, model.Id);
            if (details.Any())
            {
                _context.PackingSpecialRateDetails.AddRange(details);
                await _context.SaveChangesAsync();
            }
            
            return (true, "Created successfully");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += " Inner Error: " + ex.InnerException.Message;
                if (ex.InnerException.InnerException != null)
                {
                    msg += " Details: " + ex.InnerException.InnerException.Message;
                }
            }
            return (false, "Error: " + msg);
        }
    }

    public async Task<PackingSpecialRate?> GetPackingSpecialRateByIdAsync(int id)
    {
        try
        {
            var rate = await _context.PackingSpecialRates
                .Include(p => p.Details)
                    .ThenInclude(d => d.PurchaseItem)
                .FirstOrDefaultAsync(m => m.Id == id);
                
            if (rate != null)
            {
                if (rate.GrowerGroupId.HasValue)
                {
                    rate.GrowerGroup = await _context.BankMasters.FirstOrDefaultAsync(b => b.PartyId == (int)rate.GrowerGroupId.Value);
                }
                if (rate.FarmerId.HasValue)
                {
                    rate.Farmer = await _context.PartySubs.FirstOrDefaultAsync(p => p.PartyId == rate.FarmerId.Value);
                }
            }
            return rate;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> UpdatePackingSpecialRateAsync(int id, PackingSpecialRate model, List<PackingSpecialRateDetail> details)
    {
        try
        {
            // Clean up the main model to prevent EF from trying to insert/update parent groups/farmers
            model.GrowerGroup = null;
            model.Farmer = null;

            if (id > 0)
            {
                var existing = await _context.PackingSpecialRates
                    .Include(p => p.Details)
                    .FirstOrDefaultAsync(m => m.Id == id);
                
                if (existing == null) return (false, "Not found");

                existing.EffectiveDate = model.EffectiveDate;
                existing.GrowerGroupId = model.GrowerGroupId;
                existing.FarmerId = model.FarmerId;
                existing.IsActive = model.IsActive;

                _context.PackingSpecialRateDetails.RemoveRange(existing.Details);
                await _context.SaveChangesAsync(); // Commit the removals first
            }
            else
            {
                // For new records, ensure the details list is empty before adding the header
                // We will add filtered details manually afterwards
                model.Details = new List<PackingSpecialRateDetail>();
                model.CreatedAt = DateTime.Now;
                _context.PackingSpecialRates.Add(model);
                await _context.SaveChangesAsync();
                id = model.Id;
            }

            if (details != null)
            {
                foreach (var detail in details)
                {
                    // Only save lines where a Special Rate was actually entered
                    if (detail.SpecialRate.HasValue && detail.SpecialRate >= 0)
                    {
                        var newDetail = new PackingSpecialRateDetail
                        {
                            PackingSpecialRateId = id,
                            PurchaseItemId = detail.PurchaseItemId,
                            Rate = detail.Rate,
                            SpecialRate = detail.SpecialRate,
                            CreatedAt = DateTime.Now
                        };
                        _context.PackingSpecialRateDetails.Add(newDetail);
                    }
                }
                await _context.SaveChangesAsync();
            }

            return (true, "Saved successfully");
        }
        catch (Exception ex)
        {
            var msg = ex.Message;
            if (ex.InnerException != null)
            {
                msg += " Inner Error: " + ex.InnerException.Message;
                if (ex.InnerException.InnerException != null)
                {
                    msg += " Details: " + ex.InnerException.InnerException.Message;
                }
            }
            return (false, "Error: " + msg);
        }
    }

    public async Task<IEnumerable<LookupItem>> GetPackingItemsForRateAsync()
    {
        try
        {
            return await _context.PurchaseItems
                .Where(p => p.InventoryType == "Packing Inventory" && p.IsActive)
                .OrderBy(p => p.ItemName)
                .Select(p => new LookupItem { 
                    Id = p.Id, 
                    Name = p.ItemName,
                    Rate = p.PurchaseCostingPerNos // Assuming I added Rate to LookupItem or using PurchaseCostingPerNos
                })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> SearchMainGrowersAsync(string? searchTerm)
    {
        try
        {
            var query = _context.BankMasters.Where(b => b.IsActive && b.SourceType == "C");
            
            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(b => b.AccountName.Contains(searchTerm));
            }

            return await query
                .OrderBy(b => b.AccountName)
                .Select(b => new LookupItem { 
                    Id = b.PartyId ?? 0, 
                    Name = b.AccountName,
                    Type = "Grower"
                })
                .Take(20)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<IEnumerable<LookupItem>> GetFarmersByGroupAsync(long? groupId, string? searchTerm = null)
    {
        try
        {
            var query = _context.PartySubs.Where(p => p.FlagDeleted == false || p.FlagDeleted == null);
            
            if (groupId.HasValue && groupId > 0)
            {
                query = query.Where(p => p.MainId == groupId);
            }

            if (!string.IsNullOrEmpty(searchTerm))
            {
                query = query.Where(p => p.PartyName.Contains(searchTerm));
            }

            return await query
                .OrderBy(p => p.PartyName)
                .Select(p => new LookupItem { Id = (int)p.PartyId, Name = p.PartyName, GroupId = (int?)p.MainId })
                .Take(20)
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async Task<(bool success, string message)> CreateSubGrowerAsync(PartySub subGrower)
    {
        try
        {
            _context.PartySubs.Add(subGrower);
            await _context.SaveChangesAsync();
            return (true, "Sub Grower added successfully");
        }
        catch (Exception ex)
        {
            return (false, $"Error adding sub grower: {ex.Message}");
        }
    }

    public async Task LoadSpecialRateDropdownsAsync(dynamic viewBag)
    {
        try
        {
            // Fetch Main Growers from BankMaster where SourceType == 'C'
            var mainGrowers = await _context.BankMasters
                .Where(b => b.IsActive && b.SourceType == "C")
                .OrderBy(b => b.AccountName)
                .ToListAsync();

            viewBag.GrowerGroupId = new SelectList(mainGrowers.Select(b => new { Id = b.PartyId ?? 0, Name = b.AccountName }), "Id", "Name");
            
            // Load ALL sub-growers for initial lookup list if needed (Razor component expects this)
            var allSubGrowers = await _context.PartySubs
                .Where(p => (p.FlagDeleted == false || p.FlagDeleted == null))
                .OrderBy(p => p.PartyName)
                .Select(p => new LookupItem { Id = (int)p.PartyId, Name = p.PartyName, GroupId = (int?)p.MainId })
                .ToListAsync();
                
            viewBag.Farmers = allSubGrowers;
        }
        catch (Exception)
        {
             throw;
        }
    }

    private List<PackingSpecialRateDetail> GetSpecialRateDetailsFromForm(IFormCollection form, int specialRateId)
    {
        try
        {
            var details = new List<PackingSpecialRateDetail>();
            var detailIndex = 0;
            
            while (form.ContainsKey($"Details[{detailIndex}].PurchaseItemId"))
            {
                var purchaseItemIdStr = form[$"Details[{detailIndex}].PurchaseItemId"].ToString();
                var rateStr = form[$"Details[{detailIndex}].Rate"].ToString();
                var specialRateStr = form[$"Details[{detailIndex}].SpecialRate"].ToString();

                if (int.TryParse(purchaseItemIdStr, out int purchaseItemId) && purchaseItemId > 0)
                {
                    if (decimal.TryParse(rateStr, out decimal rate))
                    {
                        var detail = new PackingSpecialRateDetail
                        {
                            PackingSpecialRateId = specialRateId,
                            PurchaseItemId = purchaseItemId,
                            Rate = rate,
                            SpecialRate = !string.IsNullOrEmpty(specialRateStr) && decimal.TryParse(specialRateStr, out decimal specialRate) ? specialRate : null,
                            CreatedAt = DateTime.Now
                        };
                        details.Add(detail);
                    }
                }
                detailIndex++;
            }
            return details;
        }
        catch (Exception)
        {
            throw;
        }
    }
    public async Task<IEnumerable<object>> GetPackingRecipeHistoryAsync(long id)
    {
        try
        {
            var recipe = await _context.PackingRecipes.FindAsync(id);
            if (recipe == null) return Enumerable.Empty<object>();

            return await _context.TransactionHistories
                .Where(h => h.VoucherType == "PackingRecipe" && h.VoucherNo == recipe.RecipeCode)
                .OrderByDescending(h => h.ActionDate)
                .Select(h => new {
                    action = h.Action,
                    user = h.User,
                    dateTime = h.ActionDate,
                    remarks = h.Remarks
                })
                .ToListAsync();
        }
        catch (Exception)
        {
            throw;
        }
    }
    #endregion
}

