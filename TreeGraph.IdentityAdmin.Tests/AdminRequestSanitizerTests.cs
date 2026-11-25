using TreeGraph.IdentityAdmin.Auditing;
using Xunit;

namespace TreeGraph.IdentityAdmin.Tests;

public class AdminRequestSanitizerTests
{
    [Fact]
    public void Serialize_MasksSensitiveKeys()
    {
        var request = new
        {
            username = "alice",
            clientSecret = "secret-value-1",
            password = "password-value-2",
            credentialData = "credential-value-3",
            accessToken = "token-value-4"
        };

        var json = AdminRequestSanitizer.Serialize(request);

        Assert.NotNull(json);
        Assert.DoesNotContain("secret-value-1", json);
        Assert.DoesNotContain("password-value-2", json);
        Assert.DoesNotContain("credential-value-3", json);
        Assert.DoesNotContain("token-value-4", json);
        Assert.Contains("alice", json);
        Assert.Contains("***", json);
    }

    [Fact]
    public void Serialize_MatchesCaseInsensitiveFragments()
    {
        var request = new { MyPasswordField = "x", CLIENT_SECRET = "y" };
        var json = AdminRequestSanitizer.Serialize(request)!;

        Assert.DoesNotContain("\"x\"", json);
        Assert.DoesNotContain("\"y\"", json);
    }

    [Fact]
    public void Serialize_RecursesNestedObjectsAndArrays()
    {
        var request = new
        {
            user = new { password = "secret" },
            items = new object[]
            {
                new { token = "t1" },
                new { name = "keep" }
            }
        };

        var json = AdminRequestSanitizer.Serialize(request)!;

        Assert.DoesNotContain("secret", json);
        Assert.DoesNotContain("t1", json);
        Assert.Contains("keep", json);
    }

    [Fact]
    public void Serialize_NullRequest_ReturnsNull()
        => Assert.Null(AdminRequestSanitizer.Serialize(null));
}
