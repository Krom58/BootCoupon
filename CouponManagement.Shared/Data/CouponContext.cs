using CouponManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace CouponManagement.Shared
{
    public partial class CouponContext : DbContext
    {
        public DbSet<CouponType> CouponTypes { get; set; } = null!;
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

        // New: Customers table
        public DbSet<Customer> Customers { get; set; } = null!;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer("Server=KROM\\SQLEXPRESS;Database=CouponDbV2;Integrated Security=True;TrustServerCertificate=True;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // CouponType mapping
            modelBuilder.Entity<CouponType>(entity =>
            {
                entity.ToTable("CouponTypes");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.CreatedBy).IsRequired().HasMaxLength(100);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_CouponTypes_Name");
            });

            // Coupon mapping
            modelBuilder.Entity<Coupon>(entity =>
            {
                entity.ToTable("Coupons");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.CouponTypeId).IsRequired();

                // Foreign key relationship
                entity.HasOne(c => c.CouponType)
                      .WithMany()
                      .HasForeignKey(c => c.CouponTypeId)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(e => e.Code).IsUnique().HasDatabaseName("UK_Coupons_Code");
                entity.HasIndex(e => e.CouponTypeId).HasDatabaseName("IX_Coupons_CouponTypeId");
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
            });

            modelBuilder.Entity<ReceiptModel>(entity =>
            {
                entity.ToTable("Receipts");
                entity.HasKey(e => e.ReceiptID);
                entity.Property(e => e.CustomerName).IsRequired();
                entity.Property(e => e.CustomerPhoneNumber).IsRequired();
                entity.Property(e => e.Status).HasDefaultValue("Active");
                entity.HasMany(r => r.Items)
                      .WithOne()
                      .HasForeignKey(ri => ri.ReceiptId);

                // New FK to Customers
                entity.Property<int?>("CustomerId");
                entity.HasOne<Customer>()
                      .WithMany()
                      .HasForeignKey("CustomerId")
                      .OnDelete(DeleteBehavior.SetNull);
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

                entity.Property(e => e.CouponTypeId)
                      .IsRequired();

                entity.HasOne(d => d.CouponType)
                      .WithMany()
                      .HasForeignKey(d => d.CouponTypeId)
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

                entity.HasIndex(e => e.CouponTypeId)
                      .HasDatabaseName("IX_CouponDefinitions_Type");

                entity.HasIndex(e => new { e.ValidFrom, e.ValidTo })
                      .HasDatabaseName("IX_CouponDefinitions_ValidDates");

                entity.HasIndex(e => e.IsActive)
                      .HasDatabaseName("IX_CouponDefinitions_IsActive");
            });

            // CouponCodeGenerator configuration
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

            // GeneratedCoupon configuration
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

                // New: optional FK to Customers for denormalized queries
                entity.HasOne(g => g.Customer)
                      .WithMany()
                      .HasForeignKey(g => g.CustomerId)
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

                // Index on CustomerId to speed up reporting joins
                entity.HasIndex(e => e.CustomerId).HasDatabaseName("IX_GeneratedCoupons_CustomerId");
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
            });

            modelBuilder.Entity<CanceledReceiptNumber>(entity =>
            {
                entity.ToTable("CanceledReceiptNumbers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReceiptCode).IsRequired().HasMaxLength(50);
                entity.HasIndex(e => e.ReceiptCode).IsUnique();
                entity.Property(e => e.Reason).HasMaxLength(255);
                entity.Property(e => e.CanceledDate).HasDefaultValueSql("GETDATE()");
            });

            modelBuilder.Entity<PaymentMethod>(entity =>
            {
                entity.ToTable("PaymentMethods");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.IsActive).HasDefaultValue(true);
                entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
            });

            // Customers configuration
            modelBuilder.Entity<Customer>(entity =>
            {
                entity.ToTable("Customers");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Phone).HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(200);
                entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETDATE()");
                entity.HasIndex(e => e.Name).HasDatabaseName("IX_Customers_Name");
            });
        }
    }
}