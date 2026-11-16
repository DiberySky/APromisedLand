using Microsoft.EntityFrameworkCore;
using TreeGraph.Api.LineSky.Entities;
using TreeGraph.Api.NodeEavSky.Entities;
using TreeGraph.Api.SpaceNodeSky.Entities;
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

    // TreeEavSky：计量单位树 / 分类树 / 知识树 / 空间树 / EAV 树
    public DbSet<UnitTree> UnitTrees => Set<UnitTree>();
    public DbSet<CategoryTree> CategoryTrees => Set<CategoryTree>();
    public DbSet<KnowledgeTree> KnowledgeTrees => Set<KnowledgeTree>();
    public DbSet<SpaceTree> SpaceTrees => Set<SpaceTree>();
    public DbSet<EavTree> EavTrees => Set<EavTree>();

    // NodeEavSky：property_tree 映射表（(EAV 实体, 属性) → 专属 eav_trees rootNode）
    public DbSet<EavTreeRoot> EavTreeRoots => Set<EavTreeRoot>();

    // 空间（独立简单实体）
    public DbSet<Space> Spaces => Set<Space>();

    // SpaceNodeSky：空间节点（归属空间）与组合属性派生的 Tag 投影账本
    public DbSet<SpaceNode> SpaceNodes => Set<SpaceNode>();
    public DbSet<TagProjection> TagProjections => Set<TagProjection>();

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
        // 自动发现并注册本程序集中所有 IEntityTypeConfiguration<T> 实现
        // （Data/Configurations/{NodeEavSky,TreeEavSky,NodeSky,SpaceSky,SpaceNodeSky,LineSky,EdgeSky}/）
        mb.ApplyConfigurationsFromAssembly(typeof(TreeGraphDbContext).Assembly);
    }
}
