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
            entity.Property(t => t.Name).HasMaxLength(50).IsRequired();
            entity.Property(t => t.Description).HasMaxLength(512);
            entity.Property(t => t.SystemType)
                  .HasConversion<string>()
                  .HasMaxLength(32);
        });
        modelBuilder.Entity<AttributeType>().HasData(
            new AttributeType { Id = 1, Name = "文本", Description = "用于存储短文本、备注、名称等单行或多行文字信息", SystemType = AttributeTypeEnum.文本 },
            new AttributeType { Id = 2, Name = "整数", Description = "用于存储整数值，如数量、计数、等级等", SystemType = AttributeTypeEnum.整数 },
            new AttributeType { Id = 3, Name = "小数", Description = "用于存储带小数的数值，如价格、重量等", SystemType = AttributeTypeEnum.小数 },
            new AttributeType { Id = 4, Name = "日期", Description = "用于存储日期（不含时间）", SystemType = AttributeTypeEnum.日期 },
            new AttributeType { Id = 5, Name = "时间", Description = "用于存储时间（不含日期）", SystemType = AttributeTypeEnum.时间 },
            new AttributeType { Id = 6, Name = "日期时间", Description = "用于存储精确的日期和时间", SystemType = AttributeTypeEnum.日期时间 },
            new AttributeType { Id = 7, Name = "文件", Description = "用于上传和存储文件，记录文件路径、名称等", SystemType = AttributeTypeEnum.文件 },
            new AttributeType { Id = 8, Name = "定位", Description = "用于存储地理位置信息（经度、纬度）", SystemType = AttributeTypeEnum.定位 }
        );

        // ------ AttributeDefinition (Id 改为 string) ------
        modelBuilder.Entity<AttributeDefinition>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.Id).HasMaxLength(36).ValueGeneratedOnAdd();
            entity.Property(d => d.Name).HasMaxLength(256).IsRequired();
            entity.Property(d => d.Lines);
            entity.Property(d => d.Precision);
            entity.Property(d => d.Scale);

            entity.HasOne(d => d.AttributeType)
                  .WithMany(t => t.Definitions)
                  .HasForeignKey(d => d.AttributeTypeId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(d => d.UnitOfMeasure)
                  .WithMany()
                  .HasForeignKey(d => d.UnitOfMeasureId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

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