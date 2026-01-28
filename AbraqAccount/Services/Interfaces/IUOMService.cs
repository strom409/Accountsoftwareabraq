using AbraqAccount.Models;

namespace AbraqAccount.Services.Interfaces;

public interface IUOMService
{
    Task<List<UOM>> GetUOMsAsync(string? searchTerm);
    Task<UOM?> GetUOMByIdAsync(int id);
    Task<bool> CreateUOMAsync(UOM uom, string username);
    Task<bool> UpdateUOMAsync(UOM uom, string username);
    Task<bool> ApproveUOMAsync(int id, string username);
    Task<bool> DeleteUOMAsync(int id);
    Task<List<UOMHistory>> GetUOMHistoryAsync(int uomId);
}
