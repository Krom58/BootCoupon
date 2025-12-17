using System;
using Microsoft.EntityFrameworkCore;
using CouponManagement.Shared.Models;

namespace CouponManagement.Shared
{
    public partial class CouponContext : DbContext
    {
        public CouponContext()
        {
        }

        // Constructor for DI
        public CouponContext(DbContextOptions<CouponContext> options) : base(options)
        {
        }

        // Renamed DbSet to Branches
        public DbSet<Branch> Branches { get; set; } = null!;
        public DbSet<Coupon> Coupons { get; set; } = null!;
        public DbSet<ReceiptModel> Receipts { get; set; } = null!;
        public DbSet<DatabaseReceiptItem> ReceiptItems { get; set; } = null!;
        public DbSet<SalesPerson> SalesPerson { get; set; } = null!;

        // CouponDefinition entities
        public DbSet<CouponDefinition> CouponDefinitions { get; set; } = null!;
        public DbSet<CouponCodeGenerator> CouponCodeGenerators { get; set; } = null!;
        public DbSet<GeneratedCoupon> GeneratedCoupons { get; set; } = null!;

        // Receipt number etc
        public DbSet<ReceiptNumberManager> ReceiptNumberManagers { get; set; } = null!;
        public DbSet<CanceledReceiptNumber> CanceledReceiptNumbers { get; set; } = null!;
        public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;
        public DbSet<BootCoupon.Models.ReservedCoupon> ReservedCoupons { get; set; } = null!;
        public DbSet<CouponManagement.Shared.Models.ApplicationUser> ApplicationUsers { get; set; } = null!;

        // Login logs (added so controllers can write login entries)
        public DbSet<LoginLog> LoginLogs { get; set; } = null!;

        // แอดใหม่
        public DbSet<SaleEvent> SaleEvents { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            // If not configured via DI, use fallback connection from environment or local default.
            if (!optionsBuilder.IsConfigured) 
            {
                var conn = Environment.GetEnvironmentVariable("COUPONDB_CONNECTION")
                           ?? "Server=10.10.0.42\\SQLSET;Database=CouponTest;User Id=sa;Password=Wutt@1976;TrustServerCertificate=True;Trusted_Connection=False;";
                optionsBuilder.UseSqlServer(conn);
            }
        }
        //"Server=(localdb)\\MSSQLLocalDB;Database=CouponDbV2;Integrated Security=True;TrustServerCertificate=True;"
        //"Server=KROM\\SQLEXPRESS;Database=CouponDbV2;Integrated Security=True;TrustServerCertificate=True;"
        //"Server=10.10.0.42\\SQLSET;Database=CouponDbV2;User Id=sa;Password=Wutt@1976;TrustServerCertificate=True;Trusted_Connection=False;"
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Branch mapping (was Branch)
            modelBuilder.Entity<Branch>(entity =>
            {
                entity.ToTable("Branch");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_Branch_Name");
            });

            // Coupon mapping
            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.ToTable("Coupons");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);

                // map CLR property BranchId to DB column BranchId
                entity.Property(e => e.BranchId).IsRequired();
                entity.Property(e => e.Price).HasPrecision(18,2);

