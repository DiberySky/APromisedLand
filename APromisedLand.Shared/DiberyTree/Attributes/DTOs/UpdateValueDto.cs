using System.Text.Json;

namespace APromisedLand.Shared.DiberyTree.Attributes.DTOs;

/// <summary>
/// 用于更新属性值的 DTO，Value 以 JsonElement 形式传递
/// </summary>
public class UpdateValueDto
{
    /// <summary>
    /// 新值，支持各种数据类型（文本、数字、日期等）
    /// </summary>
    public JsonElement Value { get; set; }
}