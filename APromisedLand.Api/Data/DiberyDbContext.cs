using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace APromisedLand.Api.Data;

public class DiberyDbContext(DbContextOptions<DiberyDbContext> options) : DbContext(options)
{
    public DbSet<CategoryTree> CategoryTrees { get; set; }
    public DbSet<UnitTree> UnitTrees { get; set; }
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    public DbSet<AttributeType> AttributeTypes => Set<AttributeType>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    public DbSet<TextAttributeValue> TextAttributeValues => Set<TextAttributeValue>();
    public DbSet<DecimalAttributeValue> DecimalAttributeValues => Set<DecimalAttributeValue>();
    public DbSet<IntegerAttributeValue> IntegerAttributeValues => Set<IntegerAttributeValue>();
    public DbSet<DateAttributeValue> DateAttributeValues => Set<DateAttributeValue>();
    public DbSet<TimeAttributeValue> TimeAttributeValues => Set<TimeAttributeValue>();
    public DbSet<DateTimeAttributeValue> DateTimeAttributeValues => Set<DateTimeAttributeValue>();
    public DbSet<FileAttributeValue> FileAttributeValues => Set<FileAttributeValue>();
    public DbSet<LocationAttributeValue> LocationAttributeValues => Set<LocationAttributeValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ------ CategoryTree ------
        modelBuilder.Entity<CategoryTree>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentId);
        });
        modelBuilder.Entity<CategoryTree>().HasData(CategoryTree.SeedData());

        // ------ UnitTrees ------
        modelBuilder.Entity<UnitTree>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ParentId);
        });
        modelBuilder.Entity<UnitTree>().HasData(UnitTree.SeedData());

        // ------ UnitOfMeasure ------
        modelBuilder.Entity<UnitOfMeasure>(entity =>
        {
            entity.ToTable("UnitsOfMeasure");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasMaxLength(36).ValueGeneratedNever();
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.Symbol).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Description).HasMaxLength(200);
            entity.HasIndex(e => e.Name).IsUnique();
        });
        modelBuilder.Entity<UnitOfMeasure>().HasData(UnitOfMeasure.SeedData());

        // ------ AttributeType ------
        modelBuilder.Entity<AttributeType>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.Id).HasMaxLength(36).ValueGeneratedNever();
            entity.Property(t => t.Name).HasMaxLength(50).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(512);
            entity.Property(t => t.SystemType)
                  .HasConversion<string>()
                  .HasMaxLength(32);
        });
        modelBuilder.Entity<AttributeType>().HasData(AttributeType.SeedData());
       

        // ------ AttributeDefinition (Id 改为 string) ------
        modelBuilder.Entity<AttributeDefinition>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasMaxLength(36).ValueGeneratedNever();
            entity.Property(d => d.Name).HasMaxLength(256).IsRequired();
            entity.Property(d => d.Lines);
            entity.Property(d => d.Precision);
            entity.Property(d => d.Scale);

            entity.HasOne(d => d.AttributeType)
                  .WithMany(t => t.Definitions)
                  .HasForeignKey(d => d.AttributeTypeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.Unit)
                  .WithMany()
                  .HasForeignKey(d => d.UnitId)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<AttributeDefinition>().HasData(AttributeDefinition.SeedData());

        // ------ TPC 继承映射 (根类型 AttributeValueBase) ------
        modelBuilder.Entity<AttributeValueBase>(entity =>
        {
            entity.UseTpcMappingStrategy();

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).ValueGeneratedOnAdd();

            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(255);
            entity.Property(e => e.AttributeDefinitionId).IsRequired().HasMaxLength(36);
        });

        // ------ 各派生类型配置 ------
        modelBuilder.Entity<TextAttributeValue>(entity =>
        {
            entity.ToTable("TextAttributeValues");
            entity.Property(e => e.Value).IsRequired();

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.TextValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DecimalAttributeValue>(entity =>
        {
            entity.ToTable("DecimalAttributeValues");

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.DecimalValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<IntegerAttributeValue>(entity =>
        {
            entity.ToTable("IntegerAttributeValues");

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.IntegerValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DateAttributeValue>(entity =>
        {
            entity.ToTable("DateAttributeValues");

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.DateValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TimeAttributeValue>(entity =>
        {
            entity.ToTable("TimeAttributeValues");

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.TimeValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<DateTimeAttributeValue>(entity =>
        {
            entity.ToTable("DateTimeAttributeValues");

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.DateTimeValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<FileAttributeValue>(entity =>
        {
            entity.ToTable("FileAttributeValues");
            entity.Property(e => e.Value).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.ContentType).HasMaxLength(128);
            entity.Property(e => e.Size);

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.FileValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<LocationAttributeValue>(entity =>
        {
            entity.ToTable("LocationAttributeValues");

            entity.HasOne(e => e.Definition)
                  .WithMany(d => d.LocationValues)
                  .HasForeignKey(e => e.AttributeDefinitionId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        base.OnModelCreating(modelBuilder);
    }
}