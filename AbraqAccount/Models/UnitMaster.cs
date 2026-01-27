using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AbraqAccount.Models;

[Table("Unit_master")]
public class UnitMaster
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.None)] // Based on SQL INSERTs having explicit IDs
    public int Id { get; set; }

    public string? Ucode { get; set; }

    public string? UnitName { get; set; }

    public string? Stat { get; set; }

    public string? Details { get; set; }
}
