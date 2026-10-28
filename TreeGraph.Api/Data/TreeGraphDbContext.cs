using System.Linq;
using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.LineSky.Entities;
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

    // TreeEavSky：计量单位树 / 分类树 / 知识树 / 空间树
    public DbSet<UnitTree> UnitTrees => Set<UnitTree>();
    public DbSet<CategoryTree> CategoryTrees => Set<CategoryTree>();
    public DbSet<KnowledgeTree> KnowledgeTrees => Set<KnowledgeTree>();
    public DbSet<SpaceTree> SpaceTrees => Set<SpaceTree>();

    // 空间（独立简单实体）
    public DbSet<Space> Spaces => Set<Space>();

    // LineSky：线路（独立一等公民，实现 Inode；R7 起补注册 nodes，host_type='Line'）
    public DbSet<TreeGraph.Shared.LineSky.Entities.Line> Lines => Set<TreeGraph.Shared.LineSky.Entities.Line>();

    // LineSky：线路外部连接端子（R7 起 node_id FK 直指 nodes；结构段 = edges 表 line_segment）
    public DbSet<LineTerminal> LineTerminals => Set<LineTerminal>();

    // EdgeSky：边（NebulaGraph 对齐的顶级实体；EdgeType = 名称 + 属性 Schema，边实例 = 四元组）
    public DbSet<EdgeSky.Entities.EdgeType> EdgeTypes => Set<EdgeSky.Entities.EdgeType>();
    public DbSet<EdgeSky.Entities.EdgeTypeProp> EdgeTypeProps => Set<EdgeSky.Entities.EdgeTypeProp>();
    public DbSet<EdgeSky.Entities.Edge> Edges => Set<EdgeSky.Entities.Edge>();

    // NodeSky：通用节点（无归属一等公民；nodes 表一期为宿主主表，
    //   重构期兼作全系统节点身份注册表，specs/node-sky §8.2）
    public DbSet<TreeGraph.Shared.NodeSky.Entities.Node> Nodes => Set<TreeGraph.Shared.NodeSky.Entities.Node>();

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
        ConfigureUnitTrees(mb);
        ConfigureCategoryTrees(mb);
        ConfigureKnowledgeTrees(mb);
        ConfigureSpaceTrees(mb);
        ConfigureSpaces(mb);
        ConfigureLines(mb);
        ConfigureLineTerminals(mb);
        ConfigureNodes(mb);
        ConfigureEdgeSky(mb);
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

            // 共享主键：宿主 Id 即 nodes.id，名称唯一真源在 nodes（R2 Name 收编）
            e.HasOne(x => x.Node)
                .WithOne()
                .HasForeignKey<UnitTree>(x => x.Id)
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
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
            e.Property(x => x.IsArchived).HasColumnName("is_archived");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);

            // 共享主键：宿主 Id 即 nodes.id，名称唯一真源在 nodes（R2 Name 收编）
            e.HasOne(x => x.Node)
                .WithOne()
                .HasForeignKey<CategoryTree>(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ParentId).HasDatabaseName("ix_category_trees_parent");

            e.HasData(CategoryTree.SeedData());
        });
    }

    private static void ConfigureKnowledgeTrees(ModelBuilder mb)
    {
        mb.Entity<KnowledgeTree>(e =>
        {
            e.ToTable("knowledge_trees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
            e.Property(x => x.IsArchived).HasColumnName("is_archived");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);

            // 共享主键：宿主 Id 即 nodes.id，名称唯一真源在 nodes（R2 Name 收编）
            e.HasOne(x => x.Node)
                .WithOne()
                .HasForeignKey<KnowledgeTree>(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ParentId).HasDatabaseName("ix_knowledge_trees_parent");

            e.HasData(KnowledgeTree.SeedData());
        });
    }

    private static void ConfigureSpaceTrees(ModelBuilder mb)
    {
        mb.Entity<SpaceTree>(e =>
        {
            e.ToTable("space_trees");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").HasMaxLength(36);
            e.Property(x => x.ParentId).HasColumnName("parent_id").HasMaxLength(36);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.CanHaveChildren).HasColumnName("can_have_children");
            e.Property(x => x.SortOrder).HasColumnName("sort_order");
            e.Property(x => x.HasChildren).HasColumnName("has_children");

            e.HasOne(x => x.Parent)
                .WithMany()
                .HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);

            // 共享主键：宿主 Id 即 nodes.id，名称唯一真源在 nodes（R2 Name 收编）
            e.HasOne(x => x.Node)
                .WithOne()
                .HasForeignKey<SpaceTree>(x => x.Id)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasIndex(x => x.ParentId).HasDatabaseName("ix_space_trees_parent");
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
            e.Property(x => x.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(50).IsRequired();

            e.HasData(Space.SeedData());
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
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // partial unique：软删后同名可重建
            e.HasIndex(x => x.EntityType)
                .IsUnique()
                .HasDatabaseName("uq_entity_type")
                .HasFilter("is_deleted = false");
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

            // 属性为实体类型私有：同一类型内属性名唯一，不同类型可有同名属性
            e.HasIndex(x => new { x.EntityType, x.AttributeName })
                .IsUnique()
                .HasDatabaseName("uq_attr_catalog");

            e.HasIndex(x => x.EntityType)
                .HasDatabaseName("ix_attr_catalog_entity");

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

            e.HasIndex(x => new { x.EntityId, x.AttributeId })
                .IsUnique().HasDatabaseName("uq_av_entity_attr");

            e.HasIndex(x => x.EntityId)
                .HasDatabaseName("ix_av_entity");

            // ★ #3 冲突检测：加速 max(UpdatedAt) 查询
            e.HasIndex(x => new { x.EntityId, x.UpdatedAt })
                .HasDatabaseName("ix_av_entity_updated")
                .IsDescending(false, true);

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
            e.HasIndex(x => new { x.EntityId, x.AttributeId })
                .IsUnique().HasDatabaseName("uq_lav_entity_attr");

            e.HasIndex(x => x.EntityId)
                .HasDatabaseName("ix_lav_entity");

            // 冲突检测：加速 max(UpdatedAt)（与 attribute_values 对齐）
            e.HasIndex(x => new { x.EntityId, x.UpdatedAt })
                .HasDatabaseName("ix_lav_entity_updated")
                .IsDescending(false, true);

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
            e.Property(x => x.TypeName).HasColumnName("type_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // 组合类型全局化：唯一键不再含实体类型维度
            e.HasIndex(x => new { x.TypeName, x.Version })
                .IsUnique().HasDatabaseName("uq_composite_type");
        });

        mb.Entity<CompositeFieldDefinition>(e =>
        {
            // CHECK：int / decimal 才能绑单位；single_choice 才能绑选项集
            e.ToTable("composite_field_definitions", t =>
            {
                t.HasCheckConstraint(
                    "ck_composite_field_decimal_unit",
                    "data_type IN ('int', 'decimal') OR unit_id IS NULL");

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

            // 不采用级联删除：组合字段非空时由应用层拒绝删除组合类型
            e.HasOne(x => x.CompositeType).WithMany(t => t.Fields)
                .HasForeignKey(x => x.CompositeTypeId).OnDelete(DeleteBehavior.Restrict);
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

            // 不采用级联删除：选项非空时由应用层拒绝删除选项集
            e.HasOne(x => x.OptionSet).WithMany(s => s.Items)
                .HasForeignKey(x => x.OptionSetId).OnDelete(DeleteBehavior.Restrict);
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
            e.Property(x => x.TableName).HasColumnName("table_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.IsDeleted).HasColumnName("is_deleted");
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");
            e.Property(x => x.IsRecordTable).HasColumnName("is_record_table");
            e.Property(x => x.HasTimeColumn).HasColumnName("has_time_column");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => new { x.TableName, x.Version })
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

            // 不采用级联删除：列非空时由应用层拒绝删除自定义表
            e.HasOne(x => x.Table).WithMany(t => t.Columns)
                .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Restrict);
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

            // 不采用级联删除：属性有实例数据（含表行）时由应用层拒绝删除该属性
            e.HasOne(x => x.Attribute).WithMany()
                .HasForeignKey(x => x.AttributeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Table).WithMany()
                .HasForeignKey(x => x.TableDefinitionId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureAuditLog(ModelBuilder mb)
    {
        mb.Entity<AttributeAuditLog>(e =>
        {
            // 审计只增不减，普通表 + 两个索引即可（此前按月分区，仅为归档便利，已取消）。
            e.ToTable("attribute_audit_log");
            e.HasKey(x => x.AuditId);
            e.Property(x => x.AuditId)
                .HasColumnName("audit_id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.EntityId).HasColumnName("entity_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.AttributeId).HasColumnName("attribute_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.AttributeName).HasColumnName("attribute_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.OldValue).HasColumnName("old_value");
            e.Property(x => x.NewValue).HasColumnName("new_value");
            e.Property(x => x.ChangeType).HasColumnName("change_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.ChangedBy).HasColumnName("changed_by").HasMaxLength(200).IsRequired();
            e.Property(x => x.ChangedAt).HasColumnName("changed_at");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id").HasMaxLength(100);
            e.Property(x => x.ClientIp).HasColumnName("client_ip").HasMaxLength(50);

            e.HasIndex(x => new { x.EntityId, x.ChangedAt })
                .HasDatabaseName("ix_audit_entity")
                .IsDescending(false, true);
            e.HasIndex(x => x.ChangedAt).HasDatabaseName("ix_audit_time");
        });
    }

    // ============================================================
    // NodeSky：通用节点（一期宿主主表；重构期全系统节点身份注册表）
    // ============================================================
    private static void ConfigureNodes(ModelBuilder mb)
    {
        mb.Entity<TreeGraph.Shared.NodeSky.Entities.Node>(e =>
        {
            e.ToTable("nodes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            // 宿主类型名：一期恒为 "Node"；重构期各宿主写入自己的类型名（spec §8.2）
            e.Property(x => x.HostType).HasColumnName("host_type").HasMaxLength(16).IsRequired()
                .HasDefaultValue("Node");
            // 可空：边注册进 nodes 时名字为派生物（from名 → to名），存 null（spec §8.6 R3）
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200);
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            // R7 身份锚点：仅 LineNode 必填，恒 = 所属 Line 的 nodes.id（自引用 Restrict）
            e.Property(x => x.LineId).HasColumnName("line_id").HasMaxLength(36);
            e.HasOne<TreeGraph.Shared.NodeSky.Entities.Node>()
                .WithMany()
                .HasForeignKey(x => x.LineId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_nodes_line_id");
            e.HasIndex(x => x.LineId).HasDatabaseName("ix_nodes_line_id");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });

        // R2 Name 收编：宿主树种子对应的 nodes 注册表行（名称唯一真源）
        mb.Entity<TreeGraph.Shared.NodeSky.Entities.Node>()
            .HasData(
                UnitTree.SeedNodes()
                    .Concat(CategoryTree.SeedNodes())
                    .Concat(KnowledgeTree.SeedNodes())
                    .ToList());
    }

    // ============================================================
    // LineSky：线路（独立一等公民，实现 Inode）
    // ============================================================
    private static void ConfigureLines(ModelBuilder mb)
    {
        mb.Entity<TreeGraph.Shared.LineSky.Entities.Line>(e =>
        {
            e.ToTable("lines");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id)
                .HasColumnName("id")
                .HasMaxLength(36).IsRequired()
                .HasDefaultValueSql("gen_random_uuid()::text")
                .HasSentinel("");
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
            e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // 线路从属于图空间（结构段边 line_segment 写入该空间）；Restrict：有线路时禁止删空间
            e.HasOne<TreeGraph.Api.SpaceSky.Entities.Space>()
                .WithMany()
                .HasForeignKey(x => x.SpaceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_lines_space");
            e.HasIndex(x => x.SpaceId).HasDatabaseName("ix_lines_space");
        });
    }

    // ============================================================
    // LineSky：线路外部连接端子（R7：结构段 = relation 值，头表/段表已删）
    // ============================================================
    private static void ConfigureLineTerminals(ModelBuilder mb)
    {
        mb.Entity<LineTerminal>(e =>
        {
            e.ToTable("line_terminals");
            e.HasKey(x => new { x.LineId, x.NodeId });
            e.Property(x => x.LineId).HasColumnName("line_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.NodeId).HasColumnName("node_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.Role)
                .HasColumnName("role")
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.OperatingState)
                .HasColumnName("operating_state")
                .HasConversion<string>().HasMaxLength(20).IsRequired();
            e.Property(x => x.ExternalNodeId).HasColumnName("external_node_id").HasMaxLength(36);

            // 端子挂线路（删除线路由服务层守卫拦截，Restrict 兜底）
            e.HasOne<TreeGraph.Shared.LineSky.Entities.Line>()
                .WithMany()
                .HasForeignKey(x => x.LineId)
                .OnDelete(DeleteBehavior.Restrict);

            // R7：端子节点直指 nodes(id)（LineNode = nodes 注册行）；同线归属由服务层校验
            e.HasOne<TreeGraph.Shared.NodeSky.Entities.Node>()
                .WithMany()
                .HasForeignKey(x => x.NodeId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_lt_node");

            e.HasIndex(x => x.ExternalNodeId).HasDatabaseName("ix_line_terminals_external_node");
        });
    }

    // ============================================================
    // EdgeSky：边类型 / 边类型属性 / 边实例（NebulaGraph 对齐）
    // ============================================================
    private static void ConfigureEdgeSky(ModelBuilder mb)
    {
        mb.Entity<EdgeSky.Entities.EdgeType>(e =>
        {
            // TTL 列与时长成对出现（Nebula：TTL_COL 须配 TTL_DURATION）
            e.ToTable("edge_types", t => t.HasCheckConstraint(
                "ck_edge_types_ttl",
                "(ttl_col IS NULL) = (ttl_duration_seconds IS NULL)"));
            // Nebula 对齐：EdgeType 是空间级对象，(space_id, name) 复合主键，同名可跨空间
            e.HasKey(x => new { x.SpaceId, x.Name });
            e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
            e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            e.Property(x => x.TtlCol).HasColumnName("ttl_col").HasMaxLength(200);
            e.Property(x => x.TtlDurationSeconds).HasColumnName("ttl_duration_seconds");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            // 空间 Restrict：空间内仍有边类型时禁止删空间（服务层先校验）
            e.HasOne<SpaceSky.Entities.Space>()
                .WithMany()
                .HasForeignKey(x => x.SpaceId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_edge_types_space");
            // 内置种子（line_segment）改由 BuiltinEdgeTypeSeeder 启动时按空间幂等播种
        });

        mb.Entity<EdgeSky.Entities.EdgeTypeProp>(e =>
        {
            // Nebula 约束：NOT NULL 必须配 DEFAULT
            e.ToTable("edge_type_props", t => t.HasCheckConstraint(
                "ck_edge_type_props_notnull_default",
                "nullable OR default_value IS NOT NULL"));
            e.HasKey(x => new { x.SpaceId, x.EdgeTypeName, x.PropName });
            e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EdgeTypeName).HasColumnName("edge_type_name").HasMaxLength(100).IsRequired();
            e.Property(x => x.PropName).HasColumnName("prop_name").HasMaxLength(200).IsRequired();
            e.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(20).IsRequired();
            e.Property(x => x.Nullable).HasColumnName("nullable");
            e.Property(x => x.DefaultValue).HasColumnName("default_value").HasColumnType("jsonb");
            e.Property(x => x.Comment).HasColumnName("comment").HasMaxLength(1000);
            e.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(200);
            e.Property(x => x.DisplayOrder).HasColumnName("display_order");

            // 属性定义是类型的内在组成：删类型前由服务层先删属性行（Restrict 兜底）
            e.HasOne(x => x.EdgeType).WithMany(t => t.Props)
                .HasForeignKey(x => new { x.SpaceId, x.EdgeTypeName }).OnDelete(DeleteBehavior.Restrict);
            // 内置种子（line_segment.name）改由 BuiltinEdgeTypeSeeder 启动时按空间幂等播种
        });

        mb.Entity<EdgeSky.Entities.Edge>(e =>
        {
            e.ToTable("edges");
            // 五元组复合主键：<SpaceId, EdgeType, 起点 VID, Rank, 终点 VID>（Nebula 边唯一标识；边从属于空间）
            e.HasKey(x => new { x.SpaceId, x.EdgeType, x.SrcVid, x.Rank, x.DstVid });
            e.Property(x => x.SpaceId).HasColumnName("space_id").HasMaxLength(36).IsRequired();
            e.Property(x => x.EdgeType).HasColumnName("edge_type").HasMaxLength(100).IsRequired();
            e.Property(x => x.SrcVid).HasColumnName("src_vid").HasMaxLength(36).IsRequired();
            e.Property(x => x.DstVid).HasColumnName("dst_vid").HasMaxLength(36).IsRequired();
            e.Property(x => x.Rank).HasColumnName("rank");
            e.Property(x => x.Props).HasColumnName("props").HasColumnType("jsonb");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.SrcVid).HasDatabaseName("ix_edges_src");
            e.HasIndex(x => x.DstVid).HasDatabaseName("ix_edges_dst");
            e.HasIndex(x => x.Props).HasDatabaseName("ix_edges_props").HasMethod("GIN")
                .HasFilter("props IS NOT NULL");

            // 两端必须是 nodes 中的真实节点（Restrict：被边引用时禁止删除）
            e.HasOne<TreeGraph.Shared.NodeSky.Entities.Node>()
                .WithMany()
                .HasForeignKey(x => x.SrcVid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_edges_src_node");
            e.HasOne<TreeGraph.Shared.NodeSky.Entities.Node>()
                .WithMany()
                .HasForeignKey(x => x.DstVid)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_edges_dst_node");

            // 边类型 Restrict：有实例时由服务层拒绝删类型（复合 FK 对齐空间级主键）
            e.HasOne<EdgeSky.Entities.EdgeType>()
                .WithMany()
                .HasForeignKey(x => new { x.SpaceId, x.EdgeType })
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("fk_edges_edge_type");
        });
    }
}
