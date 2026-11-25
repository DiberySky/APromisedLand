using TreeGraph.Shared.Identity;
using Xunit;

namespace TreeGraph.Api.Tests.Tests;

/// <summary>
/// <see cref="SpaceAccessEvaluator"/> 纯逻辑单测：不依赖 HTTP/数据库。
/// </summary>
public class SpaceAccessEvaluatorTests
{
    [Fact]
    public void Admin_Bypasses_Space_Check()
    {
        Assert.True(SpaceAccessEvaluator.CanAccess(new[] { "space-1001" }, "9999", isAdmin: true));
        Assert.True(SpaceAccessEvaluator.CanAccess(Array.Empty<string>(), "9999", isAdmin: true));
    }

    [Fact]
    public void Matching_SpaceId_With_Prefix_Is_Allowed()
    {
        Assert.True(SpaceAccessEvaluator.CanAccess(new[] { "space-1001", "space-1002" }, "1002", isAdmin: false));
    }

    [Fact]
    public void Non_Matching_SpaceId_Is_Denied()
    {
        Assert.False(SpaceAccessEvaluator.CanAccess(new[] { "space-1001" }, "2002", isAdmin: false));
    }

    [Fact]
    public void Empty_User_Spaces_Is_Denied_When_SpaceId_Present()
    {
        Assert.False(SpaceAccessEvaluator.CanAccess(Array.Empty<string>(), "1001", isAdmin: false));
    }

    [Fact]
    public void Null_Or_Empty_SpaceId_Is_Allowed_No_Constraint()
    {
        Assert.True(SpaceAccessEvaluator.CanAccess(new[] { "space-1001" }, null, isAdmin: false));
        Assert.True(SpaceAccessEvaluator.CanAccess(new[] { "space-1001" }, "  ", isAdmin: false));
    }

    [Fact]
    public void Bare_Id_Without_Prefix_In_User_List_Does_Not_Match()
    {
        // ICurrentUser.SpaceIds 口径带 space- 前缀；裸 ID 不应命中，避免越权。
        Assert.False(SpaceAccessEvaluator.CanAccess(new[] { "1001" }, "1001", isAdmin: false));
    }

    [Fact]
    public void Comparison_Is_Ordinal_And_Trims_Target()
    {
        Assert.True(SpaceAccessEvaluator.CanAccess(new[] { "space-1001" }, " 1001 ", isAdmin: false));
        Assert.False(SpaceAccessEvaluator.CanAccess(new[] { "space-1001" }, "SPACE-1001", isAdmin: false));
    }
}
