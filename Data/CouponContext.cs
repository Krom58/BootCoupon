using BootCoupon;
using Microsoft.EntityFrameworkCore;

public class CouponContext : DbContext
{
    public DbSet<CouponType> CouponTypes { get; set; } = null!;
    public DbSet<Coupon> Coupons { get; set; } = null!;
    public DbSet<ReceiptModel> Receipts { get; set; } = null!;
    public DbSet<DatabaseReceiptItem> ReceiptItems { get; set; } = null!;
    public DbSet<SalesPerson> SalesPerson { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer("Server=10.10.0.42;Database=CouponDB;User Id=sa;Password=Wutt@1976;TrustServerCertificate=True;");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<DatabaseReceiptItem>(entity =>
        {
            entity.ToTable("ReceiptItems");
            entity.HasKey(e => e.ReceiptItemId); // Define primary key
            entity.HasOne<ReceiptModel>() // Define relationship
                  .WithMany(r => r.Items)
                  .HasForeignKey(ri => ri.ReceiptId);
        });

        modelBuilder.Entity<ReceiptModel>(entity =>
        {
            entity.ToTable("Receipts");
            entity.HasKey(e => e.ReceiptID);
            entity.Property(e => e.CustomerName).IsRequired();
            entity.Property(e => e.CustomerPhoneNumber).IsRequired();
            entity.HasMany(r => r.Items)
                  .WithOne()
                  .HasForeignKey(ri => ri.ReceiptId);
        });
    }
}