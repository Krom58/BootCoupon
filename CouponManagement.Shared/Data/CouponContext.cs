using CouponManagement.Shared.Models;
using Microsoft.EntityFrameworkCore;

public class CouponContext : DbContext
{
    public DbSet<CouponType> CouponTypes { get; set; } = null!;
    public DbSet<Coupon> Coupons { get; set; } = null!;
    public DbSet<ReceiptModel> Receipts { get; set; } = null!;
    public DbSet<DatabaseReceiptItem> ReceiptItems { get; set; } = null!;
    public DbSet<SalesPerson> SalesPerson { get; set; } = null!;

    // เพิ่มตารางใหม่
    public DbSet<ReceiptNumberManager> ReceiptNumberManagers { get; set; } = null!;
    public DbSet<CanceledReceiptNumber> CanceledReceiptNumbers { get; set; } = null!;
    public DbSet<PaymentMethod> PaymentMethods { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=KROM\\SQLEXPRESS;Database=CouponDB;Integrated Security=True;TrustServerCertificate=True;");
    }
    //Server=10.10.0.42;Database=CouponDB;User Id=sa;Password=Wutt@1976;TrustServerCertificate=True;
    //Server=KROM\\SQLEXPRESS;Database=CouponDB;Integrated Security=True;TrustServerCertificate=True;
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DatabaseReceiptItem>(entity =>
        {
            entity.ToTable("ReceiptItems");
            entity.HasKey(e => e.ReceiptItemId);
            entity.HasOne<ReceiptModel>()
                  .WithMany(r => r.Items)
                  .HasForeignKey(ri => ri.ReceiptId);
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
        });

        // เพิ่ม configuration สำหรับตารางใหม่
        modelBuilder.Entity<ReceiptNumberManager>(entity =>
        {
            entity.ToTable("ReceiptNumberManager");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Prefix).IsRequired().HasMaxLength(10);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });

        modelBuilder.Entity<CanceledReceiptNumber>(entity =>
        {
            entity.ToTable("CanceledReceiptNumbers");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ReceiptCode).IsRequired().HasMaxLength(50);
            entity.HasIndex(e => e.ReceiptCode).IsUnique();
            entity.Property(e => e.Reason).HasMaxLength(255);
        });

        // เพิ่ม configuration สำหรับ PaymentMethod
        modelBuilder.Entity<PaymentMethod>(entity =>
        {
            entity.ToTable("PaymentMethods");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("GETDATE()");
        });
    }
}