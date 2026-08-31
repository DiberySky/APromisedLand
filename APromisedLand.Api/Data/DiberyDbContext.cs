using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace APromisedLand.Api.Data;

public partial class DiberyDbContext(DbContextOptions<DiberyDbContext> options) : DbContext(options)
{
    public DbSet<CategoryTree> CategoryTrees { get; set; }
    public DbSet<UnitTree> UnitTrees { get; set; }
    public DbSet<UnitOfMeasure> UnitsOfMeasure => Set<UnitOfMeasure>();
    // public DbSet<AttributeType> AttributeTypes => Set<AttributeType>();
    public DbSet<AttributeDefinition> AttributeDefinitions => Set<AttributeDefinition>();

    public DbSet<TextAttributeValue> TextAttributeValues => Set<TextAttributeValue>();
    public DbSet<DecimalAttributeValue> DecimalAttributeValues => Set<DecimalAttributeValue>();
    public DbSet<IntegerAttributeValue> IntegerAttributeValues => Set<IntegerAttributeValue>();
    public DbSet<DateAttributeValue> DateAttributeValues => Set<DateAttributeValue>();
    public DbSet<TimeAttributeValue> TimeAttributeValues => Set<TimeAttributeValue>();
    public DbSet<DateTimeAttributeValue> DateTimeAttributeValues => Set<DateTimeAttributeValue>();
    public DbSet<FileAttributeValue> FileAttributeValues => Set<FileAttributeValue>();
    public DbSet<LocationAttributeDefValue> LocationAttributeDefValues => Set<LocationAttributeDefValue>();
    public DbSet<TableAttributeDefValue> TableAttributeDefValues => Set<TableAttributeDefValue>();

    // 动态表类型行实例值（表属性的一行实例）
    public DbSet<TableRowAttributeValue> TableRowAttributeValues => Set<TableRowAttributeValue>();
    public DbSet<LocationAttributeValue> LocationAttributeValues => Set<LocationAttributeValue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 遍历所有 DateTimeOffset 属性，配置 UTC 转换
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (property.ClrType == typeof(DateTimeOffset) || 
                    property.ClrType == typeof(DateTimeOffset?))
                {
                    property.SetValueConverter(
                        new ValueConverter<DateTimeOffset, DateTimeOffset>(
                            v => v.ToUniversalTime(),
                            v => v));
                }
            }
        }
        
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
        // modelBuilder.Entity<AttributeType>(entity =>
        // {
        //     entity.HasKey(t => t.Id);
        //     entity.Property(t => t.Id).HasMaxLength(36).ValueGeneratedNever();
        //     entity.Property(t => t.Description).HasMaxLength(512);
        //     entity.Property(t => t.SystemType)
        //           .HasConversion<string>()
        //           .HasMaxLength(32);
        // });
        // modelBuilder.Entity<AttributeType>().HasData(AttributeType.List());
       

        // ------ AttributeDefinition (Id 改为 string) ------
        modelBuilder.Entity<AttributeDefinition>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasMaxLength(36).ValueGeneratedNever();
            entity.Property(d => d.Name).HasMaxLength(256).IsRequired();
            entity.Property(d => d.Lines);
            entity.Property(d => d.Precision);
            entity.Property(d => d.Scale);

        });
        modelBuilder.Entity<AttributeDefinition>().HasData(AttributeDefinition.SeedData());

        // ------ TPC 继承映射 (根类型 AttributeValueBase) ------
        modelBuilder.Entity<AttributeValueBase>(entity =>
        {
            entity.UseTpcMappingStrategy();

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id)
                .HasMaxLength(36)
                .ValueGeneratedNever(); 

            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.AttributeDefinitionId).IsRequired().HasMaxLength(36);
        });

        // ------ 各派生类型配置 ------
        modelBuilder.Entity<TextAttributeValue>(entity =>
        {
            entity.ToTable("TextAttributeValues");
            entity.Property(e => e.Value).IsRequired();
        });

        modelBuilder.Entity<DecimalAttributeValue>(entity =>
        {
            entity.ToTable("DecimalAttributeValues");
});

        modelBuilder.Entity<IntegerAttributeValue>(entity =>
        {
            entity.ToTable("IntegerAttributeValues");
        });

        modelBuilder.Entity<DateAttributeValue>(entity =>
        {
            entity.ToTable("DateAttributeValues");
});

        modelBuilder.Entity<TimeAttributeValue>(entity =>
        {
            entity.ToTable("TimeAttributeValues");
        });

        modelBuilder.Entity<DateTimeAttributeValue>(entity =>
        {
            entity.ToTable("DateTimeAttributeValues");
        });

        modelBuilder.Entity<FileAttributeValue>(entity =>
        {
            entity.ToTable("FileAttributeValues");
            entity.Property(e => e.Value).HasMaxLength(2048).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(512);
            entity.Property(e => e.ContentType).HasMaxLength(128);
            entity.Property(e => e.Size);
        });

        modelBuilder.Entity<LocationAttributeDefValue>(entity =>
        {
            entity.ToTable("LocationAttributeDefValues");
            entity.Property(e => e.Value).IsRequired();
        });

        modelBuilder.Entity<LocationAttributeValue>(entity =>
        {
            entity.ToTable("LocationAttributeValues");
            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.LocationId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.AttributeDefinitionId).IsRequired().HasMaxLength(36);
        });
        
        modelBuilder.Entity<TableAttributeDefValue>(entity =>
        {
            entity.ToTable("TableAttributeDefValues");
            entity.Property(e => e.Value).IsRequired();
        });
        
        modelBuilder.Entity<TableRowAttributeValue>(entity =>
        {
            entity.ToTable("TableRowAttributeValues");
            entity.Property(e => e.NodeId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.TableId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.AttributeDefinitionId).IsRequired().HasMaxLength(36);
            entity.Property(e => e.CreatedAt).IsRequired();
        });

        OnModelCreatingPartial(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// 动态表相关的模型配置挂载点（实现在 partial 文件 DiberyDbContext.DynamicTable.cs）。
    /// </summary>
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}