                // Foreign key relationship
                entity.HasOne(c => c.Branch)
                      .WithMany()
                      .HasForeignKey(c => c.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_Coupons_Code");
                entity.HasIndex(e => e.BranchId).HasDatabaseName("IX_Coupons_BranchId");
            });

            // ReceiptItems
            modelBuilder.Entity<DatabaseReceiptItem>(entity =>
            {
                entity.ToTable("ReceiptItems");
                entity.HasKey(e => e.ReceiptItemId);
                entity.HasOne<ReceiptModel>()
                      .WithMany(r => r.Items)
                      .HasForeignKey(ri => ri.ReceiptId);

                // Indexes to speed up reporting
                entity.HasIndex(e => e.CouponId).HasDatabaseName("IX_ReceiptItems_CouponId");
                entity.HasIndex(e => e.ReceiptId).HasDatabaseName("IX_ReceiptItems_ReceiptId");
                entity.Property(e => e.TotalPrice).HasPrecision(18,2);
                entity.Property(e => e.UnitPrice).HasPrecision(18,2);
            });

            modelBuilder.Entity<ReceiptModel>(entity =>
            {
                entity.ToTable("Receipts");
                entity.HasKey(e => e.ReceiptID);
                entity.Property(e => e.CustomerName).IsRequired();
                entity.Property(e => e.CustomerPhoneNumber).IsRequired();
                entity.Property(e => e.Status).HasDefaultValue("Active");
                entity.Property(e => e.TotalAmount).HasPrecision(18,2);
                entity.Property(e => e.Discount).HasPrecision(18,2);
                entity.HasMany(r => r.Items)
                      .WithOne()
                      .HasForeignKey(ri => ri.ReceiptId);
            });

            // CouponDefinition configuration
            modelBuilder.Entity<CouponDefinition>(entity =>
            {
                entity.ToTable("CouponDefinitions");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Code)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.Name)
                      .IsRequired()
                      .HasMaxLength(200);

                // map CLR property BranchId to DB column BranchId
                entity.Property(e => e.BranchId)
                      .IsRequired();

                entity.Property(e => e.Price).HasPrecision(18,2);

                entity.HasOne(d => d.Branch)
                      .WithMany()
                      .HasForeignKey(d => d.BranchId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.Property(e => e.Params)
                      .IsRequired()
                      .HasColumnType("nvarchar(max)");

                entity.Property(e => e.CreatedBy)
                      .IsRequired()
                      .HasMaxLength(100);

                entity.Property(e => e.UpdatedBy)
                      .HasMaxLength(100);

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.HasIndex(e => e.Code)
                      .IsUnique()
                      .HasDatabaseName("UK_CouponDefinitions_Code");

                entity.HasIndex(e => e.BranchId)
                      .HasDatabaseName("IX_CouponDefinitions_BranchId");

                entity.HasIndex(e => new { e.ValidFrom, e.ValidTo })
                      .HasDatabaseName("IX_CouponDefinitions_ValidDates");

                entity.HasIndex(e => e.IsActive)
                      .HasDatabaseName("IX_CouponDefinitions_IsActive");

                // แอดใหม่
                entity.HasOne(d => d.SaleEvent)
                      .WithMany()
                      .HasForeignKey(d => d.SaleEventId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.SaleEventId)
                      .HasDatabaseName("IX_CouponDefinitions_SaleEventId");
            });

            // CouponCodeGenerator configuration (unchanged)
            modelBuilder.Entity<CouponCodeGenerator>(entity =>
            {
                entity.ToTable("CouponCodeGenerators");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Prefix)
                      .IsRequired()
                      .HasMaxLength(10);

                entity.Property(e => e.Suffix)
                      .IsRequired()
                      .HasMaxLength(10);

                entity.Property(e => e.SequenceLength)
                      .HasDefaultValue(4);

                entity.Property(e => e.CurrentSequence)
                      .HasDefaultValue(0);

                entity.Property(e => e.GeneratedCount)
                      .HasDefaultValue(0);

                entity.Property(e => e.UpdatedBy)
                      .HasMaxLength(100);

                entity.HasOne(g => g.CouponDefinition)
                      .WithOne(d => d.CodeGenerator)
                      .HasForeignKey<CouponCodeGenerator>(g => g.CouponDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasIndex(e => e.CouponDefinitionId)
                      .IsUnique()
                      .HasDatabaseName("UK_CouponCodeGenerators_CouponDefinitionId");
            });

            // GeneratedCoupon configuration (unchanged)
            modelBuilder.Entity<GeneratedCoupon>(entity =>
            {
                entity.ToTable("GeneratedCoupons");
                entity.HasKey(e => e.Id);

                entity.Property(e => e.GeneratedCode)
                      .IsRequired()
                      .HasMaxLength(50);

                entity.Property(e => e.BatchNumber)
                      .IsRequired();

                entity.Property(e => e.CreatedAt)
                      .HasDefaultValueSql("GETDATE()");

                entity.Property(e => e.UsedBy)
                      .HasMaxLength(100);

                entity.Property(e => e.CreatedBy)
                      .HasMaxLength(100);

                entity.HasOne(g => g.CouponDefinition)
                      .WithMany(d => d.GeneratedCoupons)
                      .HasForeignKey(g => g.CouponDefinitionId)
                      .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne<DatabaseReceiptItem>(g => g.ReceiptItem)
                      .WithMany()
                      .HasForeignKey(g => g.ReceiptItemId)
                      .OnDelete(DeleteBehavior.SetNull);

                entity.HasIndex(e => e.GeneratedCode)
                      .IsUnique()
                      .HasDatabaseName("UK_GeneratedCoupons_GeneratedCode");

                entity.HasIndex(e => e.CouponDefinitionId)
                      .HasDatabaseName("IX_GeneratedCoupons_CouponDefinitionId");

                entity.HasIndex(e => e.IsUsed)
                      .HasDatabaseName("IX_GeneratedCoupons_IsUsed");

                entity.HasIndex(e => e.BatchNumber)
                      .HasDatabaseName("IX_GeneratedCoupons_BatchNumber");
            });

            // SalesPerson mapping
            modelBuilder.Entity<SalesPerson>(entity =>
            {
                entity.ToTable("SalesPerson");
                entity.HasKey(e => e.ID);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Branch).HasMaxLength(100);
                entity.Property(e => e.Telephone).HasMaxLength(20);
            });

            // ReceiptNumberManager
            modelBuilder.Entity<ReceiptNumberManager>(entity =>
            {
                entity.ToTable("ReceiptNumberManager");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Prefix).IsRequired().HasMaxLength(10);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
                entity.Property(e => e.LastUpdated).HasDefaultValueSql("GETDATE()");
                
                // *** เพิ่ม YearCode ***
                entity.Property(e => e.YearCode)
                      .IsRequired()
                      .HasDefaultValue(DateTime.Now.Year % 100);
            });

            modelBuilder.Entity<CanceledReceiptNumber>(entity =>
            {
                entity.ToTable("CanceledReceiptNumbers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReceiptCode).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.ReceiptCode).IsUnique();
                entity.Property(e => e.Reason).HasMaxLength(255);
                entity.Property(e => e.CanceledDate).HasDefaultValueSql("GETDATE()");
                
                // *** เพิ่ม Index สำหรับ OwnerMachineId ***
                entity.HasIndex(e => new { e.OwnerMachineId, e.CanceledDate })
                      .HasDatabaseName("IX_CanceledReceiptNumbers_OwnerMachineId_CanceledDate");
                
                entity.Property(e => e.OwnerMachineId).HasMaxLength(255);
            });

            modelBuilder.Entity<PaymentMethod>(entity =>
            {
                entity.ToTable("PaymentMethods");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });

            // ApplicationUser mapping
            modelBuilder.Entity<CouponManagement.Shared.Models.ApplicationUser>(entity =>
            {
                entity.ToTable("ApplicationUsers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Username).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Password).IsRequired().HasMaxLength(200);
                entity.Property(e => e.DisplayName).HasMaxLength(200);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.HasIndex(e => e.Username).IsUnique();
            });

            // LoginLog mapping
            modelBuilder.Entity<LoginLog>(entity =>
            {
                entity.ToTable("LoginLogs");
                entity.HasKey(e => e.LogId);
                entity.Property(e => e.LoggedAt).HasDefaultValueSql("GETDATE()");
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Action).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Location).HasMaxLength(100);
                entity.Property(e => e.App).HasMaxLength(200);
                entity.HasIndex(e => e.UserName).HasDatabaseName("IX_LoginLogs_UserName");
            });

            // แอดใหม่
            modelBuilder.Entity<SaleEvent>(entity =>
            {
                entity.ToTable("SaleEvent");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.StartDate).IsRequired();
                entity.Property(e => e.EndDate).IsRequired();
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.UpdatedBy).HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                
                entity.HasIndex(e => e.Name).IsUnique().HasDatabaseName("UK_SaleEvent_Name");
                entity.HasIndex(e => new { e.StartDate, e.EndDate }).HasDatabaseName("IX_SaleEvent_Dates");
                entity.HasIndex(e => e.IsActive).HasDatabaseName("IX_SaleEvent_IsActive");
            });
        }
    }
}