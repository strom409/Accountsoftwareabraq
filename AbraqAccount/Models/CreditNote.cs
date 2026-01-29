namespace AbraqAccount.Models;

public class CreditNote
{
    [System.ComponentModel.DataAnnotations.Key]
    [System.ComponentModel.DataAnnotations.Schema.DatabaseGenerated(System.ComponentModel.DataAnnotations.Schema.DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }
    public string CreditNoteNo { get; set; } = string.Empty;
    
    // Legacy / Specific FKs (Make optional/nullable in usage)
    public int? GroupId { get; set; }
    public int? FarmerId { get; set; }
    
    // Polymorphic Columns
    public int CreditAccountId { get; set; }
    public string CreditAccountType { get; set; } = string.Empty; // "Farmer", "GrowerGroup", "BankMaster", "SubGroupLedger"
    
    public int DebitAccountId { get; set; }
    public string DebitAccountType { get; set; } = string.Empty;

    public DateTime CreditNoteDate { get; set; }
    public decimal? Amount { get; set; }
    public string Status { get; set; } = "UnApproved"; // UnApproved, Approved
    public string? Remarks { get; set; }
    public string? Narration { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public string? Unit { get; set; }
    public bool IsActive { get; set; } = true;

    // New Columns
    public int? EntryForId { get; set; }
    public string? EntryForName { get; set; }

    // Navigation properties
    public GrowerGroup? GrowerGroup { get; set; }
    public Farmer? Farmer { get; set; }
    public List<CreditNoteDetail> Details { get; set; } = new List<CreditNoteDetail>();
    
    // Helper Properties (NotMapped) for Display
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string CreditAccountName { get; set; } = string.Empty; // Populated manually
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DebitAccountName { get; set; } = string.Empty; // Populated manually
}




