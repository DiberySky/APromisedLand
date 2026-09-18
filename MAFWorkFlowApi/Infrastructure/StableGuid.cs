using System.Security.Cryptography;
using System.Text;

namespace MAFWorkFlowApi.Infrastructure;

/// <summary>
/// 基于确定性哈希生成稳定 GUID，确保相同输入产生相同 GUID。
/// 用于幂等录入：同一份数据重复提交不会产生重复顶点/边。
/// </summary>
public static class StableGuid
{
    public static readonly Guid TenantGuid =
        Guid.Parse("00000000-0000-0000-0000-000000000000");

    /// <summary>顶点 GUID = SHA256(graphGuid + "node" + pathName)。</summary>
    public static Guid ForNode(Guid graphGuid, string pathName)
        => HashGuid($"{graphGuid:N}|node|{pathName}");

    /// <summary>边 GUID = SHA256(graphGuid + "edge" + from + to + type)。</summary>
    public static Guid ForEdge(
        Guid graphGuid, string from, string to, string edgeType)
        => HashGuid($"{graphGuid:N}|edge|{from}|{to}|{edgeType}");

    private static Guid HashGuid(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        Span<byte> slice = stackalloc byte[16];
        bytes.AsSpan(0, 16).CopyTo(slice);
        return new Guid(slice);
    }
}