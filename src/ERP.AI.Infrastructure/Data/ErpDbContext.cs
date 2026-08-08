using ERP.AI.Core.Entities;
using ERP.AI.Knowledge.Entities;
using Microsoft.EntityFrameworkCore;

namespace ERP.AI.Infrastructure.Data;

public class ErpDbContext : DbContext
{
    public ErpDbContext(DbContextOptions<ErpDbContext> options) : base(options)
    {
    }

    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Customer Configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.CustomerCode).IsUnique();
            entity.Property(c => c.CustomerCode).IsRequired().HasMaxLength(50);
            entity.Property(c => c.CustomerName).IsRequired().HasMaxLength(250);
        });

        // Invoice Configuration
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.InvoiceNo).IsUnique();
            entity.Property(i => i.InvoiceNo).IsRequired().HasMaxLength(50);
            entity.Property(i => i.TotalAmount).HasConversion<double>();
            entity.Property(i => i.PaidAmount).HasConversion<double>();
            entity.Property(i => i.Status).HasConversion<string>();

            entity.HasOne(i => i.Customer)
                .WithMany(c => c.Invoices)
                .HasForeignKey(i => i.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Sale Configuration
        modelBuilder.Entity<Sale>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.HasIndex(s => s.DocumentNo).IsUnique();
            entity.Property(s => s.DocumentNo).IsRequired().HasMaxLength(50);
            entity.Property(s => s.Amount).HasConversion<double>();

            entity.HasOne(s => s.Customer)
                .WithMany(c => c.Sales)
                .HasForeignKey(s => s.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // Item Configuration
        modelBuilder.Entity<Item>(entity =>
        {
            entity.HasKey(it => it.Id);
            entity.HasIndex(it => it.ItemCode).IsUnique();
            entity.Property(it => it.ItemCode).IsRequired().HasMaxLength(50);
            entity.Property(it => it.ItemName).IsRequired().HasMaxLength(250);
            entity.Property(it => it.CurrentStock).HasConversion<double>();
            entity.Property(it => it.MinimumStock).HasConversion<double>();
        });

        // Project Configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.ProjectCode).IsUnique();
            entity.Property(p => p.ProjectCode).IsRequired().HasMaxLength(50);
            entity.Property(p => p.ProjectName).IsRequired().HasMaxLength(250);
            entity.Property(p => p.BudgetAmount).HasConversion<double>();
            entity.Property(p => p.ActualCost).HasConversion<double>();
        });

        // KnowledgeDocument Configuration
        modelBuilder.Entity<KnowledgeDocument>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.HasIndex(d => d.DocumentId).IsUnique();
            entity.HasIndex(d => d.FileHash);
            entity.HasIndex(d => d.Status);
            entity.HasIndex(d => d.UploadedAt);
            entity.Property(d => d.DocumentId).IsRequired().HasMaxLength(50);
            entity.Property(d => d.Title).IsRequired().HasMaxLength(250);
            entity.Property(d => d.FileName).IsRequired().HasMaxLength(250);
            entity.Property(d => d.Status).HasConversion<string>();
            entity.Property(d => d.ProcessingStage).HasConversion<string>();
            entity.Property(d => d.EmbeddingStatus).HasConversion<string>();
            entity.HasIndex(d => d.EmbeddingStatus);
        });

        // KnowledgeChunk Configuration
        modelBuilder.Entity<KnowledgeChunk>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.HasIndex(c => c.ChunkId).IsUnique();
            entity.HasIndex(c => c.DocumentId);
            entity.HasIndex(c => c.ChunkIndex);
            entity.HasIndex(c => c.ContentHash);
            entity.HasIndex(c => new { c.DocumentId, c.ChunkIndex }).IsUnique();
            entity.Property(c => c.ChunkId).IsRequired().HasMaxLength(50);
            entity.Property(c => c.DocumentId).IsRequired().HasMaxLength(50);
            entity.Property(c => c.Content).IsRequired();

            entity.HasOne(c => c.Document)
                .WithMany(d => d.Chunks)
                .HasPrincipalKey(d => d.DocumentId)
                .HasForeignKey(c => c.DocumentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

