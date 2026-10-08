using System.ComponentModel.DataAnnotations;
using TreeGraph.FileStorageApi.Uploads;
using Xunit;

namespace TreeGraph.FileStorageApi.Tests;

/// <summary>
/// UploadCleanupOptions 纯逻辑单测：
/// Effective* 计算属性的下限/上限钳制 + IValidatableObject 校验规则
/// （含关键约束 StalePendingMinutes &gt; StaleMergingMinutes）。
/// </summary>
public sealed class UploadCleanupOptionsTests
{
    // ── 维度 1：Effective* 钳制 ──

    [Fact]
    public void EffectiveInterval_ClampsToConfiguredBounds()
    {
        var below = new UploadCleanupOptions { IntervalMinutes = 0 };
        var normal = new UploadCleanupOptions { IntervalMinutes = 30 };
        var above = new UploadCleanupOptions { IntervalMinutes = 10_000 };

        Assert.Equal(TimeSpan.FromMinutes(1), below.EffectiveInterval);
        Assert.Equal(TimeSpan.FromMinutes(30), normal.EffectiveInterval);
        Assert.Equal(TimeSpan.FromMinutes(24 * 60), above.EffectiveInterval);
    }

    [Fact]
    public void EffectiveStartupDelay_ClampsToConfiguredBounds()
    {
        var below = new UploadCleanupOptions { StartupDelayMinutes = -5 };
        var above = new UploadCleanupOptions { StartupDelayMinutes = 120 };

        Assert.Equal(TimeSpan.Zero, below.EffectiveStartupDelay);
        Assert.Equal(TimeSpan.FromMinutes(60), above.EffectiveStartupDelay);
    }

    [Fact]
    public void EffectiveStaleMerging_ClampsToMin()
    {
        var options = new UploadCleanupOptions { StaleMergingMinutes = 0 };

        Assert.Equal(TimeSpan.FromMinutes(1), options.EffectiveStaleMerging);
    }

    [Fact]
    public void EffectiveStalePending_ClampsToMin()
    {
        var options = new UploadCleanupOptions { StalePendingMinutes = 0, StaleMergingMinutes = 0 };

        // 校验会拒绝（StalePending <= StaleMerging），但 Effective 钳制独立生效
        Assert.Equal(TimeSpan.FromMinutes(1), options.EffectiveStalePending);
    }

    [Fact]
    public void EffectiveCompletedRetention_ZeroDisables_NormalClampsToMax()
    {
        var disabled = new UploadCleanupOptions { CompletedRetentionMinutes = 0 };
        var normal = new UploadCleanupOptions { CompletedRetentionMinutes = 120 };
        var above = new UploadCleanupOptions { CompletedRetentionMinutes = 10_000_000 };

        Assert.Equal(TimeSpan.Zero, disabled.EffectiveCompletedRetention);
        Assert.Equal(TimeSpan.FromMinutes(120), normal.EffectiveCompletedRetention);
        Assert.Equal(TimeSpan.FromMinutes(90 * 24 * 60), above.EffectiveCompletedRetention);
    }

    [Fact]
    public void EffectiveBatchSize_And_Concurrency_Clamp()
    {
        var options = new UploadCleanupOptions
        {
            BatchSize = 0,
            OrphanDeleteConcurrency = 0,
        };
        Assert.Equal(1, options.EffectiveBatchSize);
        Assert.Equal(1, options.EffectiveOrphanDeleteConcurrency);

        var above = new UploadCleanupOptions
        {
            BatchSize = 99_999,
            OrphanDeleteConcurrency = 99,
        };
        Assert.Equal(10_000, above.EffectiveBatchSize);
        Assert.Equal(64, above.EffectiveOrphanDeleteConcurrency);
    }

    // ── 维度 2：校验规则 ──

    [Fact]
    public void Validate_Defaults_AreValid()
    {
        var results = new UploadCleanupOptions().Validate(new("opts"));

        Assert.Empty(results);
    }

    [Fact]
    public void Validate_StalePendingNotGreaterThanStaleMerging_IsRejected()
    {
        // 关键约束：pending_upload 阈值必须 > merging 阈值，否则合并中被清元数据
        var options = new UploadCleanupOptions
        {
            StaleMergingMinutes = 60,
            StalePendingMinutes = 60,   // 相等也不行
        };

        var results = options.Validate(new("opts")).ToList();

        var message = Assert.Single(results).ErrorMessage;
        Assert.NotNull(message);
        Assert.Contains("StalePendingMinutes", message);
        Assert.Contains("StaleMergingMinutes", message);
    }

    [Theory]
    [InlineData(nameof(UploadCleanupOptions.IntervalMinutes), 0)]
    [InlineData(nameof(UploadCleanupOptions.StartupDelayMinutes), 61)]
    [InlineData(nameof(UploadCleanupOptions.CompletedRetentionMinutes), -1)]
    [InlineData(nameof(UploadCleanupOptions.BatchSize), 0)]
    [InlineData(nameof(UploadCleanupOptions.OrphanDeleteConcurrency), 65)]
    public void Validate_OutOfRangeField_IsRejected(string field, int value)
    {
        var options = new UploadCleanupOptions();

        typeof(UploadCleanupOptions).GetProperty(field)!.SetValue(options, value);

        var results = options.Validate(new("opts")).ToList();

        Assert.Contains(results, r => r.MemberNames.Contains(field));
    }
}
