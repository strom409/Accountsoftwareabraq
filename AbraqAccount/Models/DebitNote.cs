using System.ComponentModel.DataAnnotations;

namespace AbraqAccount.Models;

public class DebitNote
{
    public int Id { get; set; }
    
    public string DebitNoteNo { get; set; } = string.Empty; // Auto-generated
    
    [Required]
    public string Unit { get; set; } = string.Empty;
    
    // Legacy / Specific FK (Make optional/nullable in usage)
    public int? BankMasterId { get; set; } 
    
    // Polymorphic Columns
    public int CreditAccountId { get; set; }
    public string CreditAccountType { get; set; } = string.Empty;
    
    public int DebitAccountId { get; set; }
    public string DebitAccountType { get; set; } = string.Empty;

    // Helper Properties (NotMapped) for Display
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string CreditAccountName { get; set; } = string.Empty;
    
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string DebitAccountName { get; set; } = string.Empty;

    [Required]
    public DateTime DebitNoteDate { get; set; } = DateTime.Now;
    
    public decimal? Amount { get; set; }
    
    public string Status { get; set; } = "UnApproved"; // UnApproved, Approved, etc.
    
    public string? Narration { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    
    public bool IsActive { get; set; } = true;
    
    // New Columns
    public int? EntryForId { get; set; }
    public string? EntryForName { get; set; }

    // Navigation properties
    public BankMaster? BankMaster { get; set; }
    public List<DebitNoteDetail> Details { get; set; } = new List<DebitNoteDetail>();
}

public class DebitNoteDetail
{
    public int Id { get; set; }
    public int DebitNoteId { get; set; }
    
    [Required]
    public string AccountType { get; set; } = string.Empty; // Discount, Return, Freight Expense, etc.
    
    public string? RefNo { get; set; } // Bill No
    
    public string? HsnSacCode { get; set; }
    
    public decimal? Qty { get; set; }
    
    public decimal? Rate { get; set; }
    
    [Required]
    public decimal Amount { get; set; }
    
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    
    // Navigation property
    public DebitNote? DebitNote { get; set; }
}


