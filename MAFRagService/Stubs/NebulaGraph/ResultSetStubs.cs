// 桩代码：NebulaGraph ResultSet 类型

namespace MAFRagService.Stubs.NebulaGraph;

public class ResultSet
{
    public bool IsSucceeded { get; set; } = true;
    public string ErrorMessage { get; set; } = string.Empty;
    public List<ResultSetRecord> Records { get; set; } = new();
}

public class ResultSetRecord
{
    public Dictionary<string, ResultSetValue> Values { get; set; } = new();
}

public class ResultSetValue
{
    public string? RawValue { get; set; }

    public string AsString()
    {
        return RawValue ?? string.Empty;
    }
}
