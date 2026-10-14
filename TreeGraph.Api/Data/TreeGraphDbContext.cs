using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.SpaceSky.Entities;
using TreeGraph.Shared.TreeEavSky.Entities;

namespace TreeGraph.Api.Data;

public class TreeGraphDbContext : DbContext
{
    public DbSet<EntityTypeDefinition> EntityTypes => Set<EntityTypeDefinition>();
    public DbSet<AttributeDefinition> AttributeCatalog => Set<AttributeDefinition>();
    public DbSet<AttributeValue> AttributeValues => Set<AttributeValue>();
    /// <summary>位置属性值（location 类型，独立坐标表）。</summary>
    public DbSet<LocationAttributeValue> LocationAttributeValues => Set<LocationAttributeValue>();
    public DbSet<CompositeTypeDefinition> CompositeTypes => Set<CompositeTypeDefinition>();
    public DbSet<CompositeFieldDefinition> CompositeFields => Set<CompositeFieldDefinition>();
    public DbSet<AttributeAuditLog> AttributeAuditLogs => Set<AttributeAuditLog>();
    public DbSet<OptionSet> OptionSets => Set<OptionSet>();
    public DbSet<OptionItem> OptionItems => Set<OptionItem>();
    public DbSet<CustomTableDefinition> CustomTables => Set<CustomTableDefinition>();
    public DbSet<CustomTableColumn> CustomTableColumns => Set<CustomTableColumn>();
    public DbSet<CustomTableRow> CustomTableRows => Set<CustomTableRow>();

    // iNode 关联（外挂表，不改动现有 EAV 表）
    public DbSet<InodeEntityType> InodeEntityTypes => Set<InodeEntityType>();
    public DbSet<InodeEntity> InodeEntities => Set<InodeEntity>();

    // Edge（关系/边）
    public DbSet<EdgeDefinition> EdgeDefinitions => Set<EdgeDefinition>();
    public DbSet<EdgeInstance> EdgeInstances => Set<EdgeInstance>();

    // TreeEavSky：计量单位树 / 分类树 / 空间树
    public DbSet<UnitTree> UnitTrees => Set<UnitTree>();
    public DbSet<CategoryTree> CategoryTrees => Set<CategoryTree>();
    public DbSet<SpaceTree> SpaceTrees => Set<SpaceTree>();

    // 空间（独立简单实体）
    public DbSet<Space> Spaces => Set<Space>();

    public TreeGraphDbContext(DbContextOptions<TreeGraphDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder mb)
    {
        ConfigureEntityTypes(mb);
        ConfigureInodeEntityTypes(mb);
        ConfigureInodeEntities(mb);
        ConfigureOptionSets(mb);
        ConfigureAttributeCatalog(mb);
        ConfigureAttributeValues(mb);
        ConfigureLocationAttributeValues(mb);
        ConfigureCompositeTypes(mb);
        ConfigureCustomTables(mb);
        ConfigureAuditLog(mb);
        ConfigureEdges(mb);
        ConfigureUnitTrees(mb);
        ConfigureCategoryTrees(mb);
        ConfigureSpaceTrees(mb);
        ConfigureSpaces(mb);
    }

