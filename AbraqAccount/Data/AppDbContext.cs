using Microsoft.EntityFrameworkCore;
using AbraqAccount.Models;

namespace AbraqAccount.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; }
    public DbSet<MasterGroup> MasterGroups { get; set; }
    public DbSet<MasterSubGroup> MasterSubGroups { get; set; }
    public DbSet<SubGroupLedger> SubGroupLedgers { get; set; }
    public DbSet<GrowerGroup> GrowerGroups { get; set; }
    public DbSet<Farmer> Farmers { get; set; }
    public DbSet<Lot> Lots { get; set; }
    public DbSet<CreditNote> CreditNotes { get; set; }
    public DbSet<CreditNoteDetail> CreditNoteDetails { get; set; }
    public DbSet<BankMaster> BankMasters { get; set; }
    public DbSet<BankBook> BankBooks { get; set; }
    public DbSet<PurchaseItemGroup> PurchaseItemGroups { get; set; }
    public DbSet<PurchaseItemGroupHistory> PurchaseItemGroupHistories { get; set; }
    public DbSet<PurchaseItem> PurchaseItems { get; set; }
    public DbSet<PurchaseItemHistory> PurchaseItemHistories { get; set; }
    public DbSet<SpecialRate> SpecialRates { get; set; }
    public DbSet<PackingSpecialRate> PackingSpecialRates { get; set; }
    public DbSet<PackingSpecialRateDetail> PackingSpecialRateDetails { get; set; }
    public DbSet<PurchaseOrderTC> PurchaseOrderTCs { get; set; }
    public DbSet<PurchaseOrderTCHistory> PurchaseOrderTCHistories { get; set; }
    public DbSet<PackingRecipe> PackingRecipes { get; set; }
    public DbSet<PackingRecipeMaterial> PackingRecipeMaterials { get; set; }
    public DbSet<PackingRecipeSpecialRate> PackingRecipeSpecialRates { get; set; }
    public DbSet<PackingRecipeSpecialRateDetail> PackingRecipeSpecialRateDetails { get; set; }
    public DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }
    public DbSet<PurchaseOrderMiscCharge> PurchaseOrderMiscCharges { get; set; }
    public DbSet<PurchaseOrderTermsCondition> PurchaseOrderTermsConditions { get; set; }
    public DbSet<Vendor> Vendors { get; set; }
    public DbSet<PurchaseRequest> PurchaseRequests { get; set; }
    public DbSet<PurchaseRequestItem> PurchaseRequestItems { get; set; }
    public DbSet<PurchaseReceive> PurchaseReceives { get; set; }
    public DbSet<PurchaseReceiveItem> PurchaseReceiveItems { get; set; }
    public DbSet<MaterialIssue> MaterialIssues { get; set; }
    public DbSet<MaterialIssueItem> MaterialIssueItems { get; set; }
    public DbSet<DebitNote> DebitNotes { get; set; }
    public DbSet<DebitNoteDetail> DebitNoteDetails { get; set; }
    public DbSet<ReceiptEntry> ReceiptEntries { get; set; }
    public DbSet<PaymentSettlement> PaymentSettlements { get; set; }
    public DbSet<GeneralEntry> GeneralEntries { get; set; }
    public DbSet<ExpensesIncurred> ExpensesIncurreds { get; set; }
    public DbSet<AccountRule> AccountRules { get; set; }
    public DbSet<ExpenseItem> ExpenseItems { get; set; }
    public DbSet<UOM> UOMs { get; set; }
    public DbSet<UOMHistory> UOMHistories { get; set; }
    public DbSet<ExpenseMiscCharge> ExpenseMiscCharges { get; set; }
    public DbSet<TransactionHistory> TransactionHistories { get; set; }
    public DbSet<EntryForAccount> EntryForAccounts { get; set; }
    public DbSet<Menu> Menus { get; set; }
    public DbSet<UserPermission> UserPermissions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Map to SQL Server Menus table
        modelBuilder.Entity<Menu>(entity =>
        {
            entity.ToTable("Menus");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ControllerName).HasColumnName("ControllerName").HasMaxLength(100);
            entity.Property(e => e.ActionName).HasColumnName("ActionName").HasMaxLength(100);
            entity.Property(e => e.IconClass).HasColumnName("IconClass").HasMaxLength(100);
            entity.Property(e => e.ParentId).HasColumnName("ParentId");
            entity.Property(e => e.DisplayOrder).HasColumnName("DisplayOrder").HasDefaultValue(0);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasOne(e => e.Parent)
                  .WithMany(m => m.Children)
                  .HasForeignKey(e => e.ParentId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server UserPermissions table
        modelBuilder.Entity<UserPermission>(entity =>
        {
            entity.ToTable("UserPermissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.UserId).HasColumnName("UserId").IsRequired();
            entity.Property(e => e.MenuId).HasColumnName("MenuId").IsRequired();
            entity.Property(e => e.CanView).HasColumnName("CanView").HasDefaultValue(false);
            entity.Property(e => e.CanCreate).HasColumnName("CanCreate").HasDefaultValue(false);
            entity.Property(e => e.CanEdit).HasColumnName("CanEdit").HasDefaultValue(false);
            entity.Property(e => e.CanDelete).HasColumnName("CanDelete").HasDefaultValue(false);
            entity.Property(e => e.LastUpdated).HasColumnName("LastUpdated").HasDefaultValueSql("GETDATE()");
            
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.Menu)
                  .WithMany()
                  .HasForeignKey(e => e.MenuId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            // Allow multiple permissions per user/menu (technically just one row, but ensure unique combo)
            entity.HasIndex(e => new { e.UserId, e.MenuId }).IsUnique();
        });

        // Map to SQL Server UOMs table
        modelBuilder.Entity<UOM>(entity =>
        {
            entity.ToTable("UOMs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UOMCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.UOMName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Length).HasColumnType("decimal(18,4)").IsRequired();
            entity.Property(e => e.Width).HasColumnType("decimal(18,4)").IsRequired();
            entity.Property(e => e.Height).HasColumnType("decimal(18,4)").IsRequired();
            entity.Property(e => e.CFT).HasColumnType("decimal(18,4)");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsInventory).HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
        });

        // Map to SQL Server UOMHistories table
        modelBuilder.Entity<UOMHistory>(entity =>
        {
            entity.ToTable("UOMHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Action).HasMaxLength(50).IsRequired();
            entity.Property(e => e.User).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ActionDate).HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.Remarks).HasMaxLength(500);

            entity.HasOne(e => e.UOM)
                  .WithMany()
                  .HasForeignKey(e => e.UOMId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server users table
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Username).HasColumnName("Username").HasMaxLength(255);
            entity.Property(e => e.Password).HasColumnName("Password").HasMaxLength(255);
        });

        // Map to SQL Server MasterGroups table
        modelBuilder.Entity<MasterGroup>(entity =>
        {
            entity.ToTable("MasterGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50);
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        // Map to SQL Server MasterSubGroups table
        modelBuilder.Entity<MasterSubGroup>(entity =>
        {
            entity.ToTable("MasterSubGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.MasterGroupId).HasColumnName("MasterGroupId").IsRequired();
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            // Foreign key relationship
            entity.HasOne(e => e.MasterGroup)
                  .WithMany()
                  .HasForeignKey(e => e.MasterGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server SubGroupLedgers table
        modelBuilder.Entity<SubGroupLedger>(entity =>
        {
            entity.ToTable("SubGroupLedgers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.MasterGroupId).HasColumnName("MasterGroupId").IsRequired();
            entity.Property(e => e.MasterSubGroupId).HasColumnName("MasterSubGroupId").IsRequired();
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            // Foreign key relationships
            entity.HasOne(e => e.MasterGroup)
                  .WithMany()
                  .HasForeignKey(e => e.MasterGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.MasterSubGroup)
                  .WithMany()
                  .HasForeignKey(e => e.MasterSubGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server GrowerGroups table
        modelBuilder.Entity<GrowerGroup>(entity =>
        {
            entity.ToTable("GrowerGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.GroupCode).HasColumnName("GroupCode").HasMaxLength(50).IsRequired();
            entity.Property(e => e.GroupName).HasColumnName("GroupName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.GroupType).HasColumnName("GroupType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ContactPerson).HasColumnName("ContactPerson").HasMaxLength(255);
            entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(e => e.WhatsApp).HasColumnName("WhatsApp").HasMaxLength(20);
            entity.Property(e => e.Village).HasColumnName("Village").HasMaxLength(255);
            entity.Property(e => e.Tehsil).HasColumnName("Tehsil").HasMaxLength(255);
            entity.Property(e => e.BillingMode).HasColumnName("BillingMode").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasIndex(e => e.GroupCode).IsUnique();
        });

        // Map to SQL Server Farmers table
        modelBuilder.Entity<Farmer>(entity =>
        {
            entity.ToTable("Farmers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.FarmerCode).HasColumnName("FarmerCode").HasMaxLength(50).IsRequired();
            entity.Property(e => e.FarmerName).HasColumnName("FarmerName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.GroupId).HasColumnName("GroupId").IsRequired();
            entity.Property(e => e.Village).HasColumnName("Village").HasMaxLength(255);
            entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasIndex(e => e.FarmerCode).IsUnique();
            
            entity.HasOne(e => e.GrowerGroup)
                  .WithMany(g => g.Farmers)
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server Lots table
        modelBuilder.Entity<Lot>(entity =>
        {
            entity.ToTable("Lots");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.LotNo).HasColumnName("LotNo").HasMaxLength(100).IsRequired();
            entity.Property(e => e.GroupId).HasColumnName("GroupId").IsRequired();
            entity.Property(e => e.FarmerId).HasColumnName("FarmerId").IsRequired();
            entity.Property(e => e.ChamberNo).HasColumnName("ChamberNo").HasMaxLength(50);
            entity.Property(e => e.Cartons).HasColumnName("Cartons");
            entity.Property(e => e.Crates).HasColumnName("Crates");
            entity.Property(e => e.Bins).HasColumnName("Bins");
            entity.Property(e => e.Variety).HasColumnName("Variety").HasMaxLength(255);
            entity.Property(e => e.Grade).HasColumnName("Grade").HasMaxLength(50);
            entity.Property(e => e.ArrivalDate).HasColumnName("ArrivalDate").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasOne(e => e.GrowerGroup)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Farmer)
                  .WithMany(f => f.Lots)
                  .HasForeignKey(e => e.FarmerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server CreditNotes table
        modelBuilder.Entity<CreditNote>(entity =>
        {
            entity.ToTable("CreditNotes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id").ValueGeneratedOnAdd();
            entity.Property(e => e.CreditNoteNo).HasColumnName("CreditNoteNo").HasMaxLength(100).IsRequired();
            entity.Property(e => e.GroupId).HasColumnName("GroupId");
            entity.Property(e => e.FarmerId).HasColumnName("FarmerId");
            entity.Property(e => e.CreditNoteDate).HasColumnName("CreditNoteDate").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(500);
            entity.Property(e => e.Unit).HasColumnName("Unit").HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.EntryForId).HasColumnName("EntryForId");
            entity.Property(e => e.EntryForName).HasColumnName("EntryForName").HasMaxLength(255);
            
            entity.HasIndex(e => e.CreditNoteNo).IsUnique();
            
            entity.HasOne(e => e.GrowerGroup)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Farmer)
                  .WithMany()
                  .HasForeignKey(e => e.FarmerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server CreditNoteDetails table
        modelBuilder.Entity<CreditNoteDetail>(entity =>
        {
            entity.ToTable("CreditNoteDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.CreditNoteId).HasColumnName("CreditNoteId").IsRequired();
            entity.Property(e => e.AccountType).HasColumnName("AccountType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.RefNoBillNo).HasColumnName("RefNo").HasMaxLength(100);
            entity.Property(e => e.HsnSacCode).HasColumnName("HsnSacCode").HasMaxLength(50);
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Rate).HasColumnName("Rate").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasOne(e => e.CreditNote)
                  .WithMany(c => c.Details)
                  .HasForeignKey(e => e.CreditNoteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server BankMasters table
        modelBuilder.Entity<BankMaster>(entity =>
        {
            entity.ToTable("BankMasters");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.AccountName).HasColumnName("AccountName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.GroupId).HasColumnName("GroupId").IsRequired();
            entity.Property(e => e.Address).HasColumnName("Address").HasMaxLength(500);
            entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(255);
            entity.Property(e => e.AccountNumber).HasColumnName("AccountNumber").HasMaxLength(50);
            entity.Property(e => e.IfscCode).HasColumnName("IfscCode").HasMaxLength(20);
            entity.Property(e => e.BranchName).HasColumnName("BranchName").HasMaxLength(255);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(20).IsRequired();
            entity.Property(e => e.CreatedBy).HasColumnName("CreatedBy").HasMaxLength(255);
            entity.Property(e => e.CreatedDate).HasColumnName("CreatedDate").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            // Foreign key relationship
            entity.HasOne(e => e.Group)
                  .WithMany()
                  .HasForeignKey(e => e.GroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            // Unique constraint on AccountNumber (only for non-null values)
            entity.HasIndex(e => e.AccountNumber)
                  .IsUnique()
                  .HasFilter("[AccountNumber] IS NOT NULL");
        });

        // Map to SQL Server BankBooks table
        modelBuilder.Entity<BankBook>(entity =>
        {
            entity.ToTable("BankBooks");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.BankName).HasColumnName("BankName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.Date).HasColumnName("Date").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.TransactionType).HasColumnName("TransactionType").HasMaxLength(10);
            entity.Property(e => e.RefBankName).HasColumnName("RefBankName").HasMaxLength(255);
            entity.Property(e => e.ToBankName).HasColumnName("ToBankName").HasMaxLength(255);
            entity.Property(e => e.PaymentMode).HasColumnName("PaymentMode").HasMaxLength(50);
            entity.Property(e => e.TransactionNumber).HasColumnName("TransactionNumber").HasMaxLength(100);
            entity.Property(e => e.ChequeDate).HasColumnName("ChequeDate");
            entity.Property(e => e.Particular).HasColumnName("Particular").HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
        });

        // Map to SQL Server PurchaseItemGroups table
        modelBuilder.Entity<PurchaseItemGroup>(entity =>
        {
            entity.ToTable("PurchaseItemGroups");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(10).IsRequired();
            entity.Property(e => e.Name).HasColumnName("Name").HasMaxLength(255).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasIndex(e => e.Code).IsUnique();
        });

        // Map to SQL Server PurchaseItemGroupHistories table
        modelBuilder.Entity<PurchaseItemGroupHistory>(entity =>
        {
            entity.ToTable("PurchaseItemGroupHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseItemGroupId).HasColumnName("PurchaseItemGroupId").IsRequired();
            entity.Property(e => e.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.User).HasColumnName("User").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ActionDate).HasColumnName("ActionDate").IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnName("OldValues").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.NewValues).HasColumnName("NewValues").HasColumnType("NVARCHAR(MAX)");
            
            entity.HasOne(e => e.PurchaseItemGroup)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server PurchaseItems table
        modelBuilder.Entity<PurchaseItem>(entity =>
        {
            entity.ToTable("PurchaseItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.Code).HasColumnName("Code").HasMaxLength(50).IsRequired();
            entity.Property(e => e.InventoryType).HasColumnName("InventoryType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PurchaseItemGroupId).HasColumnName("PurchaseItemGroupId").IsRequired();
            entity.Property(e => e.BillingName).HasColumnName("BillingName").HasMaxLength(255);
            entity.Property(e => e.ItemName).HasColumnName("ItemName").HasMaxLength(255).IsRequired();
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50).IsRequired();
            entity.Property(e => e.MinimumStock).HasColumnName("MinimumStock").HasColumnType("decimal(18,2)");
            entity.Property(e => e.MaximumStock).HasColumnName("MaximumStock").HasColumnType("decimal(18,2)");
            entity.Property(e => e.PurchaseCostingPerNos).HasColumnName("PurchaseCostingPerNos").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.SaleCostingPerNos).HasColumnName("SaleCostingPerNos").HasColumnType("decimal(18,2)");
            entity.Property(e => e.GST).HasColumnName("GST").HasMaxLength(20).IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasIndex(e => e.Code).IsUnique();
            
            entity.HasOne(e => e.PurchaseItemGroup)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PurchaseItemHistories table
        modelBuilder.Entity<PurchaseItemHistory>(entity =>
        {
            entity.ToTable("PurchaseItemHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId").IsRequired();
            entity.Property(e => e.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.User).HasColumnName("User").HasMaxLength(255).IsRequired();
            entity.Property(e => e.ActionDate).HasColumnName("ActionDate").IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnName("OldValues").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.NewValues).HasColumnName("NewValues").HasColumnType("NVARCHAR(MAX)");
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server SpecialRates table
        modelBuilder.Entity<SpecialRate>(entity =>
        {
            entity.ToTable("SpecialRates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId").IsRequired();
            entity.Property(e => e.GrowerGroupId).HasColumnName("GrowerGroupId");
            entity.Property(e => e.FarmerId).HasColumnName("FarmerId");
            entity.Property(e => e.EffectiveFrom).HasColumnName("EffectiveFrom").IsRequired();
            entity.Property(e => e.LabourCost).HasColumnName("LabourCost").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.GrowerGroup)
                  .WithMany()
                  .HasForeignKey(e => e.GrowerGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Farmer)
                  .WithMany()
                  .HasForeignKey(e => e.FarmerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PackingSpecialRates table
        modelBuilder.Entity<PackingSpecialRate>(entity =>
        {
            entity.ToTable("PackingSpecialRates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.EffectiveDate).HasColumnName("EffectiveDate").IsRequired();
            entity.Property(e => e.GrowerGroupId).HasColumnName("GrowerGroupId");
            entity.Property(e => e.FarmerId).HasColumnName("FarmerId");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.GrowerGroup)
                  .WithMany()
                  .HasForeignKey(e => e.GrowerGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Farmer)
                  .WithMany()
                  .HasForeignKey(e => e.FarmerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PackingSpecialRateDetails table
        modelBuilder.Entity<PackingSpecialRateDetail>(entity =>
        {
            entity.ToTable("PackingSpecialRateDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PackingSpecialRateId).HasColumnName("PackingSpecialRateId").IsRequired();
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId").IsRequired();
            entity.Property(e => e.Rate).HasColumnName("Rate").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.SpecialRate).HasColumnName("SpecialRate").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PackingSpecialRate)
                  .WithMany(p => p.Details)
                  .HasForeignKey(e => e.PackingSpecialRateId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PurchaseOrderTCs table
        modelBuilder.Entity<PurchaseOrderTC>(entity =>
        {
            entity.ToTable("PurchaseOrderTCs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TCType).HasColumnName("TCType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Caption).HasColumnName("Caption").HasMaxLength(500).IsRequired();
            entity.Property(e => e.TermsAndConditions).HasColumnName("TermsAndConditions").HasColumnType("NVARCHAR(MAX)").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        // Map to SQL Server PurchaseOrderTCHistories table
        modelBuilder.Entity<PurchaseOrderTCHistory>(entity =>
        {
            entity.ToTable("PurchaseOrderTCHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseOrderTCId).HasColumnName("PurchaseOrderTCId").IsRequired();
            entity.Property(e => e.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.User).HasColumnName("User").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ActionDate).HasColumnName("ActionDate");
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(500);
            
            entity.HasOne(e => e.PurchaseOrderTC)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseOrderTCId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server PackingRecipes table
        modelBuilder.Entity<PackingRecipe>(entity =>
        {
            entity.ToTable("PackingRecipes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.RecipeCode).HasColumnName("RecipeCode").HasMaxLength(50).IsRequired();
            entity.Property(e => e.RecipeName).HasColumnName("RecipeName").HasMaxLength(200).IsRequired();
            entity.Property(e => e.RecipeUOMName).HasColumnName("RecipeUOMName").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CostUnit).HasColumnName("CostUnit").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.LabourCost).HasColumnName("LabourCost").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.HighDensityRate).HasColumnName("HighDensityRate").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.Value).HasColumnName("Value").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        // Map to SQL Server PackingRecipeMaterials table
        modelBuilder.Entity<PackingRecipeMaterial>(entity =>
        {
            entity.ToTable("PackingRecipeMaterials");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PackingRecipeId).HasColumnName("PackingRecipeId").IsRequired();
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId").IsRequired();
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Value).HasColumnName("Value").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PackingRecipe)
                  .WithMany(p => p.Materials)
                  .HasForeignKey(e => e.PackingRecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PackingRecipeSpecialRates table
        modelBuilder.Entity<PackingRecipeSpecialRate>(entity =>
        {
            entity.ToTable("PackingRecipeSpecialRates");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PackingRecipeId).HasColumnName("PackingRecipeId").IsRequired();
            entity.Property(e => e.GrowerGroupId).HasColumnName("GrowerGroupId");
            entity.Property(e => e.EffectiveFrom).HasColumnName("EffectiveFrom").IsRequired();
            entity.Property(e => e.HighDensityRate).HasColumnName("HighDensityRate").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            entity.HasOne(e => e.PackingRecipe)
                  .WithMany()
                  .HasForeignKey(e => e.PackingRecipeId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.GrowerGroup)
                  .WithMany()
                  .HasForeignKey(e => e.GrowerGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PackingRecipeSpecialRateDetails table
        modelBuilder.Entity<PackingRecipeSpecialRateDetail>(entity =>
        {
            entity.ToTable("PackingRecipeSpecialRateDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PackingRecipeSpecialRateId).HasColumnName("PackingRecipeSpecialRateId").IsRequired();
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId").IsRequired();
            entity.Property(e => e.Rate).HasColumnName("Rate").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PackingRecipeSpecialRate)
                  .WithMany(p => p.Details)
                  .HasForeignKey(e => e.PackingRecipeSpecialRateId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PurchaseOrders table
        modelBuilder.Entity<PurchaseOrder>(entity =>
        {
            entity.ToTable("PurchaseOrders");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PONumber).HasColumnName("PONumber").HasMaxLength(50);
            entity.Property(e => e.PODate).HasColumnName("PODate").IsRequired();
            entity.Property(e => e.POType).HasColumnName("POType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VendorId).HasColumnName("VendorId").IsRequired();
            entity.Property(e => e.ExpectedReceivedDate).HasColumnName("ExpectedReceivedDate").IsRequired();
            entity.Property(e => e.VendorReference).HasColumnName("VendorReference").HasMaxLength(100);
            entity.Property(e => e.BillingTo).HasColumnName("BillingTo").HasMaxLength(200).IsRequired();
            entity.Property(e => e.DeliveryAddress).HasColumnName("DeliveryAddress").HasMaxLength(500).IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.POQty).HasColumnName("POQty").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TaxAmount).HasColumnName("TaxAmount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("UnApproved");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.Vendor)
                  .WithMany()
                  .HasForeignKey(e => e.VendorId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PurchaseOrderItems table
        modelBuilder.Entity<PurchaseOrderItem>(entity =>
        {
            entity.ToTable("PurchaseOrderItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderId").IsRequired();
            entity.Property(e => e.PurchaseItemGroupId).HasColumnName("PurchaseItemGroupId").IsRequired();
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId").IsRequired();
            entity.Property(e => e.ItemDescription).HasColumnName("ItemDescription").HasMaxLength(500);
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Discount).HasColumnName("Discount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.GST).HasColumnName("GST").HasMaxLength(20).HasDefaultValue("NA");
            entity.Property(e => e.GSTAmount).HasColumnName("GSTAmount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.Items)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.PurchaseItemGroup)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PurchaseOrderMiscCharges table
        modelBuilder.Entity<PurchaseOrderMiscCharge>(entity =>
        {
            entity.ToTable("PurchaseOrderMiscCharges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderId").IsRequired();
            entity.Property(e => e.ExpenseType).HasColumnName("ExpenseType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Tax).HasColumnName("Tax").HasMaxLength(20).HasDefaultValue("Select");
            entity.Property(e => e.GSTAmount).HasColumnName("GSTAmount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.MiscCharges)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server PurchaseOrderTermsConditions table (for linking PO to T&C)
        modelBuilder.Entity<PurchaseOrderTermsCondition>(entity =>
        {
            entity.ToTable("PurchaseOrderTermsConditions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseOrderId).HasColumnName("PurchaseOrderId").IsRequired();
            entity.Property(e => e.PurchaseOrderTCId).HasColumnName("PurchaseOrderTCId").IsRequired();
            entity.Property(e => e.IsSelected).HasColumnName("IsSelected").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PurchaseOrder)
                  .WithMany(p => p.TermsAndConditions)
                  .HasForeignKey(e => e.PurchaseOrderId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.TermsAndConditions)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseOrderTCId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server Vendors table
        modelBuilder.Entity<Vendor>(entity =>
        {
            entity.ToTable("Vendors");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.VendorCode).HasColumnName("VendorCode").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VendorName).HasColumnName("VendorName").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ContactPerson).HasColumnName("ContactPerson").HasMaxLength(100);
            entity.Property(e => e.Phone).HasColumnName("Phone").HasMaxLength(20);
            entity.Property(e => e.Email).HasColumnName("Email").HasMaxLength(100);
            entity.Property(e => e.Address).HasColumnName("Address").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        // Map to SQL Server PurchaseRequests table
        modelBuilder.Entity<PurchaseRequest>(entity =>
        {
            entity.ToTable("PurchaseRequests");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PORequestNo).HasColumnName("PORequestNo").HasMaxLength(50);
            entity.Property(e => e.RequestDate).HasColumnName("RequestDate").IsRequired();
            entity.Property(e => e.RequestedById).HasColumnName("RequestedById").IsRequired();
            entity.Property(e => e.AssignedToId).HasColumnName("AssignedToId").IsRequired();
            entity.Property(e => e.RequestType).HasColumnName("RequestType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.RequestedBy)
                  .WithMany()
                  .HasForeignKey(e => e.RequestedById)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.AssignedTo)
                  .WithMany()
                  .HasForeignKey(e => e.AssignedToId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PurchaseRequestItems table
        modelBuilder.Entity<PurchaseRequestItem>(entity =>
        {
            entity.ToTable("PurchaseRequestItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseRequestId).HasColumnName("PurchaseRequestId").IsRequired();
            entity.Property(e => e.ItemName).HasColumnName("ItemName").HasMaxLength(200).IsRequired();
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ItemDescription).HasColumnName("ItemDescription").HasMaxLength(500);
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.UseOfItem).HasColumnName("UseOfItem").HasMaxLength(200).IsRequired();
            entity.Property(e => e.ItemRemarks).HasColumnName("ItemRemarks").HasMaxLength(500);
            entity.Property(e => e.IsReturnable).HasColumnName("IsReturnable").HasDefaultValue(false);
            entity.Property(e => e.IsReusable).HasColumnName("IsReusable").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PurchaseRequest)
                  .WithMany(p => p.Items)
                  .HasForeignKey(e => e.PurchaseRequestId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server PurchaseReceives table
        modelBuilder.Entity<PurchaseReceive>(entity =>
        {
            entity.ToTable("PurchaseReceives");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ReceiptNo).HasColumnName("ReceiptNo").HasMaxLength(50);
            entity.Property(e => e.PONumber).HasColumnName("PONumber").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Date).HasColumnName("Date").IsRequired();
            entity.Property(e => e.VendorId).HasColumnName("VendorId").IsRequired();
            entity.Property(e => e.PurchaseType).HasColumnName("PurchaseType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PANNo).HasColumnName("PANNo").HasMaxLength(50);
            entity.Property(e => e.FirmType).HasColumnName("FirmType").HasMaxLength(50);
            entity.Property(e => e.ReceivedDate).HasColumnName("ReceivedDate").IsRequired();
            entity.Property(e => e.BillDate).HasColumnName("BillDate").IsRequired();
            entity.Property(e => e.VendorGSTNo).HasColumnName("VendorGSTNo").HasMaxLength(50).IsRequired();
            entity.Property(e => e.ExpenseGroupId).HasColumnName("ExpenseGroupId").IsRequired();
            entity.Property(e => e.ExpenseSubGroupId).HasColumnName("ExpenseSubGroupId").IsRequired();
            entity.Property(e => e.ExpenseLedgerId).HasColumnName("ExpenseLedgerId").IsRequired();
            entity.Property(e => e.VehicleNo).HasColumnName("VehicleNo").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.BillNo).HasColumnName("BillNo").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ScannedCopyBillPath).HasColumnName("ScannedCopyBillPath").HasMaxLength(500);
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("Completed");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.Vendor)
                  .WithMany()
                  .HasForeignKey(e => e.VendorId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.ExpenseGroup)
                  .WithMany()
                  .HasForeignKey(e => e.ExpenseGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.ExpenseSubGroup)
                  .WithMany()
                  .HasForeignKey(e => e.ExpenseSubGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.ExpenseLedger)
                  .WithMany()
                  .HasForeignKey(e => e.ExpenseLedgerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server EntryForAccounts table
        modelBuilder.Entity<EntryForAccount>(entity =>
        {
            entity.ToTable("EntryForAccounts");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.TransactionType).HasColumnName("TransactionType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.AccountName).HasColumnName("AccountName").HasMaxLength(255);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        // Map to SQL Server PurchaseReceiveItems table
        modelBuilder.Entity<PurchaseReceiveItem>(entity =>
        {
            entity.ToTable("PurchaseReceiveItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PurchaseReceiveId).HasColumnName("PurchaseReceiveId").IsRequired();
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId");
            entity.Property(e => e.ItemName).HasColumnName("ItemName").HasMaxLength(200);
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50);
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.PurchaseReceive)
                  .WithMany(p => p.Items)
                  .HasForeignKey(e => e.PurchaseReceiveId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Map to SQL Server MaterialIssues table
        modelBuilder.Entity<MaterialIssue>(entity =>
        {
            entity.ToTable("MaterialIssues");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.MaterialIssueNo).HasColumnName("MaterialIssueNo").HasMaxLength(50);
            entity.Property(e => e.OrderDate).HasColumnName("OrderDate").IsRequired();
            entity.Property(e => e.DeliveryDate).HasColumnName("DeliveryDate").IsRequired();
            entity.Property(e => e.DeliveredTo).HasColumnName("DeliveredTo").HasMaxLength(255).IsRequired();
            entity.Property(e => e.OrderBy).HasColumnName("OrderBy").HasMaxLength(255).IsRequired();
            entity.Property(e => e.VehicleInfo).HasColumnName("VehicleInfo").HasMaxLength(255);
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("Completed");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
        });

        // Map to SQL Server MaterialIssueItems table
        modelBuilder.Entity<MaterialIssueItem>(entity =>
        {
            entity.ToTable("MaterialIssueItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.MaterialIssueId).HasColumnName("MaterialIssueId").IsRequired();
            entity.Property(e => e.PurchaseItemId).HasColumnName("PurchaseItemId");
            entity.Property(e => e.ItemName).HasColumnName("ItemName").HasMaxLength(200).IsRequired();
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50);
            entity.Property(e => e.BalanceQty).HasColumnName("BalanceQty").HasColumnType("decimal(18,2)");
            entity.Property(e => e.IssuedQty).HasColumnName("IssuedQty").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.IsReturnable).HasColumnName("IsReturnable").HasDefaultValue(false);
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.MaterialIssue)
                  .WithMany(m => m.Items)
                  .HasForeignKey(e => e.MaterialIssueId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.PurchaseItem)
                  .WithMany()
                  .HasForeignKey(e => e.PurchaseItemId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Map to SQL Server DebitNotes table
        modelBuilder.Entity<DebitNote>(entity =>
        {
            entity.ToTable("DebitNotes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.DebitNoteNo).HasColumnName("DebitNoteNo").HasMaxLength(50);
            entity.Property(e => e.Unit).HasColumnName("Unit").HasMaxLength(50).IsRequired();
            // Temporarily map to GroupId until SQL script is run
            // TODO: After running QUICK_FIX_ADD_BANKMASTERID.sql, change this to "BankMasterId"
            entity.Property(e => e.BankMasterId).HasColumnName("GroupId").IsRequired();
            // Note: FarmerId column still exists in database and is NOT NULL
            // We handle it via raw SQL in the controller until SQL migration script is run
            entity.Property(e => e.DebitNoteDate).HasColumnName("DebitNoteDate").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("UnApproved");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.EntryForId).HasColumnName("EntryForId");
            entity.Property(e => e.EntryForName).HasColumnName("EntryForName").HasMaxLength(255);
            
            // Temporarily remove foreign key until SQL script is run
            // TODO: After running QUICK_FIX_ADD_BANKMASTERID.sql, uncomment this:
            // entity.HasOne(e => e.BankMaster)
            //       .WithMany()
            //       .HasForeignKey(e => e.BankMasterId)
            //       .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server DebitNoteDetails table
        modelBuilder.Entity<DebitNoteDetail>(entity =>
        {
            entity.ToTable("DebitNoteDetails");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.DebitNoteId).HasColumnName("DebitNoteId").IsRequired();
            entity.Property(e => e.AccountType).HasColumnName("AccountType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.RefNo).HasColumnName("RefNo").HasMaxLength(100);
            entity.Property(e => e.HsnSacCode).HasColumnName("HsnSacCode").HasMaxLength(50);
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Rate).HasColumnName("Rate").HasColumnType("decimal(18,2)");
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.DebitNote)
                  .WithMany(d => d.Details)
                  .HasForeignKey(e => e.DebitNoteId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Map to SQL Server ReceiptEntries table
        modelBuilder.Entity<ReceiptEntry>(entity =>
        {
            entity.ToTable("ReceiptEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.VoucherNo).HasColumnName("VoucherNo").HasMaxLength(50);
            entity.Property(e => e.ReceiptDate).HasColumnName("ReceiptDate").IsRequired();
            entity.Property(e => e.MobileNo).HasColumnName("MobileNo").HasMaxLength(20);
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountId).HasColumnName("AccountId").IsRequired();
            entity.Property(e => e.PaymentType).HasColumnName("PaymentType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.RefNoChequeUTR).HasColumnName("RefNoChequeUTR").HasMaxLength(100);
            entity.Property(e => e.Narration).HasColumnName("Narration").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("Unapproved");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.EntryForId).HasColumnName("EntryForId");
            entity.Property(e => e.EntryForName).HasColumnName("EntryForName").HasMaxLength(255);
            entity.Property(e => e.AccountType).HasColumnName("AccountType").HasMaxLength(50).IsRequired();
            
            // Note: AccountId can refer to MasterGroup, MasterSubGroup, or SubGroupLedger based on AccountType
            // We cannot set up foreign keys for polymorphic relationships in EF Core easily
            // Navigation properties will be loaded manually in the controller based on AccountType
            // Ignore navigation properties in queries to avoid EF trying to use non-existent foreign keys
            entity.Ignore(e => e.MasterGroup);
            entity.Ignore(e => e.MasterSubGroup);
            entity.Ignore(e => e.SubGroupLedger);
            
            entity.Property(e => e.PaymentFromSubGroupId).HasColumnName("PaymentFromSubGroupId");
            
            entity.HasOne(e => e.PaymentFromSubGroup)
                  .WithMany()
                  .HasForeignKey(e => e.PaymentFromSubGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server PaymentSettlements table
        modelBuilder.Entity<PaymentSettlement>(entity =>
        {
            entity.ToTable("PaymentSettlements");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.PANumber).HasColumnName("PANumber").HasMaxLength(50);
            entity.Property(e => e.SettlementDate).HasColumnName("SettlementDate").IsRequired();
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(50).IsRequired();
            entity.Property(e => e.AccountId).HasColumnName("AccountId").IsRequired();
            entity.Property(e => e.AccountName).HasColumnName("AccountName").HasMaxLength(200).IsRequired();
            entity.Property(e => e.AccountType).HasColumnName("AccountType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.PaymentType).HasColumnName("PaymentType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.RefNo).HasColumnName("RefNo").HasMaxLength(100);
            entity.Property(e => e.Narration).HasColumnName("Narration").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ApprovalStatus).HasColumnName("ApprovalStatus").HasMaxLength(50).HasDefaultValue("Unapproved");
            entity.Property(e => e.PaymentStatus).HasColumnName("PaymentStatus").HasMaxLength(50).HasDefaultValue("Pending");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.Unit).HasColumnName("Unit").HasMaxLength(50);
            entity.Property(e => e.EntryForId).HasColumnName("EntryForId");
            entity.Property(e => e.EntryForName).HasColumnName("EntryForName").HasMaxLength(255);
        });

        // Map to SQL Server GeneralEntries table
        modelBuilder.Entity<GeneralEntry>(entity =>
        {
            entity.ToTable("GeneralEntries");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.VoucherNo).HasColumnName("VoucherNo").HasMaxLength(50);
            entity.Property(e => e.EntryDate).HasColumnName("EntryDate").IsRequired();
            entity.Property(e => e.DebitAccountId).HasColumnName("DebitAccountId").IsRequired();
            entity.Property(e => e.DebitAccountType).HasColumnName("DebitAccountType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.CreditAccountId).HasColumnName("CreditAccountId").IsRequired();
            entity.Property(e => e.CreditAccountType).HasColumnName("CreditAccountType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Type).HasColumnName("Type").HasMaxLength(100);
            entity.Property(e => e.Narration).HasColumnName("Narration").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.ReferenceNo).HasColumnName("ReferenceNo").HasMaxLength(100);
            // Temporarily ignore ImagePath until SQL script is run
            entity.Ignore(e => e.ImagePath);
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("Unapproved");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            entity.Property(e => e.EntryForId).HasColumnName("EntryForId");
            entity.Property(e => e.EntryForName).HasColumnName("EntryForName").HasMaxLength(255);
            
            // Note: DebitAccountId and CreditAccountId can refer to MasterGroup, MasterSubGroup, or SubGroupLedger
            // Navigation properties will be loaded manually in the controller based on AccountType
            // Ignore navigation properties to prevent EF from trying to create foreign keys
            entity.Ignore(e => e.DebitMasterGroup);
            entity.Ignore(e => e.DebitMasterSubGroup);
            entity.Ignore(e => e.DebitSubGroupLedger);
            entity.Ignore(e => e.DebitBankMasterInfo);
            entity.Ignore(e => e.CreditMasterGroup);
            entity.Ignore(e => e.CreditMasterSubGroup);
            entity.Ignore(e => e.CreditSubGroupLedger);
            entity.Ignore(e => e.CreditBankMasterInfo);
        });

        // Map to SQL Server ExpensesIncurreds table
        modelBuilder.Entity<ExpensesIncurred>(entity =>
        {
            entity.ToTable("ExpensesIncurreds");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.VoucherNo).HasColumnName("VoucherNo").HasMaxLength(50);
            entity.Property(e => e.ExpenseDate).HasColumnName("ExpenseDate").IsRequired();
            entity.Property(e => e.ExpenseGroupId).HasColumnName("ExpenseGroupId").IsRequired();
            entity.Property(e => e.ExpenseSubGroupId).HasColumnName("ExpenseSubGroupId").IsRequired();
            entity.Property(e => e.ExpenseLedgerId).HasColumnName("ExpenseLedgerId").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.PaymentMode).HasColumnName("PaymentMode").HasMaxLength(50);
            entity.Property(e => e.ReferenceNo).HasColumnName("ReferenceNo").HasMaxLength(100);
            entity.Property(e => e.Narration).HasColumnName("Narration").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.VendorId).HasColumnName("VendorId");
            entity.Property(e => e.VendorName).HasColumnName("VendorName").HasMaxLength(200);
            entity.Property(e => e.POType).HasColumnName("POType").HasMaxLength(50);
            entity.Property(e => e.PANNo).HasColumnName("PANNo").HasMaxLength(50);
            entity.Property(e => e.FirmType).HasColumnName("FirmType").HasMaxLength(50);
            entity.Property(e => e.BillDate).HasColumnName("BillDate");
            entity.Property(e => e.BillNo).HasColumnName("BillNo").HasMaxLength(100);
            entity.Property(e => e.VehicleNo).HasColumnName("VehicleNo").HasMaxLength(50);
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.Status).HasColumnName("Status").HasMaxLength(50).HasDefaultValue("Unapproved");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt").HasDefaultValueSql("GETDATE()");
            entity.Property(e => e.IsActive).HasColumnName("IsActive").HasDefaultValue(true);
            
            // Note: ExpenseGroupId, ExpenseSubGroupId, ExpenseLedgerId refer to MasterGroup, MasterSubGroup, SubGroupLedger
            // Navigation properties will be loaded manually in the controller
            // Ignore navigation properties to prevent EF from trying to create foreign keys
            entity.Ignore(e => e.ExpenseGroup);
            entity.Ignore(e => e.ExpenseSubGroup);
            entity.Ignore(e => e.ExpenseLedger);
        });
        // Map to SQL Server ExpenseItems table
        modelBuilder.Entity<ExpenseItem>(entity =>
        {
            entity.ToTable("ExpenseItems");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ExpensesIncurredId).HasColumnName("ExpensesIncurredId").IsRequired();
            entity.Property(e => e.ItemGroupId).HasColumnName("ItemGroupId").IsRequired();
            entity.Property(e => e.ItemId).HasColumnName("ItemId").IsRequired();
            entity.Property(e => e.Description).HasColumnName("Description").HasMaxLength(500);
            entity.Property(e => e.UOM).HasColumnName("UOM").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Qty).HasColumnName("Qty").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.UnitPrice).HasColumnName("UnitPrice").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Discount).HasColumnName("Discount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.GST).HasColumnName("GST").HasMaxLength(20).HasDefaultValue("NA");
            entity.Property(e => e.GSTAmount).HasColumnName("GSTAmount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.ExpensesIncurred)
                  .WithMany(p => p.Items)
                  .HasForeignKey(e => e.ExpensesIncurredId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.ItemGroup)
                  .WithMany()
                  .HasForeignKey(e => e.ItemGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasOne(e => e.Item)
                  .WithMany()
                  .HasForeignKey(e => e.ItemId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // Map to SQL Server ExpenseMiscCharges table
        modelBuilder.Entity<ExpenseMiscCharge>(entity =>
        {
            entity.ToTable("ExpenseMiscCharges");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.ExpensesIncurredId).HasColumnName("ExpensesIncurredId").IsRequired();
            entity.Property(e => e.ExpenseType).HasColumnName("ExpenseType").HasMaxLength(100).IsRequired();
            entity.Property(e => e.Amount).HasColumnName("Amount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.Tax).HasColumnName("Tax").HasMaxLength(20).HasDefaultValue("Select");
            entity.Property(e => e.GSTAmount).HasColumnName("GSTAmount").HasColumnType("decimal(18,2)");
            entity.Property(e => e.TotalAmount).HasColumnName("TotalAmount").HasColumnType("decimal(18,2)").IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnName("CreatedAt");
            
            entity.HasOne(e => e.ExpensesIncurred)
                  .WithMany(p => p.MiscCharges)
                  .HasForeignKey(e => e.ExpensesIncurredId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        // Map to SQL Server TransactionHistories table
        modelBuilder.Entity<TransactionHistory>(entity =>
        {
            entity.ToTable("TransactionHistories");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("Id");
            entity.Property(e => e.VoucherNo).HasColumnName("VoucherNo").HasMaxLength(50).IsRequired();
            entity.Property(e => e.VoucherType).HasColumnName("VoucherType").HasMaxLength(50).IsRequired();
            entity.Property(e => e.Action).HasColumnName("Action").HasMaxLength(50).IsRequired();
            entity.Property(e => e.User).HasColumnName("User").HasMaxLength(100).IsRequired();
            entity.Property(e => e.ActionDate).HasColumnName("ActionDate").IsRequired();
            entity.Property(e => e.Remarks).HasColumnName("Remarks").HasMaxLength(500);
            entity.Property(e => e.OldValues).HasColumnType("NVARCHAR(MAX)");
            entity.Property(e => e.NewValues).HasColumnType("NVARCHAR(MAX)");
        });
        modelBuilder.Entity<AccountRule>(entity =>
        {
            entity.ToTable("AccountRules");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AccountType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RuleType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Value).HasMaxLength(50).IsRequired();
            entity.Property(e => e.EntryAccountId).HasColumnName("EntryAccountId");
            
            // Index for fast lookups (updated to include EntryAccountId)
            entity.HasIndex(e => new { e.AccountType, e.AccountId, e.EntryAccountId });
        });
    }
}

