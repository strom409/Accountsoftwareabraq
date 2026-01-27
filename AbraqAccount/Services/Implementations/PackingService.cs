using Microsoft.EntityFrameworkCore;
using AbraqAccount.Data;
using AbraqAccount.Models;
using AbraqAccount.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AbraqAccount.Services.Implementations;

public class PackingService : IPackingService
{
    private readonly AppDbContext _context;

    public PackingService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<PackingRecipe>> GetPackingRecipesAsync(string? searchTerm)
    {
        var query = _context.PackingRecipes
            .Include(p => p.Materials)
            .AsQueryable();

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(p => 
                (p.RecipeCode != null && p.RecipeCode.Contains(searchTerm)) ||
                (p.recipename != null && p.recipename.Contains(searchTerm)));
        }

        return await query.OrderByDescending(p => p.createddate).ToListAsync();
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
            
            foreach (var material in materials)
            {
                material.PackingRecipeId = model.Recipeid;
                material.CreatedAt = DateTime.Now;
                _context.PackingRecipeMaterials.Add(material);
            }
            await _context.SaveChangesAsync();

            return (true, "Packing Recipe created successfully!");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<PackingRecipe?> GetPackingRecipeByIdAsync(long id)
    {
        return await _context.PackingRecipes
            .Include(p => p.Materials)
                .ThenInclude(m => m.PurchaseItem)
            .FirstOrDefaultAsync(m => m.Recipeid == id);
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
            existing.ItemWeight = (double)model.CostUnit; 
            existing.labourcost = model.LabourCost;
            existing.HighDensityRate = (double)model.HighDensityRate;
            existing.status = model.IsActive;
            existing.flagdeleted = false;

            if (materials != null && materials.Any())
            {
                existing.unitcost = materials.Sum(m => m.Value);
                
                foreach (var material in materials)
                {
                    if (material.PurchaseItemId > 0)
                    {
                        var newMat = new PackingRecipeMaterial
                        {
                            PackingRecipeId = existing.Recipeid,
                            PurchaseItemId = material.PurchaseItemId,
                            Qty = material.Qty,
                            UOM = material.UOM ?? "",
                            Value = material.Value,
                            CreatedAt = DateTime.Now
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
        model.Recipeid = id;
        return await SavePackingRecipeAsync(model, materials);
    }

    public async Task<IEnumerable<LookupItem>> GetPackingMaterialsAsync(string? searchTerm)
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

    public async Task<string> GetMaterialUOMAsync(int id)
    {
        var material = await _context.PurchaseItems.FindAsync(id);
        return material?.UOM ?? "";
    }

    public async Task<object?> GetSpecialRateFormDataAsync(long id)
    {
        var recipe = await _context.PackingRecipes
            .Include(p => p.Materials)
                .ThenInclude(m => m.PurchaseItem)
            .FirstOrDefaultAsync(r => r.Recipeid == id);

        if (recipe == null) return null;

        var growerGroups = await _context.GrowerGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.GroupName)
            .Select(g => new { id = g.Id, name = g.GroupName, code = g.GroupCode })
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
            growerGroups = growerGroups
        };
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
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task LoadRecipeDropdownsAsync(dynamic viewBag)
    {
        var uomList = await _context.UOMs
            .Where(u => u.IsActive && u.IsApproved)
            .OrderBy(u => u.UOMName)
            .Select(u => new { Value = u.UOMName, Text = u.UOMName })
            .ToListAsync();

        viewBag.RecipeUOMName = new SelectList(uomList, "Value", "Text");
    }

    private List<PackingRecipeMaterial> GetMaterialsFromForm(IFormCollection form)
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

    // --- Packing Special Rate Implementation ---

    public async Task<List<PackingSpecialRate>> GetPackingSpecialRatesAsync(string? growerGroupSearch, string? growerNameSearch, string? status)
    {
        var query = _context.PackingSpecialRates
            .Include(p => p.GrowerGroup)
            .Include(p => p.Farmer)
            .AsQueryable();

        if (!string.IsNullOrEmpty(growerGroupSearch))
        {
            query = query.Where(p => 
                (p.GrowerGroup != null && p.GrowerGroup.GroupName.Contains(growerGroupSearch)) ||
                (p.GrowerGroup != null && p.GrowerGroup.GroupCode.Contains(growerGroupSearch)));
        }

        if (!string.IsNullOrEmpty(growerNameSearch))
        {
            query = query.Where(p => 
                (p.Farmer != null && p.Farmer.FarmerName.Contains(growerNameSearch)) ||
                (p.Farmer != null && p.Farmer.FarmerCode.Contains(growerNameSearch)));
        }

        if (!string.IsNullOrEmpty(status))
        {
            bool isActive = status.ToLower() == "active";
            query = query.Where(p => p.IsActive == isActive);
        }

        return await query.OrderByDescending(p => p.EffectiveDate).ToListAsync();
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
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<PackingSpecialRate?> GetPackingSpecialRateByIdAsync(int id)
    {
        return await _context.PackingSpecialRates
            .Include(p => p.GrowerGroup)
            .Include(p => p.Farmer)
            .Include(p => p.Details)
                .ThenInclude(d => d.PurchaseItem)
            .FirstOrDefaultAsync(m => m.Id == id);
    }

    public async Task<(bool success, string message)> UpdatePackingSpecialRateAsync(int id, PackingSpecialRate model, List<PackingSpecialRateDetail> details)
    {
        try
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

            if (details != null && details.Any())
            {
                foreach (var detail in details)
                {
                    if (detail.PurchaseItemId > 0)
                    {
                        detail.PackingSpecialRateId = id;
                        detail.CreatedAt = DateTime.Now;
                        _context.PackingSpecialRateDetails.Add(detail);
                    }
                }
            }
            await _context.SaveChangesAsync();
            return (true, "Updated successfully");
        }
        catch (Exception ex)
        {
            return (false, "Error: " + ex.Message);
        }
    }

    public async Task<IEnumerable<LookupItem>> GetPackingItemsForRateAsync()
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

    public async Task<IEnumerable<LookupItem>> GetFarmersByGroupAsync(int groupId)
    {
        return await _context.Farmers
            .Where(f => f.GroupId == groupId && f.IsActive)
            .OrderBy(f => f.FarmerName)
            .Select(f => new LookupItem { Id = f.Id, Name = f.FarmerName })
            .ToListAsync();
    }

    public async Task LoadSpecialRateDropdownsAsync(dynamic viewBag)
    {
        var growerGroups = await _context.GrowerGroups
            .Where(g => g.IsActive)
            .OrderBy(g => g.GroupName)
            .ToListAsync();

        viewBag.GrowerGroupId = new SelectList(growerGroups, "Id", "GroupName");
    }

    private List<PackingSpecialRateDetail> GetSpecialRateDetailsFromForm(IFormCollection form, int specialRateId)
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
}