    // ============================================================
    // TreeEavSky：计量单位树 / 分类树
    // ============================================================
    private static void ConfigureUnitTrees(ModelBuilder mb)
    {
        mb.Entity<UnitTree>(e =>
        {
            e.ToTable("unit_trees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Abbreviation).HasColumnName("abbreviation").HasMaxLength(50);
            e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.HasChildren).HasColumnName("has_children");

            e.HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ParentId).HasDatabaseName("ix_unit_trees_parent");

            e.HasData(UnitTree.SeedData());
        });
    }

    private static void ConfigureCategoryTrees(ModelBuilder mb)
    {
        mb.Entity<CategoryTree>(e =>
        {
            e.ToTable("category_trees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(300).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
            e.Property(x => x.IsArchived).HasColumnName("is_archived");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);

            e.HasIndex(x => x.ParentId).HasDatabaseName("ix_category_trees_parent");

            e.HasData(CategoryTree.SeedData());
        });
    }

    private static void ConfigureSpaceTrees(ModelBuilder mb)
    {
        mb.Entity<SpaceTree>(e =>
        {
            e.ToTable("space_trees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.HasChildren).HasColumnName("has_children");

            e.HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ParentId).HasDatabaseName("ix_space_trees_parent");

            // 仅播种唯一根节点「空间树」，其余节点全部由界面创建
            e.HasData(SpaceTree.SeedData());
        });
    }

    private static void ConfigureSpaces(ModelBuilder mb)
    {
        mb.Entity<Space>(e =>
        {
            e.ToTable("spaces");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        });
    }

    // ============================================================
    // StringTreeSky：已移除
    // ============================================================

    private static void ConfigureEntityTypes(ModelBuilder mb)
    {
        mb.Entity<EntityTypeDefinition>(e =>
        {
            e.ToTable("entity_type_catalog");
            e.HasKey(x => x.EntityTypeId);
            e.Property(x => x.EntityTypeId)
                .HasColumnName("entity_type_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType)
                .HasColumnName("entity_type")
                .HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName)
                .HasColumnName("display_name")
                .HasMaxLength(200).IsRequired();
            e.Property(x => x.Description)
                .HasColumnName("description")
                .HasMaxLength(1000);
            e.Property(x => x.OwnerSpaceId)
                .HasColumnName("owner_space_id")
                .HasMaxLength(36);
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // partial unique：软删后同名可重建
            e.HasIndex(x => x.EntityType)
                .IsUnique()
                .HasDatabaseName("uq_entity_type")
                .HasFilter("is_deleted = false");

            // 按空间过滤可见类型（共享 ∪ 本空间独占）
            e.HasIndex(x => x.OwnerSpaceId)
                .HasDatabaseName("ix_entity_type_owner_space");
        });
    }

    // ============================================================
    // iNode → EntityType 声明（N:N）
    // ============================================================
    private static void ConfigureInodeEntityTypes(ModelBuilder mb)
    {
        mb.Entity<InodeEntityType>(e =>
        {
            e.ToTable("inode_entitytype");

            // 复合主键：每 (inode, entityType) 只 1 条
            e.HasKey(x => new { x.InodeId, x.EntityType });

            e.Property(x => x.InodeId).HasColumnName("inode_id")
                .HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type")
                .HasMaxLength(100).IsRequired();
            e.Property(x => x.AttachedAt).HasColumnName("attached_at");

            e.HasIndex(x => x.InodeId).HasDatabaseName("ix_inode_et_inode");
            e.HasIndex(x => x.EntityType).HasDatabaseName("ix_inode_et_type");
        });
    }

    // ============================================================
    // iNode ↔ 实体归属
    // ============================================================
    private static void ConfigureInodeEntities(ModelBuilder mb)
    {
        mb.Entity<InodeEntity>(e =>
        {
            e.ToTable("inode_entity");

            // R2：每 iNode 每类型只 1 个实体
            e.HasKey(x => new { x.InodeId, x.EntityType });

            e.Property(x => x.InodeId).HasColumnName("inode_id")
                .HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type")
                .HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasColumnName("entity_id")
                .HasMaxLength(36).IsRequired();
            e.Property(x => x.AttachedAt).HasColumnName("attached_at");

            // R3：每个实体只属于 1 个 iNode
            e.HasIndex(x => new { x.EntityType, x.EntityId })
                .IsUnique()
                .HasDatabaseName("uq_inode_entity_global");

            e.HasIndex(x => x.InodeId).HasDatabaseName("ix_inode_entity_inode");
            e.HasIndex(x => x.EntityId).HasDatabaseName("ix_inode_entity_entity");
        });
    }

    private static void ConfigureAttributeCatalog(ModelBuilder mb)
    {
        mb.Entity<AttributeDefinition>(e =>
        {
            e.ToTable("attribute_catalog");

            e.HasKey(x => x.AttributeId);
            e.Property(x => x.AttributeId)
                .HasColumnName("attribute_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
            e.Property(x => x.IsSortable).HasColumnName("is_sortable");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);

            e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");
            e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
            e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);
            e.Property(x => x.RefTableDefinitionId).HasColumnName("ref_table_definition_id").HasMaxLength(36);
            e.Property(x => x.RefOptionSetId).HasColumnName("ref_option_set_id").HasMaxLength(36);
            e.Property(x => x.UnitId).HasColumnName("unit_id").HasMaxLength(36);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.EntityType, x.AttributeName })
                .IsUnique()
                .HasDatabaseName("uq_attr_catalog");

            e.HasIndex(x => x.EntityType)
                .HasDatabaseName("ix_attr_catalog_entity")
                .HasFilter("is_deleted = false");

            e.HasOne(x => x.RefCompositeType)
                .WithMany().HasForeignKey(x => x.RefCompositeTypeId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RefTableDefinition).WithMany()
                .HasForeignKey(x => x.RefTableDefinitionId).OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.RefOptionSet).WithMany()
                .HasForeignKey(x => x.RefOptionSetId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAttributeValues(ModelBuilder mb)
    {
        mb.Entity<AttributeValue>(e =>
        {
            e.ToTable("attribute_values");
            e.HasKey(x => x.ValueId);
            e.Property(x => x.ValueId)
                .HasColumnName("value_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ValueString).HasColumnName("value_string").HasMaxLength(2000);
            e.Property(x => x.ValueInt).HasColumnName("value_int");

            e.Property(x => x.ValueDecimal)
                .HasColumnName("value_decimal")
                .HasPrecision(38, 15);

            e.Property(x => x.ValueBool).HasColumnName("value_bool");
            e.Property(x => x.ValueDatetime).HasColumnName("value_datetime").HasColumnType("timestamptz");
            e.Property(x => x.ValueDateOnly).HasColumnName("value_dateonly").HasColumnType("date");
            e.Property(x => x.ValueTime).HasColumnName("value_time").HasColumnType("time(0)");
            e.Property(x => x.ValueFileMeta).HasColumnName("value_file_meta").HasColumnType("jsonb");
            e.Property(x => x.ValueJsonb).HasColumnName("value_jsonb").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.UnitId).HasColumnName("unit_id").HasMaxLength(36);

            e.HasIndex(x => new { x.EntityId, x.EntityType, x.AttributeId })
                .IsUnique().HasDatabaseName("uq_av_entity_attr");

            e.HasIndex(x => new { x.EntityType, x.EntityId })
                .HasDatabaseName("ix_av_entity");

            // ★ #3 冲突检测：加速 max(UpdatedAt) 查询
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.UpdatedAt })
                .HasDatabaseName("ix_av_entity_updated")
                .IsDescending(false, false, true);

            e.HasIndex(x => new { x.AttributeId, x.ValueInt })
                .HasDatabaseName("ix_av_attr_int").HasFilter("value_int IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueDecimal })
                .HasDatabaseName("ix_av_attr_decimal").HasFilter("value_decimal IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueString })
                .HasDatabaseName("ix_av_attr_string").HasFilter("value_string IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueBool })
                .HasDatabaseName("ix_av_attr_bool").HasFilter("value_bool IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueDatetime })
                .HasDatabaseName("ix_av_attr_datetime").HasFilter("value_datetime IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueDateOnly })
                .HasDatabaseName("ix_av_attr_dateonly").HasFilter("value_dateonly IS NOT NULL");
            e.HasIndex(x => new { x.AttributeId, x.ValueTime })
                .HasDatabaseName("ix_av_attr_time").HasFilter("value_time IS NOT NULL");

            e.HasIndex(x => x.ValueJsonb)
                .HasDatabaseName("ix_av_jsonb").HasMethod("GIN")
                .HasFilter("value_jsonb IS NOT NULL");
            e.HasIndex(x => x.ValueFileMeta)
                .HasDatabaseName("ix_av_file_meta").HasMethod("GIN")
                .HasFilter("value_file_meta IS NOT NULL");

            e.HasOne(x => x.Attribute).WithMany()
                .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLocationAttributeValues(ModelBuilder mb)
    {
        mb.Entity<LocationAttributeValue>(e =>
        {
            e.ToTable("location_attribute_values");
            e.HasKey(x => x.ValueId);
            e.Property(x => x.ValueId)
                .HasColumnName("value_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();

            e.Property(x => x.Latitude).HasColumnName("latitude");
            e.Property(x => x.Longitude).HasColumnName("longitude");
            e.Property(x => x.Label).HasColumnName("label").HasMaxLength(200);
            e.Property(x => x.Address).HasColumnName("address").HasMaxLength(500);
            e.Property(x => x.CoordinateSystem)
                .HasColumnName("coordinate_system").HasMaxLength(20)
                .HasDefaultValue("gcj02").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // 一个实体的一个位置属性最多一个坐标点
            e.HasIndex(x => new { x.EntityId, x.EntityType, x.AttributeId })
                .IsUnique().HasDatabaseName("uq_lav_entity_attr");

            e.HasIndex(x => new { x.EntityType, x.EntityId })
                .HasDatabaseName("ix_lav_entity");

            // 冲突检测：加速 max(UpdatedAt)（与 attribute_values 对齐）
            e.HasIndex(x => new { x.EntityType, x.EntityId, x.UpdatedAt })
                .HasDatabaseName("ix_lav_entity_updated")
                .IsDescending(false, false, true);

            e.HasOne(x => x.Attribute).WithMany()
                .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureCompositeTypes(ModelBuilder mb)
    {
        mb.Entity<CompositeTypeDefinition>(e =>
        {
            e.ToTable("composite_type_definitions");
            e.HasKey(x => x.CompositeTypeId);
            e.Property(x => x.CompositeTypeId)
                .HasColumnName("composite_type_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.TypeName).HasColumnName("type_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.EntityType, x.TypeName, x.Version })
                .IsUnique().HasDatabaseName("uq_composite_type");
        });

        mb.Entity<CompositeFieldDefinition>(e =>
        {
            // CHECK：decimal 才能绑单位；single_choice 才能绑选项集
            e.ToTable("composite_field_definitions", t =>
            {
                t.HasCheckConstraint(
                    "ck_composite_field_decimal_unit",
                    "data_type = 'decimal' OR unit_id IS NULL");

                // ★ #8：single_choice 才能绑选项集
                t.HasCheckConstraint(
                    "ck_composite_field_single_choice_optionset",
                    "data_type = 'single_choice' OR ref_option_set_id IS NULL");
            });

            e.HasKey(x => x.FieldId);
            e.Property(x => x.FieldId)
                .HasColumnName("field_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.CompositeTypeId).HasColumnName("composite_type_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.FieldName).HasColumnName("field_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);

            // ★ #4：单位外键列（指向 unit_trees 节点）
            e.Property(x => x.UnitId).HasColumnName("unit_id").HasMaxLength(36);
            // ★ #8：选项集外键列
            e.Property(x => x.RefOptionSetId).HasColumnName("ref_option_set_id").HasMaxLength(36);

            e.Property(x => x.IsArray).HasColumnName("is_array");
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
            e.Property(x => x.IsSortable).HasColumnName("is_sortable");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);
            e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
            e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");

            e.HasIndex(x => new { x.CompositeTypeId, x.FieldName })
                .IsUnique().HasDatabaseName("uq_composite_field");

            e.HasOne(x => x.CompositeType).WithMany(t => t.Fields)
                .HasForeignKey(x => x.CompositeTypeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RefCompositeType).WithMany()
                .HasForeignKey(x => x.RefCompositeTypeId).OnDelete(DeleteBehavior.Restrict);

            // ★ #4：单位外键（Restrict）
            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);

            // ★ #8：选项集外键（Restrict）
            e.HasOne(x => x.RefOptionSet).WithMany()
                .HasForeignKey(x => x.RefOptionSetId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureOptionSets(ModelBuilder mb)
    {
        mb.Entity<OptionSet>(e =>
        {
            e.ToTable("option_sets");
            e.HasKey(x => x.OptionSetId);
            e.Property(x => x.OptionSetId)
                .HasColumnName("option_set_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.SetName).HasColumnName("set_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");   // ★ 新增

            // ★ 改为 partial unique index：软删的集合不占用 SetName
            e.HasIndex(x => new { x.EntityType, x.SetName })
                .IsUnique()
                .HasDatabaseName("uq_option_set")
                .HasFilter("is_deleted = false");
        });

        mb.Entity<OptionItem>(e =>
        {
            e.ToTable("option_items");
            e.HasKey(x => x.OptionItemId);
            e.Property(x => x.OptionItemId)
                .HasColumnName("option_item_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.OptionSetId).HasColumnName("option_set_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.Value).HasColumnName("value").HasMaxLength(200).IsRequired();
            e.Property(x => x.Label).HasColumnName("label").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsDefault).HasColumnName("is_default");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");

            e.HasIndex(x => new { x.OptionSetId, x.Value })
                .IsUnique().HasDatabaseName("uq_option_item_value");
            e.HasIndex(x => x.OptionSetId).HasDatabaseName("ix_option_items_set");

            e.HasOne(x => x.OptionSet).WithMany(s => s.Items)
                .HasForeignKey(x => x.OptionSetId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCustomTables(ModelBuilder mb)
    {
        mb.Entity<CustomTableDefinition>(e =>
        {
            e.ToTable("custom_table_definitions");
            e.HasKey(x => x.TableDefinitionId);
            e.Property(x => x.TableDefinitionId)
                .HasColumnName("table_definition_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.TableName).HasColumnName("table_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.EntityType, x.TableName, x.Version })
                .IsUnique().HasDatabaseName("uq_custom_table");
        });

        mb.Entity<CustomTableColumn>(e =>
        {
            // CHECK：int / decimal 才能绑单位
            e.ToTable("custom_table_columns", t =>
            {
                t.HasCheckConstraint(
                    "ck_custom_table_column_numeric_unit",
                    "data_type IN ('int', 'decimal') OR unit_id IS NULL");
            });

            e.HasKey(x => x.ColumnId);
            e.Property(x => x.ColumnId)
                .HasColumnName("column_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.TableDefinitionId).HasColumnName("table_definition_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ColumnName).HasColumnName("column_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.RefCompositeTypeId).HasColumnName("ref_composite_type_id").HasMaxLength(36);
            e.Property(x => x.IsRequired).HasColumnName("is_required");
            e.Property(x => x.IsSearchable).HasColumnName("is_searchable");
            e.Property(x => x.IsSortable).HasColumnName("is_sortable");
            e.Property(x => x.IsUnique).HasColumnName("is_unique");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasMaxLength(500);
            e.Property(x => x.ValidationRule).HasColumnName("validation_rule").HasColumnType("jsonb");
            e.Property(x => x.AllowedValues).HasColumnName("allowed_values").HasColumnType("jsonb");
            e.Property(x => x.UnitId).HasColumnName("unit_id").HasMaxLength(36);

            e.HasIndex(x => new { x.TableDefinitionId, x.ColumnName })
                .IsUnique().HasDatabaseName("uq_custom_table_column");

            e.HasOne(x => x.Table).WithMany(t => t.Columns)
                .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.RefCompositeType).WithMany()
                .HasForeignKey(x => x.RefCompositeTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Unit).WithMany()
                .HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<CustomTableRow>(e =>
        {
            e.ToTable("custom_table_rows");
            e.HasKey(x => x.RowId);
            e.Property(x => x.RowId)
                .HasColumnName("row_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.TableDefinitionId).HasColumnName("table_definition_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ParentEntityId).HasColumnName("parent_entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ParentEntityType).HasColumnName("parent_entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.RowData).HasColumnName("row_data").HasColumnType("jsonb").IsRequired();
            e.Property(x => x.RowOrder).HasColumnName("row_order");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.ParentEntityType, x.ParentEntityId, x.AttributeId })
                .HasDatabaseName("ix_ctr_parent");
            e.HasIndex(x => x.TableDefinitionId).HasDatabaseName("ix_ctr_table");
            e.HasIndex(x => x.RowData).HasDatabaseName("ix_ctr_rowdata").HasMethod("GIN");
            e.HasIndex(x => new { x.ParentEntityId, x.AttributeId, x.RowOrder })
                .HasDatabaseName("ix_ctr_order");

            e.HasOne(x => x.Attribute).WithMany()
                .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Table).WithMany()
                .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder mb)
    {
        mb.Entity<AttributeAuditLog>(e =>
        {
            // ★ 按月分区表（审计只增不减，分区便于按月归档/清理）：
            //   分区表 DDL 由迁移中的手写 SQL 创建（PARTITION BY RANGE），
            //   EF 只负责运行时映射，故排除出自动迁移。
            e.ToTable("attribute_audit_log", t => t.ExcludeFromMigrations());
            // ★ PG 分区表主键必须包含分区键 changed_at。
            e.HasKey(x => new { x.AuditId, x.ChangedAt });
            e.Property(x => x.AuditId)
                .HasColumnName("audit_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EntityType).HasColumnName("entity_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.OldValue).HasColumnName("old_value");
            e.Property(x => x.NewValue).HasColumnName("new_value");
            e.Property(x => x.ChangeType).HasColumnName("change_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.ChangedAt).HasColumnName("changed_at");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            e.Property(x => x.ClientIp).HasColumnName("client_ip").HasMaxLength(50);

            e.HasIndex(x => new { x.EntityType, x.EntityId, x.ChangedAt })
                .HasDatabaseName("ix_audit_entity")
                .IsDescending(false, false, true);
            e.HasIndex(x => x.ChangedAt).HasDatabaseName("ix_audit_time");
        });
    }

    // ============================================================
    // Edge（关系/边）
    // ============================================================
    private static void ConfigureEdges(ModelBuilder mb)
    {
        mb.Entity<EdgeDefinition>(e =>
        {
            e.ToTable("edge_definitions");
            e.HasKey(x => x.EdgeTypeId);
            e.Property(x => x.EdgeTypeId)
                .HasColumnName("edge_type_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.TypeName).HasColumnName("type_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.FromNodeType).HasColumnName("from_node_type").HasMaxLength(100);
            e.Property(x => x.ToNodeType).HasColumnName("to_node_type").HasMaxLength(100);
            e.Property(x => x.IsGlobal).HasColumnName("is_global");
            e.Property(x => x.IsDirected).HasColumnName("is_directed");
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.IsActive).HasColumnName("is_active");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.TypeName)
                .IsUnique()
                .HasDatabaseName("uq_edge_definition_type_name");
        });

        mb.Entity<EdgeInstance>(e =>
        {
            e.ToTable("edge_instances");
            e.HasKey(x => x.EdgeId);
            e.Property(x => x.EdgeId)
                .HasColumnName("edge_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EdgeTypeId).HasColumnName("edge_type_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.FromNodeId).HasColumnName("from_node_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.ToNodeId).HasColumnName("to_node_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.Weight).HasColumnName("weight").HasPrecision(18, 4);
            e.Property(x => x.EnforceSingleIncoming)
                .HasColumnName("enforce_single_incoming").HasDefaultValue(false);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // 同类型同方向不重复（UNIQUE）
            e.HasIndex(x => new { x.EdgeTypeId, x.FromNodeId, x.ToNodeId })
                .IsUnique()
                .HasDatabaseName("uq_edge_instance");

            // 严格线性链（IsChain && IsLinear）：同一类型下同一 to_node_id 至多一条边
            // （入度 ≤ 1）。用部分唯一索引把并发下“查后插”的竞态收敛为数据库原子约束。
            //
            // 注意：列顺序刻意与下方通用索引 ix_ei_to 相反（ToNodeId, EdgeTypeId）。
            // EF Core 按属性列表识别索引，相同列集合会被合并覆盖；反转顺序使二者成为
            // 两个独立索引，既保留通用入边查询索引，又叠加唯一约束。唯一性语义与
            // (EdgeTypeId, ToNodeId) 完全等价（列顺序不影响唯一性）。
            e.HasIndex(x => new { x.ToNodeId, x.EdgeTypeId })
                .IsUnique()
                .HasFilter("enforce_single_incoming = true")
                .HasDatabaseName("uq_ei_single_incoming");

            // 图查询索引：from 出边 / to 入边
            e.HasIndex(x => new { x.EdgeTypeId, x.FromNodeId })
                .HasDatabaseName("ix_ei_from");
            e.HasIndex(x => new { x.EdgeTypeId, x.ToNodeId })
                .HasDatabaseName("ix_ei_to");

            // 反向图查询：按 node_id 查所有关联边（不分类型）
            e.HasIndex(x => x.FromNodeId).HasDatabaseName("ix_ei_from_node");
            e.HasIndex(x => x.ToNodeId).HasDatabaseName("ix_ei_to_node");

            // FK：级联删 Definition → 级联删 Instance
            e.HasOne(x => x.EdgeType)
                .WithMany(d => d.Instances)
                .HasForeignKey(x => x.EdgeTypeId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
