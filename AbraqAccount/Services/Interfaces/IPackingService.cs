using AbraqAccount.Models;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Http;

namespace AbraqAccount.Services.Interfaces;

public interface IPackingService
{
    // Packing Recipe
    Task<List<PackingRecipe>> GetPackingRecipesAsync(string? searchTerm);
    Task<(bool success, string message)> CreatePackingRecipeAsync(PackingRecipe model, IFormCollection form);
    Task<PackingRecipe?> GetPackingRecipeByIdAsync(long id);
    Task<(bool success, string message)> UpdatePackingRecipeAsync(long id, PackingRecipe model, List<PackingRecipeMaterial> materials);
    Task<(bool success, string message)> SavePackingRecipeAsync(PackingRecipe model, List<PackingRecipeMaterial> materials);
    Task<IEnumerable<LookupItem>> GetPackingMaterialsAsync(string? searchTerm); // For AJAX
    Task<string> GetMaterialUOMAsync(int id);
    Task<object?> GetSpecialRateFormDataAsync(long id);
    Task<(bool success, string message)> SaveSpecialRateAsync(SavePackingRateRequest request);
    Task LoadRecipeDropdownsAsync(dynamic viewBag);
    
    // Packing Special Rate
    Task<List<PackingSpecialRate>> GetPackingSpecialRatesAsync(string? growerGroupSearch, string? growerNameSearch, string? status);
    Task<(bool success, string message)> CreatePackingSpecialRateAsync(PackingSpecialRate model, Microsoft.AspNetCore.Http.IFormCollection form);
    Task<PackingSpecialRate?> GetPackingSpecialRateByIdAsync(int id);
    Task<(bool success, string message)> UpdatePackingSpecialRateAsync(int id, PackingSpecialRate model, List<PackingSpecialRateDetail> details);
    Task<IEnumerable<LookupItem>> GetPackingItemsForRateAsync();
    Task<IEnumerable<LookupItem>> GetFarmersByGroupAsync(int groupId);
    Task LoadSpecialRateDropdownsAsync(dynamic viewBag);
}
