using FloreriaBautista.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace FloreriaBautista.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // ── Usuarios y autenticación ───────────────────────────────────
    public DbSet<User>      Users      => Set<User>();
    public DbSet<Role>      Roles      => Set<Role>();
    public DbSet<UserRole>  UserRoles  => Set<UserRole>();
    public DbSet<AuthToken> AuthTokens => Set<AuthToken>();

    // ── Clientes ───────────────────────────────────────────────────
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Address>  Addresses => Set<Address>();

    // ── Catálogo ───────────────────────────────────────────────────
    public DbSet<Product>             Products             => Set<Product>();
    public DbSet<Category>            Categories           => Set<Category>();
    public DbSet<Collection>          Collections          => Set<Collection>();
    public DbSet<CustomizationOption> CustomizationOptions => Set<CustomizationOption>();

    public DbSet<ProductCategory>            ProductCategories           => Set<ProductCategory>();
    public DbSet<ProductCollection>          ProductCollections          => Set<ProductCollection>();
    public DbSet<ProductCustomizationOption> ProductCustomizationOptions => Set<ProductCustomizationOption>();

    // ── Pedidos ────────────────────────────────────────────────────
    public DbSet<Order>                  Orders                  => Set<Order>();
    public DbSet<OrderItem>              OrderItems              => Set<OrderItem>();
    public DbSet<OrderItemCustomization> OrderItemCustomizations => Set<OrderItemCustomization>();
    public DbSet<Payment>                Payments                => Set<Payment>();
    public DbSet<Delivery>               Deliveries              => Set<Delivery>();

    // ── Inventario ─────────────────────────────────────────────────
    public DbSet<InventoryItem>     InventoryItems     => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();

    // ── Operación técnica ──────────────────────────────────────────
    public DbSet<BackupJob> BackupJobs => Set<BackupJob>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<AuditLog>  AuditLogs  => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Carga todas las IEntityTypeConfiguration<T> — cada entidad tiene su archivo en Configurations/
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Claves compuestas de tablas N:M (no tienen archivo de configuración propio)
        modelBuilder.Entity<UserRole>()
            .HasKey(ur => new { ur.UserId, ur.RoleId });

        modelBuilder.Entity<ProductCategory>()
            .HasKey(pc => new { pc.ProductId, pc.CategoryId });

        modelBuilder.Entity<ProductCollection>()
            .HasKey(pc => new { pc.ProductId, pc.CollectionId });

        modelBuilder.Entity<ProductCustomizationOption>()
            .HasKey(pco => new { pco.ProductId, pco.CustomizationOptionId });
    }
}
