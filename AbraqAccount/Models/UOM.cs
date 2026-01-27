using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AbraqAccount.Models;

public class UOM
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(50)]
    public string UOMCode { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string UOMName { get; set; } = string.Empty;
    
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal Length { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal Width { get; set; }
    
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal Height { get; set; }
    
    [Column(TypeName = "decimal(18,4)")]
    public decimal CFT { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    public bool IsApproved { get; set; } = false;
    
    public bool IsInventory { get; set; } = false;
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

