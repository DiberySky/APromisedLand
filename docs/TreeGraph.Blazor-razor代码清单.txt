# TreeGraph.Blazor Razor 组件代码清单

- 生成时间：2026-10-02 21:04:14
- 文件总数：46
- 项目状态：ID→GUID String 重构完成；EntityEdit single_choice 对象形态修复；EntityList 列表预览对象修复

## 文件 1/46 TreeGraph.Blazor/Components/_Imports.razor

```razor
@* _Imports.razor *@
@using System.Net.Http
@using System.Net.Http.Json
@using Microsoft.AspNetCore.Components.Forms
@using Microsoft.AspNetCore.Components.Routing
@using Microsoft.AspNetCore.Components.Web
@using Microsoft.AspNetCore.Components.Web.Virtualization
@using Microsoft.JSInterop
@using TreeGraph.Blazor
@using TreeGraph.Blazor.Components
@using TreeGraph.Blazor.Components.FieldRenderers
@using TreeGraph.Blazor.Components.Layout
@using TreeGraph.Blazor.Components.Shared
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav
@using TreeGraph.Shared.Eav.Dtos
@using MudBlazor

@using static Microsoft.AspNetCore.Components.Web.RenderMode
```

## 文件 2/46 TreeGraph.Blazor/Components/App.razor

```razor
<!DOCTYPE html>
<html lang="en">
@* App.razor *@
<head>
    <meta charset="utf-8"/>
    <meta name="viewport" content="width=device-width, initial-scale=1.0"/>
    <base href="/"/>
    <ResourcePreloader/>
    <link rel="stylesheet" href="@Assets["lib/bootstrap/dist/css/bootstrap.min.css"]"/>
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet"/>
    <link rel="stylesheet" href="@Assets["app.css"]"/>
    <link rel="stylesheet" href="@Assets["TreeGraph.Blazor.styles.css"]"/>
    <ImportMap/>
    <link rel="icon" type="image/png" href="favicon.png"/>

    @* ★ HeadOutlet 也需 InteractiveServer，否则 HeadContent 内的动态改动不生效 *@
    <HeadOutlet @rendermode="InteractiveServer" />
</head>

<body>
@* ★ 关键修复：Routes 全树 InteractiveServer。
       这样 MainLayout 里的 MudPopoverProvider / MudDialogProvider / MudSnackbarProvider
       与页面内的 MudSelect / MudDialog / ISnackbar 处于同一渲染作用域。 *@
<Routes @rendermode="InteractiveServer" />
<ReconnectModal/>
<script src="@Assets["_framework/blazor.web.js"]"></script>
<script src="_content/MudBlazor/MudBlazor.min.js"></script>
</body>

</html>
```

## 文件 3/46 TreeGraph.Blazor/Components/DynamicForm.razor

```razor
@using System.Text.Json

@* 按 EAV Schema 动态渲染表单（文档第五章，核心组件）。
   支持 string/int/decimal/bool/date/datetime/time/single_choice；
   json/file/composite/table 等复杂类型暂不编辑，提交时跳过以避免误清空。
   值存于内部字典 _values，保存时由 BuildSubmitValues 转换为 PUT 提交格式。 *@

<MudForm @ref="_form">
    @foreach (var attr in SortedAttributes)
    {
        <div class="mb-4">
            @switch (attr.DataType)
            {
                case EavDataTypes.String:
                    <StringField Attr="attr"
                                 Value="GetString(attr.AttributeName)"
                                 ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Int:
                case EavDataTypes.Decimal:
                    if (attr.AvailableUnits is { Count: > 0 })
                    {
                        <UnitNumberField Attr="attr"
                                         AvailableUnits="attr.AvailableUnits"
                                         Value="GetNumeric(attr.AttributeName)"
                                         ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    }
                    else
                    {
                        <NumberField Attr="attr"
                                     Value="GetDecimal(attr.AttributeName)"
                                     ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    }
                    break;

                case EavDataTypes.Bool:
                    <BoolField Attr="attr"
                               Value="GetBool(attr.AttributeName)"
                               ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Date:
                    <DateField Attr="attr"
                               Value="GetDateTime(attr.AttributeName)"
                               ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Datetime:
                    <DateTimeField Attr="attr"
                                   Value="GetDateTime(attr.AttributeName)"
                                   ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Time:
                    <TimeField Attr="attr"
                               Value="GetTime(attr.AttributeName)"
                               ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.SingleChoice:
                    <SingleChoiceField Attr="attr"
                                       Value="GetString(attr.AttributeName)"
                                       ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Composite:
                    <CompositeField Attr="attr"
                                    Value="GetJson(attr.AttributeName)"
                                    ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Table:
                    @* 表数据由 TableField 通过独立端点自行加载/保存，不参与本表单提交 *@
                    <TableField Attr="attr"
                                EntityType="EntityType"
                                EntityId="EntityId"
                                Value="GetJson(attr.AttributeName)"
                                ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.File:
                    <FileField Attr="attr"
                               Value="GetJson(attr.AttributeName)"
                               ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                case EavDataTypes.Json:
                    @* json：StringField 兜底，原文本编辑 *@
                    <StringField Attr="attr"
                                 Value="GetJsonText(attr.AttributeName)"
                                 ValueChanged="v => SetValue(attr.AttributeName, v)" />
                    break;

                default:
                    <MudAlert Severity="Severity.Info">
                        @attr.DisplayName：类型 @attr.DataType 暂不支持在表单中编辑（保存时不会提交此字段）
                    </MudAlert>
                    break;
            }
        </div>
    }

    <MudButton Variant="Variant.Filled" Color="Color.Primary"
               OnClick="SubmitAsync" Disabled="_submitting"
               StartIcon="@Icons.Material.Filled.Save">
        @(_submitting ? "保存中..." : "保存")
    </MudButton>
</MudForm>

@code {
    [Parameter, EditorRequired] public string EntityType { get; set; } = "";
    [Parameter] public string EntityId { get; set; } = "";

    /// <summary>动态表单 Schema。</summary>
    [Parameter, EditorRequired] public IReadOnlyList<AttributeSchemaDto> Schema { get; set; } = [];

    /// <summary>保存回调，参数为已转换为提交格式的值字典。</summary>
    [Parameter] public EventCallback<Dictionary<string, object?>> OnSubmit { get; set; }

    private MudForm _form = null!;
    private bool _submitting;
    private readonly Dictionary<string, object?> _values = new();

    private IEnumerable<AttributeSchemaDto> SortedAttributes =>
        Schema.OrderBy(a => a.DisplayOrder);

    /// <summary>从后端返回的属性值加载到表单（按 DataType 逐属性转换）。</summary>
    public void LoadFrom(IReadOnlyDictionary<string, JsonElement> props)
    {
        _values.Clear();
        foreach (var attr in Schema)
        {
            if (props.TryGetValue(attr.AttributeName, out var elem))
                _values[attr.AttributeName] = ConvertElement(elem, attr);
        }
        StateHasChanged();
    }

    private void SetValue(string key, object? value) => _values[key] = value;

    private string? GetString(string key)
        => _values.TryGetValue(key, out var v) ? v as string : null;
    private decimal? GetDecimal(string key)
        => _values.TryGetValue(key, out var v) && v is decimal d ? d : null;
    private bool? GetBool(string key)
        => _values.TryGetValue(key, out var v) && v is bool b ? b : null;
    private DateTime? GetDateTime(string key)
        => _values.TryGetValue(key, out var v) && v is DateTime dt ? dt : null;
    private TimeSpan? GetTime(string key)
        => _values.TryGetValue(key, out var v) && v is TimeSpan t ? t : null;
    private NumericValueDto? GetNumeric(string key)
        => _values.TryGetValue(key, out var v) ? v as NumericValueDto : null;

    // composite/file 的原始 JSON 值（编辑场景由 ConvertElement 写入）
    private JsonElement? GetJson(string key)
        => _values.TryGetValue(key, out var v) && v is JsonElement je ? je : null;

    // json 类型：以原文本编辑（StringField 兜底）
    private string? GetJsonText(string key)
        => _values.TryGetValue(key, out var v) ? v?.ToString() : null;

    private async Task SubmitAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        _submitting = true;
        try
        {
            await OnSubmit.InvokeAsync(BuildSubmitValues());
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 初始值解析（按属性类型感知转换） ----------

    private static object? ConvertElement(JsonElement elem, AttributeSchemaDto attr)
    {
        if (elem.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            return null;

        return attr.DataType switch
        {
            EavDataTypes.String => elem.ValueKind == JsonValueKind.String
                ? elem.GetString() : elem.GetRawText(),
            EavDataTypes.Int or EavDataTypes.Decimal => ConvertNumeric(elem, attr),
            EavDataTypes.Bool => elem.ValueKind == JsonValueKind.True,
            EavDataTypes.Datetime => elem.TryGetDateTime(out var dt)
                ? dt : (DateTime?)null,
            EavDataTypes.Date => elem.ValueKind == JsonValueKind.String
                && DateOnly.TryParse(elem.GetString(), out var d)
                ? d.ToDateTime(TimeOnly.MinValue) : (DateTime?)null,
            EavDataTypes.Time => elem.ValueKind == JsonValueKind.String
                && TimeOnly.TryParse(elem.GetString(), out var t)
                ? t.ToTimeSpan() : (TimeSpan?)null,
            EavDataTypes.SingleChoice => elem.ValueKind == JsonValueKind.Object
                && elem.TryGetProperty("value", out var v)
                ? v.GetString()
                : elem.ValueKind == JsonValueKind.String ? elem.GetString() : null,
            // composite/file：保留原始 JSON（克隆避免节点归属冲突），由对应组件自行解析
            EavDataTypes.Composite or EavDataTypes.File => elem.Clone(),
            // json：以原文本编辑，提交时再尝试解析回 JSON
            EavDataTypes.Json => elem.GetRawText(),
            _ => null
        };
    }

    /// <summary>数值：带单位属性为 { value, unitId } 或裸数字；否则为裸数字。</summary>
    private static object? ConvertNumeric(JsonElement elem, AttributeSchemaDto attr)
    {
        if (attr.AvailableUnits is { Count: > 0 })
        {
            if (elem.ValueKind == JsonValueKind.Object
                && elem.TryGetProperty("value", out var v))
            {
                return new NumericValueDto
                {
                    Value = v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null,
                    UnitId = elem.TryGetProperty("unitId", out var u)
                        && u.ValueKind == JsonValueKind.String
                        && Guid.TryParse(u.GetString(), out var id)
                        ? id : null
                };
            }
            if (elem.ValueKind == JsonValueKind.Number)
                return new NumericValueDto { Value = elem.GetDecimal() };
            return null;
        }
        return elem.ValueKind == JsonValueKind.Number ? elem.GetDecimal() : null;
    }

    // ---------- 提交值构建 ----------

    /// <summary>把表单值转换为 PUT 接口接受的 JSON 值字典（不支持的类型不提交）。</summary>
    private Dictionary<string, object?> BuildSubmitValues()
    {
        var result = new Dictionary<string, object?>();
        foreach (var attr in Schema)
        {
            if (attr.DataType is EavDataTypes.Table)
                continue;   // 表数据由 TableField 独立保存，不随表单提交

            if (!_values.TryGetValue(attr.AttributeName, out var v) || v is null)
            {
                result[attr.AttributeName] = null;
                continue;
            }

            result[attr.AttributeName] = attr.DataType switch
            {
                // decimal 序列化会带小数点（5.0），int 属性先转为 long
                EavDataTypes.Int => v is decimal d ? (long)d : v,
                EavDataTypes.Decimal when attr.AvailableUnits is { Count: > 0 } =>
                    BuildUnitValue(v),
                EavDataTypes.Datetime => ((DateTime)v).ToString("o"),
                EavDataTypes.Date => DateOnly.FromDateTime((DateTime)v).ToString("yyyy-MM-dd"),
                EavDataTypes.Time => TimeOnly.FromTimeSpan((TimeSpan)v).ToString("HH:mm:ss"),
                // json：文本框输入的原文尝试解析回 JSON，非法 JSON 按字符串提交
                EavDataTypes.Json => TryParseJson(v),
                _ => v
            };
        }
        return result;
    }

    private static object? TryParseJson(object v)
    {
        if (v is not string s || string.IsNullOrWhiteSpace(s)) return null;
        try { return JsonDocument.Parse(s).RootElement.Clone(); }
        catch (JsonException) { return s; }
    }

    private static object? BuildUnitValue(object v)
        => v is NumericValueDto { Value: { } num } nv
            ? new { value = num, unitId = nv.UnitId }
            : null;
}
```

## 文件 4/46 TreeGraph.Blazor/Components/FieldRenderers/_Imports.razor

```razor
@* 字段渲染器目录级 Imports：确保 Razor 语言服务器能正确解析
   MudBlazor 组件与 AttributeSchemaDto，避免"无法解析符号"误报 *@
@using System.Text.Json
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Rendering
@using MudBlazor
@using TreeGraph.Shared.Eav.Dtos
@using TreeGraph.Blazor.Services
```

## 文件 5/46 TreeGraph.Blazor/Components/FieldRenderers/ArrayFieldRenderer.razor

```razor
@* 数组字段渲染器：组合字段 IsArray=true 时的多行编辑 *@

<MudText Typo="Typo.caption" Color="Color.Secondary" Class="mb-1">
    @Field.DisplayName（共 @Values.Count 项）
</MudText>

@for (int i = 0; i < Values.Count; i++)
{
    var idx = i;
    var item = Values[i];

    <MudPaper Class="pa-2 mb-2" Elevation="0" Outlined="true">
        <div class="d-flex align-center mb-1">
            <MudText Typo="Typo.caption">#@(idx + 1)</MudText>
            <MudSpacer />
            <MudIconButton Icon="@Icons.Material.Filled.Delete"
                           Size="Size.Small" Color="Color.Error"
                           OnClick="@(() => RemoveAt(idx))" />
        </div>

        @if (Field.DataType == EavDataTypes.Composite && Field.NestedType is not null)
        {
            <CompositeField Attr="MakeChildAttr(Field)"
                            Value="item"
                            ValueChanged="v => UpdateAt(idx, v)" />
        }
        else
        {
            <MudTextField T="string"
                          Value="@(item?.ToString())"
                          ValueChanged="@(v => UpdateAt(idx, v is null ? null
                              : JsonSerializer.SerializeToElement(v)))"
                          Variant="Variant.Outlined"
                          Margin="Margin.Dense" />
        }
    </MudPaper>
}

<MudButton Size="Size.Small" Variant="Variant.Outlined"
           Color="Color.Primary"
           StartIcon="@Icons.Material.Filled.Add"
           OnClick="AddNew">
    添加一项
</MudButton>

@code {
    [Parameter, EditorRequired] public CompositeFieldSchemaDto Field { get; set; } = null!;
    [Parameter] public List<JsonElement?> Values { get; set; } = new();
    [Parameter] public EventCallback<List<JsonElement?>> ValuesChanged { get; set; }

    private Task AddNew()
    {
        Values.Add(null);
        return ValuesChanged.InvokeAsync(Values);
    }

    private Task RemoveAt(int index)
    {
        Values.RemoveAt(index);
        return ValuesChanged.InvokeAsync(Values);
    }

    private Task UpdateAt(int index, JsonElement? value)
    {
        if (index >= 0 && index < Values.Count)
        {
            Values[index] = value;
            return ValuesChanged.InvokeAsync(Values);
        }
        return Task.CompletedTask;
    }

    private static AttributeSchemaDto MakeChildAttr(CompositeFieldSchemaDto field)
        => new(
            AttributeName: field.FieldName,
            DisplayName: field.DisplayName,
            DataType: field.DataType,
            IsRequired: field.IsRequired,
            IsSearchable: field.IsSearchable,
            IsSortable: false,
            DisplayOrder: field.DisplayOrder,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: field.NestedType,
            Unit: null,
            AvailableUnits: null,
            OptionSet: null);
}
```

## 文件 6/46 TreeGraph.Blazor/Components/FieldRenderers/BoolField.razor

```razor
@* 布尔字段渲染器（按文档 6.x 同模式实现） *@

<MudSwitch T="bool?" Label="@Attr.DisplayName"
           Value="Value" ValueChanged="ValueChanged"
           Required="@Attr.IsRequired"
           Color="Color.Primary" />

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public bool? Value { get; set; }
    [Parameter] public EventCallback<bool?> ValueChanged { get; set; }
}
```

## 文件 7/46 TreeGraph.Blazor/Components/FieldRenderers/CompositeField.razor

```razor
@* 组合类型字段渲染器：按 Schema 递归渲染字段，嵌套 composite 递归自身 *@

<MudPaper Class="pa-3" Elevation="1" Outlined="true">
    <div class="d-flex align-center mb-2">
        <MudIcon Icon="@Icons.Material.Filled.AccountTree"
                 Size="Size.Small" Color="Color.Primary" Class="mr-1" />
        <MudText Typo="Typo.subtitle2">@Attr.DisplayName</MudText>
        @if (Attr.IsRequired)
        {
            <MudText Typo="Typo.caption" Color="Color.Error" Class="ml-1">*</MudText>
        }
    </div>

    @if (Attr.CompositeType is null)
    {
        <MudAlert Severity="Severity.Warning">
            组合类型定义缺失
        </MudAlert>
    }
    else
    {
        @foreach (var field in SortedFields)
        {
            <div class="mb-3">
                @if (field.IsArray)
                {
                    @* 数组字段：渲染为多行列表 *@
                    <ArrayFieldRenderer Field="field"
                                        Values="GetArrayValues(field.FieldName)"
                                        ValuesChanged="v => SetArrayValues(field.FieldName, v)" />
                }
                else
                {
                    @* 单值字段：按类型声明式分发 *@
                    @switch (field.DataType)
                    {
                        case EavDataTypes.String:
                            <MudTextField T="string"
                                          Label="@field.DisplayName"
                                          Value="GetString(field.FieldName)"
                                          ValueChanged="v => SetString(field.FieldName, v)"
                                          Required="field.IsRequired"
                                          Variant="Variant.Outlined"
                                          Immediate="true" />
                            break;

                        case EavDataTypes.Int:
                        case EavDataTypes.Decimal:
                            <MudNumericField T="decimal?"
                                             Label="@field.DisplayName"
                                             Value="GetDecimal(field.FieldName)"
                                             ValueChanged="v => SetDecimal(field.FieldName, v)"
                                             Required="field.IsRequired"
                                             Variant="Variant.Outlined" />
                            break;

                        case EavDataTypes.Bool:
                            <MudSwitch T="bool?"
                                       Label="@field.DisplayName"
                                       Value="GetBool(field.FieldName)"
                                       ValueChanged="v => SetBool(field.FieldName, v)"
                                       Color="Color.Primary" />
                            break;

                        case EavDataTypes.Date:
                            <MudDatePicker Label="@field.DisplayName"
                                           Date="GetDateTime(field.FieldName)"
                                           DateChanged="v => SetDate(field.FieldName, v)"
                                           Required="field.IsRequired"
                                           Variant="Variant.Outlined"
                                           DateFormat="yyyy-MM-dd" />
                            break;

                        case EavDataTypes.Datetime:
                            <MudTextField T="string"
                                          Label="@field.DisplayName"
                                          Value="@(GetDateTime(field.FieldName)?.ToString("yyyy-MM-dd HH:mm:ss"))"
                                          ValueChanged="v => SetDateTimeText(field.FieldName, v)"
                                          Variant="Variant.Outlined"
                                          Placeholder="yyyy-MM-dd HH:mm:ss" />
                            break;

                        case EavDataTypes.Time:
                            <MudTimePicker Label="@field.DisplayName"
                                           Time="GetTime(field.FieldName)"
                                           TimeChanged="v => SetTime(field.FieldName, v)"
                                           Variant="Variant.Outlined" />
                            break;

                        case EavDataTypes.Composite:
                            @if (field.NestedType is null)
                            {
                                <MudAlert Severity="Severity.Warning">
                                    嵌套类型未定义: @field.FieldName
                                </MudAlert>
                            }
                            else
                            {
                                @* 嵌套组合：递归渲染自身 *@
                                <CompositeField Attr="MakeChildAttr(field)"
                                                Value="GetObject(field.FieldName)"
                                                ValueChanged="v => SetObject(field.FieldName, v)" />
                            }
                            break;

                        default:
                            <MudTextField T="string"
                                          Label="@field.DisplayName"
                                          Value="GetString(field.FieldName)"
                                          ValueChanged="v => SetString(field.FieldName, v)"
                                          Variant="Variant.Outlined"
                                          Immediate="true" />
                            break;
                    }
                }
            </div>
        }
    }
</MudPaper>

@code {
    [Parameter, EditorRequired]
    public AttributeSchemaDto Attr { get; set; } = null!;

    [Parameter]
    public JsonElement? Value { get; set; }

    [Parameter]
    public EventCallback<JsonElement?> ValueChanged { get; set; }

    /// <summary>内部字段值存储：字段名 → JSON 值</summary>
    private Dictionary<string, JsonElement?> _fields = new();

    private IEnumerable<CompositeFieldSchemaDto> SortedFields =>
        (Attr.CompositeType?.Fields ?? new List<CompositeFieldSchemaDto>())
            .OrderBy(f => f.DisplayOrder);

    protected override void OnParametersSet()
    {
        if (Value is { ValueKind: JsonValueKind.Object } obj)
        {
            _fields = new Dictionary<string, JsonElement?>();
            foreach (var prop in obj.EnumerateObject())
            {
                _fields[prop.Name] = prop.Value.Clone();
            }
        }
        else
        {
            _fields = new Dictionary<string, JsonElement?>();
        }
    }

    /// <summary>为嵌套 composite 字段构造临时 AttributeSchemaDto 供递归使用</summary>
    private static AttributeSchemaDto MakeChildAttr(CompositeFieldSchemaDto field)
        => new(
            AttributeName: field.FieldName,
            DisplayName: field.DisplayName,
            DataType: field.DataType,
            IsRequired: field.IsRequired,
            IsSearchable: field.IsSearchable,
            IsSortable: false,
            DisplayOrder: field.DisplayOrder,
            AllowedValues: null,
            ValidationRule: null,
            CompositeType: field.NestedType,
            Unit: null,
            AvailableUnits: null,
            OptionSet: null);

    // ---------- 值访问 ----------

    private string? GetString(string name)
        => _fields.TryGetValue(name, out var e) && e is { ValueKind: JsonValueKind.String } v
            ? v.GetString() : null;

    private decimal? GetDecimal(string name)
        => _fields.TryGetValue(name, out var e) && e is { ValueKind: JsonValueKind.Number } v
            ? v.GetDecimal() : null;

    private bool? GetBool(string name)
        => _fields.TryGetValue(name, out var e) && e is { } v
            ? v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            }
            : null;

    private DateTime? GetDateTime(string name)
    {
        var s = GetString(name);
        return DateTime.TryParse(s, out var dt) ? dt : null;
    }

    private TimeSpan? GetTime(string name)
    {
        var s = GetString(name);
        return TimeSpan.TryParse(s, out var ts) ? ts : null;
    }

    private JsonElement? GetObject(string name)
        => _fields.TryGetValue(name, out var e) ? e : null;

    private List<JsonElement?> GetArrayValues(string name)
    {
        if (!_fields.TryGetValue(name, out var e)
            || e is not { ValueKind: JsonValueKind.Array } arr)
            return new List<JsonElement?>();

        return arr.EnumerateArray()
            .Select(x => (JsonElement?)x.Clone())
            .ToList();
    }

    // ---------- 值设置 ----------

    private Task SetString(string name, string? v)
    {
        _fields[name] = v is null
            ? null
            : JsonSerializer.SerializeToElement(v);
        return NotifyChangedAsync();
    }

    private Task SetDecimal(string name, decimal? v)
    {
        _fields[name] = v.HasValue
            ? JsonSerializer.SerializeToElement(v.Value)
            : null;
        return NotifyChangedAsync();
    }

    private Task SetBool(string name, bool? v)
    {
        _fields[name] = v.HasValue
            ? JsonSerializer.SerializeToElement(v.Value)
            : null;
        return NotifyChangedAsync();
    }

    private Task SetDate(string name, DateTime? v)
    {
        _fields[name] = v.HasValue
            ? JsonSerializer.SerializeToElement(v.Value.ToString("yyyy-MM-dd"))
            : null;
        return NotifyChangedAsync();
    }

    private Task SetDateTimeText(string name, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _fields[name] = null;
        }
        else if (DateTime.TryParse(text, out var dt))
        {
            _fields[name] = JsonSerializer.SerializeToElement(
                dt.ToString("yyyy-MM-ddTHH:mm:ss"));
        }
        return NotifyChangedAsync();
    }

    private Task SetTime(string name, TimeSpan? v)
    {
        _fields[name] = v.HasValue
            ? JsonSerializer.SerializeToElement(v.Value.ToString(@"hh\:mm\:ss"))
            : null;
        return NotifyChangedAsync();
    }

    private Task SetObject(string name, JsonElement? v)
    {
        _fields[name] = v;
        return NotifyChangedAsync();
    }

    private Task SetArrayValues(string name, List<JsonElement?> values)
    {
        _fields[name] = JsonSerializer.SerializeToElement(values);
        return NotifyChangedAsync();
    }

    private Task NotifyChangedAsync()
    {
        var dict = new Dictionary<string, JsonElement?>();
        foreach (var (k, v) in _fields)
        {
            dict[k] = v;
        }
        var element = JsonSerializer.SerializeToElement(dict);
        return ValueChanged.InvokeAsync(element);
    }
}
```

## 文件 8/46 TreeGraph.Blazor/Components/FieldRenderers/DateField.razor

```razor
@* 日期字段渲染器（按文档 6.x 同模式实现） *@

<MudDatePicker Label="@Attr.DisplayName"
               Date="Value" DateChanged="ValueChanged"
               Required="@Attr.IsRequired"
               Variant="Variant.Outlined" />

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public DateTime? Value { get; set; }
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }
}
```

## 文件 9/46 TreeGraph.Blazor/Components/FieldRenderers/DateTimeField.razor

```razor
@* 日期时间字段渲染器（按文档 6.x 同模式实现）：日期 + 时间组合为 DateTime *@

<MudGrid>
    <MudItem xs="12" sm="6">
        <MudDatePicker Label="@Attr.DisplayName"
                       Date="_date" DateChanged="OnDateChanged"
                       Required="@Attr.IsRequired"
                       Variant="Variant.Outlined" />
    </MudItem>
    <MudItem xs="12" sm="6">
        <MudTimePicker Label="时间"
                       Time="_time" TimeChanged="OnTimeChanged"
                       Variant="Variant.Outlined" />
    </MudItem>
</MudGrid>

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public DateTime? Value { get; set; }
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    private DateTime? _date;
    private TimeSpan? _time;

    protected override void OnParametersSet()
    {
        // 外部值变化时拆分同步；本地编辑回传值与本地组合一致，不会触发覆盖
        if (Value != Combine(_date, _time))
        {
            _date = Value?.Date;
            _time = Value?.TimeOfDay;
        }
    }

    private Task OnDateChanged(DateTime? d)
    {
        _date = d;
        return NotifyAsync();
    }

    private Task OnTimeChanged(TimeSpan? t)
    {
        _time = t;
        return NotifyAsync();
    }

    private Task NotifyAsync()
        => ValueChanged.InvokeAsync(Combine(_date, _time));

    private static DateTime? Combine(DateTime? d, TimeSpan? t)
        => d is null ? null : d.Value.Date + (t ?? TimeSpan.Zero);
}
```

## 文件 10/46 TreeGraph.Blazor/Components/FieldRenderers/FileField.razor

```razor
@* 文件字段渲染器（基础版）：URL 输入 + JSON 元数据展示 *@

<MudTextField T="string"
              Label="@($"{Attr.DisplayName} - 文件 URL")"
              Value="_url"
              ValueChanged="OnUrlChanged"
              Required="@Attr.IsRequired"
              Variant="Variant.Outlined"
              Placeholder="https://..."
              Class="mb-2" />

@if (_meta is not null)
{
    <MudPaper Class="pa-2" Elevation="0" Outlined="true">
        <MudText Typo="Typo.caption">
            文件名: @_meta.FileName
        </MudText>
        <MudText Typo="Typo.caption">
            类型: @_meta.MimeType
        </MudText>
        <MudText Typo="Typo.caption">
            大小: @(_meta.SizeBytes / 1024) KB
        </MudText>
    </MudPaper>
}

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public JsonElement? Value { get; set; }
    [Parameter] public EventCallback<JsonElement?> ValueChanged { get; set; }

    private string? _url;
    private FileMeta? _meta;

    protected override void OnParametersSet()
    {
        if (Value is { ValueKind: JsonValueKind.Object } obj)
        {
            _url = obj.TryGetProperty("url", out var u) ? u.GetString() : null;
            _meta = new FileMeta
            {
                FileName = obj.TryGetProperty("fileName", out var fn)
                    ? fn.GetString() ?? "" : "",
                MimeType = obj.TryGetProperty("mimeType", out var mt)
                    ? mt.GetString() ?? "" : "",
                SizeBytes = obj.TryGetProperty("sizeBytes", out var sz)
                    && sz.TryGetInt64(out var s) ? s : 0
            };
        }
        else
        {
            _url = null;
            _meta = null;
        }
    }

    private Task OnUrlChanged(string? v)
    {
        _url = v;
        if (string.IsNullOrWhiteSpace(v))
        {
            _meta = null;
            return ValueChanged.InvokeAsync(null);
        }

        var obj = new
        {
            url = v,
            fileName = Path.GetFileName(v),
            mimeType = "",
            sizeBytes = 0
        };
        return ValueChanged.InvokeAsync(JsonSerializer.SerializeToElement(obj));
    }

    private class FileMeta
    {
        public string FileName { get; set; } = "";
        public string MimeType { get; set; } = "";
        public long SizeBytes { get; set; }
    }
}
```

## 文件 11/46 TreeGraph.Blazor/Components/FieldRenderers/NumberField.razor

```razor
@* 数值字段渲染器（文档 6.2），int / decimal（无单位）共用 *@

<MudNumericField T="decimal?" Label="@Attr.DisplayName"
                 Value="Value" ValueChanged="ValueChanged"
                 Required="@Attr.IsRequired"
                 Variant="Variant.Outlined" />

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public decimal? Value { get; set; }
    [Parameter] public EventCallback<decimal?> ValueChanged { get; set; }
}
```

## 文件 12/46 TreeGraph.Blazor/Components/FieldRenderers/SingleChoiceField.razor

```razor
@* 单选字段渲染器（文档 6.4） *@

<MudSelect T="string" Label="@Attr.DisplayName"
           Value="Value" ValueChanged="ValueChanged"
           Required="@Attr.IsRequired"
           Variant="Variant.Outlined">
    @if (!Attr.IsRequired)
    {
        <MudSelectItem T="string" Value="@((string?)null)">（未选择）</MudSelectItem>
    }
    @foreach (var item in Attr.OptionSet?.Items ?? Array.Empty<OptionItemSchemaDto>())
    {
        <MudSelectItem T="string" Value="@item.Value">
            @item.Label
        </MudSelectItem>
    }
</MudSelect>

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
}
```

## 文件 13/46 TreeGraph.Blazor/Components/FieldRenderers/StringField.razor

```razor
@* 字符串字段渲染器（文档 6.1） *@

<MudTextField T="string" Label="@Attr.DisplayName"
              Value="Value" ValueChanged="ValueChanged"
              Required="@Attr.IsRequired"
              Variant="Variant.Outlined"
              HelperText="@Attr.DisplayName" />

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string?> ValueChanged { get; set; }
}
```

## 文件 14/46 TreeGraph.Blazor/Components/FieldRenderers/TableField.razor

```razor
@* 自定义表字段渲染器：内嵌行编辑器，数据走 CustomTableData 端点独立保存 *@
@inject EavApiClient Api
@inject ISnackbar Snackbar

<MudPaper Class="pa-3" Elevation="1" Outlined="true">
    <div class="d-flex align-center mb-2">
        <MudIcon Icon="@Icons.Material.Filled.TableChart"
                 Size="Size.Small" Color="Color.Primary" Class="mr-1" />
        <MudText Typo="Typo.subtitle2">@Attr.DisplayName</MudText>
        <MudChip T="string" Size="Size.Small" Class="ml-2">
            @(_rows.Count) 行
        </MudChip>
        <MudSpacer />
        <MudButton Size="Size.Small" Variant="Variant.Outlined"
                   Color="Color.Primary"
                   StartIcon="@Icons.Material.Filled.Add"
                   OnClick="AddRow" Disabled="@(!CanEdit)">
            添加行
        </MudButton>
    </div>

    @if (!CanEdit)
    {
        <MudAlert Severity="Severity.Info">
            请先保存实体，然后再添加 @Attr.DisplayName 数据
        </MudAlert>
    }
    else if (_loading)
    {
        <MudProgressLinear Indeterminate="true" />
    }
    else if (_rows.Count == 0)
    {
        <MudAlert Severity="Severity.Info">
            暂无数据，点击"添加行"开始
        </MudAlert>
    }
    else
    {
        @for (int i = 0; i < _rows.Count; i++)
        {
            var idx = i;
            var row = _rows[i];

            <MudPaper Class="pa-2 mb-2" Elevation="0" Outlined="true">
                <div class="d-flex align-center mb-1">
                    <MudText Typo="Typo.caption">#@(idx + 1)</MudText>
                    @if (row.RowId is not null)
                    {
                        <MudChip T="string" Size="Size.Small" Class="ml-2">
                            RowId: @row.RowId
                        </MudChip>
                    }
                    <MudSpacer />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                   Size="Size.Small" Color="Color.Error"
                                   OnClick="@(() => RemoveRow(idx))" />
                </div>

                @foreach (var col in _columns)
                {
                    var key = col.ColumnName;
                    var value = row.Fields.TryGetValue(key, out var v) ? v : null;

                    <MudTextField T="string"
                                  Label="@col.DisplayName"
                                  Value="@(value?.ToString())"
                                  ValueChanged="@(nv => UpdateField(row, key, nv))"
                                  Required="@col.IsRequired"
                                  Variant="Variant.Outlined"
                                  Margin="Margin.Dense"
                                  Class="mb-2" />
                }
            </MudPaper>
        }
    }

    @if (CanEdit && _hasChanges)
    {
        <div class="d-flex mt-2">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Save"
                       OnClick="SaveAsync" Disabled="@_saving">
                @(_saving ? "保存中..." : "保存表数据")
            </MudButton>
            <MudButton Variant="Variant.Text" Color="Color.Default"
                       OnClick="ReloadAsync" Class="ml-2" Disabled="@_saving">
                放弃修改
            </MudButton>
        </div>
    }
</MudPaper>

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter, EditorRequired] public string EntityType { get; set; } = "";
    [Parameter] public string EntityId { get; set; } = "";
    [Parameter] public JsonElement? Value { get; set; }
    [Parameter] public EventCallback<JsonElement?> ValueChanged { get; set; }

    private List<TableRowForm> _rows = new();
    private List<CustomTableColumnDto> _columns = new();
    private bool _loading;
    private bool _saving;
    private bool _hasChanges;

    private bool CanEdit => !string.IsNullOrEmpty(EntityId) && Attr.RefTableDefinitionId is not null;

    protected override async Task OnParametersSetAsync()
    {
        if (!CanEdit) return;

        // 只在初次加载时从后端拉取
        if (_columns.Count == 0)
        {
            await LoadDefinitionAsync();
        }
        if (!_loading && _rows.Count == 0)
        {
            await LoadRowsAsync();
        }
    }

    // 列定义：端点 ㉝ GET api/eav/metadata/custom-tables
    private async Task LoadDefinitionAsync()
    {
        var tables = await Api.ListCustomTablesAsync();
        var table = tables?.FirstOrDefault(t =>
            t.TableDefinitionId == Attr.RefTableDefinitionId);
        if (table is not null)
        {
            _columns = table.Columns.OrderBy(c => c.DisplayOrder).ToList();
        }
    }

    // 行数据：端点 ⑦ GET api/eav/{entityType}/entities/{id}/tables/{tableName}
    private async Task LoadRowsAsync()
    {
        _loading = true;
        try
        {
            var table = await Api.LoadCustomTableAsync(
                EntityType, EntityId, Attr.AttributeName);
            if (table is not null)
            {
                _rows = table.Rows.Select(r => new TableRowForm
                {
                    RowId = r.RowId,
                    RowOrder = r.RowOrder,
                    Fields = r.Fields.ToDictionary(
                        k => k.Key,
                        v => v.Value?.ToString() ?? "")
                }).ToList();
            }
        }
        finally
        {
            _loading = false;
            _hasChanges = false;
        }
    }

    private async Task ReloadAsync()
    {
        await LoadRowsAsync();
        StateHasChanged();
    }

    private void AddRow()
    {
        _rows.Add(new TableRowForm
        {
            RowOrder = _rows.Count,
            Fields = _columns.ToDictionary(c => c.ColumnName, _ => "")
        });
        _hasChanges = true;
    }

    private void RemoveRow(int index)
    {
        _rows.RemoveAt(index);
        _hasChanges = true;
    }

    private void UpdateField(TableRowForm row, string key, string? value)
    {
        row.Fields[key] = value ?? "";
        _hasChanges = true;
    }

    // 端点 ⑧：PUT api/eav/{entityType}/entities/{id}/tables/{tableName}（整表替换）
    private async Task SaveAsync()
    {
        _saving = true;
        try
        {
            var value = new CustomTableValue
            {
                TableName = Attr.AttributeName,
                Rows = _rows.Select(r => new CustomTableRowValue
                {
                    RowId = r.RowId,
                    RowOrder = r.RowOrder,
                    Fields = r.Fields.ToDictionary(
                        k => k.Key,
                        v => (object?)v.Value)
                }).ToList()
            };

            var (ok, error) = await Api.ReplaceCustomTableAsync(
                EntityType, EntityId, Attr.AttributeName, value);

            if (ok)
            {
                Snackbar.Add("表数据保存成功", Severity.Success);
                await LoadRowsAsync();
            }
            else
            {
                Snackbar.Add($"保存失败：{error}", Severity.Error);
            }
        }
        finally
        {
            _saving = false;
        }
    }

    private class TableRowForm
    {
        public string? RowId { get; set; }
        public int RowOrder { get; set; }
        public Dictionary<string, string> Fields { get; set; } = new();
    }
}
```

## 文件 15/46 TreeGraph.Blazor/Components/FieldRenderers/TimeField.razor

```razor
@* 时间字段渲染器（按文档 6.x 同模式实现） *@

<MudTimePicker Label="@Attr.DisplayName"
               Time="Value" TimeChanged="ValueChanged"
               Required="@Attr.IsRequired"
               Variant="Variant.Outlined" />

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public TimeSpan? Value { get; set; }
    [Parameter] public EventCallback<TimeSpan?> ValueChanged { get; set; }
}
```

## 文件 16/46 TreeGraph.Blazor/Components/FieldRenderers/UnitNumberField.razor

```razor
@* 带单位数值字段渲染器（文档 6.3）。
   修正文档两处问题：数值变化需回传父组件；外部 LoadFrom 后需重新同步本地状态。 *@

<MudGrid>
    <MudItem xs="12" sm="8">
        <MudNumericField T="decimal?" Label="@Attr.DisplayName"
                         Value="_value" ValueChanged="OnValueChanged"
                         Required="@Attr.IsRequired"
                         Variant="Variant.Outlined" />
    </MudItem>
    <MudItem xs="12" sm="4">
        <MudSelect T="Guid?" Label="单位"
                   Value="_unitId" ValueChanged="OnUnitChanged"
                   Variant="Variant.Outlined">
            @foreach (var u in AvailableUnits)
            {
                <MudSelectItem T="Guid?" Value="@u.Id">
                    @u.Name (@u.Symbol)
                </MudSelectItem>
            }
        </MudSelect>
    </MudItem>
</MudGrid>

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attr { get; set; } = null!;
    [Parameter] public IReadOnlyList<UnitSchemaDto> AvailableUnits { get; set; }
        = Array.Empty<UnitSchemaDto>();
    [Parameter] public NumericValueDto? Value { get; set; }
    [Parameter] public EventCallback<NumericValueDto?> ValueChanged { get; set; }

    private decimal? _value;
    private Guid? _unitId;

    protected override void OnParametersSet()
    {
        // 仅当外部值与本地状态不一致时才同步（本地编辑回传的值与本地一致，不会触发覆盖）
        if (Value?.Value != _value || Value?.UnitId != _unitId)
        {
            _value = Value?.Value;
            _unitId = Value?.UnitId ?? Attr.Unit?.Id
                ?? AvailableUnits.FirstOrDefault(u => u.IsBaseUnit)?.Id;
        }
    }

    private Task OnValueChanged(decimal? v)
    {
        _value = v;
        return NotifyAsync();
    }

    private Task OnUnitChanged(Guid? id)
    {
        _unitId = id;
        return NotifyAsync();
    }

    // 始终回传 dto（即使数值为空也保留单位选择）；Value 为 null 时 DynamicForm 提交 null
    private Task NotifyAsync()
        => ValueChanged.InvokeAsync(new NumericValueDto
        {
            Value = _value,
            UnitId = _unitId
        });
}
```

## 文件 17/46 TreeGraph.Blazor/Components/Layout/MainLayout.razor

```razor
@inherits LayoutComponentBase
@* MainLayout.razor *@

@* MudBlazor 必需的全局 Provider *@
<MudThemeProvider/>
<MudPopoverProvider/>
<MudDialogProvider/>
<MudSnackbarProvider/>

<MudLayout>
    <MudAppBar Elevation="1" Color="Color.Primary">
        <MudIconButton Icon="@Icons.Material.Filled.Menu"
                       Color="Color.Inherit" Edge="Edge.Start"
                       OnClick="@(() => _drawerOpen = !_drawerOpen)" />
        <MudText Typo="Typo.h6" Class="ml-2">TreeGraph 管理台</MudText>
        <MudSpacer />
        <MudChip T="string" Size="Size.Small" Color="Color.Default">
            Aspire 服务发现
        </MudChip>
    </MudAppBar>

    <MudDrawer @bind-Open="_drawerOpen" Elevation="1" ClipMode="DrawerClipMode.Always">
        <NavMenu />
    </MudDrawer>

    <MudMainContent>
        <MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="my-4">
            @Body
        </MudContainer>
    </MudMainContent>
</MudLayout>

<div id="blazor-error-ui" data-nosnippet>
    An unhandled error has occurred.
    <a href="." class="reload">Reload</a>
    <span class="dismiss">🗙</span>
</div>

@code {
    private bool _drawerOpen = true;
}
```

## 文件 18/46 TreeGraph.Blazor/Components/Layout/NavMenu.razor

```razor
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@inject EavApiClient Api
@inject NavigationManager Nav
@inject ISnackbar Snackbar
@* NavMenu.razor *@
<MudNavMenu>
    <MudNavLink Href="/" Match="NavLinkMatch.All"
                Icon="@Icons.Material.Filled.Home">
        首页
    </MudNavLink>

    @* ★ 动态实体数据菜单 *@
    <MudNavGroup Title="实体数据"
                 Icon="@Icons.Material.Filled.Storage"
                 Expanded="_entityGroupExpanded"
                 ExpandedChanged="@(v => _entityGroupExpanded = v)">
        @if (_entityTypes is null)
        {
            <MudNavLink Disabled="true">
                <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
            </MudNavLink>
        }
        else if (_entityTypes.Count == 0)
        {
            <MudNavLink Disabled="true">（暂无实体类型）</MudNavLink>
        }
        else
        {
            @foreach (var et in _entityTypes)
            {
                <MudNavLink Href="@($"/entities/{et.EntityType}")"
                            Icon="@Icons.Material.Filled.List"
                            Match="NavLinkMatch.Prefix">
                    @et.EntityType
                    <MudChip T="string" Size="Size.Small" Class="ml-2">
                        @et.AttributeCount
                    </MudChip>
                </MudNavLink>
            }
        }
    </MudNavGroup>

    @* 元数据管理 *@
    <MudNavGroup Title="元数据管理"
                 Icon="@Icons.Material.Filled.Settings">
        <MudNavLink Href="/metadata/attributes"
                    Icon="@Icons.Material.Filled.EditAttributes"
                    Match="NavLinkMatch.Prefix">
            属性定义
        </MudNavLink>
        <MudNavLink Href="/metadata/composite-types"
                    Icon="@Icons.Material.Filled.AccountTree"
                    Match="NavLinkMatch.Prefix">
            组合类型
        </MudNavLink>
        <MudNavLink Href="/metadata/custom-tables"
                    Icon="@Icons.Material.Filled.TableChart"
                    Match="NavLinkMatch.Prefix">
            自定义表
        </MudNavLink>
        <MudNavLink Href="/metadata/option-sets"
                    Icon="@Icons.Material.Filled.Checklist"
                    Match="NavLinkMatch.Prefix">
            选项集
        </MudNavLink>
        <MudNavLink Href="/metadata/units"
                    Icon="@Icons.Material.Filled.Straighten"
                    Match="NavLinkMatch.Prefix">
            单位
        </MudNavLink>
    </MudNavGroup>
</MudNavMenu>

@code {
    private IReadOnlyList<EntityTypeSummaryDto>? _entityTypes;
    private bool _entityGroupExpanded = true;
    private bool _initialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        _entityTypes = await Api.ListEntityTypesAsync();

        // ★ P1-3：加载失败提示用户，而不是静默显示空菜单
        if (_entityTypes is null)
        {
            Snackbar.Add("实体类型加载失败，请检查后端服务是否可达", Severity.Warning);
        }

        StateHasChanged();
    }
}
```

## 文件 19/46 TreeGraph.Blazor/Components/Layout/ReconnectModal.razor

```razor
@* ReconnectModal.razor *@
<script type="module" src="@Assets["Components/Layout/ReconnectModal.razor.js"]"></script>

<dialog id="components-reconnect-modal" data-nosnippet>
    <div class="components-reconnect-container">
        <div class="components-rejoining-animation" aria-hidden="true">
            <div></div>
            <div></div>
        </div>
        <p class="components-reconnect-first-attempt-visible">
            Rejoining the server...
        </p>
        <p class="components-reconnect-repeated-attempt-visible">
            Rejoin failed... trying again in <span id="components-seconds-to-next-attempt"></span> seconds.
        </p>
        <p class="components-reconnect-failed-visible">
            Failed to rejoin.<br/>Please retry or reload the page.
        </p>
        <button id="components-reconnect-button" class="components-reconnect-failed-visible">
            Retry
        </button>
        <p class="components-pause-visible">
            The session has been paused by the server.
        </p>
        <p class="components-resume-failed-visible">
            Failed to resume the session.<br/>Please retry or reload the page.
        </p>
        <button id="components-resume-button" class="components-pause-visible components-resume-failed-visible">
            Resume
        </button>
    </div>
</dialog>
```

## 文件 20/46 TreeGraph.Blazor/Components/Pages/Counter.razor

```razor
@page "/counter"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer

<PageTitle>Counter</PageTitle>

<h1>Counter</h1>

<p role="status">Current count: @currentCount</p>

<button class="btn btn-primary" @onclick="IncrementCount">Click me</button>

@code {
    private int currentCount = 0;

    private void IncrementCount()
    {
        currentCount++;
    }

}
```

## 文件 21/46 TreeGraph.Blazor/Components/Pages/CustomTable/CustomTableEditor.razor

```razor
@page "/custom-table/{EntityType}/{EntityId}/{TableName}"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@* CustomTableEditor.razor *@
<PageTitle>自定义表 - @TableName</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">
    自定义表：@EntityType #@EntityId / @TableName
</MudText>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_table is null)
{
    <MudAlert Severity="Severity.Warning">未加载到表数据</MudAlert>
}
else
{
    <MudButton Variant="Variant.Filled" Color="Color.Success"
               OnClick="AddRow" StartIcon="@Icons.Material.Filled.Add"
               Class="mb-4">
        添加行
    </MudButton>

    @for (int i = 0; i < _table.Rows.Count; i++)
    {
        var idx = i;
        var row = _table.Rows[i];
        <MudPaper Class="pa-3 mb-2">
            <div class="d-flex align-center mb-2">
                <MudText Typo="Typo.subtitle2">行 #@(idx + 1)</MudText>
                @if (row.RowId is not null)
                {
                    <MudChip T="string" Size="Size.Small" Class="ml-2">
                        RowId: @row.RowId
                    </MudChip>
                }
                <MudSpacer />
                <MudIconButton Icon="@Icons.Material.Filled.Delete"
                               Color="Color.Error" Size="Size.Small"
                               OnClick="@(() => DeleteRowAsync(row))" />
            </div>
            @foreach (var (key, value) in row.Fields)
            {
                <MudTextField T="string"
                              Label="@key"
                              Value="@(value?.ToString())"
                              ValueChanged="@(v => UpdateField(row, key, v))"
                              Variant="Variant.Outlined"
                              Margin="Margin.Dense"
                              Class="mb-2" />
            }
        </MudPaper>
    }

    <MudButton Variant="Variant.Filled" Color="Color.Primary"
               OnClick="SaveAsync" StartIcon="@Icons.Material.Filled.Save">
        保存整表
    </MudButton>
}

@code {
    [Parameter] public string EntityType { get; set; } = "";
    [Parameter] public string EntityId { get; set; } = "";
    [Parameter] public string TableName { get; set; } = "";

    private CustomTableValue? _table;
    private bool _loading = true;

    // 端点 ⑦：GET api/eav/{entityType}/entities/{id}/tables/{tableName}
    protected override async Task OnInitializedAsync()
    {
        try
        {
            _table = await Api.LoadCustomTableAsync(EntityType, EntityId, TableName);
        }
        finally
        {
            _loading = false;
        }
    }

    private void AddRow()
    {
        _table!.Rows.Add(new CustomTableRowValue
        {
            RowId = null,
            RowOrder = _table.Rows.Count,
            Fields = new Dictionary<string, object?>()
        });
    }

    private void UpdateField(CustomTableRowValue row, string key, string? value)
    {
        row.Fields[key] = value;
    }

    // 端点 ⑧：PUT api/eav/{entityType}/entities/{id}/tables/{tableName}
    private async Task SaveAsync()
    {
        var (ok, error) = await Api.ReplaceCustomTableAsync(
            EntityType, EntityId, TableName, _table!);
        Snackbar.Add(ok ? "保存成功" : $"保存失败：{error}",
            ok ? Severity.Success : Severity.Error);
    }

    // 端点 ⑩：DELETE api/eav/{entityType}/entities/{id}/tables/{tableName}/rows/{rowId}
    private async Task DeleteRowAsync(CustomTableRowValue row)
    {
        if (row.RowId is null)
        {
            _table!.Rows.Remove(row);
            return;
        }

        var (ok, error) = await Api.DeleteCustomTableRowAsync(
            EntityType, EntityId, TableName, row.RowId!);
        if (ok)
        {
            _table!.Rows.Remove(row);
            Snackbar.Add("删除成功", Severity.Success);
        }
        else
        {
            Snackbar.Add($"删除失败：{error}", Severity.Error);
        }
    }
}
```

## 文件 22/46 TreeGraph.Blazor/Components/Pages/CustomTables.razor

```razor
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@* CustomTables.razor *@
<PageTitle>自定义表管理</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">自定义表管理</MudText>

<MudPaper Class="pa-4 mb-4">
    <MudGrid>
        <MudItem xs="12" md="4" Class="d-flex align-center">
            <MudTextField @bind-Value="_entityType" Label="实体类型（可选）"
                          Variant="Variant.Outlined" Margin="Margin.Dense"
                          Placeholder="留空显示全部" />
        </MudItem>
        <MudItem xs="12" md="2" Class="d-flex align-center">
            @* ★ 新增：显示已删除开关 *@
            <MudSwitch T="bool" @bind-Value="_includeDeleted"
                       Label="显示已删除"
                       Color="Color.Warning" />
        </MudItem>
        <MudItem xs="12" md="6" Class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="LoadAsync" Disabled="_loading"
                       StartIcon="@Icons.Material.Filled.Refresh">
                刷新
            </MudButton>
            <MudButton Variant="Variant.Filled" Color="Color.Success"
                       OnClick="OpenCreateTableDialog"
                       StartIcon="@Icons.Material.Filled.Add"
                       Class="ml-2">
                新建自定义表
            </MudButton>
        </MudItem>
    </MudGrid>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_tables is null || _tables.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.TableChart"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">暂无自定义表</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            点击"新建自定义表"创建第一个
        </MudText>
    </MudPaper>
}
else
{
    <MudExpansionPanels MultiExpansion="true">
        @foreach (var table in _tables)
        {
            <MudExpansionPanel @key="table.TableDefinitionId">
                <TitleContent>
                    <div class="d-flex align-center" style="width: 100%;">
                        <MudIcon Icon="@Icons.Material.Filled.TableChart"
                                 Color="@(table.IsDeleted ? Color.Error : Color.Primary)"
                                 Class="mr-2" />
                        <MudText Typo="Typo.subtitle1">@table.DisplayName</MudText>
                        <MudChip T="string" Size="Size.Small" Color="Color.Info"
                                 Class="ml-2">
                            @table.TableName
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Secondary"
                                 Class="ml-2">
                            @table.EntityType
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Class="ml-2">
                            v@table.Version
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Warning"
                                 Class="ml-2">
                            @table.Columns.Count(c => !c.IsDeleted) 列
                        </MudChip>
                        @* ★ 已删除标记 *@
                        @if (table.IsDeleted)
                        {
                            <MudChip T="string" Size="Size.Small" Color="Color.Error"
                                     Class="ml-2">
                                已删除
                            </MudChip>
                        }
                        <MudSpacer />
                        @if (!table.IsDeleted)
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                           Color="Color.Primary"
                                           Size="Size.Small"
                                           OnClick="@(() => OpenEditTableDialog(table))"
                                           title="编辑显示名 / 顺序" />
                            <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                           Color="Color.Error"
                                           Size="Size.Small"
                                           OnClick="@(async () => await DeleteTableAsync(table))"
                                           title="删除自定义表" />
                        }
                        else
                        {
                            @* ★ 已删除表：提供恢复入口 *@
                            <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                           Color="Color.Success"
                                           Size="Size.Small"
                                           OnClick="@(async () => await UndeleteTableAsync(table))"
                                           title="恢复" />
                        }
                    </div>
                </TitleContent>

                <ChildContent>
                    @if (!table.IsDeleted)
                    {
                        <div class="d-flex mb-3">
                            <MudButton Size="Size.Small"
                                       Variant="Variant.Outlined"
                                       Color="Color.Primary"
                                       StartIcon="@Icons.Material.Filled.Add"
                                       OnClick="@(() => OpenAddColumnDialog(table))">
                                添加列
                            </MudButton>
                        </div>
                    }

                    @if (table.Columns.Count == 0)
                    {
                        <MudAlert Severity="Severity.Info">
                            该自定义表暂无列
                        </MudAlert>
                    }
                    else
                    {
                        <MudTable Items="table.Columns" Bordered="true"
                                  Hover="true">
                            <HeaderContent>
                                <MudTh>列名</MudTh>
                                <MudTh>显示名</MudTh>
                                <MudTh>类型</MudTh>
                                <MudTh>必填</MudTh>
                                <MudTh>可搜索</MudTh>
                                <MudTh>可排序</MudTh>
                                <MudTh>唯一</MudTh>
                                <MudTh>顺序</MudTh>
                                <MudTh>操作</MudTh>
                            </HeaderContent>
                            <RowTemplate>
                                <MudTd>
                                    <code>@context.ColumnName</code>
                                    @* ★ 已删除列标记 *@
                                    @if (context.IsDeleted)
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Error" Class="ml-1">
                                            已删除
                                        </MudChip>
                                    }
                                </MudTd>
                                <MudTd>@context.DisplayName</MudTd>
                                <MudTd>
                                    <MudChip T="string" Size="Size.Small"
                                             Color="@GetDataTypeColor(context.DataType)">
                                        @context.DataType
                                    </MudChip>
                                </MudTd>
                                <MudTd>
                                    @if (context.IsRequired)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsSearchable)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsSortable)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsUnique)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>@context.DisplayOrder</MudTd>
                                <MudTd>
                                    @if (!context.IsDeleted)
                                    {
                                        <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                                       Size="Size.Small"
                                                       Color="Color.Primary"
                                                       OnClick="@(() => OpenEditColumnDialog(table, context))" />
                                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                                       Size="Size.Small"
                                                       Color="Color.Error"
                                                       OnClick="@(async () => await DeleteColumnAsync(table, context))" />
                                    }
                                    else
                                    {
                                        @* ★ 已删除列：提供恢复入口 *@
                                        <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                                       Size="Size.Small"
                                                       Color="Color.Success"
                                                       OnClick="@(async () => await UndeleteColumnAsync(table, context))"
                                                       title="恢复" />
                                    }
                                </MudTd>
                            </RowTemplate>
                        </MudTable>
                    }
                </ChildContent>
            </MudExpansionPanel>
        }
    </MudExpansionPanels>
}

@* ============================================================ *@
@* 新建自定义表 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showCreateTableDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">新建自定义表</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_newTable.EntityType"
                      Label="实体类型"
                      Placeholder="Product"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newTable.TableName"
                      Label="表名 (snake_case)"
                      Placeholder="specs"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newTable.DisplayName"
                      Label="显示名"
                      Placeholder="规格参数"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudNumericField T="int"
                         @bind-Value="_newTable.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showCreateTableDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="CreateTableAsync"
                   Disabled="@_submitting">
            @(_submitting ? "创建中..." : "创建")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 编辑自定义表 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showEditTableDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">编辑自定义表</MudText>
        @if (_editingTable is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingTable.EntityType / @_editingTable.TableName
                （表名与实体类型不可修改）
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_editTableForm.DisplayName"
                      Label="显示名"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudNumericField T="int?"
                         @bind-Value="_editTableForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />
        <MudAlert Severity="Severity.Info">
            TableName 与 EntityType 是引用标识，不可修改。如需更名，请新建并迁移引用。
        </MudAlert>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showEditTableDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="UpdateTableAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 添加 / 编辑列 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showColumnDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">
            @(_editingColumn is null ? "添加列" : "编辑列")
        </MudText>
        @if (_currentTable is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_currentTable.DisplayName (@_currentTable.TableName)
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_columnForm.ColumnName"
                      Label="列名 (snake_case)"
                      Placeholder="weight"
                      Variant="Variant.Outlined"
                      Disabled="@(_editingColumn is not null)"
                      Class="mb-3" />
        <MudTextField @bind-Value="_columnForm.DisplayName"
                      Label="显示名"
                      Placeholder="重量"
                      Variant="Variant.Outlined"
                      Class="mb-3" />

        <MudSelect T="string"
                   Value="_columnForm.DataType"
                   ValueChanged="OnColumnDataTypeChanged"
                   Label="数据类型"
                   Variant="Variant.Outlined"
                   Disabled="@(_editingColumn is not null)"
                   Class="mb-3">
            @foreach (var dt in DataTypeOptions)
            {
                <MudSelectItem T="string" Value="@dt.Value">@dt.Label</MudSelectItem>
            }
        </MudSelect>

        @if (_columnForm.DataType == "composite")
        {
            <MudSelect T="string"
                       Value="@(_columnForm.RefCompositeTypeId ?? "")"
                       ValueChanged="@(v => _columnForm.RefCompositeTypeId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="嵌套组合类型"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in _compositeTypes ?? (IReadOnlyList<CompositeTypeDetailDto>)Array.Empty<CompositeTypeDetailDto>())
                {
                    <MudSelectItem T="string" Value="@t.CompositeTypeId">
                        @t.DisplayName (@t.TypeName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        <MudNumericField T="int"
                         @bind-Value="_columnForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudGrid>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsRequired"
                           Label="必填"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsSearchable"
                           Label="可搜索"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsSortable"
                           Label="可排序"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsUnique"
                           Label="唯一"
                           Color="Color.Primary" />
            </MudItem>
        </MudGrid>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showColumnDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="SaveColumnAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private string? _entityType;
    private bool _includeDeleted;               // ★ 新增
    private IReadOnlyList<CustomTableDetailDto>? _tables;
    private IReadOnlyList<CompositeTypeDetailDto>? _compositeTypes;
    private bool _loading;
    private bool _initialized;

    private bool _submitting;
    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };

    // 新建自定义表
    private bool _showCreateTableDialog;
    private CreateCustomTableRequest _newTable = new() { EntityType = "Product" };

    // 编辑自定义表
    private bool _showEditTableDialog;
    private CustomTableDetailDto? _editingTable;
    private UpdateCustomTableRequest _editTableForm = new();

    // 添加 / 编辑列
    private bool _showColumnDialog;
    private CustomTableDetailDto? _currentTable;
    private CustomTableColumnDto? _editingColumn;
    private ColumnForm _columnForm = new();

    private static readonly (string Value, string Label)[] DataTypeOptions =
    {
        ("string",   "字符串"),
        ("int",      "整数"),
        ("decimal",  "小数"),
        ("bool",     "布尔"),
        ("datetime", "日期时间"),
        ("date",     "日期"),
        ("time",     "时间"),
        ("single_choice", "单选"),
        ("composite", "嵌套组合"),
        ("json",     "JSON")
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        await LoadCompositeTypesAsync();
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadCompositeTypesAsync()
    {
        _compositeTypes = await Api.ListCompositeTypesAsync();
        if (_compositeTypes is null)
            Snackbar.Add("组合类型加载失败（列若需嵌套类型将不可选）", Severity.Warning);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            // ★ 传 includeDeleted
            _tables = await Api.ListCustomTablesAsync(_entityType, _includeDeleted);
            if (_tables is null)
            {
                Snackbar.Add("加载失败，请检查后端服务", Severity.Error);
                _tables = Array.Empty<CustomTableDetailDto>();
            }
        }
        finally
        {
            _loading = false;
        }
    }

    // ---------- 新建自定义表 ----------

    private void OpenCreateTableDialog()
    {
        _newTable = new CreateCustomTableRequest { EntityType = "Product" };
        _showCreateTableDialog = true;
    }

    private async Task CreateTableAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTable.EntityType) ||
            string.IsNullOrWhiteSpace(_newTable.TableName) ||
            string.IsNullOrWhiteSpace(_newTable.DisplayName))
        {
            Snackbar.Add("请填写所有字段", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var id = await Api.CreateCustomTableAsync(_newTable);
            if (id is null)
            {
                Snackbar.Add("创建失败", Severity.Error);
                return;
            }

            Snackbar.Add($"创建成功：Id={id}", Severity.Success);
            _showCreateTableDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 编辑自定义表 ----------

    private void OpenEditTableDialog(CustomTableDetailDto table)
    {
        _editingTable = table;
        _editTableForm = new UpdateCustomTableRequest
        {
            DisplayName = table.DisplayName,
            DisplayOrder = table.DisplayOrder
        };
        _showEditTableDialog = true;
    }

    private async Task UpdateTableAsync()
    {
        if (_editingTable is null) return;

        if (string.IsNullOrWhiteSpace(_editTableForm.DisplayName))
        {
            Snackbar.Add("显示名不能为空", Severity.Warning);
            return;
        }

        var unchanged =
            _editTableForm.DisplayName == _editingTable.DisplayName &&
            _editTableForm.DisplayOrder == _editingTable.DisplayOrder;

        if (unchanged)
        {
            _showEditTableDialog = false;
            return;
        }

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateCustomTableAsync(
                _editingTable.TableDefinitionId, _editTableForm);

            if (!ok)
            {
                Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("更新成功", Severity.Success);
            _showEditTableDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 删除自定义表 ----------

    private async Task DeleteTableAsync(CustomTableDetailDto table)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除自定义表「{table.DisplayName}」？此操作将同时标记所有列为删除。\n\n" +
            "如需恢复，勾选上方「显示已删除」后点击恢复按钮。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteCustomTableAsync(table.TableDefinitionId);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- ★ 恢复自定义表 ----------

    private async Task UndeleteTableAsync(CustomTableDetailDto table)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复自定义表「{table.DisplayName}」({table.TableName})？\n\n" +
            "恢复后其所有列将一并恢复。",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteCustomTableAsync(table.TableDefinitionId);
        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 添加 / 编辑列 ----------

    private void OpenAddColumnDialog(CustomTableDetailDto table)
    {
        _currentTable = table;
        _editingColumn = null;
        _columnForm = new ColumnForm
        {
            DisplayOrder = table.Columns.Count(c => !c.IsDeleted) + 1,
            IsSearchable = true
        };
        _showColumnDialog = true;
    }

    private void OpenEditColumnDialog(
        CustomTableDetailDto table, CustomTableColumnDto column)
    {
        _currentTable = table;
        _editingColumn = column;
        _columnForm = new ColumnForm
        {
            ColumnName = column.ColumnName,
            DisplayName = column.DisplayName,
            DataType = column.DataType,
            RefCompositeTypeId = column.RefCompositeTypeId,
            IsRequired = column.IsRequired,
            IsSearchable = column.IsSearchable,
            IsSortable = column.IsSortable,
            IsUnique = column.IsUnique,
            DisplayOrder = column.DisplayOrder
        };
        _showColumnDialog = true;
    }

    private void OnColumnDataTypeChanged(string dt)
    {
        _columnForm.DataType = dt;
        if (dt != "composite") _columnForm.RefCompositeTypeId = null;
    }

    private async Task SaveColumnAsync()
    {
        if (_currentTable is null) return;

        if (string.IsNullOrWhiteSpace(_columnForm.ColumnName) ||
            string.IsNullOrWhiteSpace(_columnForm.DisplayName))
        {
            Snackbar.Add("请填写列名和显示名", Severity.Warning);
            return;
        }

        if (_columnForm.DataType == "composite" &&
            _columnForm.RefCompositeTypeId is null)
        {
            Snackbar.Add("组合类型列必须指定嵌套类型", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            if (_editingColumn is null)
            {
                var id = await Api.AddCustomTableColumnAsync(
                    _currentTable.TableDefinitionId,
                    new CreateTableColumnRequest
                    {
                        ColumnName = _columnForm.ColumnName,
                        DisplayName = _columnForm.DisplayName,
                        DataType = _columnForm.DataType,
                        RefCompositeTypeId = _columnForm.RefCompositeTypeId,
                        IsRequired = _columnForm.IsRequired,
                        IsSearchable = _columnForm.IsSearchable,
                        IsSortable = _columnForm.IsSortable,
                        IsUnique = _columnForm.IsUnique,
                        DisplayOrder = _columnForm.DisplayOrder
                    });

                if (id is null)
                {
                    Snackbar.Add("添加失败", Severity.Error);
                    return;
                }
                Snackbar.Add($"添加成功：Id={id}", Severity.Success);
            }
            else
            {
                var (ok, error) = await Api.UpdateTableColumnAsync(
                    _currentTable.TableDefinitionId,
                    _editingColumn.ColumnId,
                    new UpdateTableColumnRequest
                    {
                        DisplayName = _columnForm.DisplayName,
                        IsRequired = _columnForm.IsRequired,
                        IsSearchable = _columnForm.IsSearchable,
                        IsSortable = _columnForm.IsSortable,
                        IsUnique = _columnForm.IsUnique,
                        DisplayOrder = _columnForm.DisplayOrder
                    });

                if (!ok)
                {
                    Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                    return;
                }
                Snackbar.Add("更新成功", Severity.Success);
            }

            _showColumnDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 删除列 ----------

    private async Task DeleteColumnAsync(
        CustomTableDetailDto table, CustomTableColumnDto column)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除列「{column.DisplayName}」？\n\n" +
            "如需恢复，勾选「显示已删除」后点击恢复按钮。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteTableColumnAsync(
            table.TableDefinitionId, column.ColumnId);

        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- ★ 恢复列 ----------

    private async Task UndeleteColumnAsync(
        CustomTableDetailDto table, CustomTableColumnDto column)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复列「{column.DisplayName}」({column.ColumnName})？",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteTableColumnAsync(
            table.TableDefinitionId, column.ColumnId);

        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 辅助 ----------

    private static Color GetDataTypeColor(string dataType) => dataType switch
    {
        "string" => Color.Info,
        "int" or "decimal" => Color.Primary,
        "bool" => Color.Success,
        "datetime" or "date" or "time" => Color.Warning,
        "single_choice" => Color.Tertiary,
        "composite" => Color.Dark,
        _ => Color.Default
    };

    private class ColumnForm
    {
        public string ColumnName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DataType { get; set; } = "string";
        public string? RefCompositeTypeId { get; set; }
        public bool IsRequired { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }
        public bool IsUnique { get; set; }
        public int DisplayOrder { get; set; }
    }
}
```

## 文件 23/46 TreeGraph.Blazor/Components/Pages/Entities/EntityEdit.razor

```razor
@page "/entities/{EntityType}/{EntityId}"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@using System.Text.Json
@using TreeGraph.Blazor.Components.Shared
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@inject IEavFieldValidator Validator
@inject NavigationManager Nav
@* EntityEdit.razor *@
<PageTitle>编辑 @EntityType / @EntityId</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">
    编辑实体：@EntityType / @EntityId
</MudText>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_schema is null || _schema.Count == 0)
{
    <MudAlert Severity="Severity.Warning">
        实体类型 @EntityType 没有属性定义。请先在元数据管理页配置。
    </MudAlert>
}
else
{
    <MudAlert Severity="Severity.Info" Class="mb-4">
        <b>提示</b>：保存是<b>全量替换</b>——未在此页填写的属性会被删除。
        如需保留已有值，请勿清空。自定义表走独立端点，不受影响。
    </MudAlert>

    @if (TotalErrorCount > 0)
    {
        <MudAlert Severity="Severity.Error" Dense="false" Class="mb-4">
            <MudText Typo="Typo.body1">
                有 <b>@TotalErrorCount</b> 项校验错误，请修正后再保存：
            </MudText>
            <ul style="margin: 6px 0 0 0; padding-left: 20px;">
                @foreach (var (attrName, errs) in _errors.Where(kv => kv.Value.Count > 0))
                {
                    var attr = _regularAttributes.FirstOrDefault(a => a.AttributeName == attrName);
                    var label = attr?.DisplayName ?? attrName;
                    foreach (var e in errs)
                    {
                        var path = string.IsNullOrEmpty(e.Path) ? "" : $" [{e.Path}]";
                        <li>@label@(path)：@e.Message</li>
                    }
                }
            </ul>
        </MudAlert>
    }

    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h6" Class="mb-3">属性值</MudText>

        @foreach (var attr in _regularAttributes)
        {
            <div class="mb-4">
                <DynamicFieldRenderer Attribute="attr"
                                      Value="GetValue(attr.AttributeName)"
                                      ValueChanged="@(v => SetValue(attr.AttributeName, v))"
                                      Errors="GetFieldErrors(attr.AttributeName)" />
            </div>
        }
    </MudPaper>

    @if (_tableAttributes.Count > 0)
    {
        <MudPaper Class="pa-4 mb-4">
            <MudText Typo="Typo.h6" Class="mb-3">子表数据</MudText>
            @foreach (var attr in _tableAttributes)
            {
                <CustomTableEditor EntityType="@EntityType"
                                   EntityId="@EntityId"
                                   AttributeName="@attr.AttributeName"
                                   TableName="@attr.AttributeName"
                                   DisplayName="@attr.DisplayName"
                                   RefTableDefinitionId="@attr.RefTableDefinitionId" />
            }
        </MudPaper>
    }

    @* ★ #2：审计历史面板 *@
    <EntityHistoryPanel EntityType="@EntityType"
                        EntityId="@EntityId"
                        RefreshToken="_historyRefreshToken" />

    <div class="d-flex align-center" style="gap: 8px;">
        <MudButton Variant="Variant.Text" Color="Color.Default"
                   StartIcon="@Icons.Material.Filled.ArrowBack"
                   OnClick="BackToList">
            返回列表
        </MudButton>

        <MudButton Variant="Variant.Filled"
                   Color="@(TotalErrorCount > 0 ? Color.Default : Color.Primary)"
                   OnClick="SaveAsync" Disabled="@_saving"
                   StartIcon="@Icons.Material.Filled.Save">
            @(_saving ? "保存中..." : "保存")
        </MudButton>
        <MudButton Variant="Variant.Outlined" Color="Color.Default"
                   OnClick="ReloadAsync"
                   StartIcon="@Icons.Material.Filled.Refresh">
            重新加载
        </MudButton>
        <MudButton Variant="Variant.Outlined" Color="Color.Error"
                   OnClick="ClearAllAsync"
                   StartIcon="@Icons.Material.Filled.Delete">
            清空所有属性
        </MudButton>
        <MudButton Variant="Variant.Outlined" Color="Color.Error"
                   OnClick="DeleteEntityAsync"
                   StartIcon="@Icons.Material.Filled.DeleteForever"
                   Disabled="@_saving">
            删除实体
        </MudButton>
    </div>
}

@code {
    [Parameter] public string EntityType { get; set; } = "";
    [Parameter] public string EntityId { get; set; } = "";

    private IReadOnlyList<AttributeSchemaDto>? _schema;
    private readonly Dictionary<string, object?> _values = new();
    private readonly Dictionary<string, IReadOnlyList<FieldValidationError>> _errors = new();
    private List<AttributeSchemaDto> _regularAttributes = new();
    private List<AttributeSchemaDto> _tableAttributes = new();

    private bool _loading;
    private bool _saving;
    private bool _initialized;

    // ★ #3：记录加载时的 UpdatedAt（乐观锁版本）
    private DateTimeOffset? _loadedUpdatedAt;
    // ★ #2：保存成功后自增，触发审计历史面板刷新
    private int _historyRefreshToken;

    private int TotalErrorCount => _errors.Values.Sum(v => v.Count);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;
        await LoadAsync();
        StateHasChanged();
    }

    private void BackToList()
    {
        Nav.NavigateTo($"/entities/{EntityType}");
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var schema = await Api.GetSchemaAsync(EntityType);
            _schema = schema ?? Array.Empty<AttributeSchemaDto>();

            _regularAttributes = _schema.Where(a => a.DataType != "table").ToList();
            _tableAttributes = _schema.Where(a => a.DataType == "table").ToList();

            _values.Clear();
            var entity = await Api.GetEntityAsync(EntityType, EntityId);
            if (entity is not null)
            {
                _loadedUpdatedAt = entity.UpdatedAt;  // ★ #3：记录加载时版本
                foreach (var attr in _regularAttributes)
                {
                    if (entity.Properties.TryGetValue(attr.AttributeName, out var elem))
                        _values[attr.AttributeName] = JsonElementToValue(elem, attr);
                }
            }
            else
            {
                _loadedUpdatedAt = null;  // 新建实体
            }

            RevalidateAll();
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ReloadAsync()
    {
        await LoadAsync();
        StateHasChanged();
    }

    private object? GetValue(string name)
        => _values.TryGetValue(name, out var v) ? v : null;

    private IReadOnlyList<FieldValidationError>? GetFieldErrors(string name)
        => _errors.TryGetValue(name, out var e) && e.Count > 0 ? e : null;

    private void SetValue(string name, object? value)
    {
        if (value is null)
            _values.Remove(name);
        else
            _values[name] = value;

        RevalidateField(name);
    }

    private void RevalidateAll()
    {
        _errors.Clear();
        foreach (var attr in _regularAttributes)
        {
            var errs = Validator.Validate(attr, GetValue(attr.AttributeName));
            if (errs.Count > 0)
                _errors[attr.AttributeName] = errs;
        }
    }

    private void RevalidateField(string attributeName)
    {
        var attr = _regularAttributes.FirstOrDefault(a => a.AttributeName == attributeName);
        if (attr is null) return;

        var errs = Validator.Validate(attr, GetValue(attributeName));
        if (errs.Count > 0)
            _errors[attributeName] = errs;
        else
            _errors.Remove(attributeName);
    }

    private async Task SaveAsync()
    {
        RevalidateAll();

        if (TotalErrorCount > 0)
        {
            Snackbar.Add(
                $"有 {TotalErrorCount} 项校验错误未解决，请修正后再保存",
                Severity.Warning);
            StateHasChanged();
            return;
        }

        _saving = true;
        try
        {
            var submitValues = BuildSubmitValues();
            var (ok, error, currentUpdatedAt) = await Api.SaveEntityAsync(
                EntityType, EntityId, submitValues,
                expectedUpdatedAt: _loadedUpdatedAt);   // ★ #3 乐观锁

            if (!ok)
            {
                // ★ P1-1：409 冲突时展示服务端最新时间，引导用户刷新
                if (currentUpdatedAt is not null)
                {
                    var localTime = currentUpdatedAt.Value.ToLocalTime()
                        .ToString("yyyy-MM-dd HH:mm:ss");
                    Snackbar.Add(
                        $"保存失败：{error ?? "并发冲突"}\n" +
                        $"服务端最新时间：{localTime}\n请点击「重新加载」后再修改。",
                        Severity.Error);
                }
                else
                {
                    Snackbar.Add($"保存失败：{error ?? "未知错误"}", Severity.Error);
                }
                return;
            }

            Snackbar.Add("保存成功", Severity.Success);
            _historyRefreshToken++;   // ★ #2：触发历史面板刷新
            await LoadAsync();        // 重新加载会同步刷新 _loadedUpdatedAt
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task ClearAllAsync()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认清空",
            $"确定清空实体 {EntityType}/{EntityId} 的所有属性吗？此操作不可撤销。",
            yesText: "清空", cancelText: "取消");

        if (confirmed != true) return;

        _values.Clear();
        RevalidateAll();
        await SaveAsync();
    }

    // ---------- 提交值转换 ----------

    private Dictionary<string, object?> BuildSubmitValues()
    {
        var result = new Dictionary<string, object?>();
        foreach (var (k, v) in _values)
            result[k] = ConvertForSubmit(v);
        return result;
    }

    private static object? ConvertForSubmit(object? v) => v switch
    {
        null => null,
        NumericInput ni => ni.ToSubmitValue(),
        Dictionary<string, object?> dict => dict.ToDictionary(
            x => x.Key, x => ConvertForSubmit(x.Value)),
        List<object?> list => list.Select(ConvertForSubmit).ToList(),
        _ => v
    };

    // ---------- JSON -> 内部模型 ----------

    private object? JsonElementToValue(JsonElement elem, AttributeSchemaDto schema)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        return schema.DataType switch
        {
            "string" => elem.ValueKind == JsonValueKind.String ? elem.GetString() : elem.ToString(),
            "int" => elem.ValueKind == JsonValueKind.Number ? elem.GetInt64()
                   : elem.ValueKind == JsonValueKind.String
                     && long.TryParse(elem.GetString(), out var l) ? l
                   : (object?)null,
            "decimal" => JsonToNumeric(elem),
            "bool" => elem.ValueKind == JsonValueKind.True || elem.ValueKind == JsonValueKind.False
                      ? elem.GetBoolean() : null,
            "datetime" => elem.GetDateTimeOffset(),
            "date" => elem.GetString(),
            "time" => elem.GetString(),
            // ★ 后端绑定选项集时返回 SingleChoiceValue 对象 {value,label}
            //   （EavReadService.ExtractSingleChoice），未绑定时才是纯字符串
            "single_choice" => elem.ValueKind == JsonValueKind.String
                ? elem.GetString()
                : elem.ValueKind == JsonValueKind.Object
                  && elem.TryGetProperty("value", out var sc)
                  && sc.ValueKind == JsonValueKind.String ? sc.GetString()
                : elem.ToString(),
            "json" or "file" => elem.Clone(),
            "composite" => schema.CompositeType is not null
                ? JsonToComposite(elem, schema.CompositeType)
                : null,
            _ => elem.Clone()
        };
    }

    private NumericInput JsonToNumeric(JsonElement elem)
    {
        if (elem.ValueKind == JsonValueKind.Object)
        {
            var val = elem.GetProperty("value").GetDecimal();
            Guid? unitId = elem.TryGetProperty("unitId", out var u)
                            && u.ValueKind == JsonValueKind.String
                ? Guid.Parse(u.GetString()!)
                : null;
            return new NumericInput { Value = val, UnitId = unitId };
        }
        if (elem.ValueKind == JsonValueKind.Number)
            return new NumericInput { Value = elem.GetDecimal(), UnitId = null };
        return new NumericInput();
    }

    private Dictionary<string, object?> JsonToComposite(
        JsonElement elem, CompositeTypeSchemaDto typeSchema)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var field in typeSchema.Fields)
        {
            if (!elem.TryGetProperty(field.FieldName, out var fv)) continue;
            if (fv.ValueKind == JsonValueKind.Null) continue;

            if (field.IsArray)
            {
                // ★ 数组转 List<object?>
                var list = new List<object?>();
                foreach (var item in fv.EnumerateArray())
                {
                    list.Add(field.DataType == "composite" && field.NestedType is not null
                        ? JsonToComposite(item, field.NestedType)
                        : JsonToFieldValue(item, field));
                }
                dict[field.FieldName] = list;
            }
            else
            {
                dict[field.FieldName] = JsonToFieldValue(fv, field);
            }
        }
        return dict;
    }

    private object? JsonToFieldValue(JsonElement elem, CompositeFieldSchemaDto field)
    {
        if (elem.ValueKind == JsonValueKind.Null) return null;

        if (field.DataType == "composite" && field.NestedType is not null)
            return JsonToComposite(elem, field.NestedType);

        return field.DataType switch
        {
            "string" => elem.ValueKind == JsonValueKind.String ? elem.GetString() : elem.ToString(),
            "int" => elem.ValueKind == JsonValueKind.Number ? elem.GetInt64() : (object?)null,
            "decimal" => elem.ValueKind == JsonValueKind.Number ? elem.GetDecimal() : (object?)null,
            "bool" => elem.ValueKind is JsonValueKind.True or JsonValueKind.False
                      ? elem.GetBoolean() : (object?)null,
            "datetime" => elem.GetDateTimeOffset(),
            "date" or "time" or "single_choice" => elem.GetString(),
            "json" or "file" => elem.Clone(),
            _ => elem.Clone()
        };
    }

    private async Task DeleteEntityAsync()
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除实体",
            $"确定删除实体 {EntityType}/{EntityId} 及其所有属性值和子表数据吗？\n\n" +
            "此操作不可撤销。审计历史保留。",
            yesText: "永久删除", cancelText: "取消");

        if (confirmed != true) return;

        _saving = true;
        try
        {
            var (ok, error) = await Api.DeleteEntityAsync(EntityType, EntityId);
            if (!ok)
            {
                Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("实体已删除", Severity.Success);
            Nav.NavigateTo($"/entities/{EntityType}");
        }
        finally
        {
            _saving = false;
        }
    }
}
```

## 文件 24/46 TreeGraph.Blazor/Components/Pages/Entities/EntityHistory.razor

```razor
@page "/entities/{EntityType}/{EntityId}/history"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@* EntityHistory.razor *@
<PageTitle>审计历史 - @EntityType #@EntityId</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">
    @EntityType #@EntityId 的审计历史
</MudText>

<MudGrid Class="mb-4">
    <MudItem xs="12" md="4">
        <MudDatePicker @bind-Date="_from" Label="起始时间"
                       Variant="Variant.Outlined" />
    </MudItem>
    <MudItem xs="12" md="4" Class="d-flex align-center">
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   OnClick="LoadAsync" Disabled="@_loading">
            查询
        </MudButton>
    </MudItem>
</MudGrid>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" />
}
else if (_history is not null && _history.Count > 0)
{
    <MudTimeline TimelinePosition="TimelinePosition.Start">
        @foreach (var h in _history)
        {
            <MudTimelineItem Color="@GetColor(h.ChangeType)" Elevation="0">
                <MudText Typo="Typo.subtitle2">
                    @h.ChangedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                </MudText>
                <MudText Typo="Typo.caption" Class="mb-2">
                    @h.AttributeName · @h.ChangeType · @h.ChangedBy
                </MudText>
                <MudPaper Class="pa-2" Elevation="1">
                    @if (!string.IsNullOrEmpty(h.OldValue))
                    {
                        <MudText Typo="Typo.body2" Class="text-decoration-line-through">
                            旧值: @h.OldValue
                        </MudText>
                    }
                    @if (!string.IsNullOrEmpty(h.NewValue))
                    {
                        <MudText Typo="Typo.body2" Color="Color.Success">
                            新值: @h.NewValue
                        </MudText>
                    }
                </MudPaper>
            </MudTimelineItem>
        }
    </MudTimeline>
}
else if (_history is not null)
{
    <MudAlert Severity="Severity.Info">暂无历史记录</MudAlert>
}

@code {
    [Parameter] public string EntityType { get; set; } = "";
    [Parameter] public string EntityId { get; set; } = "";

    private IReadOnlyList<EntityHistoryDto>? _history;
    private DateTime? _from;
    private bool _loading = true;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    // 端点 ⑥：GET api/eav/{entityType}/entities/{id}/history?from=
    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            // ★ P1-4：MudDatePicker 的 DateTime 是"本地日历时间"，
            //   转 DateTimeOffset 时必须携带本地时区偏移，
            //   否则服务端会当作 UTC，导致查询边界与用户预期不一致。
            //
            //   修复前：new DateTimeOffset(_from.Value, TimeSpan.Zero)
            //           → 用户选 "2026-10-01 00:00" 会被当成 UTC 午夜，
            //             在 +08:00 环境下等价于本地 "2026-10-01 08:00"。
            DateTimeOffset? from = null;
            if (_from.HasValue)
            {
                var localDate = _from.Value;   // Kind = Unspecified
                var localOffset = TimeZoneInfo.Local.GetUtcOffset(localDate);
                from = new DateTimeOffset(localDate, localOffset);
            }

            _history = await Api.GetHistoryAsync(EntityType, EntityId, from);
        }
        finally
        {
            _loading = false;
        }
    }

    private static Color GetColor(string changeType) => changeType switch
    {
        "Insert" => Color.Success,
        "Update" => Color.Info,
        "Delete" => Color.Error,
        _ => Color.Default
    };
}
```

## 文件 25/46 TreeGraph.Blazor/Components/Pages/Entities/EntityList.razor

```razor
@page "/entities/{EntityType}"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@using TreeGraph.Blazor.Components.Shared
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject NavigationManager Nav
@inject IDialogService DialogService
@* EntityList.razor *@
<PageTitle>@EntityType 实体列表</PageTitle>

<div class="d-flex align-center mb-4" style="gap: 8px;">
    <MudButton Variant="Variant.Text" Color="Color.Default"
               StartIcon="@Icons.Material.Filled.ArrowBack"
               OnClick="BackToHome">
        返回
    </MudButton>
    <MudText Typo="Typo.h5">@EntityType 实体</MudText>
</div>

@* ★ 动态查询过滤器 *@
@if (_schema is not null && _schema.Count > 0)
{
    <QueryFilterBuilder Filters="_filters"
                        SupportedAttributes="_searchableAttributes"
                        OnApply="OnFilterChanged" />
}

<MudPaper Class="pa-4 mb-4">
    <div class="d-flex align-center" style="gap: 8px;">
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   StartIcon="@Icons.Material.Filled.Refresh"
                   OnClick="LoadAsync" Disabled="@_loading">
            刷新
        </MudButton>

        <MudSpacer />

        <MudTextField T="string"
                      Label="新建 EntityId (GUID)"
                      Value="_newEntityId"
                      ValueChanged="@(v => _newEntityId = v)"
                      Variant="Variant.Outlined"
                      Margin="Margin.Dense"
                      Style="width: 320px;"
                      Placeholder="xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx" />

        <MudButton Variant="Variant.Outlined"
                   Color="Color.Default"
                   StartIcon="@Icons.Material.Filled.Autorenew"
                   OnClick="GenerateNewGuid"
                   Disabled="@_loading">
            生成 GUID
        </MudButton>

        <MudButton Variant="Variant.Filled" Color="Color.Success"
                   StartIcon="@Icons.Material.Filled.Add"
                   OnClick="CreateNew"
                   Disabled="@(!IsValidGuid(_newEntityId))">
            新建
        </MudButton>
    </div>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_result is null)
{
    <MudAlert Severity="Severity.Error">加载失败，请检查后端服务</MudAlert>
}
else if (_result.Items.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.Inbox"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">未找到实体</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            @if (_filters.Count > 0)
            {
                <text>当前查询条件无匹配结果，试试清空过滤条件。</text>
            }
            else
            {
                <text>在右上角输入 EntityId 后点击"新建"创建第一个</text>
            }
        </MudText>
    </MudPaper>
}
else
{
    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            共 <b>@_result.Total</b> 个实体，第 @_result.Page / @TotalPages 页
        </MudText>

        <MudTable Items="_result.Items" Hover="true" Bordered="true"
                  @key="(_result.Page, _result.Total, _filters.Count)">
            <HeaderContent>
                <MudTh>EntityId</MudTh>
                @foreach (var col in _previewColumns)
                {
                    <MudTh>@GetColumnDisplayName(col)</MudTh>
                }
                <MudTh>操作</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>
                    <code>@context.EntityId</code>
                </MudTd>
                @foreach (var col in _previewColumns)
                {
                    <MudTd>@FormatPreview(context.Properties.GetValueOrDefault(col))</MudTd>
                }
                <MudTd>
                    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                   Size="Size.Small"
                                   Color="Color.Primary"
                                   OnClick="@(() => OpenEdit(context.EntityId))"
                                   title="编辑" />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                   Size="Size.Small"
                                   Color="Color.Error"
                                   OnClick="@(() => ConfirmDelete(context.EntityId))"
                                   title="删除" />
                </MudTd>
            </RowTemplate>
            <PagerContent>
                <div class="d-flex align-center pa-2" style="gap: 8px;">
                    <MudSelect T="int"
                               Value="_pageSize"
                               ValueChanged="OnPageSizeChanged"
                               Label="每页"
                               Variant="Variant.Outlined"
                               Margin="Margin.Dense"
                               Style="width: 120px;">
                        <MudSelectItem T="int" Value="10">10</MudSelectItem>
                        <MudSelectItem T="int" Value="20">20</MudSelectItem>
                        <MudSelectItem T="int" Value="50">50</MudSelectItem>
                        <MudSelectItem T="int" Value="100">100</MudSelectItem>
                    </MudSelect>

                    <MudSpacer />

                    <MudIconButton Icon="@Icons.Material.Filled.ChevronLeft"
                                   Size="Size.Small"
                                   Disabled="@(_result.Page <= 1)"
                                   OnClick="@(() => GoToPage(_result.Page - 1))" />
                    <MudText Typo="Typo.body2">@_result.Page / @TotalPages</MudText>
                    <MudIconButton Icon="@Icons.Material.Filled.ChevronRight"
                                   Size="Size.Small"
                                   Disabled="@(_result.Page >= TotalPages)"
                                   OnClick="@(() => GoToPage(_result.Page + 1))" />
                </div>
            </PagerContent>
        </MudTable>
    </MudPaper>
}

@code {
    [Parameter] public string EntityType { get; set; } = "";

    private IReadOnlyList<AttributeSchemaDto>? _schema;
    private List<AttributeSchemaDto> _searchableAttributes = new();
    private List<string> _previewColumns = new();

    private readonly List<AttributeFilter> _filters = new();
    private PagedResult<DynamicEntityDto>? _result;

    private bool _loading;
    private bool _initialized;
    private int _page = 1;
    private int _pageSize = 20;
    private string? _newEntityId;

    private int TotalPages => _result is null || _result.PageSize <= 0
        ? 1
        : Math.Max(1, (int)Math.Ceiling((double)_result.Total / _result.PageSize));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        var schema = await Api.GetSchemaAsync(EntityType);
        if (schema is not null)
        {
            _schema = schema;

            // 可搜索属性（用于过滤器）
            _searchableAttributes = schema
                .Where(a => a.IsSearchable && FilterOperatorCatalog.IsSupported(a))
                .OrderBy(a => a.DisplayOrder)
                .ToList();

            // 预览列（前 3 个可搜索的简单属性）
            _previewColumns = schema
                .Where(a => a.IsSearchable && a.DataType != "table" && a.DataType != "composite")
                .OrderBy(a => a.DisplayOrder)
                .Take(3)
                .Select(a => a.AttributeName)
                .ToList();
        }

        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            var request = new EavQueryRequest
            {
                EntityType = EntityType,
                Filters = _filters,
                Page = _page,
                PageSize = _pageSize
            };

            _result = await Api.QueryAsync(EntityType, request);
            if (_result is null)
                Snackbar.Add("加载失败", Severity.Error);
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>过滤器条件变化时（点击"应用查询"或条件增删）触发。</summary>
    private async Task OnFilterChanged()
    {
        _page = 1;  // 重置到第一页
        await LoadAsync();
        StateHasChanged();
    }

    private async Task OnPageSizeChanged(int size)
    {
        _pageSize = size;
        _page = 1;
        await LoadAsync();
    }

    private async Task GoToPage(int page)
    {
        if (page < 1 || page > TotalPages) return;
        _page = page;
        await LoadAsync();
    }

    private void OpenEdit(string entityId)
    {
        Nav.NavigateTo($"/entities/{EntityType}/{entityId}");
    }

    private async Task ConfirmDelete(string entityId)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除实体 {EntityType}/{entityId} 及其所有属性值和子表数据吗？\n\n" +
            "此操作不可撤销。",
            yesText: "永久删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteEntityAsync(EntityType, entityId);
        if (!ok)
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
            return;
        }

        Snackbar.Add("已删除", Severity.Success);
        await LoadAsync();
        StateHasChanged();
    }

    private void CreateNew()
    {
        var id = _newEntityId?.Trim();
        if (string.IsNullOrEmpty(id)) return;

        // 校验 GUID 格式（后端也校验，但前端先给友好提示）
        if (!Guid.TryParse(id, out var guid))
        {
            Snackbar.Add("EntityId 必须是 GUID 格式（36 字符，含连字符）", Severity.Warning);
            return;
        }

        // 统一为小写带连字符的标准格式
        Nav.NavigateTo($"/entities/{EntityType}/{guid:D}");
    }

    /// <summary>一键生成新 GUID 填入输入框（免手打）。</summary>
    private void GenerateNewGuid()
    {
        _newEntityId = Guid.NewGuid().ToString("D");
    }

    private static bool IsValidGuid(string? s)
        => !string.IsNullOrWhiteSpace(s) && Guid.TryParse(s, out _);

    private void BackToHome()
    {
        Nav.NavigateTo("/");
    }

    private string GetColumnDisplayName(string name)
        => _schema?.FirstOrDefault(a => a.AttributeName == name)?.DisplayName ?? name;

    private static string FormatPreview(System.Text.Json.JsonElement? elem)
    {
        if (elem is null) return "";
        var e = elem.Value;
        return e.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Null => "",
            System.Text.Json.JsonValueKind.String => e.GetString() ?? "",
            System.Text.Json.JsonValueKind.Number => e.ToString(),
            System.Text.Json.JsonValueKind.True => "✓",
            System.Text.Json.JsonValueKind.False => "✗",
            System.Text.Json.JsonValueKind.Object => FormatObjectPreview(e),
            System.Text.Json.JsonValueKind.Array => $"[{e.GetArrayLength()}]",
            _ => e.ToString()
        };
    }

    /// <summary>
    /// 对象形态的属性值预览。后端两类已知对象：
    ///   - SingleChoiceValue {value,label}（single_choice 绑定选项集）→ 显示 label
    ///   - NumericValue {value,unitId}（数值绑定单位）→ 显示数值
    /// 其余对象保留 {…} 占位。
    /// </summary>
    private static string FormatObjectPreview(System.Text.Json.JsonElement e)
    {
        if (e.TryGetProperty("label", out var lbl)
            && lbl.ValueKind == System.Text.Json.JsonValueKind.String)
            return lbl.GetString() ?? "";

        if (e.TryGetProperty("value", out var v)
            && v.ValueKind == System.Text.Json.JsonValueKind.Number)
            return v.ToString();

        return "{…}";
    }
}
```

## 文件 26/46 TreeGraph.Blazor/Components/Pages/Error.razor

```razor
@page "/Error"
@using System.Diagnostics
@* Error.razor *@

<PageTitle>Error</PageTitle>

<h1 class="text-danger">Error.</h1>
<h2 class="text-danger">An error occurred while processing your request.</h2>

@if (ShowRequestId)
{
    <p>
        <strong>Request ID:</strong> <code>@RequestId</code>
    </p>
}

<h3>Development Mode</h3>
<p>
    Swapping to <strong>Development</strong> environment will display more detailed information about the error that occurred.
</p>
<p>
    <strong>The Development environment shouldn't be enabled for deployed applications.</strong>
    It can result in displaying sensitive information from exceptions to end users.
    For local debugging, enable the <strong>Development</strong> environment by setting the <strong>ASPNETCORE_ENVIRONMENT</strong> environment variable to <strong>Development</strong>
    and restarting the app.
</p>

@code{
    [CascadingParameter] private HttpContext? HttpContext { get; set; }

    private string? RequestId { get; set; }
    private bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    protected override void OnInitialized() =>
        RequestId = Activity.Current?.Id ?? HttpContext?.TraceIdentifier;

}
```

## 文件 27/46 TreeGraph.Blazor/Components/Pages/Home.razor

```razor
@page "/"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@* Home.razor *@

<PageTitle>首页</PageTitle>

<MudText Typo="Typo.h4" Class="mb-4">TreeGraph EAV 管理台</MudText>

<MudGrid>
    <MudItem xs="12" md="6">
        <MudPaper Class="pa-4">
            <MudText Typo="Typo.h6">单位分类</MudText>
            @if (_categories is null)
            {
                <MudProgressLinear Indeterminate="true" />
            }
            else
            {
                <MudList T="string">
                    @foreach (var c in _categories)
                    {
                        <MudListItem T="string">
                            <div class="d-flex justify-space-between">
                                <span>@c.Category</span>
                                <MudChip T="string" Size="Size.Small">
                                    @c.Units.Count 个
                                </MudChip>
                            </div>
                        </MudListItem>
                    }
                </MudList>
            }
        </MudPaper>
    </MudItem>

    <MudItem xs="12" md="6">
        <MudPaper Class="pa-4">
            <MudText Typo="Typo.h6">快速操作</MudText>
            <MudButtonGroup Vertical="true" FullWidth="true" Class="mt-2">
                <MudButton Href="/entities" StartIcon="@Icons.Material.Filled.List">
                    浏览实体
                </MudButton>
                <MudButton Href="/query" StartIcon="@Icons.Material.Filled.Search">
                    动态查询
                </MudButton>
                <MudButton Href="/metadata/attributes" StartIcon="@Icons.Material.Filled.Add">
                    定义属性
                </MudButton>
            </MudButtonGroup>
        </MudPaper>
    </MudItem>
</MudGrid>

@code {
    private IReadOnlyList<UnitCategoryDto>? _categories;

    // 调用端点 ㉕：GET api/units/categories
    protected override async Task OnInitializedAsync()
    {
        _categories = await Api.GetUnitCategoriesAsync();
    }
}
```

## 文件 28/46 TreeGraph.Blazor/Components/Pages/Metadata/Attributes.razor

```razor
@page "/metadata/attributes"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@* Attributes.razor *@
<PageTitle>属性定义管理</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">属性定义管理</MudText>

<MudPaper Class="pa-4 mb-4">
    <MudGrid>
        <MudItem xs="12" md="3" Class="d-flex align-center">
            <MudTextField @bind-Value="_entityType" Label="实体类型"
                          Variant="Variant.Outlined" Margin="Margin.Dense"
                          Placeholder="如 Product" />
        </MudItem>
        <MudItem xs="12" md="3" Class="d-flex align-center">
            <MudSwitch T="bool" @bind-Value="_includeDeleted"
                       Label="显示已删除"
                       Color="Color.Warning" />
        </MudItem>
        <MudItem xs="12" md="6" Class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="LoadAsync" Disabled="_loading"
                       StartIcon="@Icons.Material.Filled.Refresh">
                刷新
            </MudButton>
            <MudButton Variant="Variant.Filled" Color="Color.Success"
                       OnClick="OpenCreateDialog"
                       StartIcon="@Icons.Material.Filled.Add"
                       Class="ml-2">
                新建属性
            </MudButton>
        </MudItem>
    </MudGrid>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_attributes is null || _attributes.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.ListAlt"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">暂无属性定义</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            填写实体类型后点击"刷新"加载属性
        </MudText>
    </MudPaper>
}
else
{
    <MudTable Items="_attributes" Hover="true" Bordered="true"
              Filter="new Func<AttributeDetailDto, bool>(FilterFunc)">
        <ToolBarContent>
            <MudText Typo="Typo.h6">
                共 @_attributes.Count 个属性
            </MudText>
            <MudSpacer />
            <MudTextField @bind-Value="_searchString"
                          Placeholder="搜索属性名 / 显示名"
                          Adornment="Adornment.Start"
                          AdornmentIcon="@Icons.Material.Filled.Search"
                          IconSize="Size.Medium"
                          Class="mt-0"
                          Immediate="true" />
        </ToolBarContent>
        <HeaderContent>
            <MudTh>属性名</MudTh>
            <MudTh>显示名</MudTh>
            <MudTh>类型</MudTh>
            <MudTh>引用</MudTh>
            <MudTh>必填</MudTh>
            <MudTh>可搜索</MudTh>
            <MudTh>可排序</MudTh>
            <MudTh>顺序</MudTh>
            <MudTh>操作</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd>
                <code>@context.AttributeName</code>
                @if (context.IsDeleted)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Error"
                             Class="ml-1">
                        已删除
                    </MudChip>
                }
                @* ★ 数据一致性告警：int + UnitId 是非法组合 *@
                @if (context.DataType == "int" && context.UnitId is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Error"
                             Class="ml-1">
                        ⚠ 非法单位绑定
                    </MudChip>
                }
            </MudTd>
            <MudTd>@context.DisplayName</MudTd>
            <MudTd>
                <MudChip T="string" Size="Size.Small"
                         Color="@GetDataTypeColor(context.DataType)">
                    @context.DataType
                </MudChip>
            </MudTd>
            <MudTd>
                @if (context.UnitId is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Warning">
                        单位: @context.UnitSymbol
                    </MudChip>
                }
                @if (context.RefCompositeTypeId is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Dark">
                        组合: @context.CompositeTypeName
                    </MudChip>
                }
                @if (context.RefTableDefinitionId is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Info">
                        表: @context.TableName
                    </MudChip>
                }
                @if (context.RefOptionSetId is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Secondary">
                        选项: @context.OptionSetName
                    </MudChip>
                }
            </MudTd>
            <MudTd>
                @if (context.IsRequired)
                {
                    <MudIcon Icon="@Icons.Material.Filled.Check"
                             Color="Color.Success" Size="Size.Small" />
                }
            </MudTd>
            <MudTd>
                @if (context.IsSearchable)
                {
                    <MudIcon Icon="@Icons.Material.Filled.Check"
                             Color="Color.Success" Size="Size.Small" />
                }
            </MudTd>
            <MudTd>
                @if (context.IsSortable)
                {
                    <MudIcon Icon="@Icons.Material.Filled.Check"
                             Color="Color.Success" Size="Size.Small" />
                }
            </MudTd>
            <MudTd>@context.DisplayOrder</MudTd>
            <MudTd>
                @if (!context.IsDeleted)
                {
                    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                   Size="Size.Small"
                                   Color="Color.Primary"
                                   OnClick="@(async () => await OpenEditDialogAsync(context))"
                                   title="编辑" />
                    <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                   Size="Size.Small"
                                   Color="Color.Error"
                                   OnClick="@(async () => await DeleteAsync(context))"
                                   title="删除" />
                }
                else
                {
                    @* ★ 软删除的属性：提供恢复入口 *@
                    <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                   Size="Size.Small"
                                   Color="Color.Success"
                                   OnClick="@(async () => await UndeleteAsync(context))"
                                   title="恢复" />
                }
            </MudTd>
        </RowTemplate>
        <PagerContent>
            <MudTablePager PageSizeOptions="new[] { 20, 50, 100 }" />
        </PagerContent>
    </MudTable>
}

@* ============================================================ *@
@* 新建对话框                                                    *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showCreateDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">新建属性</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_createForm.EntityType"
                      Label="实体类型" Placeholder="Product"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudTextField @bind-Value="_createForm.AttributeName"
                      Label="属性名 (snake_case)" Placeholder="screen_size"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudTextField @bind-Value="_createForm.DisplayName"
                      Label="显示名" Placeholder="屏幕尺寸"
                      Variant="Variant.Outlined" Class="mb-3" />

        <MudSelect T="string"
                   Value="_createForm.DataType"
                   ValueChanged="OnCreateDataTypeChanged"
                   Label="数据类型"
                   Variant="Variant.Outlined"
                   Class="mb-3">
            @foreach (var dt in DataTypeOptions)
            {
                <MudSelectItem T="string" Value="@dt.Value">@dt.Label</MudSelectItem>
            }
        </MudSelect>

        @* ★ 只有 decimal 显示单位选择。int 归一化会产生小数，禁止绑定单位。 *@
        @if (_createForm.DataType == "decimal")
        {
            <MudSelect T="Guid?"
                       Value="_createForm.UnitId"
                       ValueChanged="@(v => _createForm.UnitId = v)"
                       Label="基准单位（可选）"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                <MudSelectItem T="Guid?" Value="@((Guid?)null)">
                    （不使用单位）
                </MudSelectItem>
                @foreach (var u in _units ?? new List<UnitDto>())
                {
                    <MudSelectItem T="Guid?" Value="@u.Id">
                        @u.Name (@u.Symbol) - @u.Category
                    </MudSelectItem>
                }
            </MudSelect>
        }
        else if (_createForm.DataType == "int")
        {
            <MudAlert Severity="Severity.Info" Class="mb-3">
                int 类型不支持绑定单位。若需要单位换算（如 cm ↔ m），请选择 decimal。
            </MudAlert>
        }

        @if (_createForm.DataType == "composite")
        {
            <MudSelect T="string"
                       Value="@(_createForm.RefCompositeTypeId ?? "")"
                       ValueChanged="@(v => _createForm.RefCompositeTypeId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="组合类型"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in _compositeTypes ?? new List<CompositeTypeDetailDto>())
                {
                    <MudSelectItem T="string" Value="@t.CompositeTypeId">
                        @t.DisplayName (@t.TypeName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        @if (_createForm.DataType == "table")
        {
            <MudSelect T="string"
                       Value="@(_createForm.RefTableDefinitionId ?? "")"
                       ValueChanged="@(v => _createForm.RefTableDefinitionId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="自定义表"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in _customTables ?? new List<CustomTableDetailDto>())
                {
                    <MudSelectItem T="string" Value="@t.TableDefinitionId">
                        @t.DisplayName (@t.TableName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        @if (_createForm.DataType == "single_choice")
        {
            <MudSelect T="string"
                       Value="@(_createForm.RefOptionSetId ?? "")"
                       ValueChanged="@(v => _createForm.RefOptionSetId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="选项集"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var s in _optionSets ?? new List<OptionSetSummaryDto>())
                {
                    <MudSelectItem T="string" Value="@s.OptionSetId">
                        @s.DisplayName (@s.SetName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        <MudNumericField T="int"
                         @bind-Value="_createForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudGrid>
            <MudItem xs="6" md="3">
                <MudSwitch T="bool" @bind-Value="_createForm.IsRequired"
                           Label="必填" Color="Color.Primary" />
            </MudItem>
            <MudItem xs="6" md="3">
                <MudSwitch T="bool" @bind-Value="_createForm.IsSearchable"
                           Label="可搜索" Color="Color.Primary" />
            </MudItem>
            <MudItem xs="6" md="3">
                <MudSwitch T="bool" @bind-Value="_createForm.IsSortable"
                           Label="可排序" Color="Color.Primary" />
            </MudItem>
        </MudGrid>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showCreateDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="CreateAsync"
                   Disabled="@_submitting">
            @(_submitting ? "创建中..." : "创建")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 编辑对话框                                                    *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showEditDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">编辑属性</MudText>
        @if (_editingAttribute is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingAttribute.EntityType / @_editingAttribute.AttributeName
                （类型: @_editingAttribute.DataType，不可修改）
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_editForm.DisplayName"
                      Label="显示名"
                      Variant="Variant.Outlined"
                      Class="mb-3" />

        @* ★ decimal：正常显示单位选择 *@
        @if (_editingAttribute?.DataType == "decimal")
        {
            <MudSelect T="Guid?"
                       Value="_editForm.UnitId"
                       ValueChanged="@(v => _editForm.UnitId = v)"
                       Label="基准单位"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                <MudSelectItem T="Guid?" Value="@((Guid?)null)">
                    （不使用单位）
                </MudSelectItem>
                @foreach (var u in _units ?? new List<UnitDto>())
                {
                    <MudSelectItem T="Guid?" Value="@u.Id">
                        @u.Name (@u.Symbol) - @u.Category
                    </MudSelectItem>
                }
            </MudSelect>
            <MudAlert Severity="Severity.Warning" Class="mb-3">
                修改基准单位不会自动转换已有数据。如需变更，请先规划数据迁移。
            </MudAlert>
        }
        @* ★ int + 历史遗留 UnitId：只提供"清除"入口，不允许重设 *@
        else if (_editingAttribute?.DataType == "int"
                 && _editingAttribute.UnitId is not null)
        {
            <MudAlert Severity="Severity.Error" Class="mb-3">
                <b>数据一致性告警</b>：该属性类型为 int，但仍绑定了单位
                <code>@_editingAttribute.UnitSymbol</code>。
                这是历史遗留的非法配置——归一化到基准单位时会产生小数
                （如 150 cm → 1.5 m），写入 bigint 会静默截断。
            </MudAlert>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Error"
                       StartIcon="@Icons.Material.Filled.LinkOff"
                       OnClick="ClearLegacyUnitAsync"
                       Disabled="@_submitting"
                       Class="mb-3">
                清除单位绑定
            </MudButton>
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                清除后该属性不再有单位语义，已有数据保持原值不变。
            </MudText>
        }
        else if (_editingAttribute?.DataType == "int")
        {
            <MudAlert Severity="Severity.Info" Class="mb-3">
                int 类型不支持绑定单位。
            </MudAlert>
        }

        @if (_editingAttribute?.DataType == "composite")
        {
            <MudSelect T="string"
                       Value="@(_editForm.RefCompositeTypeId ?? "")"
                       ValueChanged="@(v => _editForm.RefCompositeTypeId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="组合类型"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in _compositeTypes ?? new List<CompositeTypeDetailDto>())
                {
                    <MudSelectItem T="string" Value="@t.CompositeTypeId">
                        @t.DisplayName (@t.TypeName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        @if (_editingAttribute?.DataType == "table")
        {
            <MudSelect T="string"
                       Value="@(_editForm.RefTableDefinitionId ?? "")"
                       ValueChanged="@(v => _editForm.RefTableDefinitionId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="自定义表"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in _customTables ?? new List<CustomTableDetailDto>())
                {
                    <MudSelectItem T="string" Value="@t.TableDefinitionId">
                        @t.DisplayName (@t.TableName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        @if (_editingAttribute?.DataType == "single_choice")
        {
            <MudSelect T="string"
                       Value="@(_editForm.RefOptionSetId ?? "")"
                       ValueChanged="@(v => _editForm.RefOptionSetId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="选项集"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var s in _optionSets ?? new List<OptionSetSummaryDto>())
                {
                    <MudSelectItem T="string" Value="@s.OptionSetId">
                        @s.DisplayName (@s.SetName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        <MudNumericField T="int"
                         @bind-Value="_editForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudGrid>
            <MudItem xs="6" md="3">
                <MudSwitch T="bool" @bind-Value="_editForm.IsRequired"
                           Label="必填" Color="Color.Primary" />
            </MudItem>
            <MudItem xs="6" md="3">
                <MudSwitch T="bool" @bind-Value="_editForm.IsSearchable"
                           Label="可搜索" Color="Color.Primary" />
            </MudItem>
            <MudItem xs="6" md="3">
                <MudSwitch T="bool" @bind-Value="_editForm.IsSortable"
                           Label="可排序" Color="Color.Primary" />
            </MudItem>
        </MudGrid>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showEditDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="UpdateAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private string? _entityType;
    private bool _includeDeleted;
    private string? _searchString;
    private IReadOnlyList<AttributeDetailDto>? _attributes;
    private bool _loading;
    private bool _initialized;

    private IReadOnlyList<UnitDto>? _units;
    private IReadOnlyList<CompositeTypeDetailDto>? _compositeTypes;
    private IReadOnlyList<CustomTableDetailDto>? _customTables;
    private IReadOnlyList<OptionSetSummaryDto>? _optionSets;

    private bool _submitting;
    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };

    private bool _showCreateDialog;
    private CreateAttributeForm _createForm = new();

    private bool _showEditDialog;
    private AttributeDetailDto? _editingAttribute;
    private UpdateAttributeForm _editForm = new();

    private static readonly (string Value, string Label)[] DataTypeOptions =
    {
        ("string",   "字符串"),
        ("int",      "整数"),
        ("decimal",  "小数"),
        ("bool",     "布尔"),
        ("datetime", "日期时间"),
        ("date",     "日期"),
        ("time",     "时间"),
        ("single_choice", "单选"),
        ("file",     "文件"),
        ("composite", "组合类型"),
        ("table",    "自定义表"),
        ("json",     "JSON")
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        await LoadReferencesAsync();
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadReferencesAsync()
    {
        var unitsTask = Api.ListUnitsAsync();
        var compTask = Api.ListCompositeTypesAsync();
        var tablesTask = Api.ListCustomTablesAsync();
        var optTask = Api.ListOptionSetsAsync();
        await Task.WhenAll(unitsTask, compTask, tablesTask, optTask);

        _units = unitsTask.Result;
        _compositeTypes = compTask.Result;
        _customTables = tablesTask.Result;
        _optionSets = optTask.Result;

        var failed = new List<string>();
        if (_units is null) failed.Add("单位");
        if (_compositeTypes is null) failed.Add("组合类型");
        if (_customTables is null) failed.Add("自定义表");
        if (_optionSets is null) failed.Add("选项集");
        if (failed.Count > 0)
            Snackbar.Add($"引用加载失败：{string.Join("、", failed)}", Severity.Warning);
    }

    private async Task LoadAsync()
    {
        if (string.IsNullOrWhiteSpace(_entityType))
        {
            Snackbar.Add("请先填写实体类型", Severity.Info);
            _attributes = Array.Empty<AttributeDetailDto>();
            return;
        }

        _loading = true;
        try
        {
            _attributes = await Api.ListAttributesAsync(_entityType, _includeDeleted);
        }
        finally
        {
            _loading = false;
        }
    }

    // ---------- 新建 ----------

    private void OpenCreateDialog()
    {
        _createForm = new CreateAttributeForm
        {
            EntityType = _entityType ?? "Product",
            DisplayOrder = (_attributes?.Count ?? 0) + 1,
            IsSearchable = true
        };
        _showCreateDialog = true;
    }

    /// <summary>
    /// ★ 切换数据类型时清空不适用的引用。
    /// 关键点：只有 decimal 保留 UnitId。int 会被清空（数据库 CHECK 约束禁止）。
    /// </summary>
    private void OnCreateDataTypeChanged(string dt)
    {
        _createForm.DataType = dt;
        if (dt != "decimal") _createForm.UnitId = null;
        if (dt != "composite") _createForm.RefCompositeTypeId = null;
        if (dt != "table") _createForm.RefTableDefinitionId = null;
        if (dt != "single_choice") _createForm.RefOptionSetId = null;
    }

    private async Task CreateAsync()
    {
        var f = _createForm;
        if (string.IsNullOrWhiteSpace(f.EntityType) ||
            string.IsNullOrWhiteSpace(f.AttributeName) ||
            string.IsNullOrWhiteSpace(f.DisplayName))
        {
            Snackbar.Add("请填写实体类型、属性名、显示名", Severity.Warning);
            return;
        }
        if (f.DataType == "composite" && f.RefCompositeTypeId is null)
        {
            Snackbar.Add("组合类型必须选择引用的类型", Severity.Warning);
            return;
        }
        if (f.DataType == "table" && f.RefTableDefinitionId is null)
        {
            Snackbar.Add("自定义表必须选择引用的表", Severity.Warning);
            return;
        }
        if (f.DataType == "single_choice" && f.RefOptionSetId is null)
        {
            Snackbar.Add("单选类型必须选择选项集", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var id = await Api.CreateAttributeAsync(new CreateAttributeRequest
            {
                EntityType = f.EntityType,
                AttributeName = f.AttributeName,
                DisplayName = f.DisplayName,
                DataType = f.DataType,
                IsRequired = f.IsRequired,
                IsSearchable = f.IsSearchable,
                IsSortable = f.IsSortable,
                DisplayOrder = f.DisplayOrder,
                UnitId = f.UnitId,
                RefCompositeTypeId = f.RefCompositeTypeId,
                RefTableDefinitionId = f.RefTableDefinitionId,
                RefOptionSetId = f.RefOptionSetId
            });

            if (id is null)
            {
                Snackbar.Add("创建失败", Severity.Error);
                return;
            }

            Snackbar.Add($"创建成功：AttributeId={id}", Severity.Success);
            _showCreateDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 编辑 ----------

    private async Task OpenEditDialogAsync(AttributeDetailDto attr)
    {
        var detail = await Api.GetAttributeAsync(attr.AttributeId);
        if (detail is null)
        {
            Snackbar.Add("加载详情失败", Severity.Error);
            return;
        }

        _editingAttribute = detail;
        _editForm = new UpdateAttributeForm
        {
            DisplayName = detail.DisplayName,
            IsRequired = detail.IsRequired,
            IsSearchable = detail.IsSearchable,
            IsSortable = detail.IsSortable,
            DisplayOrder = detail.DisplayOrder,
            UnitId = detail.UnitId,
            RefCompositeTypeId = detail.RefCompositeTypeId,
            RefTableDefinitionId = detail.RefTableDefinitionId,
            RefOptionSetId = detail.RefOptionSetId
        };
        _showEditDialog = true;
    }

    /// <summary>
    /// ★ 历史遗留的 int + UnitId 属性：一键清除单位绑定。
    /// 走 UpdateAttribute 的 ClearUnitId = true 路径，不修改其它字段。
    /// </summary>
    private async Task ClearLegacyUnitAsync()
    {
        if (_editingAttribute is null) return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "清除单位绑定",
            $"确定清除属性「{_editingAttribute.DisplayName}」的单位绑定？\n\n" +
            "· 该属性类型为 int，不允许绑定单位。\n" +
            "· 清除后属性不再有单位语义。\n" +
            "· 已有数据的存储值保持不变（不换算）。",
            yesText: "清除", cancelText: "取消");

        if (confirmed != true) return;

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateAttributeAsync(
                _editingAttribute.AttributeId,
                new UpdateAttributeRequest { ClearUnitId = true });

            if (!ok)
            {
                Snackbar.Add($"清除失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("已清除单位绑定", Severity.Success);
            _showEditDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task UpdateAsync()
    {
        if (_editingAttribute is null) return;

        var (unitId, clearUnit) = Diff(_editForm.UnitId, _editingAttribute.UnitId);
        var (ctId, clearCt) = Diff(_editForm.RefCompositeTypeId, _editingAttribute.RefCompositeTypeId);
        var (tblId, clearTbl) = Diff(_editForm.RefTableDefinitionId, _editingAttribute.RefTableDefinitionId);
        var (optId, clearOpt) = Diff(_editForm.RefOptionSetId, _editingAttribute.RefOptionSetId);

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateAttributeAsync(
                _editingAttribute.AttributeId,
                new UpdateAttributeRequest
                {
                    DisplayName = _editForm.DisplayName,
                    IsRequired = _editForm.IsRequired,
                    IsSearchable = _editForm.IsSearchable,
                    IsSortable = _editForm.IsSortable,
                    DisplayOrder = _editForm.DisplayOrder,

                    UnitId = unitId,
                    ClearUnitId = clearUnit,

                    RefCompositeTypeId = ctId,
                    ClearRefCompositeTypeId = clearCt,

                    RefTableDefinitionId = tblId,
                    ClearRefTableDefinitionId = clearTbl,

                    RefOptionSetId = optId,
                    ClearRefOptionSetId = clearOpt
                });

            if (!ok)
            {
                Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("更新成功", Severity.Success);
            _showEditDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    private static (T? Value, bool Clear) Diff<T>(T? current, T? original) where T : struct
    {
        if (current is null && original is not null) return (null, true);
        if (current is not null && !current.Value.Equals(original)) return (current, false);
        return (null, false);
    }

    /// <summary>string 引用类型重载（ID 字符串化后 Diff 需要独立版本）。</summary>
    private static (string? Value, bool Clear) Diff(string? current, string? original)
    {
        if (current is null && original is not null) return (null, true);
        if (current is not null && current != original) return (current, false);
        return (null, false);
    }

    // ---------- 删除 ----------

    private async Task DeleteAsync(AttributeDetailDto attr)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除属性「{attr.DisplayName}」({attr.AttributeName})？" +
            "此操作将软删除属性定义，已有数据保留但不再被新业务使用。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteAttributeAsync(attr.AttributeId);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    /// <summary>
    /// ★ 恢复被软删除的属性。
    /// 后端唯一约束不区分 IsDeleted：若已存在同名活动属性，返回 409。
    /// </summary>
    private async Task UndeleteAsync(AttributeDetailDto attr)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复属性「{attr.DisplayName}」({attr.AttributeName})？",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteAttributeAsync(attr.AttributeId);
        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 辅助 ----------

    private bool FilterFunc(AttributeDetailDto a)
    {
        if (string.IsNullOrWhiteSpace(_searchString)) return true;
        var s = _searchString;
        return a.AttributeName.Contains(s, StringComparison.OrdinalIgnoreCase)
            || a.DisplayName.Contains(s, StringComparison.OrdinalIgnoreCase)
            || a.EntityType.Contains(s, StringComparison.OrdinalIgnoreCase);
    }

    private static Color GetDataTypeColor(string dataType) => dataType switch
    {
        "string" => Color.Info,
        "int" or "decimal" => Color.Primary,
        "bool" => Color.Success,
        "datetime" or "date" or "time" => Color.Warning,
        "single_choice" => Color.Tertiary,
        "composite" => Color.Dark,
        "table" => Color.Info,
        "file" => Color.Default,
        _ => Color.Default
    };

    // ---------- 表单模型 ----------

    private class CreateAttributeForm
    {
        public string EntityType { get; set; } = "";
        public string AttributeName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DataType { get; set; } = "string";
        public bool IsRequired { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }
        public int DisplayOrder { get; set; }
        public Guid? UnitId { get; set; }
        public string? RefCompositeTypeId { get; set; }
        public string? RefTableDefinitionId { get; set; }
        public string? RefOptionSetId { get; set; }
    }

    private class UpdateAttributeForm
    {
        public string DisplayName { get; set; } = "";
        public bool IsRequired { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }
        public int DisplayOrder { get; set; }
        public Guid? UnitId { get; set; }
        public string? RefCompositeTypeId { get; set; }
        public string? RefTableDefinitionId { get; set; }
        public string? RefOptionSetId { get; set; }
    }
}
```

## 文件 29/46 TreeGraph.Blazor/Components/Pages/Metadata/CompositeTypes.razor

```razor

```razor
@page "/metadata/composite-types"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService

@* CompositeTypes.razor *@
<PageTitle>组合类型管理</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">组合类型管理</MudText>

<MudPaper Class="pa-4 mb-4">
    <MudGrid>
        <MudItem xs="12" md="4" Class="d-flex align-center">
            <MudTextField @bind-Value="_entityType" Label="实体类型（可选）"
                          Variant="Variant.Outlined" Margin="Margin.Dense"
                          Placeholder="留空显示全部" />
        </MudItem>
        <MudItem xs="12" md="2" Class="d-flex align-center">
            @* ★ 显示已删除开关 *@
            <MudSwitch T="bool" @bind-Value="_includeDeleted"
                       Label="显示已删除"
                       Color="Color.Warning" />
        </MudItem>
        <MudItem xs="12" md="6" Class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="LoadAsync" Disabled="_loading"
                       StartIcon="@Icons.Material.Filled.Refresh">
                刷新
            </MudButton>
            <MudButton Variant="Variant.Filled" Color="Color.Success"
                       OnClick="OpenCreateTypeDialog"
                       StartIcon="@Icons.Material.Filled.Add"
                       Class="ml-2">
                新建组合类型
            </MudButton>
        </MudItem>
    </MudGrid>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_types is null || _types.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.AccountTree"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">暂无组合类型</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            点击"新建组合类型"创建第一个
        </MudText>
    </MudPaper>
}
else
{
    <MudExpansionPanels MultiExpansion="true">
        @foreach (var type in _types)
        {
            <MudExpansionPanel @key="type.CompositeTypeId">
                <TitleContent>
                    <div class="d-flex align-center" style="width: 100%;">
                        <MudIcon Icon="@Icons.Material.Filled.AccountTree"
                                 Color="@(type.IsDeleted ? Color.Error : Color.Primary)"
                                 Class="mr-2" />
                        <MudText Typo="Typo.subtitle1">@type.DisplayName</MudText>
                        <MudChip T="string" Size="Size.Small" Color="Color.Info"
                                 Class="ml-2">
                            @type.TypeName
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Secondary"
                                 Class="ml-2">
                            @type.EntityType
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Class="ml-2">
                            v@type.Version
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Warning"
                                 Class="ml-2">
                            @type.Fields.Count(f => !f.IsDeleted) 字段
                        </MudChip>
                        @* ★ 已删除标记 *@
                        @if (type.IsDeleted)
                        {
                            <MudChip T="string" Size="Size.Small" Color="Color.Error"
                                     Class="ml-2">
                                已删除
                            </MudChip>
                        }
                        <MudSpacer />
                        @if (!type.IsDeleted)
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                           Color="Color.Primary"
                                           Size="Size.Small"
                                           OnClick="@(() => OpenEditTypeDialog(type))"
                                           title="编辑显示名" />
                            <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                           Color="Color.Error"
                                           Size="Size.Small"
                                           OnClick="@(async () => await DeleteTypeAsync(type))"
                                           title="删除组合类型" />
                        }
                        else
                        {
                            @* ★ 已删除类型：提供恢复入口 *@
                            <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                           Color="Color.Success"
                                           Size="Size.Small"
                                           OnClick="@(async () => await UndeleteTypeAsync(type))"
                                           title="恢复" />
                        }
                    </div>
                </TitleContent>

                <ChildContent>
                    @if (!type.IsDeleted)
                    {
                        <div class="d-flex mb-3">
                            <MudButton Size="Size.Small"
                                       Variant="Variant.Outlined"
                                       Color="Color.Primary"
                                       StartIcon="@Icons.Material.Filled.Add"
                                       OnClick="@(() => OpenAddFieldDialog(type))">
                                添加字段
                            </MudButton>
                        </div>
                    }

                    @if (type.Fields.Count == 0)
                    {
                        <MudAlert Severity="Severity.Info" Dense="true">
                            该组合类型暂无字段
                        </MudAlert>
                    }
                    else
                    {
                        <MudTable Items="type.Fields" Dense="true" Bordered="true"
                                  Hover="true">
                            <HeaderContent>
                                <MudTh>字段名</MudTh>
                                <MudTh>显示名</MudTh>
                                <MudTh>类型</MudTh>
                                <MudTh>引用</MudTh>
                                <MudTh>数组</MudTh>
                                <MudTh>必填</MudTh>
                                <MudTh>可搜索</MudTh>
                                <MudTh>可排序</MudTh>
                                <MudTh>顺序</MudTh>
                                <MudTh>操作</MudTh>
                            </HeaderContent>
                            <RowTemplate>
                                <MudTd>
                                    <code>@context.FieldName</code>
                                    @* ★ 已删除字段标记 *@
                                    @if (context.IsDeleted)
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Error" Class="ml-1">
                                            已删除
                                        </MudChip>
                                    }
                                </MudTd>
                                <MudTd>@context.DisplayName</MudTd>
                                <MudTd>
                                    <MudChip T="string" Size="Size.Small"
                                             Color="@GetDataTypeColor(context.DataType)">
                                        @context.DataType
                                    </MudChip>
                                </MudTd>
                                <MudTd>
                                    @* 单位 / 选项集 / 嵌套组合引用 *@
                                    @if (context.UnitId is not null)
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Warning" Class="mr-1">
                                            单位
                                        </MudChip>
                                    }
                                    @if (context.RefOptionSetId is not null)
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Secondary" Class="mr-1">
                                            选项: @context.OptionSetName
                                        </MudChip>
                                    }
                                    @if (context.RefCompositeTypeId is not null)
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Dark">
                                            嵌套: @GetTypeName(context.RefCompositeTypeId!)
                                        </MudChip>
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsArray)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsRequired)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsSearchable)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsSortable)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>@context.DisplayOrder</MudTd>
                                <MudTd>
                                    @if (!context.IsDeleted)
                                    {
                                        <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                                       Size="Size.Small"
                                                       Color="Color.Primary"
                                                       OnClick="@(() => OpenEditFieldDialog(type, context))" />
                                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                                       Size="Size.Small"
                                                       Color="Color.Error"
                                                       OnClick="@(async () => await DeleteFieldAsync(type, context))" />
                                    }
                                    else
                                    {
                                        @* ★ 已删除字段：提供恢复入口 *@
                                        <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                                       Size="Size.Small"
                                                       Color="Color.Success"
                                                       OnClick="@(async () => await UndeleteFieldAsync(type, context))"
                                                       title="恢复" />
                                    }
                                </MudTd>
                            </RowTemplate>
                        </MudTable>
                    }
                </ChildContent>
            </MudExpansionPanel>
        }
    </MudExpansionPanels>
}

@* ============================================================ *@
@* 新建组合类型 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showCreateTypeDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">新建组合类型</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_newType.EntityType"
                      Label="实体类型"
                      HelperText="填 Shared 表示全局共享"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newType.TypeName"
                      Label="类型名 (PascalCase)"
                      Placeholder="Address"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newType.DisplayName"
                      Label="显示名"
                      Placeholder="地址"
                      Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showCreateTypeDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="CreateTypeAsync"
                   Disabled="@_submitting">
            @(_submitting ? "创建中..." : "创建")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 编辑组合类型 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showEditTypeDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">编辑组合类型</MudText>
        @if (_editingType is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingType.EntityType / @_editingType.TypeName
                （类型标识不可修改）
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_editTypeForm.DisplayName"
                      Label="显示名"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudAlert Severity="Severity.Info" Dense="true">
            TypeName 与 EntityType 是引用标识，不可修改。如需更名，请新建并迁移引用。
        </MudAlert>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showEditTypeDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="UpdateTypeAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 添加 / 编辑字段 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showFieldDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">
            @(_editingField is null ? "添加字段" : "编辑字段")
        </MudText>
        @if (_currentType is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_currentType.DisplayName (@_currentType.TypeName)
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_fieldForm.FieldName"
                      Label="字段名 (snake_case)"
                      Placeholder="street_name"
                      Variant="Variant.Outlined"
                      Disabled="@(_editingField is not null)"
                      Class="mb-3" />
        <MudTextField @bind-Value="_fieldForm.DisplayName"
                      Label="显示名"
                      Placeholder="街道名称"
                      Variant="Variant.Outlined"
                      Class="mb-3" />

        <MudSelect T="string"
                   Value="_fieldForm.DataType"
                   ValueChanged="OnFieldDataTypeChanged"
                   Label="数据类型"
                   Variant="Variant.Outlined"
                   Disabled="@(_editingField is not null)"
                   Class="mb-3">
            @foreach (var dt in DataTypeOptions)
            {
                <MudSelectItem T="string" Value="@dt.Value">@dt.Label</MudSelectItem>
            }
        </MudSelect>

        @* decimal 类型的基准单位 *@
        @if (_fieldForm.DataType == "decimal")
        {
            <MudSelect T="Guid?"
                       Value="_fieldForm.UnitId"
                       ValueChanged="@(v => _fieldForm.UnitId = v)"
                       Label="基准单位（可选）"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                <MudSelectItem T="Guid?" Value="@((Guid?)null)">
                    （不使用单位）
                </MudSelectItem>
                @foreach (var u in _units ?? (IReadOnlyList<UnitDto>)Array.Empty<UnitDto>())
                {
                    <MudSelectItem T="Guid?" Value="@u.Id">
                        @u.Name (@u.Symbol) - @u.Category
                    </MudSelectItem>
                }
            </MudSelect>
        }

        @* single_choice 的选项集 *@
        @if (_fieldForm.DataType == "single_choice")
        {
            <MudSelect T="string"
                       Value="@(_fieldForm.RefOptionSetId ?? "")"
                       ValueChanged="@(v => _fieldForm.RefOptionSetId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="选项集"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                <MudSelectItem T="string" Value="@("")">
                    （不绑定，仅用 AllowedValues）
                </MudSelectItem>
                @foreach (var os in _optionSets ?? (IReadOnlyList<OptionSetSummaryDto>)Array.Empty<OptionSetSummaryDto>())
                {
                    <MudSelectItem T="string" Value="@os.OptionSetId">
                        @os.DisplayName (@os.SetName)
                    </MudSelectItem>
                }
            </MudSelect>
            <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mb-3">
                绑定选项集后，前端渲染为下拉并显示 Label；否则使用 AllowedValues。
            </MudText>
        }

        @* composite 类型的嵌套引用 *@
        @if (_fieldForm.DataType == "composite")
        {
            <MudSelect T="string"
                       Value="@(_fieldForm.RefCompositeTypeId ?? "")"
                       ValueChanged="@(v => _fieldForm.RefCompositeTypeId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="嵌套组合类型"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in GetNestedTypeOptions())
                {
                    <MudSelectItem T="string" Value="@t.CompositeTypeId">
                        @t.DisplayName (@t.TypeName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        <MudGrid Class="mb-3">
            <MudItem xs="6">
                <MudNumericField T="int"
                                 @bind-Value="_fieldForm.DisplayOrder"
                                 Label="显示顺序"
                                 Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="6" Class="d-flex align-center">
                <MudSwitch T="bool"
                           @bind-Value="_fieldForm.IsArray"
                           Label="数组"
                           Color="Color.Primary" />
            </MudItem>
        </MudGrid>

        <MudGrid>
            <MudItem xs="12" md="4">
                <MudSwitch T="bool"
                           @bind-Value="_fieldForm.IsRequired"
                           Label="必填"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="4">
                <MudSwitch T="bool"
                           @bind-Value="_fieldForm.IsSearchable"
                           Label="可搜索"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="4">
                <MudSwitch T="bool"
                           @bind-Value="_fieldForm.IsSortable"
                           Label="可排序"
                           Color="Color.Primary" />
            </MudItem>
        </MudGrid>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showFieldDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="SaveFieldAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private string? _entityType;
    private bool _includeDeleted;               // ★ 新增
    private IReadOnlyList<CompositeTypeDetailDto>? _types;
    private bool _loading;
    private bool _initialized;

    // 引用数据（供字段对话框使用）
    private IReadOnlyList<UnitDto>? _units;
    private IReadOnlyList<OptionSetSummaryDto>? _optionSets;

    private bool _submitting;
    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };

    // 新建组合类型
    private bool _showCreateTypeDialog;
    private CreateCompositeTypeRequest _newType = new() { EntityType = "Shared" };

    // 编辑组合类型
    private bool _showEditTypeDialog;
    private CompositeTypeDetailDto? _editingType;
    private UpdateCompositeTypeRequest _editTypeForm = new();

    // 添加 / 编辑字段
    private bool _showFieldDialog;
    private CompositeTypeDetailDto? _currentType;
    private CompositeFieldDetailDto? _editingField;
    private FieldForm _fieldForm = new();

    private static readonly (string Value, string Label)[] DataTypeOptions =
    {
        ("string",   "字符串"),
        ("int",      "整数"),
        ("decimal",  "小数"),
        ("bool",     "布尔"),
        ("datetime", "日期时间"),
        ("date",     "日期"),
        ("time",     "时间"),
        ("single_choice", "单选"),
        ("composite", "嵌套组合"),
        ("json",     "JSON")
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        await LoadReferencesAsync();
        await LoadAsync();
        StateHasChanged();
    }

    /// <summary>并行加载单位与选项集。</summary>
    private async Task LoadReferencesAsync()
    {
        var unitsTask = Api.ListUnitsAsync();
        var optTask = Api.ListOptionSetsAsync();
        await Task.WhenAll(unitsTask, optTask);

        _units = unitsTask.Result;
        _optionSets = optTask.Result;

        var failed = new List<string>();
        if (_units is null) failed.Add("单位");
        if (_optionSets is null) failed.Add("选项集");
        if (failed.Count > 0)
            Snackbar.Add($"引用加载失败：{string.Join("、", failed)}", Severity.Warning);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            // ★ 传 includeDeleted
            _types = await Api.ListCompositeTypesAsync(_entityType, _includeDeleted);
            if (_types is null)
            {
                Snackbar.Add("加载失败，请检查后端服务", Severity.Error);
                _types = Array.Empty<CompositeTypeDetailDto>();
            }
        }
        finally
        {
            _loading = false;
        }
    }

    // ---------- 新建组合类型 ----------

    private void OpenCreateTypeDialog()
    {
        _newType = new CreateCompositeTypeRequest { EntityType = "Shared" };
        _showCreateTypeDialog = true;
    }

    private async Task CreateTypeAsync()
    {
        if (string.IsNullOrWhiteSpace(_newType.EntityType) ||
            string.IsNullOrWhiteSpace(_newType.TypeName) ||
            string.IsNullOrWhiteSpace(_newType.DisplayName))
        {
            Snackbar.Add("请填写所有字段", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var id = await Api.CreateCompositeTypeAsync(_newType);
            if (id is null)
            {
                Snackbar.Add("创建失败", Severity.Error);
                return;
            }

            Snackbar.Add($"创建成功：Id={id}", Severity.Success);
            _showCreateTypeDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 编辑组合类型 ----------

    private void OpenEditTypeDialog(CompositeTypeDetailDto type)
    {
        _editingType = type;
        _editTypeForm = new UpdateCompositeTypeRequest
        {
            DisplayName = type.DisplayName
        };
        _showEditTypeDialog = true;
    }

    private async Task UpdateTypeAsync()
    {
        if (_editingType is null) return;

        if (string.IsNullOrWhiteSpace(_editTypeForm.DisplayName))
        {
            Snackbar.Add("显示名不能为空", Severity.Warning);
            return;
        }
        if (_editTypeForm.DisplayName == _editingType.DisplayName)
        {
            _showEditTypeDialog = false;
            return;
        }

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateCompositeTypeAsync(
                _editingType.CompositeTypeId, _editTypeForm);

            if (!ok)
            {
                Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("更新成功", Severity.Success);
            _showEditTypeDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 删除组合类型 ----------

    private async Task DeleteTypeAsync(CompositeTypeDetailDto type)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除组合类型「{type.DisplayName}」？此操作将同时标记所有字段为删除。\n\n" +
            "如需恢复，勾选上方「显示已删除」后点击恢复按钮。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteCompositeTypeAsync(type.CompositeTypeId);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- ★ 恢复组合类型 ----------

    private async Task UndeleteTypeAsync(CompositeTypeDetailDto type)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复组合类型「{type.DisplayName}」({type.TypeName})？\n\n" +
            "恢复后其所有字段将一并恢复。",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteCompositeTypeAsync(type.CompositeTypeId);
        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- ★ 恢复字段 ----------

    private async Task UndeleteFieldAsync(
        CompositeTypeDetailDto type, CompositeFieldDetailDto field)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复字段「{field.DisplayName}」({field.FieldName})？",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteCompositeFieldAsync(
            type.CompositeTypeId, field.FieldId);

        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 添加 / 编辑字段 ----------

    private void OpenAddFieldDialog(CompositeTypeDetailDto type)
    {
        _currentType = type;
        _editingField = null;
        _fieldForm = new FieldForm
        {
            // ★ 只统计活动字段的显示顺序
            DisplayOrder = type.Fields.Count(f => !f.IsDeleted) + 1,
            IsSearchable = true
        };
        _showFieldDialog = true;
    }

    private void OpenEditFieldDialog(
        CompositeTypeDetailDto type, CompositeFieldDetailDto field)
    {
        _currentType = type;
        _editingField = field;
        _fieldForm = new FieldForm
        {
            FieldName = field.FieldName,
            DisplayName = field.DisplayName,
            DataType = field.DataType,
            RefCompositeTypeId = field.RefCompositeTypeId,
            UnitId = field.UnitId,
            RefOptionSetId = field.RefOptionSetId,
            IsArray = field.IsArray,
            IsRequired = field.IsRequired,
            IsSearchable = field.IsSearchable,
            IsSortable = field.IsSortable,
            DisplayOrder = field.DisplayOrder
        };
        _showFieldDialog = true;
    }

    /// <summary>
    /// 切换类型时清空不适用的引用。
    ///   - decimal 保留 UnitId，清空 RefOptionSetId / RefCompositeTypeId
    ///   - single_choice 保留 RefOptionSetId，清空 UnitId / RefCompositeTypeId
    ///   - composite 保留 RefCompositeTypeId，清空 UnitId / RefOptionSetId
    ///   - 其它类型全部清空
    /// </summary>
    private void OnFieldDataTypeChanged(string dt)
    {
        _fieldForm.DataType = dt;

        if (dt != "decimal") _fieldForm.UnitId = null;
        if (dt != "single_choice") _fieldForm.RefOptionSetId = null;
        if (dt != "composite") _fieldForm.RefCompositeTypeId = null;
    }

    /// <summary>
    /// 嵌套类型候选：排除当前类型自身，且排除已删除类型。
    /// </summary>
    private IEnumerable<CompositeTypeDetailDto> GetNestedTypeOptions()
    {
        if (_types is null) yield break;
        foreach (var t in _types)
        {
            if (t.CompositeTypeId == _currentType?.CompositeTypeId) continue;
            if (t.IsDeleted) continue;
            yield return t;
        }
    }

    private string GetTypeName(string compositeTypeId)
        => _types?.FirstOrDefault(t => t.CompositeTypeId == compositeTypeId)?.TypeName
           ?? compositeTypeId.ToString();

    private async Task SaveFieldAsync()
    {
        if (_currentType is null) return;

        if (string.IsNullOrWhiteSpace(_fieldForm.FieldName) ||
            string.IsNullOrWhiteSpace(_fieldForm.DisplayName))
        {
            Snackbar.Add("请填写字段名和显示名", Severity.Warning);
            return;
        }

        if (_fieldForm.DataType == "composite" &&
            _fieldForm.RefCompositeTypeId is null)
        {
            Snackbar.Add("组合类型字段必须指定嵌套类型", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            if (_editingField is null)
            {
                var id = await Api.AddCompositeFieldAsync(
                    _currentType.CompositeTypeId,
                    new CreateCompositeFieldRequest
                    {
                        FieldName = _fieldForm.FieldName,
                        DisplayName = _fieldForm.DisplayName,
                        DataType = _fieldForm.DataType,
                        RefCompositeTypeId = _fieldForm.RefCompositeTypeId,
                        UnitId = _fieldForm.UnitId,
                        RefOptionSetId = _fieldForm.RefOptionSetId,
                        IsArray = _fieldForm.IsArray,
                        IsRequired = _fieldForm.IsRequired,
                        IsSearchable = _fieldForm.IsSearchable,
                        IsSortable = _fieldForm.IsSortable,
                        DisplayOrder = _fieldForm.DisplayOrder
                    });

                if (id is null)
                {
                    Snackbar.Add("添加失败", Severity.Error);
                    return;
                }
                Snackbar.Add($"添加成功：Id={id}", Severity.Success);
            }
            else
            {
                // diff 计算 ClearRefOptionSetId / ClearUnitId
                var (optId, clearOpt) = Diff(
                    _fieldForm.RefOptionSetId, _editingField.RefOptionSetId);
                var (unitId, clearUnit) = Diff(
                    _fieldForm.UnitId, _editingField.UnitId);

                var (ok, error) = await Api.UpdateCompositeFieldAsync(
                    _currentType.CompositeTypeId,
                    _editingField.FieldId,
                    new UpdateCompositeFieldRequest
                    {
                        DisplayName = _fieldForm.DisplayName,
                        IsRequired = _fieldForm.IsRequired,
                        IsSearchable = _fieldForm.IsSearchable,
                        IsSortable = _fieldForm.IsSortable,
                        DisplayOrder = _fieldForm.DisplayOrder,

                        RefOptionSetId = optId,
                        ClearRefOptionSetId = clearOpt,

                        UnitId = unitId,
                        ClearUnitId = clearUnit
                    });

                if (!ok)
                {
                    Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                    return;
                }
                Snackbar.Add("更新成功", Severity.Success);
            }

            _showFieldDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    /// <summary>计算 diff：返回 (要设置的值, 是否清除)。</summary>
    private static (long? Value, bool Clear) Diff(long? current, long? original)
    {
        if (current is null && original is not null) return (null, true);
        if (current is not null && current != original) return (current, false);
        return (null, false);
    }

    /// <summary>Guid 版 diff（单位引用用）。</summary>
    private static (Guid? Value, bool Clear) Diff(Guid? current, Guid? original)
    {
        if (current is null && original is not null) return (null, true);
        if (current is not null && current != original) return (current, false);
        return (null, false);
    }

    /// <summary>string 版 diff（ID 字符串化后用）。</summary>
    private static (string? Value, bool Clear) Diff(string? current, string? original)
    {
        if (current is null && original is not null) return (null, true);
        if (current is not null && current != original) return (current, false);
        return (null, false);
    }

    // ---------- 删除字段 ----------

    private async Task DeleteFieldAsync(
        CompositeTypeDetailDto type, CompositeFieldDetailDto field)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除字段「{field.DisplayName}」？\n\n" +
            "如需恢复，勾选「显示已删除」后点击恢复按钮。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteCompositeFieldAsync(
            type.CompositeTypeId, field.FieldId);

        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 辅助 ----------

    private static Color GetDataTypeColor(string dataType) => dataType switch
    {
        "string" => Color.Info,
        "int" or "decimal" => Color.Primary,
        "bool" => Color.Success,
        "datetime" or "date" or "time" => Color.Warning,
        "single_choice" => Color.Tertiary,
        "composite" => Color.Dark,
        _ => Color.Default
    };

    private class FieldForm
    {
        public string FieldName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DataType { get; set; } = "string";
        public string? RefCompositeTypeId { get; set; }
        public Guid? UnitId { get; set; }
        public string? RefOptionSetId { get; set; }
        public bool IsArray { get; set; }
        public bool IsRequired { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }
        public int DisplayOrder { get; set; }
    }
}
```
```

## 文件 30/46 TreeGraph.Blazor/Components/Pages/Metadata/CustomTables.razor

```razor
@page "/metadata/custom-tables"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@* CustomTables.razor *@
<PageTitle>自定义表管理</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">自定义表管理</MudText>

<MudPaper Class="pa-4 mb-4">
    <MudGrid>
        <MudItem xs="12" md="4" Class="d-flex align-center">
            <MudTextField @bind-Value="_entityType" Label="实体类型（可选）"
                          Variant="Variant.Outlined" Margin="Margin.Dense"
                          Placeholder="留空显示全部" />
        </MudItem>
        <MudItem xs="12" md="8" Class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="LoadAsync" Disabled="_loading"
                       StartIcon="@Icons.Material.Filled.Refresh">
                刷新
            </MudButton>
            <MudButton Variant="Variant.Filled" Color="Color.Success"
                       OnClick="OpenCreateTableDialog"
                       StartIcon="@Icons.Material.Filled.Add"
                       Class="ml-2">
                新建自定义表
            </MudButton>
        </MudItem>
    </MudGrid>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_tables is null || _tables.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.TableChart"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">暂无自定义表</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            点击"新建自定义表"创建第一个
        </MudText>
    </MudPaper>
}
else
{
    <MudExpansionPanels MultiExpansion="true">
        @foreach (var table in _tables)
        {
            <MudExpansionPanel @key="table.TableDefinitionId">
                <TitleContent>
                    <div class="d-flex align-center" style="width: 100%;">
                        <MudIcon Icon="@Icons.Material.Filled.TableChart"
                                 Color="Color.Primary" Class="mr-2" />
                        <MudText Typo="Typo.subtitle1">@table.DisplayName</MudText>
                        <MudChip T="string" Size="Size.Small" Color="Color.Info"
                                 Class="ml-2">
                            @table.TableName
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Secondary"
                                 Class="ml-2">
                            @table.EntityType
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Class="ml-2">
                            v@table.Version
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Warning"
                                 Class="ml-2">
                            @table.Columns.Count 列
                        </MudChip>
                        <MudSpacer />
                        @* ★ 新增：编辑表入口 *@
                        <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                       Color="Color.Primary"
                                       Size="Size.Small"
                                       OnClick="@(() => OpenEditTableDialog(table))"
                                       title="编辑显示名 / 顺序" />
                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                       Color="Color.Error"
                                       Size="Size.Small"
                                       OnClick="@(async () => await DeleteTableAsync(table))"
                                       title="删除自定义表" />
                    </div>
                </TitleContent>

                <ChildContent>
                    <div class="d-flex mb-3">
                        <MudButton Size="Size.Small"
                                   Variant="Variant.Outlined"
                                   Color="Color.Primary"
                                   StartIcon="@Icons.Material.Filled.Add"
                                   OnClick="@(() => OpenAddColumnDialog(table))">
                            添加列
                        </MudButton>
                    </div>

                    @if (table.Columns.Count == 0)
                    {
                        <MudAlert Severity="Severity.Info">
                            该自定义表暂无列
                        </MudAlert>
                    }
                    else
                    {
                        <MudTable Items="table.Columns" Bordered="true"
                                  Hover="true">
                            <HeaderContent>
                                <MudTh>列名</MudTh>
                                <MudTh>显示名</MudTh>
                                <MudTh>类型</MudTh>
                                <MudTh>必填</MudTh>
                                <MudTh>可搜索</MudTh>
                                <MudTh>可排序</MudTh>
                                <MudTh>唯一</MudTh>
                                <MudTh>顺序</MudTh>
                                <MudTh>操作</MudTh>
                            </HeaderContent>
                            <RowTemplate>
                                <MudTd>
                                    <code>@context.ColumnName</code>
                                </MudTd>
                                <MudTd>@context.DisplayName</MudTd>
                                <MudTd>
                                    <MudChip T="string" Size="Size.Small"
                                             Color="@GetDataTypeColor(context.DataType)">
                                        @context.DataType
                                    </MudChip>
                                </MudTd>
                                <MudTd>
                                    @if (context.IsRequired)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsSearchable)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsSortable)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsUnique)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Check"
                                                 Color="Color.Success" Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>@context.DisplayOrder</MudTd>
                                <MudTd>
                                    <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                                   Size="Size.Small"
                                                   Color="Color.Primary"
                                                   OnClick="@(() => OpenEditColumnDialog(table, context))" />
                                    <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                                   Size="Size.Small"
                                                   Color="Color.Error"
                                                   OnClick="@(async () => await DeleteColumnAsync(table, context))" />
                                </MudTd>
                            </RowTemplate>
                        </MudTable>
                    }
                </ChildContent>
            </MudExpansionPanel>
        }
    </MudExpansionPanels>
}

@* ============================================================ *@
@* 新建自定义表 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showCreateTableDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">新建自定义表</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_newTable.EntityType"
                      Label="实体类型"
                      Placeholder="Product"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newTable.TableName"
                      Label="表名 (snake_case)"
                      Placeholder="specs"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newTable.DisplayName"
                      Label="显示名"
                      Placeholder="规格参数"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudNumericField T="int"
                         @bind-Value="_newTable.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showCreateTableDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="CreateTableAsync"
                   Disabled="@_submitting">
            @(_submitting ? "创建中..." : "创建")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* ★ 编辑自定义表（后端支持改 DisplayName / DisplayOrder） *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showEditTableDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">编辑自定义表</MudText>
        @if (_editingTable is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingTable.EntityType / @_editingTable.TableName
                （表名与实体类型不可修改）
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_editTableForm.DisplayName"
                      Label="显示名"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudNumericField T="int?"
                         @bind-Value="_editTableForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />
        <MudAlert Severity="Severity.Info">
            TableName 与 EntityType 是引用标识，不可修改。如需更名，请新建并迁移引用。
        </MudAlert>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showEditTableDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="UpdateTableAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 添加 / 编辑列 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showColumnDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">
            @(_editingColumn is null ? "添加列" : "编辑列")
        </MudText>
        @if (_currentTable is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_currentTable.DisplayName (@_currentTable.TableName)
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_columnForm.ColumnName"
                      Label="列名 (snake_case)"
                      Placeholder="weight"
                      Variant="Variant.Outlined"
                      Disabled="@(_editingColumn is not null)"
                      Class="mb-3" />
        <MudTextField @bind-Value="_columnForm.DisplayName"
                      Label="显示名"
                      Placeholder="重量"
                      Variant="Variant.Outlined"
                      Class="mb-3" />

        <MudSelect T="string"
                   Value="_columnForm.DataType"
                   ValueChanged="OnColumnDataTypeChanged"
                   Label="数据类型"
                   Variant="Variant.Outlined"
                   Disabled="@(_editingColumn is not null)"
                   Class="mb-3">
            @foreach (var dt in DataTypeOptions)
            {
                <MudSelectItem T="string" Value="@dt.Value">@dt.Label</MudSelectItem>
            }
        </MudSelect>

        @if (_columnForm.DataType == "composite")
        {
            <MudSelect T="string"
                       Value="@(_columnForm.RefCompositeTypeId ?? "")"
                       ValueChanged="@(v => _columnForm.RefCompositeTypeId = string.IsNullOrEmpty(v) ? null : v)"
                       Label="嵌套组合类型"
                       Variant="Variant.Outlined"
                       Class="mb-3">
                @foreach (var t in _compositeTypes ?? (IReadOnlyList<CompositeTypeDetailDto>)Array.Empty<CompositeTypeDetailDto>())
                {
                    <MudSelectItem T="string" Value="@t.CompositeTypeId">
                        @t.DisplayName (@t.TypeName)
                    </MudSelectItem>
                }
            </MudSelect>
        }

        <MudNumericField T="int"
                         @bind-Value="_columnForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudGrid>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsRequired"
                           Label="必填"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsSearchable"
                           Label="可搜索"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsSortable"
                           Label="可排序"
                           Color="Color.Primary" />
            </MudItem>
            <MudItem xs="12" md="3">
                <MudSwitch T="bool"
                           @bind-Value="_columnForm.IsUnique"
                           Label="唯一"
                           Color="Color.Primary" />
            </MudItem>
        </MudGrid>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showColumnDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="SaveColumnAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private string? _entityType;
    private IReadOnlyList<CustomTableDetailDto>? _tables;
    private IReadOnlyList<CompositeTypeDetailDto>? _compositeTypes;
    private bool _loading;
    private bool _initialized;

    private bool _submitting;
    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };

    // 新建自定义表
    private bool _showCreateTableDialog;
    private CreateCustomTableRequest _newTable = new() { EntityType = "Product" };

    // ★ 编辑自定义表
    private bool _showEditTableDialog;
    private CustomTableDetailDto? _editingTable;
    private UpdateCustomTableRequest _editTableForm = new();

    // 添加 / 编辑列
    private bool _showColumnDialog;
    private CustomTableDetailDto? _currentTable;
    private CustomTableColumnDto? _editingColumn;
    private ColumnForm _columnForm = new();

    private static readonly (string Value, string Label)[] DataTypeOptions =
    {
        ("string",   "字符串"),
        ("int",      "整数"),
        ("decimal",  "小数"),
        ("bool",     "布尔"),
        ("datetime", "日期时间"),
        ("date",     "日期"),
        ("time",     "时间"),
        ("single_choice", "单选"),
        ("composite", "嵌套组合"),
        ("json",     "JSON")
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        await LoadCompositeTypesAsync();
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadCompositeTypesAsync()
    {
        _compositeTypes = await Api.ListCompositeTypesAsync();
        if (_compositeTypes is null)
            Snackbar.Add("组合类型加载失败（列若需嵌套类型将不可选）", Severity.Warning);
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _tables = await Api.ListCustomTablesAsync(_entityType);
            if (_tables is null)
            {
                Snackbar.Add("加载失败，请检查后端服务", Severity.Error);
                _tables = Array.Empty<CustomTableDetailDto>();
            }
        }
        finally
        {
            _loading = false;
        }
    }

    // ---------- 新建自定义表 ----------

    private void OpenCreateTableDialog()
    {
        _newTable = new CreateCustomTableRequest { EntityType = "Product" };
        _showCreateTableDialog = true;
    }

    private async Task CreateTableAsync()
    {
        if (string.IsNullOrWhiteSpace(_newTable.EntityType) ||
            string.IsNullOrWhiteSpace(_newTable.TableName) ||
            string.IsNullOrWhiteSpace(_newTable.DisplayName))
        {
            Snackbar.Add("请填写所有字段", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var id = await Api.CreateCustomTableAsync(_newTable);
            if (id is null)
            {
                Snackbar.Add("创建失败", Severity.Error);
                return;
            }

            Snackbar.Add($"创建成功：Id={id}", Severity.Success);
            _showCreateTableDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- ★ 编辑自定义表 ----------

    private void OpenEditTableDialog(CustomTableDetailDto table)
    {
        _editingTable = table;
        _editTableForm = new UpdateCustomTableRequest
        {
            DisplayName = table.DisplayName,
            DisplayOrder = table.DisplayOrder
        };
        _showEditTableDialog = true;
    }

    private async Task UpdateTableAsync()
    {
        if (_editingTable is null) return;

        if (string.IsNullOrWhiteSpace(_editTableForm.DisplayName))
        {
            Snackbar.Add("显示名不能为空", Severity.Warning);
            return;
        }

        // 未变更则直接关闭，避免无谓请求（后端 null 语义为"不修改"）
        var unchanged =
            _editTableForm.DisplayName == _editingTable.DisplayName &&
            _editTableForm.DisplayOrder == _editingTable.DisplayOrder;

        if (unchanged)
        {
            _showEditTableDialog = false;
            return;
        }

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateCustomTableAsync(
                _editingTable.TableDefinitionId,
                _editTableForm);

            if (!ok)
            {
                Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("更新成功", Severity.Success);
            _showEditTableDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 删除自定义表 ----------

    private async Task DeleteTableAsync(CustomTableDetailDto table)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除自定义表「{table.DisplayName}」？此操作将同时标记所有列为删除。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteCustomTableAsync(table.TableDefinitionId);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 添加 / 编辑列 ----------

    private void OpenAddColumnDialog(CustomTableDetailDto table)
    {
        _currentTable = table;
        _editingColumn = null;
        _columnForm = new ColumnForm
        {
            DisplayOrder = table.Columns.Count + 1,
            IsSearchable = true
        };
        _showColumnDialog = true;
    }

    private void OpenEditColumnDialog(
        CustomTableDetailDto table, CustomTableColumnDto column)
    {
        _currentTable = table;
        _editingColumn = column;
        _columnForm = new ColumnForm
        {
            ColumnName = column.ColumnName,
            DisplayName = column.DisplayName,
            DataType = column.DataType,
            RefCompositeTypeId = column.RefCompositeTypeId,
            IsRequired = column.IsRequired,
            IsSearchable = column.IsSearchable,
            IsSortable = column.IsSortable,
            IsUnique = column.IsUnique,
            DisplayOrder = column.DisplayOrder
        };
        _showColumnDialog = true;
    }

    private void OnColumnDataTypeChanged(string dt)
    {
        _columnForm.DataType = dt;
        if (dt != "composite") _columnForm.RefCompositeTypeId = null;
    }

    private async Task SaveColumnAsync()
    {
        if (_currentTable is null) return;

        if (string.IsNullOrWhiteSpace(_columnForm.ColumnName) ||
            string.IsNullOrWhiteSpace(_columnForm.DisplayName))
        {
            Snackbar.Add("请填写列名和显示名", Severity.Warning);
            return;
        }

        if (_columnForm.DataType == "composite" &&
            _columnForm.RefCompositeTypeId is null)
        {
            Snackbar.Add("组合类型列必须指定嵌套类型", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            if (_editingColumn is null)
            {
                var id = await Api.AddCustomTableColumnAsync(
                    _currentTable.TableDefinitionId,
                    new CreateTableColumnRequest
                    {
                        ColumnName = _columnForm.ColumnName,
                        DisplayName = _columnForm.DisplayName,
                        DataType = _columnForm.DataType,
                        RefCompositeTypeId = _columnForm.RefCompositeTypeId,
                        IsRequired = _columnForm.IsRequired,
                        IsSearchable = _columnForm.IsSearchable,
                        IsSortable = _columnForm.IsSortable,
                        IsUnique = _columnForm.IsUnique,
                        DisplayOrder = _columnForm.DisplayOrder
                    });

                if (id is null)
                {
                    Snackbar.Add("添加失败", Severity.Error);
                    return;
                }
                Snackbar.Add($"添加成功：Id={id}", Severity.Success);
            }
            else
            {
                var (ok, error) = await Api.UpdateTableColumnAsync(
                    _currentTable.TableDefinitionId,
                    _editingColumn.ColumnId,
                    new UpdateTableColumnRequest
                    {
                        DisplayName = _columnForm.DisplayName,
                        IsRequired = _columnForm.IsRequired,
                        IsSearchable = _columnForm.IsSearchable,
                        IsSortable = _columnForm.IsSortable,
                        IsUnique = _columnForm.IsUnique,
                        DisplayOrder = _columnForm.DisplayOrder
                    });

                if (!ok)
                {
                    Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                    return;
                }
                Snackbar.Add("更新成功", Severity.Success);
            }

            _showColumnDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 删除列 ----------

    private async Task DeleteColumnAsync(
        CustomTableDetailDto table, CustomTableColumnDto column)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除列「{column.DisplayName}」？",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteTableColumnAsync(
            table.TableDefinitionId, column.ColumnId);

        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 辅助 ----------

    private static Color GetDataTypeColor(string dataType) => dataType switch
    {
        "string" => Color.Info,
        "int" or "decimal" => Color.Primary,
        "bool" => Color.Success,
        "datetime" or "date" or "time" => Color.Warning,
        "single_choice" => Color.Tertiary,
        "composite" => Color.Dark,
        _ => Color.Default
    };

    private class ColumnForm
    {
        public string ColumnName { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string DataType { get; set; } = "string";
        public string? RefCompositeTypeId { get; set; }
        public bool IsRequired { get; set; }
        public bool IsSearchable { get; set; }
        public bool IsSortable { get; set; }
        public bool IsUnique { get; set; }
        public int DisplayOrder { get; set; }
    }
}
```

## 文件 31/46 TreeGraph.Blazor/Components/Pages/Metadata/OptionSets.razor

```razor
@page "/metadata/option-sets"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@* OptionSets.razor *@
<PageTitle>选项集管理</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">选项集管理</MudText>

<MudPaper Class="pa-4 mb-4">
    <MudGrid>
        <MudItem xs="12" md="4" Class="d-flex align-center">
            <MudTextField @bind-Value="_entityType" Label="实体类型（可选）"
                          Variant="Variant.Outlined" Margin="Margin.Dense"
                          Placeholder="留空显示全部（含 Shared）" />
        </MudItem>
        <MudItem xs="12" md="2" Class="d-flex align-center">
            @* ★ 显示已删除开关 *@
            <MudSwitch T="bool" @bind-Value="_includeDeleted"
                       Label="显示已删除"
                       Color="Color.Warning" />
        </MudItem>
        <MudItem xs="12" md="6" class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="LoadAsync" Disabled="_loading"
                       StartIcon="@Icons.Material.Filled.Refresh">
                刷新
            </MudButton>
            <MudButton Variant="Variant.Filled" Color="Color.Success"
                       OnClick="OpenCreateSetDialog"
                       StartIcon="@Icons.Material.Filled.Add"
                       Class="ml-2">
                新建选项集
            </MudButton>
        </MudItem>
    </MudGrid>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_sets is null || _sets.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.Checklist"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">暂无选项集</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            点击"新建选项集"创建第一个
        </MudText>
    </MudPaper>
}
else
{
    <MudExpansionPanels MultiExpansion="true">
        @foreach (var set in _sets)
        {
            var currentSet = set;
            var items = _itemsCache.TryGetValue(currentSet.OptionSetId, out var list)
                ? list
                : new List<OptionItemDetailDto>();
            var activeItems = items.Where(i => !i.IsDeleted).ToList();
            var deletedCount = items.Count - activeItems.Count;

            <MudExpansionPanel @key="currentSet.OptionSetId">
                <TitleContent>
                    <div class="d-flex align-center" style="width: 100%;">
                        <MudIcon Icon="@Icons.Material.Filled.Checklist"
                                 Color="@(currentSet.IsDeleted ? Color.Error : Color.Primary)"
                                 Class="mr-2" />
                        <MudText Typo="Typo.subtitle1">
                            @currentSet.DisplayName
                        </MudText>
                        <MudChip T="string" Size="Size.Small" Color="Color.Info"
                                 Class="ml-2">
                            @currentSet.SetName
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Secondary"
                                 Class="ml-2">
                            @currentSet.EntityType
                        </MudChip>
                        <MudChip T="string" Size="Size.Small" Color="Color.Warning"
                                 Class="ml-2">
                            @activeItems.Count 项
                        </MudChip>
                        @if (deletedCount > 0)
                        {
                            <MudChip T="string" Size="Size.Small" Color="Color.Error"
                                     Class="ml-2">
                                @deletedCount 已删除
                            </MudChip>
                        }
                        @* ★ 集合级已删除标记 *@
                        @if (currentSet.IsDeleted)
                        {
                            <MudChip T="string" Size="Size.Small" Color="Color.Error"
                                     Class="ml-2">
                                集合已删除
                            </MudChip>
                        }
                        <MudSpacer />
                        @if (!currentSet.IsDeleted)
                        {
                            @* 批量恢复选项（仅存在已删除项时显示） *@
                            @if (deletedCount > 0)
                            {
                                <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                               Color="Color.Success"
                                               Size="Size.Small"
                                               OnClick="@(async () => await UndeleteAllItemsAsync(currentSet))"
                                               title="@($"恢复全部 {deletedCount} 个已删除选项")" />
                            }
                            <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                           Color="Color.Primary"
                                           Size="Size.Small"
                                           OnClick="@(() => OpenEditSetDialog(currentSet))"
                                           title="编辑选项集" />
                            <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                           Color="Color.Error"
                                           Size="Size.Small"
                                           OnClick="@(async () => await DeleteSetAsync(currentSet))"
                                           title="删除选项集" />
                        }
                        else
                        {
                            @* ★ 集合级恢复 *@
                            <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                           Color="Color.Success"
                                           Size="Size.Small"
                                           OnClick="@(async () => await UndeleteSetAsync(currentSet))"
                                           title="恢复选项集（含全部选项）" />
                        }
                    </div>
                </TitleContent>

                <ChildContent>
                    @* 已删除集合不显示"添加选项" *@
                    @if (!currentSet.IsDeleted)
                    {
                        <div class="d-flex mb-3">
                            <MudButton Size="Size.Small"
                                       Variant="Variant.Outlined"
                                       Color="Color.Primary"
                                       StartIcon="@Icons.Material.Filled.Add"
                                       OnClick="@(() => OpenAddItemDialog(currentSet))">
                                添加选项
                            </MudButton>
                        </div>
                    }

                    @if (items.Count == 0)
                    {
                        <MudAlert Severity="Severity.Info">
                            该选项集暂无选项
                        </MudAlert>
                    }
                    else
                    {
                        <MudTable Items="items" Bordered="true"
                                  Hover="true">
                            <HeaderContent>
                                <MudTh>Value</MudTh>
                                <MudTh>Label</MudTh>
                                <MudTh>顺序</MudTh>
                                <MudTh>默认</MudTh>
                                <MudTh>状态</MudTh>
                                <MudTh>操作</MudTh>
                            </HeaderContent>
                            <RowTemplate>
                                <MudTd>
                                    <code>@context.Value</code>
                                </MudTd>
                                <MudTd>@context.Label</MudTd>
                                <MudTd>@context.DisplayOrder</MudTd>
                                <MudTd>
                                    @if (context.IsDefault)
                                    {
                                        <MudIcon Icon="@Icons.Material.Filled.Star"
                                                 Color="Color.Warning"
                                                 Size="Size.Small" />
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (context.IsDeleted)
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Error">
                                            已删除
                                        </MudChip>
                                    }
                                    else
                                    {
                                        <MudChip T="string" Size="Size.Small"
                                                 Color="Color.Success">
                                            启用
                                        </MudChip>
                                    }
                                </MudTd>
                                <MudTd>
                                    @if (!context.IsDeleted)
                                    {
                                        <MudIconButton Icon="@Icons.Material.Filled.KeyboardArrowUp"
                                                       Size="Size.Small"
                                                       OnClick="@(async () => await MoveUpAsync(currentSet, context))"
                                                       title="上移" />
                                        <MudIconButton Icon="@Icons.Material.Filled.KeyboardArrowDown"
                                                       Size="Size.Small"
                                                       OnClick="@(async () => await MoveDownAsync(currentSet, context))"
                                                       title="下移" />
                                        @if (!context.IsDefault)
                                        {
                                            <MudIconButton Icon="@Icons.Material.Filled.StarBorder"
                                                           Size="Size.Small"
                                                           Color="Color.Warning"
                                                           OnClick="@(async () => await SetDefaultAsync(currentSet, context))"
                                                           title="设为默认" />
                                        }
                                        <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                                       Size="Size.Small"
                                                       Color="Color.Primary"
                                                       OnClick="@(() => OpenEditItemDialog(currentSet, context))"
                                                       title="编辑" />
                                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                                       Size="Size.Small"
                                                       Color="Color.Error"
                                                       OnClick="@(async () => await DeleteItemAsync(currentSet, context))"
                                                       title="删除" />
                                    }
                                    else
                                    {
                                        <MudIconButton Icon="@Icons.Material.Filled.RestoreFromTrash"
                                                       Size="Size.Small"
                                                       Color="Color.Success"
                                                       OnClick="@(async () => await UndeleteItemAsync(currentSet, context))"
                                                       title="恢复" />
                                    }
                                </MudTd>
                            </RowTemplate>
                        </MudTable>
                    }
                </ChildContent>
            </MudExpansionPanel>
        }
    </MudExpansionPanels>
}

@* ============================================================ *@
@* 新建选项集 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showCreateSetDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">新建选项集</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_newSet.EntityType"
                      Label="实体类型"
                      HelperText="填 Shared 表示全局共享"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newSet.SetName"
                      Label="集合名 (snake_case)"
                      Placeholder="gender"
                      Variant="Variant.Outlined"
                      Class="mb-3" />
        <MudTextField @bind-Value="_newSet.DisplayName"
                      Label="显示名"
                      Placeholder="性别"
                      Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showCreateSetDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="CreateSetAsync"
                   Disabled="@_submitting">
            @(_submitting ? "创建中..." : "创建")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 编辑选项集 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showEditSetDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">编辑选项集</MudText>
        @if (_editingSet is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingSet.EntityType / @_editingSet.SetName（不可修改）
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_editSetForm.DisplayName"
                      Label="显示名"
                      Variant="Variant.Outlined" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showEditSetDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="UpdateSetAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 添加 / 编辑选项 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showItemDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">
            @(_editingItem is null ? "添加选项" : "编辑选项")
        </MudText>
        @if (_currentSet is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_currentSet.DisplayName (@_currentSet.SetName)
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_itemForm.Value"
                      Label="Value（存储值，如 male）"
                      Variant="Variant.Outlined"
                      Disabled="@(_editingItem is not null)"
                      Class="mb-3" />
        @if (_editingItem is not null)
        {
            <MudAlert Severity="Severity.Info" Class="mb-3">
                Value 是存储的稳定标识，不可修改。如需变更，请新建选项并迁移数据。
            </MudAlert>
        }

        <MudTextField @bind-Value="_itemForm.Label"
                      Label="Label（显示名，如 男）"
                      Variant="Variant.Outlined"
                      Class="mb-3" />

        <MudNumericField T="int"
                         @bind-Value="_itemForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudSwitch T="bool"
                   @bind-Value="_itemForm.IsDefault"
                   Label="设为默认选项"
                   Color="Color.Primary" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showItemDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="SaveItemAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private string? _entityType;
    private bool _includeDeleted;               // ★ 新增
    private IReadOnlyList<OptionSetSummaryDto>? _sets;
    private readonly Dictionary<string, List<OptionItemDetailDto>> _itemsCache = new();
    private bool _loading;
    private bool _initialized;

    private bool _submitting;
    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

    private bool _showCreateSetDialog;
    private CreateOptionSetRequest _newSet = new() { EntityType = "Shared" };

    private bool _showEditSetDialog;
    private OptionSetSummaryDto? _editingSet;
    private UpdateOptionSetRequest _editSetForm = new();

    private bool _showItemDialog;
    private OptionSetSummaryDto? _currentSet;
    private OptionItemDetailDto? _editingItem;
    private OptionItemForm _itemForm = new();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            // ★ 传 includeDeleted
            _sets = await Api.ListOptionSetsAsync(_entityType, _includeDeleted);
            if (_sets is null)
            {
                Snackbar.Add("加载失败，请检查后端服务", Severity.Error);
                _sets = Array.Empty<OptionSetSummaryDto>();
                _itemsCache.Clear();
                return;
            }

            if (_sets.Count == 0)
            {
                _itemsCache.Clear();
                return;
            }

            var tasks = _sets.Select(async set =>
            {
                var items = await Api.ListOptionItemsAsync(set.OptionSetId);
                return (set.OptionSetId, Items: items);
            });

            var results = await Task.WhenAll(tasks);

            _itemsCache.Clear();
            int failedCount = 0;
            foreach (var (setId, items) in results)
            {
                if (items is null) failedCount++;
                _itemsCache[setId] = items?.ToList() ?? new List<OptionItemDetailDto>();
            }

            if (failedCount > 0)
                Snackbar.Add($"{failedCount} 个选项集的选项加载失败", Severity.Warning);
        }
        finally
        {
            _loading = false;
        }
    }

    private async Task ReloadItemsAsync(string optionSetId)
    {
        var items = await Api.ListOptionItemsAsync(optionSetId);
        _itemsCache[optionSetId] = items?.ToList() ?? new List<OptionItemDetailDto>();
    }

    // ---------- 选项集 CRUD ----------

    private void OpenCreateSetDialog()
    {
        _newSet = new CreateOptionSetRequest { EntityType = "Shared" };
        _showCreateSetDialog = true;
    }

    private async Task CreateSetAsync()
    {
        if (string.IsNullOrWhiteSpace(_newSet.EntityType) ||
            string.IsNullOrWhiteSpace(_newSet.SetName) ||
            string.IsNullOrWhiteSpace(_newSet.DisplayName))
        {
            Snackbar.Add("请填写所有字段", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var id = await Api.CreateOptionSetAsync(_newSet);
            if (id is null)
            {
                Snackbar.Add("创建失败", Severity.Error);
                return;
            }
            Snackbar.Add($"创建成功：Id={id}", Severity.Success);
            _showCreateSetDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    private void OpenEditSetDialog(OptionSetSummaryDto set)
    {
        _editingSet = set;
        _editSetForm = new UpdateOptionSetRequest { DisplayName = set.DisplayName };
        _showEditSetDialog = true;
    }

    private async Task UpdateSetAsync()
    {
        if (_editingSet is null) return;

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateOptionSetAsync(
                _editingSet.OptionSetId, _editSetForm);

            if (!ok)
            {
                Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("更新成功", Severity.Success);
            _showEditSetDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task DeleteSetAsync(OptionSetSummaryDto set)
    {
        var refs = await Api.GetOptionSetReferencesAsync(set.OptionSetId);
        if (refs is not null && refs.Count > 0)
        {
            var refList = string.Join("\n",
                refs.Select(r => $"  · {r.EntityType}.{r.AttributeName} ({r.DisplayName})"));
            await DialogService.ShowMessageBoxAsync(
                "无法删除",
                $"选项集「{set.DisplayName}」仍被以下属性引用：\n{refList}\n\n请先解除引用。",
                yesText: "知道了");
            return;
        }

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除选项集「{set.DisplayName}」？\n\n" +
            "集合与其所有选项将被软删除，可通过「显示已删除」+「恢复」还原。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteOptionSetAsync(set.OptionSetId);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- ★ 恢复选项集 ----------

    private async Task UndeleteSetAsync(OptionSetSummaryDto set)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复选项集「{set.DisplayName}」({set.SetName})？\n\n" +
            "恢复后其所有选项将一并恢复（「默认」标记需手动重设）。",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteOptionSetAsync(set.OptionSetId);
        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 批量恢复已删除选项 ----------

    private async Task UndeleteAllItemsAsync(OptionSetSummaryDto set)
    {
        if (!_itemsCache.TryGetValue(set.OptionSetId, out var items)) return;

        var deletedCount = items.Count(i => i.IsDeleted);
        if (deletedCount == 0) return;

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认批量恢复",
            $"确定恢复选项集「{set.DisplayName}」中全部 {deletedCount} 个已删除选项？\n\n" +
            "· 若有选项的 Value 与活动项冲突，整批恢复将被拒绝。\n" +
            "· 恢复后「默认」标记需手动重设。",
            yesText: "恢复全部", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteOptionSetAsync(set.OptionSetId);

        if (ok)
        {
            Snackbar.Add($"已恢复 {deletedCount} 个选项", Severity.Success);
            await ReloadItemsAsync(set.OptionSetId);
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 选项 CRUD ----------

    private void OpenAddItemDialog(OptionSetSummaryDto set)
    {
        _currentSet = set;
        _editingItem = null;
        var existingCount = _itemsCache.TryGetValue(set.OptionSetId, out var items)
            ? items.Count(i => !i.IsDeleted)
            : 0;
        _itemForm = new OptionItemForm { DisplayOrder = existingCount + 1 };
        _showItemDialog = true;
    }

    private void OpenEditItemDialog(
        OptionSetSummaryDto set, OptionItemDetailDto item)
    {
        _currentSet = set;
        _editingItem = item;
        _itemForm = new OptionItemForm
        {
            Value = item.Value,
            Label = item.Label,
            DisplayOrder = item.DisplayOrder,
            IsDefault = item.IsDefault
        };
        _showItemDialog = true;
    }

    private async Task SaveItemAsync()
    {
        if (_currentSet is null) return;

        if (string.IsNullOrWhiteSpace(_itemForm.Value) ||
            string.IsNullOrWhiteSpace(_itemForm.Label))
        {
            Snackbar.Add("请填写 Value 和 Label", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            if (_editingItem is null)
            {
                var id = await Api.AddOptionItemAsync(
                    _currentSet.OptionSetId,
                    new CreateOptionItemRequest
                    {
                        Value = _itemForm.Value,
                        Label = _itemForm.Label,
                        DisplayOrder = _itemForm.DisplayOrder,
                        IsDefault = _itemForm.IsDefault
                    });

                if (id is null)
                {
                    Snackbar.Add("添加失败（Value 可能已存在）", Severity.Error);
                    return;
                }
                Snackbar.Add($"添加成功：Id={id}", Severity.Success);
            }
            else
            {
                var (ok, error) = await Api.UpdateOptionItemAsync(
                    _currentSet.OptionSetId,
                    _editingItem.OptionItemId,
                    new UpdateOptionItemRequest
                    {
                        Label = _itemForm.Label,
                        DisplayOrder = _itemForm.DisplayOrder,
                        IsDefault = _itemForm.IsDefault
                    });

                if (!ok)
                {
                    Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                    return;
                }
                Snackbar.Add("更新成功", Severity.Success);
            }

            _showItemDialog = false;
            await ReloadItemsAsync(_currentSet.OptionSetId);
        }
        finally
        {
            _submitting = false;
        }
    }

    private async Task DeleteItemAsync(
        OptionSetSummaryDto set, OptionItemDetailDto item)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除选项「{item.Label}」({item.Value})？" +
            "已有数据中引用的该值仍会保留，读取时降级显示。",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteOptionItemAsync(set.OptionSetId, item.OptionItemId);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await ReloadItemsAsync(set.OptionSetId);
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- ★ 恢复单个选项 ----------

    private async Task UndeleteItemAsync(
        OptionSetSummaryDto set, OptionItemDetailDto item)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认恢复",
            $"确定恢复选项「{item.Label}」({item.Value})？",
            yesText: "恢复", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.UndeleteOptionItemAsync(
            set.OptionSetId, item.OptionItemId);

        if (ok)
        {
            Snackbar.Add("恢复成功", Severity.Success);
            await ReloadItemsAsync(set.OptionSetId);
        }
        else
        {
            Snackbar.Add($"恢复失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 设为默认 ----------

    private async Task SetDefaultAsync(
        OptionSetSummaryDto set, OptionItemDetailDto item)
    {
        var (ok, error) = await Api.UpdateOptionItemAsync(
            set.OptionSetId, item.OptionItemId,
            new UpdateOptionItemRequest { IsDefault = true });

        if (!ok)
        {
            Snackbar.Add($"设置默认失败：{error ?? "未知错误"}", Severity.Error);
            return;
        }

        Snackbar.Add($"已将「{item.Label}」设为默认", Severity.Success);
        await ReloadItemsAsync(set.OptionSetId);
    }

    // ---------- 重排序（上移 / 下移） ----------

    private async Task MoveUpAsync(
        OptionSetSummaryDto set, OptionItemDetailDto item)
    {
        var items = _itemsCache[set.OptionSetId]
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.OptionItemId)
            .ToList();

        var idx = items.FindIndex(i => i.OptionItemId == item.OptionItemId);
        if (idx <= 0) return;

        (items[idx - 1], items[idx]) = (items[idx], items[idx - 1]);
        await ApplyReorderAsync(set.OptionSetId, items);
    }

    private async Task MoveDownAsync(
        OptionSetSummaryDto set, OptionItemDetailDto item)
    {
        var items = _itemsCache[set.OptionSetId]
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.DisplayOrder)
            .ThenBy(i => i.OptionItemId)
            .ToList();

        var idx = items.FindIndex(i => i.OptionItemId == item.OptionItemId);
        if (idx < 0 || idx >= items.Count - 1) return;

        (items[idx + 1], items[idx]) = (items[idx], items[idx + 1]);
        await ApplyReorderAsync(set.OptionSetId, items);
    }

    private async Task ApplyReorderAsync(
        string optionSetId, List<OptionItemDetailDto> orderedItems)
    {
        var orders = orderedItems
            .Select((it, i) => new ReorderOptionItem
            {
                OptionItemId = it.OptionItemId,
                DisplayOrder = i + 1
            })
            .ToList();

        var (ok, error) = await Api.ReorderOptionItemsAsync(optionSetId, orders);

        if (!ok)
            Snackbar.Add($"重排序失败：{error ?? "未知错误"}", Severity.Error);

        await ReloadItemsAsync(optionSetId);
    }

    // ---------- 辅助 ----------

    private class OptionItemForm
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
        public int DisplayOrder { get; set; }
        public bool IsDefault { get; set; }
    }
}
```

## 文件 32/46 TreeGraph.Blazor/Components/Pages/Metadata/Units.razor

```razor
@page "/metadata/units"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IDialogService DialogService
@* Units.razor *@
<PageTitle>单位管理</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">单位管理</MudText>

<MudPaper Class="pa-4 mb-4">
    <div class="d-flex align-center">
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   OnClick="LoadAsync" Disabled="_loading"
                   StartIcon="@Icons.Material.Filled.Refresh">
            刷新
        </MudButton>
        <MudButton Variant="Variant.Filled" Color="Color.Success"
                   OnClick="OpenCreate"
                   StartIcon="@Icons.Material.Filled.Add"
                   Class="ml-2">
            新建单位
        </MudButton>
    </div>
</MudPaper>

@if (_loading)
{
    <MudProgressLinear Indeterminate="true" Color="Color.Primary" />
}
else if (_categories is null || _categories.Count == 0)
{
    <MudPaper Class="pa-8 text-center">
        <MudIcon Icon="@Icons.Material.Filled.Straighten"
                 Size="Size.Large" Color="Color.Default" />
        <MudText Typo="Typo.h6" Class="mt-2">暂无单位</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            点击"新建单位"创建第一个
        </MudText>
    </MudPaper>
}
else
{
    @foreach (var c in _categories)
    {
        <MudPaper Class="pa-4 mb-4">
            <div class="d-flex align-center mb-2">
                <MudText Typo="Typo.h6">@c.Category</MudText>
                @if (c.BaseUnit is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Success"
                             Class="ml-2">
                        基准: @c.BaseUnit.Symbol
                    </MudChip>
                }
                else
                {
                    @* ★ 无基准分类的"重新指定"入口 *@
                    <MudChip T="string" Size="Size.Small" Color="Color.Warning"
                             Class="ml-2">
                        无基准单位
                    </MudChip>
                    @if (c.Units.Count > 0)
                    {
                        <MudButton Size="Size.Small"
                                   Variant="Variant.Outlined"
                                   Color="Color.Warning"
                                   StartIcon="@Icons.Material.Filled.Star"
                                   Class="ml-2"
                                   OnClick="@(() => OpenAssignBaseDialog(c))">
                            指定基准单位
                        </MudButton>
                    }
                }
            </div>
            <MudTable Items="c.Units" Hover="true" Bordered="true">
                <HeaderContent>
                    <MudTh>名称</MudTh>
                    <MudTh>符号</MudTh>
                    <MudTh>换算系数</MudTh>
                    <MudTh>顺序</MudTh>
                    <MudTh>基准</MudTh>
                    <MudTh>操作</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd>@context.Name</MudTd>
                    <MudTd>@context.Symbol</MudTd>
                    <MudTd>@context.ToBaseFactor</MudTd>
                    <MudTd>@context.DisplayOrder</MudTd>
                    <MudTd>
                        @if (context.IsBaseUnit)
                        {
                            <MudIcon Icon="@Icons.Material.Filled.Check"
                                     Color="Color.Success" />
                        }
                    </MudTd>
                    <MudTd>
                        <MudIconButton Icon="@Icons.Material.Filled.Edit"
                                       Size="Size.Small"
                                       Color="Color.Primary"
                                       OnClick="@(() => OpenEdit(context))"
                                       title="编辑" />
                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                       Size="Size.Small"
                                       Color="Color.Error"
                                       OnClick="@(async () => await DeleteAsync(context))"
                                       title="删除" />
                    </MudTd>
                </RowTemplate>
            </MudTable>
        </MudPaper>
    }
}

@* ============================================================ *@
@* 新建单位 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showCreateDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">新建单位</MudText>
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_newUnit.Category" Label="分类"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudTextField @bind-Value="_newUnit.Name" Label="名称"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudTextField @bind-Value="_newUnit.Symbol" Label="符号"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudNumericField T="decimal" @bind-Value="_newUnit.ToBaseFactor"
                         Label="换算系数（相对基准单位）"
                         Variant="Variant.Outlined" Class="mb-3" />
        <MudSwitch T="bool" @bind-Value="_newUnit.IsBaseUnit"
                   Label="基准单位（每分类只能一个）" />
        <MudNumericField T="int" @bind-Value="_newUnit.DisplayOrder"
                         Label="显示顺序" Variant="Variant.Outlined"
                         Class="mt-3" />
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showCreateDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="CreateAsync"
                   Disabled="@_submitting">
            @(_submitting ? "创建中..." : "创建")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 编辑单位（含高级操作） *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showEditDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">编辑单位</MudText>
        @if (_editingUnit is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingUnit.Category / @_editingUnit.Name
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudTextField @bind-Value="_editForm.Name" Label="名称"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudTextField @bind-Value="_editForm.Symbol" Label="符号"
                      Variant="Variant.Outlined" Class="mb-3" />
        <MudNumericField T="int" @bind-Value="_editForm.DisplayOrder"
                         Label="显示顺序"
                         Variant="Variant.Outlined" Class="mb-3" />

        @if (_editingUnit is not null)
        {
            <MudSwitch T="bool"
                       Value="_editForm.IsBaseUnit"
                       ValueChanged="OnIsBaseUnitChanged"
                       Label="基准单位（同分类仅一个）"
                       Color="Color.Primary"
                       Disabled="@_editingUnit.IsBaseUnit" />

            @if (_editingUnit.IsBaseUnit)
            {
                <MudAlert Severity="Severity.Success" Class="mb-3">
                    该单位已是基准单位。若要更换基准，请在另一个单位上勾选"基准单位"。
                </MudAlert>
            }
            else
            {
                <MudAlert Severity="Severity.Info" Class="mb-3">
                    勾选后本分类的其它基准单位会自动降级。
                </MudAlert>
            }

            <MudDivider Class="my-4" />

            @* ★ 高级操作 *@
            <MudExpansionPanels>
                <MudExpansionPanel Text="高级操作">
                    <MudAlert Severity="Severity.Warning" Class="mb-3">
                        以下操作会修改已有数据的语义或分类归属，请谨慎执行。建议先备份数据库。
                    </MudAlert>

                    <div class="d-flex flex-column gap-2">
                        <MudButton Variant="Variant.Outlined"
                                   Color="Color.Primary"
                                   StartIcon="@Icons.Material.Filled.SwapHoriz"
                                   OnClick="OpenMigrateDialog">
                            迁移到其它分类
                        </MudButton>
                        <MudText Typo="Typo.caption" Color="Color.Secondary" Class="ml-2 mb-2">
                            仅允许迁移无引用的单位；被属性或数值数据引用时服务端将拒绝。
                        </MudText>

                        <MudButton Variant="Variant.Outlined"
                                   Color="Color.Error"
                                   StartIcon="@Icons.Material.Filled.Calculate"
                                   OnClick="OpenRecalculateDialog">
                            修改换算系数并重算数据
                        </MudButton>
                        <MudText Typo="Typo.caption" Color="Color.Secondary" Class="ml-2">
                            数据库端批量重算（通常几秒内完成）；预计超过 20 万行时服务端将拒绝。
                        </MudText>
                    </div>
                </MudExpansionPanel>
            </MudExpansionPanels>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showEditDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="UpdateAsync"
                   Disabled="@_submitting">
            @(_submitting ? "保存中..." : "保存")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 迁移分类对话框 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showMigrateDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">迁移到其它分类</MudText>
        @if (_editingUnit is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingUnit.Category / @_editingUnit.Name
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudAlert Severity="Severity.Warning" Class="mb-3">
            迁移后 ToBaseFactor 的语义会变化，需要重新指定。
        </MudAlert>

        <MudSelect T="string"
                   Value="_migrateForm.NewCategory"
                   ValueChanged="@(v => _migrateForm.NewCategory = v)"
                   Label="目标分类"
                   Variant="Variant.Outlined"
                   Class="mb-3">
            @foreach (var cat in GetAllCategories())
            {
                <MudSelectItem T="string" Value="@cat">@cat</MudSelectItem>
            }
        </MudSelect>

        <MudTextField @bind-Value="_migrateForm.NewCategory"
                      Label="或输入新分类名"
                      Variant="Variant.Outlined"
                      Class="mb-3" />

        <MudNumericField T="decimal"
                         @bind-Value="_migrateForm.NewToBaseFactor"
                         Label="新的换算系数（相对新分类基准单位）"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudAlert Severity="Severity.Info">
            保守模式：单位被属性绑定或被数值数据引用时，服务端将拒绝迁移，请先解除引用。
        </MudAlert>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showMigrateDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="MigrateAsync"
                   Disabled="@_submitting">
            @(_submitting ? "迁移中..." : "迁移")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 重算换算系数对话框 *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showRecalculateDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">修改换算系数并重算</MudText>
        @if (_editingUnit is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                @_editingUnit.Category / @_editingUnit.Name
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudAlert Severity="Severity.Error" Class="mb-3">
            <b>危险操作。</b>将在数据库端批量重算所有引用该单位的历史数据，请确认已备份数据库。
        </MudAlert>

        @if (_editingUnit is not null)
        {
            <MudText Typo="Typo.body2" Class="mb-3">
                当前换算系数：<code>@_editingUnit.ToBaseFactor</code>
            </MudText>
        }

        <MudNumericField T="decimal"
                         @bind-Value="_recalcForm.NewToBaseFactor"
                         Label="新的换算系数"
                         Variant="Variant.Outlined"
                         Class="mb-3" />

        <MudText Typo="Typo.caption" Color="Color.Secondary">
            重算公式：<br />
            · 作为输入单位：值 × 新系数 / 旧系数<br />
            · 作为基准单位：值 × 旧系数 / 新系数<br />
            · 同时命中两种角色：不变<br />
            · 预计影响超过 20 万行时服务端将拒绝（请走离线脚本）
        </MudText>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showRecalculateDialog = false)">取消</MudButton>
        <MudButton Color="Color.Error"
                   OnClick="RecalculateAsync"
                   Disabled="@_submitting">
            @(_submitting ? "重算中..." : "确认重算")
        </MudButton>
    </DialogActions>
</MudDialog>

@* ============================================================ *@
@* 指定基准单位对话框（无基准分类用） *@
@* ============================================================ *@
<MudDialog @bind-Visible="_showAssignBaseDialog" Options="_dialogOptions">
    <TitleContent>
        <MudText Typo="Typo.h6">指定基准单位</MudText>
        @if (_assignBaseCategory is not null)
        {
            <MudText Typo="Typo.caption" Color="Color.Secondary">
                分类：@_assignBaseCategory.Category
            </MudText>
        }
    </TitleContent>
    <DialogContent>
        <MudAlert Severity="Severity.Info" Class="mb-3">
            该分类当前无基准单位。请选择一个单位作为基准——此操作不会重算已有数据。
        </MudAlert>

        <MudSelect T="Guid?"
                   Value="_assignBaseUnitId"
                   ValueChanged="@(v => _assignBaseUnitId = v)"
                   Label="选择单位"
                   Variant="Variant.Outlined">
            @if (_assignBaseCategory is not null)
            {
                @foreach (var u in _assignBaseCategory.Units)
                {
                    <MudSelectItem T="Guid?" Value="@u.Id">
                        @u.Name (@u.Symbol) — 系数 @u.ToBaseFactor
                    </MudSelectItem>
                }
            }
        </MudSelect>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showAssignBaseDialog = false)">取消</MudButton>
        <MudButton Color="Color.Primary"
                   OnClick="AssignBaseAsync"
                   Disabled="@(_submitting || _assignBaseUnitId is null)">
            @(_submitting ? "设置中..." : "设为基准")
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private IReadOnlyList<UnitCategoryDto>? _categories;
    private bool _loading;
    private bool _initialized;
    private bool _submitting;

    private readonly DialogOptions _dialogOptions = new()
    {
        MaxWidth = MaxWidth.Small,
        FullWidth = true
    };

    // 新建
    private bool _showCreateDialog;
    private CreateUnitRequest _newUnit = new();

    // 编辑
    private bool _showEditDialog;
    private UnitDto? _editingUnit;
    private UpdateUnitForm _editForm = new();

    // 迁移分类
    private bool _showMigrateDialog;
    private MigrateUnitCategoryForm _migrateForm = new();

    // 重算系数
    private bool _showRecalculateDialog;
    private RecalculateUnitFactorForm _recalcForm = new();

    // 指定基准
    private bool _showAssignBaseDialog;
    private UnitCategoryDto? _assignBaseCategory;
    private Guid? _assignBaseUnitId;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;

        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            _categories = await Api.GetUnitCategoriesAsync();
            if (_categories is null)
            {
                Snackbar.Add("单位加载失败，请检查后端服务", Severity.Error);
                _categories = Array.Empty<UnitCategoryDto>();
            }
        }
        finally
        {
            _loading = false;
        }
    }

    private IEnumerable<string> GetAllCategories()
        => _categories?.Select(c => c.Category).Distinct() ?? Enumerable.Empty<string>();

    // ---------- 新建 ----------

    private void OpenCreate()
    {
        _newUnit = new CreateUnitRequest();
        _showCreateDialog = true;
    }

    private async Task CreateAsync()
    {
        if (string.IsNullOrWhiteSpace(_newUnit.Category) ||
            string.IsNullOrWhiteSpace(_newUnit.Name) ||
            string.IsNullOrWhiteSpace(_newUnit.Symbol))
        {
            Snackbar.Add("请填写分类、名称、符号", Severity.Warning);
            return;
        }
        if (_newUnit.ToBaseFactor <= 0)
        {
            Snackbar.Add("换算系数必须大于 0", Severity.Warning);
            return;
        }

        _submitting = true;
        try
        {
            var id = await Api.CreateUnitAsync(_newUnit);
            if (id is null)
            {
                Snackbar.Add("创建失败（可能分类下已存在同名单位或基准单位）", Severity.Error);
                return;
            }

            Snackbar.Add($"创建成功：{id}", Severity.Success);
            _showCreateDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 编辑 ----------

    private void OpenEdit(UnitDto unit)
    {
        _editingUnit = unit;
        _editForm = new UpdateUnitForm
        {
            Name = unit.Name,
            Symbol = unit.Symbol,
            DisplayOrder = unit.DisplayOrder,
            IsBaseUnit = unit.IsBaseUnit
        };
        _showEditDialog = true;
    }

    private async Task OnIsBaseUnitChanged(bool desired)
    {
        if (_editingUnit is null) return;

        if (!desired && _editingUnit.IsBaseUnit)
        {
            Snackbar.Add(
                "不能直接取消基准单位。如需切换，请在目标单位上勾选'基准单位'。",
                Severity.Warning);
            _editForm.IsBaseUnit = true;
            return;
        }

        if (desired && !_editingUnit.IsBaseUnit)
        {
            var confirmed = await DialogService.ShowMessageBoxAsync(
                "设为基准单位",
                $"将「{_editingUnit.Name}」({_editingUnit.Symbol}) " +
                $"设为分类「{_editingUnit.Category}」的基准单位？\n\n" +
                "· 本分类现有的基准单位将自动降级。\n" +
                "· 已有属性值按原基准单位归一化存储，切换基准会导致数值语义变化。\n" +
                "· 若属性目录中存在绑定到本分类基准单位的属性，请评估后再操作。",
                yesText: "确认", cancelText: "取消");

            _editForm.IsBaseUnit = confirmed == true;
            return;
        }

        _editForm.IsBaseUnit = desired;
    }

    private async Task UpdateAsync()
    {
        if (_editingUnit is null) return;

        if (string.IsNullOrWhiteSpace(_editForm.Name))
        {
            Snackbar.Add("名称不能为空", Severity.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_editForm.Symbol))
        {
            Snackbar.Add("符号不能为空", Severity.Warning);
            return;
        }

        var baseUnchanged = _editForm.IsBaseUnit == _editingUnit.IsBaseUnit
                         || _editingUnit.IsBaseUnit;

        if (_editForm.Name == _editingUnit.Name &&
            _editForm.Symbol == _editingUnit.Symbol &&
            _editForm.DisplayOrder == _editingUnit.DisplayOrder &&
            baseUnchanged)
        {
            _showEditDialog = false;
            return;
        }

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateUnitAsync(
                _editingUnit.Id,
                new UpdateUnitRequest
                {
                    Name = _editForm.Name,
                    Symbol = _editForm.Symbol,
                    DisplayOrder = _editForm.DisplayOrder,
                    IsBaseUnit = !_editingUnit.IsBaseUnit && _editForm.IsBaseUnit
                        ? true
                        : null
                });

            if (!ok)
            {
                Snackbar.Add($"更新失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("更新成功", Severity.Success);
            _showEditDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 迁移分类 ----------

    private void OpenMigrateDialog()
    {
        if (_editingUnit is null) return;

        _migrateForm = new MigrateUnitCategoryForm
        {
            NewCategory = "",
            NewToBaseFactor = _editingUnit.ToBaseFactor
        };
        _showMigrateDialog = true;
    }

    private async Task MigrateAsync()
    {
        if (_editingUnit is null) return;

        if (string.IsNullOrWhiteSpace(_migrateForm.NewCategory))
        {
            Snackbar.Add("请选择或输入目标分类", Severity.Warning);
            return;
        }
        if (_migrateForm.NewCategory == _editingUnit.Category)
        {
            Snackbar.Add("目标分类与原分类相同", Severity.Warning);
            return;
        }
        if (_migrateForm.NewToBaseFactor <= 0)
        {
            Snackbar.Add("新换算系数必须大于 0", Severity.Warning);
            return;
        }

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认迁移",
            $"将「{_editingUnit.Name}」从分类「{_editingUnit.Category}」" +
            $"迁移到「{_migrateForm.NewCategory}」，新系数 {_migrateForm.NewToBaseFactor}？\n\n" +
            "此操作会改变该单位的分类归属，不可撤销。若单位仍被引用，服务端将拒绝。",
            yesText: "确认迁移", cancelText: "取消");

        if (confirmed != true) return;

        _submitting = true;
        try
        {
            var (ok, error) = await Api.MigrateUnitCategoryAsync(
                _editingUnit.Id,
                new MigrateUnitCategoryRequest
                {
                    NewCategory = _migrateForm.NewCategory,
                    NewToBaseFactor = _migrateForm.NewToBaseFactor
                });

            if (!ok)
            {
                Snackbar.Add($"迁移失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("迁移成功", Severity.Success);
            _showMigrateDialog = false;
            _showEditDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 重算系数 ----------

    private void OpenRecalculateDialog()
    {
        if (_editingUnit is null) return;

        _recalcForm = new RecalculateUnitFactorForm
        {
            NewToBaseFactor = _editingUnit.ToBaseFactor
        };
        _showRecalculateDialog = true;
    }

    private async Task RecalculateAsync()
    {
        if (_editingUnit is null) return;

        if (_recalcForm.NewToBaseFactor <= 0)
        {
            Snackbar.Add("新换算系数必须大于 0", Severity.Warning);
            return;
        }
        if (_recalcForm.NewToBaseFactor == _editingUnit.ToBaseFactor)
        {
            Snackbar.Add("系数未变化", Severity.Info);
            return;
        }

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认重算",
            $"将「{_editingUnit.Name}」的换算系数从 {_editingUnit.ToBaseFactor} " +
            $"改为 {_recalcForm.NewToBaseFactor}，并在数据库端批量重算所有引用该单位的历史数据？\n\n" +
            "操作同步执行、整体事务提交，不可撤销，请确认已备份数据库。",
            yesText: "确认重算", cancelText: "取消");

        if (confirmed != true) return;

        _submitting = true;
        try
        {
            var (ok, result, error) = await Api.RecalculateUnitFactorAsync(
                _editingUnit.Id,
                new RecalculateUnitFactorRequest
                {
                    NewToBaseFactor = _recalcForm.NewToBaseFactor
                });

            if (!ok)
            {
                Snackbar.Add($"重算失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            var affected = result?.AffectedValues ?? 0;
            var attrs = result?.AffectedAttributes ?? 0;
            Snackbar.Add(
                $"重算完成：共更新 {affected} 行数值数据（涉及 {attrs} 个属性）",
                Severity.Success);

            _showRecalculateDialog = false;
            _showEditDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 指定基准单位（无基准分类） ----------

    private void OpenAssignBaseDialog(UnitCategoryDto category)
    {
        _assignBaseCategory = category;
        _assignBaseUnitId = null;
        _showAssignBaseDialog = true;
    }

    private async Task AssignBaseAsync()
    {
        if (_assignBaseCategory is null || _assignBaseUnitId is null) return;

        _submitting = true;
        try
        {
            var (ok, error) = await Api.UpdateUnitAsync(
                _assignBaseUnitId.Value,
                new UpdateUnitRequest { IsBaseUnit = true });

            if (!ok)
            {
                Snackbar.Add($"设置失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("已设为基准单位", Severity.Success);
            _showAssignBaseDialog = false;
            await LoadAsync();
        }
        finally
        {
            _submitting = false;
        }
    }

    // ---------- 删除 ----------

    private async Task DeleteAsync(UnitDto unit)
    {
        var extraHint = unit.IsBaseUnit
            ? "\n\n注意：这是该分类的基准单位，删除后本分类将没有基准单位。"
            : "";

        var confirmed = await DialogService.ShowMessageBoxAsync(
            "确认删除",
            $"确定删除单位「{unit.Name}」({unit.Symbol})？{extraHint}",
            yesText: "删除", cancelText: "取消");

        if (confirmed != true) return;

        var (ok, error) = await Api.DeleteUnitAsync(unit.Id);
        if (ok)
        {
            Snackbar.Add("删除成功", Severity.Success);
            await LoadAsync();
        }
        else
        {
            Snackbar.Add($"删除失败：{error ?? "未知错误"}", Severity.Error);
        }
    }

    // ---------- 表单模型 ----------

    private class UpdateUnitForm
    {
        public string Name { get; set; } = "";
        public string Symbol { get; set; } = "";
        public int DisplayOrder { get; set; }
        public bool IsBaseUnit { get; set; }
    }

    private class MigrateUnitCategoryForm
    {
        public string NewCategory { get; set; } = "";
        public decimal NewToBaseFactor { get; set; }
    }

    private class RecalculateUnitFactorForm
    {
        public decimal NewToBaseFactor { get; set; }
    }
}
```

## 文件 33/46 TreeGraph.Blazor/Components/Pages/NotFound.razor

```razor
@page "/not-found"
@layout MainLayout
@* NotFound.razor *@

<h3>Not Found</h3>
<p>Sorry, the content you are looking for does not exist.</p>
```

## 文件 34/46 TreeGraph.Blazor/Components/Pages/Query/DynamicQuery.razor

```razor
@page "/query"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api
@inject ISnackbar Snackbar
@* DynamicQuery.razor *@

<PageTitle>动态查询</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">动态查询</MudText>

<MudPaper Class="pa-4 mb-4">
    <MudGrid>
        <MudItem xs="12" md="4">
            <MudTextField @bind-Value="_entityType" Label="实体类型"
                          Variant="Variant.Outlined" />
        </MudItem>
        <MudItem xs="12" md="4">
            <MudSelect @bind-Value="_queryMode" Label="查询方式"
                       Variant="Variant.Outlined">
                <MudSelectItem Value="@("filter")">属性过滤</MudSelectItem>
                <MudSelectItem Value="@("by-table")">自定义表行查询</MudSelectItem>
            </MudSelect>
        </MudItem>
        <MudItem xs="12" md="4" Class="d-flex align-center">
            <MudButton Variant="Variant.Filled" Color="Color.Primary"
                       OnClick="LoadSchemaAsync"
                       StartIcon="@Icons.Material.Filled.Refresh">
                加载 Schema
            </MudButton>
        </MudItem>
    </MudGrid>

    @if (_queryMode == "by-table")
    {
        <MudGrid Class="mt-4">
            <MudItem xs="12" md="6">
                <MudTextField @bind-Value="_tableAttributeName"
                              Label="自定义表属性名"
                              Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="12" md="6">
                <MudTextField @bind-Value="_rowConditionsJson"
                              Label="行条件 (JSON 数组)"
                              Lines="3"
                              Variant="Variant.Outlined"
                              HelperText="示例: [{&quot;name&quot;:&quot;重量&quot;,&quot;value&quot;:&quot;1.8&quot;}]" />
            </MudItem>
        </MudGrid>
    }
    else if (_schema is not null)
    {
        <MudDivider Class="my-4" />
        @* ★ 修复：用 QueryFilterBuilder 替换硬编码运算符列表。
           运算符由 FilterOperatorCatalog 驱动，与后端 EavQueryService 严格对应。 *@
        <QueryFilterBuilder Filters="_filters"
                            SupportedAttributes="_searchableAttributes"
                            OnApply="QueryAsync" />
    }

    <MudDivider Class="my-4" />
    <MudButton Variant="Variant.Filled" Color="Color.Primary"
               OnClick="QueryAsync" Disabled="@_loading"
               StartIcon="@Icons.Material.Filled.Search">
        @(_loading ? "查询中..." : "执行查询")
    </MudButton>
</MudPaper>

@if (_ids is not null)
{
    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h6" Class="mb-2">
            结果（@_ids.Count 个）
        </MudText>
        @if (_ids.Count == 0)
        {
            <MudAlert Severity="Severity.Info">无结果</MudAlert>
        }
        else
        {
            <MudChipSet T="string">
                @foreach (var id in _ids)
                {
                    <MudChip T="string" Value="@id" Color="Color.Primary">
                        #@id
                    </MudChip>
                }
            </MudChipSet>
        }
    </MudPaper>
}
else if (_entities is not null)
{
    <MudPaper Class="pa-4">
        <MudText Typo="Typo.h6" Class="mb-2">
            结果（共 @_entities.Total 条）
        </MudText>
        <MudTable Items="_entities.Items" Hover="true" Bordered="true">
            <HeaderContent>
                <MudTh>EntityId</MudTh>
                <MudTh>属性预览</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>@context.EntityId</MudTd>
                <MudTd>
                    @foreach (var (k, v) in context.Properties.Take(3))
                    {
                        <MudChip T="string" Size="Size.Small">
                            @k=@v.ToString()
                        </MudChip>
                    }
                </MudTd>
            </RowTemplate>
        </MudTable>
    </MudPaper>
}

@code {
    private string _entityType = "Product";
    private string _queryMode = "filter";
    private string _tableAttributeName = "specs";
    private string _rowConditionsJson = "[]";

    private IReadOnlyList<AttributeSchemaDto>? _schema;
    private IReadOnlyList<AttributeSchemaDto> _searchableAttributes
        = Array.Empty<AttributeSchemaDto>();
    private PagedResult<DynamicEntityDto>? _entities;
    private IReadOnlyList<string>? _ids;
    private bool _loading;

    private readonly List<AttributeFilter> _filters = new();

    // 端点 ①：加载 Schema
    private async Task LoadSchemaAsync()
    {
        var schema = await Api.GetSchemaAsync(_entityType);
        _schema = schema ?? Array.Empty<AttributeSchemaDto>();

        // ★ 用 FilterOperatorCatalog 筛可搜索属性（与后端能力对齐）
        _searchableAttributes = _schema
            .Where(a => a.IsSearchable && FilterOperatorCatalog.IsSupported(a))
            .OrderBy(a => a.DisplayOrder)
            .ToList();

        _filters.Clear();
        Snackbar.Add($"已加载 {_schema.Count} 个属性，其中 {_searchableAttributes.Count} 个可搜索",
            Severity.Info);
    }

    // 端点 ④ 或 ⑤
    private async Task QueryAsync()
    {
        _loading = true;
        _entities = null;
        _ids = null;
        try
        {
            if (_queryMode == "by-table")
            {
                var conditions = System.Text.Json.JsonSerializer
                    .Deserialize<List<Dictionary<string, object?>>>(_rowConditionsJson)
                    ?? new();

                _ids = await Api.QueryByTableAsync(_entityType, new QueryByTableRequest
                {
                    AttributeName = _tableAttributeName,
                    RowConditions = conditions
                });
            }
            else
            {
                _entities = await Api.QueryAsync(_entityType, new EavQueryRequest
                {
                    EntityType = _entityType,
                    Filters = _filters,
                    Page = 1,
                    PageSize = 50
                });
            }
        }
        finally
        {
            _loading = false;
        }
    }
}
```

## 文件 35/46 TreeGraph.Blazor/Components/Pages/Schema/SchemaViewer.razor

```razor
@page "/schema"
@rendermode Microsoft.AspNetCore.Components.Web.RenderMode.InteractiveServer
@inject EavApiClient Api

@* SchemaViewer.razor *@
<PageTitle>Schema 查看</PageTitle>

<MudText Typo="Typo.h5" Class="mb-4">Schema 查看</MudText>

<MudGrid Class="mb-4">
    <MudItem xs="12" md="6">
        <MudTextField @bind-Value="_entityType" Label="实体类型"
                      Variant="Variant.Outlined" />
    </MudItem>
    <MudItem xs="12" md="6" Class="d-flex align-center">
        <MudButton Variant="Variant.Filled" Color="Color.Primary"
                   OnClick="LoadAsync" Disabled="@_loading">
            @(_loading ? "加载中..." : "加载 Schema")
        </MudButton>
    </MudItem>
</MudGrid>

@if (_schema is not null)
{
    <MudTable Items="_schema" Hover="true" Bordered="true">
        <HeaderContent>
            <MudTh>属性名</MudTh>
            <MudTh>显示名</MudTh>
            <MudTh>类型</MudTh>
            <MudTh>必填</MudTh>
            <MudTh>可搜索</MudTh>
            <MudTh>可排序</MudTh>
            <MudTh>附加信息</MudTh>
        </HeaderContent>
        <RowTemplate>
            <MudTd>@context.AttributeName</MudTd>
            <MudTd>@context.DisplayName</MudTd>
            <MudTd>
                <MudChip T="string" Size="Size.Small" Color="Color.Info">
                    @context.DataType
                </MudChip>
            </MudTd>
            <MudTd>
                @if (context.IsRequired)
                {
                    <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                }
            </MudTd>
            <MudTd>
                @if (context.IsSearchable)
                {
                    <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                }
            </MudTd>
            <MudTd>
                @if (context.IsSortable)
                {
                    <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                }
            </MudTd>
            <MudTd>
                @if (context.Unit is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Warning">
                        单位: @context.Unit.Symbol
                    </MudChip>
                }
                @if (context.OptionSet is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Secondary">
                        选项: @context.OptionSet.Items.Count 项
                    </MudChip>
                }
                @if (context.CompositeType is not null)
                {
                    <MudChip T="string" Size="Size.Small" Color="Color.Tertiary">
                        组合: @context.CompositeType.Fields.Count 字段
                    </MudChip>
                }
            </MudTd>
        </RowTemplate>
    </MudTable>
}
else if (!_loading && _loaded)
{
    <MudAlert Severity="Severity.Warning">未找到 Schema</MudAlert>
}

@code {
    private string _entityType = "Product";
    private IReadOnlyList<AttributeSchemaDto>? _schema;
    private bool _loading;
    private bool _loaded;

    // 调用端点 ①：GET api/eav/{entityType}/schema
    private async Task LoadAsync()
    {
        _loading = true;
        _loaded = false;
        try
        {
            _schema = await Api.GetSchemaAsync(_entityType);
        }
        finally
        {
            _loading = false;
            _loaded = true;
        }
    }
}
```

## 文件 36/46 TreeGraph.Blazor/Components/Pages/Weather.razor

```razor
@page "/weather"
@attribute [StreamRendering]

<PageTitle>Weather</PageTitle>

<h1>Weather</h1>

<p>This component demonstrates showing data.</p>

@if (forecasts == null)
{
    <p>
        <em>Loading...</em>
    </p>
}
else
{
    <table class="table">
        <thead>
        <tr>
            <th>Date</th>
            <th aria-label="Temperature in Celsius">Temp. (C)</th>
            <th aria-label="Temperature in Fahrenheit">Temp. (F)</th>
            <th>Summary</th>
        </tr>
        </thead>
        <tbody>
        @foreach (var forecast in forecasts)
        {
            <tr>
                <td>@forecast.Date.ToShortDateString()</td>
                <td>@forecast.TemperatureC</td>
                <td>@forecast.TemperatureF</td>
                <td>@forecast.Summary</td>
            </tr>
        }
        </tbody>
    </table>
}

@code {
    private WeatherForecast[]? forecasts;

    protected override async Task OnInitializedAsync()
    {
        // Simulate asynchronous loading to demonstrate streaming rendering
        await Task.Delay(500);

        var startDate = DateOnly.FromDateTime(DateTime.Now);
        var summaries = new[] { "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching" };
        forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = startDate.AddDays(index),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = summaries[Random.Shared.Next(summaries.Length)]
        }).ToArray();
    }

    private class WeatherForecast
    {
        public DateOnly Date { get; set; }
        public int TemperatureC { get; set; }
        public string? Summary { get; set; }
        public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
    }

}
```

## 文件 37/46 TreeGraph.Blazor/Components/Routes.razor

```razor
@* Routes.razor *@
<Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(Pages.NotFound)">
    <Found Context="routeData">
        <RouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)"/>
        <FocusOnNavigate RouteData="routeData" Selector="h1"/>
    </Found>
</Router>
```

## 文件 38/46 TreeGraph.Blazor/Components/Shared/ArrayFieldEditor.razor

```razor
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos

@* ArrayFieldEditor.razor *@

@* 数组字段编辑器：每个元素独立渲染 + 独立校验错误；添加/删除元素。 *@

<MudText Typo="Typo.subtitle2" Class="mb-1">
    @Field.DisplayName
    @if (Field.IsRequired)
    {
        <span class="mud-error-text">*</span>
    }
</MudText>

@if (Errors is { Count: > 0 } && Errors.Any(e => string.IsNullOrEmpty(e.Path)))
{
    <MudAlert Severity="Severity.Error" Class="mb-2">
        @string.Join("；", Errors.Where(e => string.IsNullOrEmpty(e.Path)).Select(e => e.Message))
    </MudAlert>
}

@for (int i = 0; i < _items.Count; i++)
{
    var idx = i;
    var itemErrors = SliceItemErrors(idx);
    <MudPaper Class="pa-3 mb-2" Outlined="true">
        <div class="d-flex align-center mb-2">
            <MudText Typo="Typo.caption">#@(idx + 1)</MudText>
            <MudSpacer />
            <MudIconButton Icon="@Icons.Material.Filled.Delete"
                           Size="Size.Small"
                           Color="Color.Error"
                           OnClick="@(() => RemoveAt(idx))" />
        </div>

        @if (Field.DataType == "composite" && Field.NestedType is not null)
        {
            <CompositeFieldEditor TypeSchema="@Field.NestedType"
                                  BasePath=""
                                  Value="@(GetItem(idx) as Dictionary<string, object?>)"
                                  ValueChanged="@(v => SetItem(idx, v))"
                                  Errors="@itemErrors" />
        }
        else
        {
            <RenderLeafField Field="Field"
                             Value="@GetItem(idx)"
                             ValueChanged="@(v => SetItem(idx, v))"
                             Errors="@itemErrors" />
        }
    </MudPaper>
}

<MudButton Variant="Variant.Outlined"
           Color="Color.Primary"
           Size="Size.Small"
           StartIcon="@Icons.Material.Filled.Add"
           OnClick="AddItem">
    添加元素
</MudButton>

@code {
    [Parameter, EditorRequired] public CompositeFieldSchemaDto Field { get; set; } = null!;
    [Parameter] public string BasePath { get; set; } = "";

    /// <summary>数组值（List&lt;object?&gt;）。</summary>
    [Parameter] public List<object?>? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    /// <summary>该数组字段的全部错误，Path 形如 "[0]" / "[0].sub"。</summary>
    [Parameter] public IReadOnlyList<FieldValidationError>? Errors { get; set; }

    private List<object?> _items = new();

    protected override void OnParametersSet()
    {
        _items = Value ?? new List<object?>();
    }

    private object? GetItem(int i)
        => i >= 0 && i < _items.Count ? _items[i] : null;

    private void SetItem(int i, object? v)
    {
        var newList = new List<object?>(_items);
        if (i >= 0 && i < newList.Count)
        {
            newList[i] = v;
            ValueChanged.InvokeAsync(newList);
        }
    }

    private void AddItem()
    {
        var newList = new List<object?>(_items) { null };
        ValueChanged.InvokeAsync(newList);
    }

    private void RemoveAt(int i)
    {
        if (i < 0 || i >= _items.Count) return;
        var newList = new List<object?>(_items);
        newList.RemoveAt(i);
        ValueChanged.InvokeAsync(newList);
    }

    /// <summary>筛出属于第 i 个元素的错误，并剥离 "[i]" 前缀。</summary>
    private IReadOnlyList<FieldValidationError> SliceItemErrors(int i)
    {
        if (Errors is null || Errors.Count == 0)
            return Array.Empty<FieldValidationError>();

        var prefix = $"[{i}]";
        var result = new List<FieldValidationError>();
        foreach (var e in Errors)
        {
            if (string.IsNullOrEmpty(e.Path)) continue;

            if (e.Path == prefix)
            {
                result.Add(new FieldValidationError("", e.Message));
            }
            else if (e.Path.StartsWith(prefix + "."))
            {
                result.Add(new FieldValidationError(
                    e.Path.Substring(prefix.Length + 1), e.Message));
            }
            else if (e.Path.StartsWith(prefix + "["))
            {
                result.Add(new FieldValidationError(
                    e.Path.Substring(prefix.Length), e.Message));
            }
        }
        return result;
    }
}
```

## 文件 39/46 TreeGraph.Blazor/Components/Shared/CompositeFieldEditor.razor

```razor
@using System.Text.Json
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos

@* CompositeFieldEditor.razor *@
@* 组合类型编辑器：递归渲染所有字段，把错误按字段名 slice 后传给子组件。 *@

@foreach (var field in TypeSchema.Fields.OrderBy(f => f.DisplayOrder))
{
    var fieldErrors = SliceErrors(field.FieldName);

    @if (field.IsArray)
    {
        <div class="mb-3">
            <ArrayFieldEditor Field="field"
                              BasePath="@field.FieldName"
                              Value="@GetArrayValue(field.FieldName)"
                              ValueChanged="@(v => OnFieldChanged(field.FieldName, v))"
                              Errors="@fieldErrors" />
        </div>
    }
    else if (field.DataType == "composite" && field.NestedType is not null)
    {
        <div class="mb-3">
            <MudText Typo="Typo.subtitle2" Class="mb-1">
                @field.DisplayName
            </MudText>
            <MudPaper Class="pa-3" Outlined="true">
                <CompositeFieldEditor TypeSchema="@field.NestedType"
                                      BasePath="@field.FieldName"
                                      Value="@GetNestedValue(field.FieldName)"
                                      ValueChanged="@(v => OnFieldChanged(field.FieldName, v))"
                                      Errors="@fieldErrors" />
            </MudPaper>
        </div>
    }
    else
    {
        <div class="mb-3">
            <RenderLeafField Field="field"
                             Value="@GetRaw(field.FieldName)"
                             ValueChanged="@(v => OnFieldChanged(field.FieldName, v))"
                             Errors="@fieldErrors" />
        </div>
    }
}

@code {
    [Parameter, EditorRequired] public CompositeTypeSchemaDto TypeSchema { get; set; } = null!;

    /// <summary>当前组合在完整路径中的位置（用于 slice）。根组合时为 ""。</summary>
    [Parameter] public string BasePath { get; set; } = "";

    [Parameter] public Dictionary<string, object?>? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    /// <summary>属于当前组合的错误（路径已剥离 BasePath）。</summary>
    [Parameter] public IReadOnlyList<FieldValidationError>? Errors { get; set; }

    private Dictionary<string, object?> _dict = new();

    protected override void OnParametersSet()
    {
        _dict = Value ?? new Dictionary<string, object?>();
    }

    /// <summary>从当前组合的错误集里筛出属于某个字段的错误，并把字段名前缀剥离。</summary>
    private IReadOnlyList<FieldValidationError> SliceErrors(string fieldName)
    {
        if (Errors is null || Errors.Count == 0)
            return Array.Empty<FieldValidationError>();

        var result = new List<FieldValidationError>();
        foreach (var e in Errors)
        {
            if (string.IsNullOrEmpty(e.Path)) continue;

            if (e.Path == fieldName)
            {
                // 错误直接属于该字段
                result.Add(new FieldValidationError("", e.Message));
            }
            else if (e.Path.StartsWith(fieldName + "."))
            {
                result.Add(new FieldValidationError(
                    e.Path.Substring(fieldName.Length + 1), e.Message));
            }
            else if (e.Path.StartsWith(fieldName + "["))
            {
                result.Add(new FieldValidationError(
                    e.Path.Substring(fieldName.Length), e.Message));
            }
        }
        return result;
    }

    private object? GetRaw(string name)
        => _dict.TryGetValue(name, out var v) ? v : null;

    private Dictionary<string, object?>? GetNestedValue(string name)
        => GetRaw(name) as Dictionary<string, object?>;

    private List<object?>? GetArrayValue(string name)
        => GetRaw(name) as List<object?>;

    private void OnFieldChanged(string name, object? v)
    {
        var newDict = new Dictionary<string, object?>(_dict);
        if (v is null)
            newDict.Remove(name);
        else
            newDict[name] = v;
        ValueChanged.InvokeAsync(newDict);
    }
}
```

## 文件 40/46 TreeGraph.Blazor/Components/Shared/CustomTableEditor.razor

```razor
@using System.Text.Json
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@using Microsoft.AspNetCore.Components.Rendering
@inject EavApiClient Api
@inject ISnackbar Snackbar
@inject IEavFieldValidator Validator

@* CustomTableEditor.razor *@
@* 自定义表子编辑：列 schema 驱动的类型化输入 + 列级校验。 *@

<MudExpansionPanel Text="@DisplayName" Class="mb-2">
    <TitleContent>
        <MudText Typo="Typo.subtitle1">@DisplayName</MudText>
        <MudChip T="string" Size="Size.Small" Class="ml-2">
            @_rows.Count 行
        </MudChip>
        @if (_rows.Any(r => r.FieldErrors.Count > 0))
        {
            <MudChip T="string" Size="Size.Small" Color="Color.Error" Class="ml-2">
                存在错误
            </MudChip>
        }
    </TitleContent>

    <ChildContent>
        @if (_loading)
        {
            <MudProgressLinear Indeterminate="true" />
        }
        else if (_tableDef is null)
        {
            <MudAlert Severity="Severity.Warning">
                无法加载表结构。请检查后端服务或该表的元数据。
            </MudAlert>
            <MudButton Variant="Variant.Outlined" Class="mt-2"
                       StartIcon="@Icons.Material.Filled.Refresh"
                       OnClick="LoadAsync">
                重新加载
            </MudButton>
        }
        else if (_tableDef.Columns.Count == 0)
        {
            <MudAlert Severity="Severity.Info">
                该表没有定义任何列，无法录入数据。
            </MudAlert>
        }
        else
        {
            @for (int i = 0; i < _rows.Count; i++)
            {
                var idx = i;
                var row = _rows[idx];
                <MudPaper Class="pa-3 mb-2" Outlined="true">
                    <div class="d-flex align-center mb-2">
                        <MudText Typo="Typo.caption">
                            行 @(idx + 1) @(row.RowId is not null ? $"（ID: {row.RowId}）" : "（新增）")
                        </MudText>
                        <MudSpacer />
                        <MudIconButton Icon="@Icons.Material.Filled.Delete"
                                       Size="Size.Small"
                                       Color="Color.Error"
                                       OnClick="@(() => RemoveRow(idx))" />
                    </div>

                    <MudGrid Spacing="2">
                        @* 数据编辑器只渲染活动列；已删除列由管理页（CustomTables.razor）负责恢复 *@
                        @foreach (var col in _tableDef.Columns.Where(c => !c.IsDeleted).OrderBy(c => c.DisplayOrder))
                        {
                            var colCaptured = col;
                            <MudItem xs="12" md="@(col.DataType == "composite" || col.DataType == "json" ? 12 : 6)">
                                @RenderColumnInput(row, colCaptured)
                            </MudItem>
                        }
                    </MudGrid>
                </MudPaper>
            }

            <div class="d-flex" style="gap: 8px;">
                <MudButton Variant="Variant.Outlined"
                           Color="Color.Primary"
                           StartIcon="@Icons.Material.Filled.Add"
                           OnClick="AddRow">
                    添加行
                </MudButton>
                <MudButton Variant="Variant.Filled"
                           Color="Color.Success"
                           StartIcon="@Icons.Material.Filled.Save"
                           OnClick="SaveAsync"
                           Disabled="@(_saving || HasErrors)">
                    @(_saving ? "保存中..." : "保存表数据")
                </MudButton>
                <MudButton Variant="Variant.Outlined"
                           Color="Color.Default"
                           StartIcon="@Icons.Material.Filled.Refresh"
                           OnClick="LoadAsync">
                    重新加载
                </MudButton>
            </div>
        }
    </ChildContent>
</MudExpansionPanel>

@code {
    [Parameter, EditorRequired] public string EntityType { get; set; } = "";
    [Parameter, EditorRequired] public string EntityId { get; set; } = "";
    [Parameter, EditorRequired] public string AttributeName { get; set; } = "";
    [Parameter, EditorRequired] public string TableName { get; set; } = "";
    [Parameter, EditorRequired] public string DisplayName { get; set; } = "";
    [Parameter] public string? RefTableDefinitionId { get; set; }

    private class RowState
    {
        public string? RowId { get; set; }
        public int RowOrder { get; set; }
        public Dictionary<string, object?> Fields { get; set; } = new();

        /// <summary>列名 → 错误消息列表（校验或 JSON 解析失败）。</summary>
        public Dictionary<string, List<string>> FieldErrors { get; set; } = new();
    }

    private CustomTableDetailDto? _tableDef;
    private readonly List<RowState> _rows = new();
    private bool _loading;
    private bool _saving;
    private bool _initialized;

    private bool HasErrors => _rows.Any(r => r.FieldErrors.Count > 0);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _initialized) return;
        _initialized = true;
        await LoadAsync();
        StateHasChanged();
    }

    private async Task LoadAsync()
    {
        _loading = true;
        try
        {
            if (RefTableDefinitionId is not { } tableDefId)
            {
                Snackbar.Add($"表「{TableName}」缺少 RefTableDefinitionId", Severity.Error);
                _tableDef = null;
                return;
            }

            _tableDef = await Api.GetCustomTableAsync(tableDefId);
            if (_tableDef is null)
            {
                Snackbar.Add($"加载表结构「{TableName}」失败", Severity.Warning);
                return;
            }

            var tableValue = await Api.LoadCustomTableAsync(EntityType, EntityId, TableName);
            _rows.Clear();

            if (tableValue is null)
            {
                Snackbar.Add($"加载表数据「{TableName}」失败", Severity.Warning);
                return;
            }

            if (tableValue.Rows is not null)
            {
                // 只保留活动列数据：已删除列既不渲染/校验，也不能回传
                // （后端 CustomTableCache 加载表定义时会 RemoveAll(IsDeleted)，
                //   回传已删除列的键会按未知列被拒绝）。
                var activeColumnNames = _tableDef.Columns
                    .Where(c => !c.IsDeleted)
                    .Select(c => c.ColumnName)
                    .ToHashSet();

                foreach (var r in tableValue.Rows)
                {
                    var rowState = new RowState
                    {
                        RowId = r.RowId,
                        RowOrder = r.RowOrder,
                        Fields = r.Fields
                            .Where(kv => activeColumnNames.Contains(kv.Key))
                            .ToDictionary(kv => kv.Key, kv => kv.Value)
                    };
                    _rows.Add(rowState);
                }
            }

            // 加载后立即全表校验（捕获已有数据中的类型不匹配等）
            ValidateAllRows();
        }
        finally
        {
            _loading = false;
        }
    }

    // ============================================================
    // 渲染
    // ============================================================

    private RenderFragment RenderColumnInput(RowState row, CustomTableColumnDto col)
    {
        return builder =>
        {
            row.Fields.TryGetValue(col.ColumnName, out var current);
            var error = row.FieldErrors.TryGetValue(col.ColumnName, out var list)
                ? string.Join("；", list) : null;
            var hasError = error is not null;

            switch (col.DataType)
            {
                case "string":
                    RenderString(builder, row, col, current, error, hasError);
                    break;
                case "int":
                    RenderInt(builder, row, col, current, error, hasError);
                    break;
                case "decimal":
                    RenderDecimal(builder, row, col, current, error, hasError);
                    break;
                case "bool":
                    RenderBool(builder, row, col, current, error, hasError);
                    break;
                case "date":
                    RenderDate(builder, row, col, current, error, hasError);
                    break;
                case "time":
                    RenderTime(builder, row, col, current, error, hasError);
                    break;
                case "datetime":
                    RenderDatetime(builder, row, col, current, error, hasError);
                    break;
                case "single_choice":
                    RenderSingleChoice(builder, row, col, current, error, hasError);
                    break;
                case "composite":
                case "json":
                case "file":
                    RenderJson(builder, row, col, current, error, hasError);
                    break;
                default:
                    RenderString(builder, row, col, current, error, hasError);
                    break;
            }
        };
    }

    private void RenderString(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        b.OpenComponent<MudTextField<string>>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Value", current as string ?? current?.ToString());
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<string?>(this, v => SetField(row, col, v)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.AddAttribute(9, "Immediate", true);
        b.AddAttribute(10, "FullWidth", true);
        b.CloseComponent();
    }

    private void RenderInt(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        long? v = current switch
        {
            long l => l,
            int i => i,
            _ => null
        };
        b.OpenComponent<MudNumericField<long?>>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Value", v);
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<long?>(this, nv => SetField(row, col, nv)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.AddAttribute(9, "FullWidth", true);
        b.CloseComponent();
    }

    private void RenderDecimal(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        decimal? v = current switch
        {
            decimal d => d,
            double db => (decimal)db,
            int i => i,
            long l => l,
            _ => null
        };
        b.OpenComponent<MudNumericField<decimal?>>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Value", v);
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<decimal?>(this, nv => SetField(row, col, nv)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.AddAttribute(9, "FullWidth", true);
        b.CloseComponent();
    }

    private void RenderBool(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        bool v = current is bool bo && bo;
        b.OpenComponent<MudSwitch<bool>>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Value", v);
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<bool>(this, nv => SetField(row, col, nv)));
        b.AddAttribute(4, "Color", Color.Primary);
        b.CloseComponent();
    }

    private void RenderDate(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        DateTime? dt = null;
        if (current is string ds && DateOnly.TryParse(ds, out var d))
            dt = d.ToDateTime(TimeOnly.MinValue);

        b.OpenComponent<MudDatePicker>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Date", dt);
        b.AddAttribute(3, "DateChanged",
            EventCallback.Factory.Create<DateTime?>(this, nv => SetField(row, col,
                nv.HasValue ? nv.Value.ToString("yyyy-MM-dd") : null)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.CloseComponent();
    }

    private void RenderTime(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        TimeSpan? ts = null;
        if (current is string tsStr && TimeOnly.TryParse(tsStr, out var t))
            ts = t.ToTimeSpan();

        b.OpenComponent<MudTimePicker>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Time", ts);
        b.AddAttribute(3, "TimeChanged",
            EventCallback.Factory.Create<TimeSpan?>(this, nv => SetField(row, col,
                nv.HasValue
                    ? new TimeOnly(nv.Value.Hours, nv.Value.Minutes, nv.Value.Seconds)
                        .ToString("HH:mm:ss")
                    : null)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.CloseComponent();
    }

    private void RenderDatetime(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        string? text = current switch
        {
            DateTimeOffset dto => dto.ToString("O"),
            string s => s,
            _ => null
        };
        b.OpenComponent<MudTextField<string>>(0);
        b.AddAttribute(1, "Label", ColLabel(col) + " (ISO 8601)");
        b.AddAttribute(2, "Value", text);
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<string?>(this, nv =>
            {
                if (string.IsNullOrWhiteSpace(nv))
                    SetField(row, col, null);
                else if (DateTimeOffset.TryParse(nv, out var parsed))
                    SetField(row, col, parsed.ToString("O"));
            }));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.AddAttribute(9, "Immediate", true);
        b.AddAttribute(10, "FullWidth", true);
        b.CloseComponent();
    }

    private void RenderSingleChoice(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        var options = ExtractAllowedValues(col.AllowedValues);

        if (options.Count == 0)
        {
            RenderString(b, row, col, current, error, hasError);
            return;
        }

        b.OpenComponent<MudSelect<string>>(0);
        b.AddAttribute(1, "Label", ColLabel(col));
        b.AddAttribute(2, "Value", current as string);
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<string?>(this, nv => SetField(row, col, nv)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Required", col.IsRequired);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.AddAttribute(9, "FullWidth", true);
        b.AddAttribute(10, "ChildContent", (RenderFragment)(inner =>
        {
            if (!col.IsRequired)
            {
                inner.OpenComponent<MudSelectItem<string>>(0);
                inner.AddAttribute(1, "Value", (string?)null);
                inner.AddAttribute(2, "ChildContent",
                    (RenderFragment)(x => x.AddContent(0, "（未选择）")));
                inner.CloseComponent();
            }
            foreach (var opt in options)
            {
                inner.OpenComponent<MudSelectItem<string>>(0);
                inner.AddAttribute(1, "Value", opt);
                inner.AddAttribute(2, "ChildContent", (RenderFragment)(x => x.AddContent(0, opt)));
                inner.CloseComponent();
            }
        }));
        b.CloseComponent();
    }

    private void RenderJson(RenderTreeBuilder b, RowState row, CustomTableColumnDto col,
        object? current, string? error, bool hasError)
    {
        var rawText = current switch
        {
            null => "",
            JsonElement je => je.GetRawText(),
            string s => s,
            _ => JsonSerializer.Serialize(current)
        };

        b.OpenComponent<MudTextField<string>>(0);
        b.AddAttribute(1, "Label", ColLabel(col) + " (JSON)");
        b.AddAttribute(2, "Value", rawText);
        b.AddAttribute(3, "ValueChanged",
            EventCallback.Factory.Create<string?>(this, nv => OnJsonChanged(row, col, nv)));
        b.AddAttribute(4, "Variant", Variant.Outlined);
        b.AddAttribute(5, "Margin", Margin.Dense);
        b.AddAttribute(6, "Lines", 3);
        b.AddAttribute(7, "Error", hasError);
        b.AddAttribute(8, "ErrorText", error);
        b.AddAttribute(9, "Immediate", true);
        b.AddAttribute(10, "FullWidth", true);
        b.CloseComponent();
    }

    // ============================================================
    // 字段值管理 + 校验
    // ============================================================

    private void SetField(RowState row, CustomTableColumnDto col, object? value)
    {
        if (value is null)
            row.Fields.Remove(col.ColumnName);
        else
            row.Fields[col.ColumnName] = value;

        // ★ 实时校验当前列
        ValidateRowColumn(row, col);

        // ★ 若该列 IsUnique，所有行都要重算（唯一性受影响）
        if (col.IsUnique)
            RecheckUniqueForColumn(col);

        StateHasChanged();
    }

    private void OnJsonChanged(RowState row, CustomTableColumnDto col, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            row.Fields.Remove(col.ColumnName);
            row.FieldErrors.Remove(col.ColumnName);
            StateHasChanged();
            return;
        }

        try
        {
            using var doc = JsonDocument.Parse(text);
            row.Fields[col.ColumnName] = doc.RootElement.Clone();
            row.FieldErrors.Remove(col.ColumnName);
        }
        catch (JsonException ex)
        {
            // JSON 解析失败：报错，但不污染 Fields（保留原值）
            row.FieldErrors[col.ColumnName] = new List<string> { $"JSON 格式错误：{ex.Message}" };
        }
        StateHasChanged();
    }

    /// <summary>
    /// 把 JsonElement 解包为 CLR 类型，便于后续校验统一处理。
    /// 用户编辑后写入的是 CLR 类型，加载数据时是 JsonElement；
    /// 校验路径必须先归一化，否则空串/数字检查会静默失效。
    /// </summary>
    private static object? UnwrapValue(object? value)
    {
        if (value is not JsonElement je) return value;
        return je.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.String => je.GetString(),
            JsonValueKind.Number => je.TryGetInt64(out var l) ? l : je.GetDecimal(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => value   // Object / Array 保持 JsonElement
        };
    }

    /// <summary>校验单行的单列。</summary>
    private void ValidateRowColumn(RowState row, CustomTableColumnDto col)
    {
        row.FieldErrors.Remove(col.ColumnName);

        row.Fields.TryGetValue(col.ColumnName, out var rawValue);
        var value = UnwrapValue(rawValue);   // ★ 归一化

        var errors = new List<string>();

        // 1. 必填
        var isNull = value is null;
        var isEmptyString = value is string s && string.IsNullOrWhiteSpace(s);

        if (col.IsRequired && (isNull || isEmptyString))
        {
            errors.Add("必填字段");
        }
        else if (!isNull && !isEmptyString)
        {
            // 2. 类型化规则
            errors.AddRange(ValidateValueByType(col, value));

            // 3. single_choice 的 AllowedValues（字符串分支里没有覆盖到，需单独校验）
            if (col.DataType == "single_choice")
            {
                var allowed = ExtractAllowedValues(col.AllowedValues);
                if (allowed.Count > 0)
                {
                    var vs = value?.ToString() ?? "";
                    if (!allowed.Contains(vs))
                        errors.Add($"值必须是以下之一：{string.Join("、", allowed)}");
                }
            }
        }

        if (errors.Count > 0)
            row.FieldErrors[col.ColumnName] = errors;
    }

    private IEnumerable<string> ValidateValueByType(CustomTableColumnDto col, object? value)
    {
        switch (col.DataType)
        {
            case "string":
                foreach (var msg in FieldValidationRules.ValidateString(
                    value?.ToString() ?? "",
                    col.ValidationRule,
                    allowedValues: null))
                    yield return msg;
                break;

            case "int":
            case "decimal":
                if (TryGetDecimal(value, out var d))
                {
                    if (col.DataType == "int" && d != Math.Truncate(d))
                    {
                        yield return "int 类型不接受小数";
                    }
                    else
                    {
                        foreach (var msg in FieldValidationRules.ValidateNumeric(d, col.ValidationRule))
                            yield return msg;
                    }
                }
                else
                {
                    yield return "数值格式错误";
                }
                break;

            case "date":
                if (value is string ds && DateOnly.TryParse(ds, out var dt))
                {
                    foreach (var msg in FieldValidationRules.ValidateDate(dt, col.ValidationRule))
                        yield return msg;
                }
                else
                {
                    yield return "日期格式错误（应为 yyyy-MM-dd）";
                }
                break;

            case "time":
                if (value is string ts && TimeOnly.TryParse(ts, out var t))
                {
                    foreach (var msg in FieldValidationRules.ValidateTime(t, col.ValidationRule))
                        yield return msg;
                }
                else
                {
                    yield return "时间格式错误（应为 HH:mm:ss）";
                }
                break;

            // bool / datetime / composite / json / file：控件层已保证格式，无额外规则
            default:
                yield break;
        }
    }

    /// <summary>整表校验。用于保存前和加载后。</summary>
    private void ValidateAllRows()
    {
        if (_tableDef is null) return;

        foreach (var row in _rows)
        {
            // 保留 JSON 解析错误（在 OnJsonChanged 里赋值，不与规则校验重叠）
            var preservedJsonErrors = row.FieldErrors
                .Where(kv => kv.Value.Any(v => v.StartsWith("JSON")))
                .ToDictionary(kv => kv.Key, kv => kv.Value);

            row.FieldErrors.Clear();
            foreach (var kv in preservedJsonErrors)
                row.FieldErrors[kv.Key] = kv.Value;

            foreach (var col in _tableDef.Columns.Where(c => !c.IsDeleted))
                ValidateRowColumn(row, col);
        }

        // 跨行唯一性
        foreach (var col in _tableDef.Columns.Where(c => c.IsUnique && !c.IsDeleted))
            RecheckUniqueForColumn(col);
    }

    /// <summary>检查列 IsUnique：同列重复值 → 给相关行加错误。</summary>
    private void RecheckUniqueForColumn(CustomTableColumnDto col)
    {
        // 先清掉该列上的"跨行唯一"错误（保留其它错误）
        foreach (var row in _rows)
        {
            if (row.FieldErrors.TryGetValue(col.ColumnName, out var errs))
            {
                var kept = errs.Where(e => !e.Contains("在列中重复")).ToList();
                if (kept.Count > 0) row.FieldErrors[col.ColumnName] = kept;
                else row.FieldErrors.Remove(col.ColumnName);
            }
        }

        // 分组（对 UnwrapValue 后的值做比较，JsonElement 与 CLR 能对齐）
        var groups = _rows
            .Select(r => new
            {
                Row = r,
                Key = UnwrapValue(r.Fields.TryGetValue(col.ColumnName, out var v) ? v : null)
            })
            .Where(x => x.Key is not null
                     && !(x.Key is string s && string.IsNullOrWhiteSpace(s)))
            .GroupBy(x => x.Key!.ToString() ?? "")
            .Where(g => g.Count() > 1);

        foreach (var g in groups)
        {
            foreach (var x in g)
            {
                if (!x.Row.FieldErrors.TryGetValue(col.ColumnName, out var list))
                    list = new List<string>();
                list.Add($"值 '{g.Key}' 在列中重复");
                x.Row.FieldErrors[col.ColumnName] = list;
            }
        }
    }

    // ============================================================
    // 行管理
    // ============================================================

    private void AddRow()
    {
        _rows.Add(new RowState
        {
            RowId = null,
            RowOrder = _rows.Count + 1,
            Fields = new Dictionary<string, object?>()
        });
        StateHasChanged();
    }

    private void RemoveRow(int index)
    {
        if (index >= 0 && index < _rows.Count)
        {
            _rows.RemoveAt(index);
            // 唯一性可能变化，重算
            if (_tableDef is not null)
            {
                foreach (var col in _tableDef.Columns.Where(c => c.IsUnique && !c.IsDeleted))
                    RecheckUniqueForColumn(col);
            }
            StateHasChanged();
        }
    }

    // ============================================================
    // 保存
    // ============================================================

    private async Task SaveAsync()
    {
        ValidateAllRows();

        if (HasErrors)
        {
            Snackbar.Add("存在校验错误，请先修正", Severity.Warning);
            return;
        }

        var value = new CustomTableValue { TableName = TableName };
        for (int i = 0; i < _rows.Count; i++)
        {
            var row = _rows[i];
            value.Rows.Add(new CustomTableRowValue
            {
                RowId = row.RowId,
                RowOrder = row.RowOrder,
                Fields = new Dictionary<string, object?>(row.Fields)
            });
        }

        _saving = true;
        try
        {
            var (ok, error) = await Api.ReplaceCustomTableAsync(
                EntityType, EntityId, TableName, value);

            if (!ok)
            {
                Snackbar.Add($"保存失败：{error ?? "未知错误"}", Severity.Error);
                return;
            }

            Snackbar.Add("表数据保存成功", Severity.Success);
            await LoadAsync();
        }
        finally
        {
            _saving = false;
        }
    }

    // ============================================================
    // 辅助
    // ============================================================

    private static string ColLabel(CustomTableColumnDto col)
        => col.IsRequired ? $"{col.DisplayName} *" : col.DisplayName;

    private static bool TryGetDecimal(object? v, out decimal d)
    {
        switch (v)
        {
            case decimal dd: d = dd; return true;
            case double db: d = (decimal)db; return true;
            case int i: d = i; return true;
            case long l: d = l; return true;
            default: d = 0; return false;
        }
    }

    private static List<string> ExtractAllowedValues(JsonElement? allowedValues)
    {
        var result = new List<string>();
        if (allowedValues is JsonElement av && av.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in av.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    result.Add(item.GetString()!);
            }
        }
        return result;
    }
}
```

## 文件 41/46 TreeGraph.Blazor/Components/Shared/DynamicFieldRenderer.razor

```razor
@using System.Text.Json
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos

@* DynamicFieldRenderer.razor *@
@* 属性字段渲染器。错误以结构化 FieldValidationError 传入，只取 Path=="" 的作为属性级错误。 *@

@{
    var selfErrors = FilterSelfErrors();
}

@if (Attribute.DataType == "string")
{
    <MudTextField T="string"
                  Label="@DisplayLabel"
                  Value="@(Value as string)"
                  ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                  Variant="Variant.Outlined"
                  Required="@Attribute.IsRequired"
                  Error="@HasSelfErrors"
                  ErrorText="@SelfErrorText"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Attribute.DataType == "int")
{
    <MudNumericField T="long?"
                     Label="@DisplayLabel"
                     Value="@LongValue"
                     ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                     Variant="Variant.Outlined"
                     Required="@Attribute.IsRequired"
                     Error="@HasSelfErrors"
                     ErrorText="@SelfErrorText"
                     FullWidth="true" />
}
else if (Attribute.DataType == "decimal")
{
    @if (Attribute.Unit is not null)
    {
        var numeric = Value as NumericInput ?? new NumericInput();
        <MudGrid>
            <MudItem xs="12" md="8">
                <MudNumericField T="decimal?"
                                 Label="@DisplayLabel"
                                 Value="@numeric.Value"
                                 ValueChanged="@(v =>
                                 {
                                     var n = Value as NumericInput ?? new NumericInput();
                                     n.Value = v ?? 0;
                                     ValueChanged.InvokeAsync(n);
                                 })"
                                 Variant="Variant.Outlined"
                                 Required="@Attribute.IsRequired"
                                 Error="@HasSelfErrors"
                                 ErrorText="@SelfErrorText"
                                 FullWidth="true" />
            </MudItem>
            <MudItem xs="12" md="4">
                <MudSelect T="Guid?"
                           Label="单位"
                           Value="@numeric.UnitId"
                           ValueChanged="@(u =>
                           {
                               var n = Value as NumericInput ?? new NumericInput();
                               n.UnitId = u;
                               ValueChanged.InvokeAsync(n);
                           })"
                           Variant="Variant.Outlined"
                           FullWidth="true">
                    @foreach (var u in Attribute.AvailableUnits ?? Array.Empty<UnitSchemaDto>())
                    {
                        <MudSelectItem T="Guid?" Value="@u.Id">
                            @u.Name (@u.Symbol)
                        </MudSelectItem>
                    }
                </MudSelect>
            </MudItem>
        </MudGrid>
    }
    else
    {
        <MudNumericField T="decimal?"
                         Label="@DisplayLabel"
                         Value="@DecimalValue"
                         ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                         Variant="Variant.Outlined"
                         Required="@Attribute.IsRequired"
                         Error="@HasSelfErrors"
                         ErrorText="@SelfErrorText"
                         FullWidth="true" />
    }
}
else if (Attribute.DataType == "bool")
{
    <MudSwitch T="bool"
               Label="@DisplayLabel"
               Value="@(Value is bool b && b)"
               ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
               Color="Color.Primary" />
}
else if (Attribute.DataType == "datetime")
{
    <MudTextField T="string"
                  Label="@(DisplayLabel + " (ISO 8601)")"
                  Placeholder="2026-10-01T10:00:00+08:00"
                  Value="@(Value is DateTimeOffset dto ? dto.ToString("O") : Value as string)"
                  ValueChanged="@OnDatetimeTextChanged"
                  Variant="Variant.Outlined"
                  Required="@Attribute.IsRequired"
                  Error="@HasSelfErrors"
                  ErrorText="@SelfErrorText"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Attribute.DataType == "date")
{
    <MudDatePicker Label="@DisplayLabel"
                   Date="@DateValue"
                   DateChanged="@OnDateChanged"
                   Variant="Variant.Outlined"
                   Required="@Attribute.IsRequired"
                   Error="@HasSelfErrors"
                   ErrorText="@SelfErrorText" />
}
else if (Attribute.DataType == "time")
{
    <MudTimePicker Label="@DisplayLabel"
                   Time="@TimeValue"
                   TimeChanged="@OnTimeChanged"
                   Variant="Variant.Outlined"
                   Required="@Attribute.IsRequired"
                   Error="@HasSelfErrors"
                   ErrorText="@SelfErrorText" />
}
else if (Attribute.DataType == "single_choice")
{
    <MudSelect T="string"
               Label="@DisplayLabel"
               Value="@(Value as string)"
               ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
               Variant="Variant.Outlined"
               Required="@Attribute.IsRequired"
               Error="@HasSelfErrors"
               ErrorText="@SelfErrorText"
               FullWidth="true">
        @if (!Attribute.IsRequired)
        {
            <MudSelectItem T="string" Value="@((string?)null)">（未选择）</MudSelectItem>
        }
        @foreach (var item in Attribute.OptionSet?.Items ?? Array.Empty<OptionItemSchemaDto>())
        {
            <MudSelectItem T="string" Value="@item.Value">
                @item.Label
            </MudSelectItem>
        }
    </MudSelect>
}
else if (Attribute.DataType is "json" or "file")
{
    <MudTextField T="string"
                  Label="@(DisplayLabel + " (JSON)")"
                  Value="@JsonText"
                  ValueChanged="@OnJsonTextChanged"
                  Variant="Variant.Outlined"
                  Lines="4"
                  Required="@Attribute.IsRequired"
                  Error="@(_jsonParseError is not null || HasSelfErrors)"
                  ErrorText="@(_jsonParseError ?? SelfErrorText)"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Attribute.DataType == "composite")
{
    @if (Attribute.CompositeType is not null)
    {
        <MudText Typo="Typo.subtitle2" Class="mb-2">@DisplayLabel</MudText>

        @if (HasSelfErrors)
        {
            <MudAlert Severity="Severity.Error" Class="mb-2">
                <ul style="margin: 0; padding-left: 20px;">
                    @foreach (var e in selfErrors)
                    {
                        <li>@e.Message</li>
                    }
                </ul>
            </MudAlert>
        }

        <MudPaper Class="@CompositePaperClass" Outlined="true">
            <CompositeFieldEditor TypeSchema="@Attribute.CompositeType"
                                  BasePath=""
                                  Value="@(Value as Dictionary<string, object?>)"
                                  ValueChanged="@ValueChanged"
                                  Errors="@NestedErrors" />
        </MudPaper>
    }
}
else if (Attribute.DataType == "table")
{
    <MudAlert Severity="Severity.Info">
        @DisplayLabel 是自定义表——请使用下方的子表编辑区域
    </MudAlert>
}

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attribute { get; set; } = null!;
    [Parameter] public object? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    /// <summary>该属性的全部错误（可能包含子路径）。</summary>
    [Parameter] public IReadOnlyList<FieldValidationError>? Errors { get; set; }

    private string? _jsonParseError;

    private IReadOnlyList<FieldValidationError> FilterSelfErrors()
        => Errors?.Where(e => string.IsNullOrEmpty(e.Path)).ToList()
           ?? new List<FieldValidationError>();

    private IReadOnlyList<FieldValidationError> NestedErrors
        => Errors?.Where(e => !string.IsNullOrEmpty(e.Path)).ToList()
           ?? new List<FieldValidationError>();

    private bool HasSelfErrors => FilterSelfErrors().Count > 0;
    private string? SelfErrorText => HasSelfErrors
        ? string.Join("；", FilterSelfErrors().Select(e => e.Message))
        : null;

    private string DisplayLabel =>
        Attribute.IsRequired ? $"{Attribute.DisplayName} *" : Attribute.DisplayName;

    private string CompositePaperClass =>
        NestedErrors.Count > 0 ? "pa-3 mud-border-error" : "pa-3";

    private long? LongValue => Value switch
    {
        long l => l,
        int i => i,
        _ => null
    };

    private decimal? DecimalValue => Value switch
    {
        decimal d => d,
        double db => (decimal)db,
        int i => i,
        long l => l,
        _ => null
    };

    private DateTime? DateValue => Value is string s
        && DateOnly.TryParse(s, out var d)
            ? d.ToDateTime(TimeOnly.MinValue)
            : null;

    private TimeSpan? TimeValue => Value is string s
        && TimeOnly.TryParse(s, out var t)
            ? t.ToTimeSpan()
            : null;

    private string JsonText
    {
        get
        {
            if (Value is null) return "";
            if (Value is JsonElement elem) return elem.GetRawText();
            return JsonSerializer.Serialize(Value, JsonOpts);
        }
    }

    private void OnDatetimeTextChanged(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            ValueChanged.InvokeAsync(null);
        else if (DateTimeOffset.TryParse(v, out var parsed))
            ValueChanged.InvokeAsync(parsed);
    }

    private void OnDateChanged(DateTime? v)
        => ValueChanged.InvokeAsync(v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null);

    private void OnTimeChanged(TimeSpan? v)
        => ValueChanged.InvokeAsync(v.HasValue
            ? new TimeOnly(v.Value.Hours, v.Value.Minutes, v.Value.Seconds).ToString("HH:mm:ss")
            : null);

    private void OnJsonTextChanged(string? text)
    {
        _jsonParseError = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            ValueChanged.InvokeAsync(null);
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(text);
            ValueChanged.InvokeAsync(doc.RootElement.Clone());
        }
        catch (JsonException ex)
        {
            _jsonParseError = $"JSON 格式错误：{ex.Message}";
        }
    }

    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = false };
}
```

## 文件 42/46 TreeGraph.Blazor/Components/Shared/EntityHistoryPanel.razor

```razor
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@inject EavApiClient Api

@* EntityHistoryPanel.razor *@
@* 审计历史面板：在实体编辑页底部展示变更记录。 *@

<MudExpansionPanel @ref="_panel"
                   Text="@($"变更历史 ({_items?.Count ?? 0})")"
                   Expanded="_panelExpanded"
                   ExpandedChanged="OnPanelExpandedChanged"
                   Class="mt-4">
    <TitleContent>
        <div class="d-flex align-center" style="width: 100%;">
            <MudIcon Icon="@Icons.Material.Filled.History"
                     Color="Color.Primary" Class="mr-2" />
            <MudText Typo="Typo.subtitle1">变更历史</MudText>
            @if (_items is { Count: > 0 })
            {
                <MudChip T="string" Size="Size.Small" Class="ml-2">
                    @_items.Count 条
                </MudChip>
            }
            <MudSpacer />
            <MudIconButton Icon="@Icons.Material.Filled.Refresh"
                           Size="Size.Small"
                           OnClick="@(async () => await LoadAsync())"
                           title="刷新" />
        </div>
    </TitleContent>

    <ChildContent>
        @if (_loading)
        {
            <MudProgressLinear Indeterminate="true" />
        }
        else if (_items is null || _items.Count == 0)
        {
            <MudAlert Severity="Severity.Info">
                暂无变更历史
            </MudAlert>
        }
        else
        {
            <MudTable Items="_items" Bordered="true" Hover="true"
                      Elevation="0">
                <HeaderContent>
                    <MudTh>时间</MudTh>
                    <MudTh>属性</MudTh>
                    <MudTh>类型</MudTh>
                    <MudTh>旧值</MudTh>
                    <MudTh>新值</MudTh>
                    <MudTh>操作人</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd>
                        @context.ChangedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    </MudTd>
                    <MudTd>
                        <code>@context.AttributeName</code>
                    </MudTd>
                    <MudTd>
                        <MudChip T="string" Size="Size.Small"
                                 Color="@GetChangeColor(context.ChangeType)">
                            @GetChangeLabel(context.ChangeType)
                        </MudChip>
                    </MudTd>
                    <MudTd>
                        @if (string.IsNullOrEmpty(context.OldValue))
                        {
                            <span class="mud-text-secondary">—</span>
                        }
                        else
                        {
                            <code>@Truncate(context.OldValue, 60)</code>
                        }
                    </MudTd>
                    <MudTd>
                        @if (string.IsNullOrEmpty(context.NewValue))
                        {
                            <span class="mud-text-secondary">—</span>
                        }
                        else
                        {
                            <code>@Truncate(context.NewValue, 60)</code>
                        }
                    </MudTd>
                    <MudTd>
                        @if (string.IsNullOrEmpty(context.ChangedBy))
                        {
                            <span class="mud-text-secondary">—</span>
                        }
                        else
                        {
                            @context.ChangedBy
                        }
                    </MudTd>
                </RowTemplate>
            </MudTable>
        }
    </ChildContent>
</MudExpansionPanel>

@code {
    [Parameter, EditorRequired] public string EntityType { get; set; } = "";
    [Parameter, EditorRequired] public string EntityId { get; set; } = "";

    /// <summary>
    /// 父组件保存成功后自增此值，触发面板重新加载。
    /// 首次渲染时不加载（用户展开面板时按需加载）。
    /// </summary>
    [Parameter] public int RefreshToken { get; set; }

    private MudExpansionPanel? _panel;
    private bool _panelExpanded;
    private IReadOnlyList<EntityHistoryDto>? _items;
    private bool _loading;
    private int _lastRefreshToken = -1;
    private bool _loaded;

    protected override void OnAfterRender(bool firstRender)
    {
        base.OnAfterRender(firstRender);

        // 刷新令牌变化时：如面板展开则立即重载，否则清空由下次展开触发
        if (RefreshToken != _lastRefreshToken)
        {
            _lastRefreshToken = RefreshToken;
            if (_panelExpanded)
            {
                _ = LoadAsync();
            }
            else
            {
                _loaded = false;
                _items = null;
            }
        }
    }

    /// <summary>
    /// ★ 修复 P0-NEW-1：面板展开/折叠事件。
    /// 展开时若尚未加载则立即加载；否则什么都不做（保留已加载数据）。
    /// 通过 @bind-Expanded + ExpandedChanged 触发，之前的方法从未被绑定。
    /// </summary>
    private async Task OnPanelExpandedChanged(bool expanded)
    {
        _panelExpanded = expanded;
        if (expanded && !_loaded)
        {
            await LoadAsync();
        }
    }

    private async Task LoadAsync()
    {
        _loading = true;
        _loaded = true;
        try
        {
            _items = await Api.GetHistoryAsync(EntityType, EntityId);
        }
        finally
        {
            _loading = false;
        }
    }

    private static Color GetChangeColor(string changeType) => changeType switch
    {
        "Insert" => Color.Success,
        "Update" => Color.Info,
        "Delete" => Color.Error,
        _ => Color.Default
    };

    private static string GetChangeLabel(string changeType) => changeType switch
    {
        "Insert" => "新增",
        "Update" => "修改",
        "Delete" => "删除",
        _ => changeType
    };

    private static string Truncate(string s, int max)
        => s.Length <= max ? s : s.Substring(0, max) + "…";
}
```

## 文件 43/46 TreeGraph.Blazor/Components/Shared/QueryFilterBuilder.razor

```razor
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos

@* QueryFilterBuilder.razor *@

@* 多个过滤条件的组合管理。 *@

<MudExpansionPanel Text="动态查询" Class="mb-3">
    <TitleContent>
        <div class="d-flex align-center" style="width: 100%;">
            <MudIcon Icon="@Icons.Material.Filled.FilterAlt"
                     Color="Color.Primary" Class="mr-2" />
            <MudText Typo="Typo.subtitle1">动态查询</MudText>
            @if (Filters.Count > 0)
            {
                <MudChip T="string" Size="Size.Small" Color="Color.Info" Class="ml-2">
                    @Filters.Count 个条件
                </MudChip>
            }
        </div>
    </TitleContent>

    <ChildContent>
        @if (SupportedAttributes.Count == 0)
        {
            <MudAlert Severity="Severity.Info">
                该实体类型没有可搜索的属性。
            </MudAlert>
        }
        else
        {
            @foreach (var filter in Filters.ToList())
            {
                var captured = filter;
                <QueryFilterEditor Filter="captured"
                                   SupportedAttributes="SupportedAttributes"
                                   OnRemove="@(async () => await RemoveFilterAsync(captured))"
                                   OnChanged="OnFilterChanged" />
            }

            <div class="d-flex" style="gap: 8px;">
                <MudButton Variant="Variant.Outlined"
                           Color="Color.Primary"
                           Size="Size.Small"
                           StartIcon="@Icons.Material.Filled.Add"
                           OnClick="AddFilter">
                    添加条件
                </MudButton>
                @if (Filters.Count > 0)
                {
                    <MudButton Variant="Variant.Outlined"
                               Color="Color.Default"
                               Size="Size.Small"
                               StartIcon="@Icons.Material.Filled.Clear"
                               OnClick="ClearAllAsync">
                        清空条件
                    </MudButton>
                    <MudButton Variant="Variant.Filled"
                               Color="Color.Success"
                               Size="Size.Small"
                               StartIcon="@Icons.Material.Filled.Search"
                               OnClick="@(async () => await OnApply.InvokeAsync())">
                        应用查询
                    </MudButton>
                }
            </div>
        }
    </ChildContent>
</MudExpansionPanel>

@code {
    [Parameter, EditorRequired] public List<AttributeFilter> Filters { get; set; } = new();
    [Parameter, EditorRequired] public IReadOnlyList<AttributeSchemaDto> SupportedAttributes { get; set; }
        = Array.Empty<AttributeSchemaDto>();
    [Parameter] public EventCallback OnApply { get; set; }

    /// <summary>
    /// ★ 修复 P0-NEW-2：Add/Remove/Clear 直接改 List 后必须 StateHasChanged，
    ///   否则父级不会重渲染，UI 表现"点了没反应"。
    /// </summary>
    private void AddFilter()
    {
        var first = SupportedAttributes.FirstOrDefault();
        var filter = new AttributeFilter();
        if (first is not null)
        {
            filter.AttributeName = first.AttributeName;
            var ops = FilterOperatorCatalog.For(first);
            if (ops.Count > 0) filter.Operator = ops[0].Code;
        }
        Filters.Add(filter);
        StateHasChanged();
    }

    /// <summary>
    /// 删除条件后立即触发查询：用户预期"移除条件 → 结果刷新"。
    /// </summary>
    private async Task RemoveFilterAsync(AttributeFilter filter)
    {
        Filters.Remove(filter);
        StateHasChanged();
        await OnApply.InvokeAsync();
    }

    /// <summary>
    /// 清空全部条件后触发查询，回到"无过滤"的完整列表。
    /// </summary>
    private async Task ClearAllAsync()
    {
        Filters.Clear();
        StateHasChanged();
        await OnApply.InvokeAsync();
    }

    private async Task OnFilterChanged()
    {
        await OnApply.InvokeAsync();
    }
}
```

## 文件 44/46 TreeGraph.Blazor/Components/Shared/QueryFilterEditor.razor

```razor
@using System.Globalization
@using System.Text.Json
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@* QueryFilterEditor.razor *@
@* 单条过滤条件编辑器：属性 + 运算符 + 值。 *@

<MudPaper Class="pa-3 mb-2" Outlined="true">
    <MudGrid Spacing="2">
        @* 属性 *@
        <MudItem xs="12" md="4">
            <MudSelect T="string"
                       Value="@Filter.AttributeName"
                       ValueChanged="OnAttributeChanged"
                       Label="属性"
                       Variant="Variant.Outlined"
                       Margin="Margin.Dense"
                       FullWidth="true">
                @foreach (var attr in SupportedAttributes)
                {
                    <MudSelectItem T="string" Value="@attr.AttributeName">
                        @attr.DisplayName (@attr.DataType)
                    </MudSelectItem>
                }
            </MudSelect>
        </MudItem>

        @* 运算符 *@
        <MudItem xs="12" md="3">
            <MudSelect T="string"
                       Value="@Filter.Operator"
                       ValueChanged="OnOperatorChanged"
                       Label="运算符"
                       Variant="Variant.Outlined"
                       Margin="Margin.Dense"
                       Disabled="@(SelectedAttr is null)"
                       FullWidth="true">
                @if (SelectedAttr is not null)
                {
                    foreach (var op in FilterOperatorCatalog.For(SelectedAttr))
                    {
                        <MudSelectItem T="string" Value="@op.Code">@op.DisplayName</MudSelectItem>
                    }
                }
            </MudSelect>
        </MudItem>

        @* 值 *@
        <MudItem xs="12" md="4">
            @if (SelectedAttr is not null && OperatorInfo is not null)
            {
                @if (OperatorInfo.IsMultiValue)
                {
                    <MudTextField T="string"
                                  Label="值（逗号分隔）"
                                  Value="@MultiValueText"
                                  ValueChanged="OnMultiValueChanged"
                                  Variant="Variant.Outlined"
                                  Margin="Margin.Dense"
                                  FullWidth="true" />
                }
                else if (OperatorInfo.NeedsValue2)
                {
                    <div class="d-flex" style="gap: 8px;">
                        @RenderValueInput(1)
                        @RenderValueInput(2)
                    </div>
                }
                else
                {
                    @RenderValueInput(1)
                }
            }
        </MudItem>

        @* 删除 *@
        <MudItem xs="12" md="1" Class="d-flex align-center">
            <MudIconButton Icon="@Icons.Material.Filled.Close"
                           Size="Size.Small"
                           Color="Color.Error"
                           OnClick="@(() => OnRemove.InvokeAsync())"
                           title="移除" />
        </MudItem>
    </MudGrid>
</MudPaper>

@code {
    [Parameter, EditorRequired] public AttributeFilter Filter { get; set; } = new();
    [Parameter, EditorRequired] public IReadOnlyList<AttributeSchemaDto> SupportedAttributes { get; set; }
        = Array.Empty<AttributeSchemaDto>();
    [Parameter] public EventCallback OnRemove { get; set; }
    [Parameter] public EventCallback OnChanged { get; set; }

    private AttributeSchemaDto? SelectedAttr =>
        SupportedAttributes.FirstOrDefault(a => a.AttributeName == Filter.AttributeName);

    private FilterOperatorCatalog.OperatorInfo? OperatorInfo =>
        SelectedAttr is null ? null : FilterOperatorCatalog.Get(SelectedAttr, Filter.Operator);

    private string MultiValueText
    {
        get => Filter.Value is JsonElement je && je.ValueKind == JsonValueKind.Array
            ? string.Join(", ", je.EnumerateArray().Select(x => x.ToString()))
            : Filter.Value?.ToString() ?? "";
        set { }
    }

    private async Task OnAttributeChanged(string name)
    {
        Filter.AttributeName = name;
        Filter.Operator = "";
        Filter.Value = null;
        Filter.Value2 = null;

        // 自动选默认运算符
        var attr = SelectedAttr;
        if (attr is not null)
        {
            var ops = FilterOperatorCatalog.For(attr);
            if (ops.Count > 0)
                Filter.Operator = ops[0].Code;
        }

        await OnChanged.InvokeAsync();
    }

    private async Task OnOperatorChanged(string op)
    {
        Filter.Operator = op;
        Filter.Value = null;
        Filter.Value2 = null;
        await OnChanged.InvokeAsync();
    }

    private async Task OnMultiValueChanged(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            Filter.Value = null;
        }
        else
        {
            var parts = text.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).ToArray();

            // 根据属性类型转换
            var attr = SelectedAttr;
            Filter.Value = attr?.DataType switch
            {
                "int" => parts.Select(s => long.TryParse(s, out var l) ? l : 0).ToArray(),
                "decimal" => parts.Select(s =>
                    decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
                        ? d : 0m).ToArray(),
                _ => parts
            };
        }
        await OnChanged.InvokeAsync();
    }

    private RenderFragment RenderValueInput(int position) => builder =>
    {
        var attr = SelectedAttr;
        if (attr is null) return;

        void OnValueChanged(object? v)
        {
            if (position == 1) Filter.Value = v;
            else Filter.Value2 = v;

            // 状态变化后通知父级（异步 fire-and-forget）
            _ = OnChanged.InvokeAsync();
        }

        builder.OpenComponent(0, typeof(ValueInput));
        builder.AddAttribute(1, nameof(ValueInput.Attribute), attr);
        builder.AddAttribute(2, nameof(ValueInput.Value),
            position == 1 ? Filter.Value : Filter.Value2);
        builder.AddAttribute(3, nameof(ValueInput.ValueChanged),
            (EventCallback<object?>)new EventCallback<object?>(null, (Action<object?>)OnValueChanged));
        builder.CloseComponent();
    };
}
```

## 文件 45/46 TreeGraph.Blazor/Components/Shared/RenderLeafField.razor

```razor
@using System.Text.Json
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@* RenderLeafField.razor *@
@* 组合叶子字段。★ 支持单字段 inline 高亮；★ single_choice 用 AllowedValues 生成下拉。 *@

@{
    var errorText = Errors is { Count: > 0 }
        ? string.Join("；", Errors.Select(e => e.Message))
        : null;
    var hasError = errorText is not null;
}

@if (Field.DataType == "string")
{
    <MudTextField T="string"
                  Label="@Field.DisplayName"
                  Value="@(Value as string)"
                  ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                  Variant="Variant.Outlined"
                  Required="@Field.IsRequired"
                  Error="@hasError"
                  ErrorText="@errorText"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Field.DataType == "int")
{
    <MudNumericField T="long?"
                     Label="@Field.DisplayName"
                     Value="@(Value switch { long l => l, int i => i, _ => null })"
                     ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                     Variant="Variant.Outlined"
                     Required="@Field.IsRequired"
                     Error="@hasError"
                     ErrorText="@errorText"
                     FullWidth="true" />
}
else if (Field.DataType == "decimal")
{
    @if (Field.Unit is not null)
    {
        var numeric = Value as NumericInput ?? new NumericInput();
        <MudGrid>
            <MudItem xs="12" md="8">
                <MudNumericField T="decimal?"
                                 Label="@Field.DisplayName"
                                 Value="@numeric.Value"
                                 ValueChanged="@(v =>
                                 {
                                     var n = Value as NumericInput ?? new NumericInput();
                                     n.Value = v ?? 0;
                                     ValueChanged.InvokeAsync(n);
                                 })"
                                 Variant="Variant.Outlined"
                                 Required="@Field.IsRequired"
                                 Error="@hasError"
                                 ErrorText="@errorText"
                                 FullWidth="true" />
            </MudItem>
            <MudItem xs="12" md="4">
                <MudSelect T="Guid?"
                           Label="单位"
                           Value="@numeric.UnitId"
                           ValueChanged="@(u =>
                           {
                               var n = Value as NumericInput ?? new NumericInput();
                               n.UnitId = u;
                               ValueChanged.InvokeAsync(n);
                           })"
                           Variant="Variant.Outlined"
                           FullWidth="true">
                    @foreach (var u in Field.AvailableUnits ?? Array.Empty<UnitSchemaDto>())
                    {
                        <MudSelectItem T="Guid?" Value="@u.Id">
                            @u.Name (@u.Symbol)
                        </MudSelectItem>
                    }
                </MudSelect>
            </MudItem>
        </MudGrid>
    }
    else
    {
        <MudNumericField T="decimal?"
                         Label="@Field.DisplayName"
                         Value="@(Value switch
                         {
                             decimal d => d,
                             double db => (decimal)db,
                             int i => i,
                             long l => l,
                             _ => null
                         })"
                         ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                         Variant="Variant.Outlined"
                         Required="@Field.IsRequired"
                         Error="@hasError"
                         ErrorText="@errorText"
                         FullWidth="true" />
    }
}
else if (Field.DataType == "bool")
{
    <MudSwitch T="bool"
               Label="@Field.DisplayName"
               Value="@(Value is bool b && b)"
               ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
               Color="Color.Primary" />
}
else if (Field.DataType == "datetime")
{
    <MudTextField T="string"
                  Label="@(Field.DisplayName + " (ISO 8601)")"
                  Value="@(Value is DateTimeOffset dto ? dto.ToString("O") : Value as string)"
                  ValueChanged="@OnDatetimeTextChanged"
                  Variant="Variant.Outlined"
                  Required="@Field.IsRequired"
                  Error="@hasError"
                  ErrorText="@errorText"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Field.DataType == "date")
{
    <MudDatePicker Label="@Field.DisplayName"
                   Date="@(Value is string s && DateOnly.TryParse(s, out var d)
                          ? d.ToDateTime(TimeOnly.MinValue) : null)"
                   DateChanged="@(v => ValueChanged.InvokeAsync(
                       v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null))"
                   Variant="Variant.Outlined"
                   Required="@Field.IsRequired"
                   Error="@hasError"
                   ErrorText="@errorText" />
}
else if (Field.DataType == "time")
{
    <MudTimePicker Label="@Field.DisplayName"
                   Time="@(Value is string s && TimeOnly.TryParse(s, out var t)
                          ? t.ToTimeSpan() : null)"
                   TimeChanged="@(v => ValueChanged.InvokeAsync(
                       v.HasValue
                           ? new TimeOnly(v.Value.Hours, v.Value.Minutes, v.Value.Seconds)
                               .ToString("HH:mm:ss")
                           : null))"
                   Variant="Variant.Outlined"
                   Required="@Field.IsRequired"
                   Error="@hasError"
                   ErrorText="@errorText" />
}
else if (Field.DataType == "single_choice")
{
    @if (Field.OptionSet is not null)
    {
        <MudSelect T="string"
                   Label="@Field.DisplayName"
                   Value="@(Value as string)"
                   ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                   Variant="Variant.Outlined"
                   Required="@Field.IsRequired"
                   Error="@hasError"
                   ErrorText="@errorText"
                   FullWidth="true">
            @if (!Field.IsRequired)
            {
                <MudSelectItem T="string" Value="@((string?)null)">（未选择）</MudSelectItem>
            }
            @foreach (var item in Field.OptionSet.Items)
            {
                <MudSelectItem T="string" Value="@item.Value">
                    @item.Label
                </MudSelectItem>
            }
        </MudSelect>
    }
    else
    {
        var options = ExtractAllowedValues();
        @if (options.Count > 0)
        {
            <MudSelect T="string"
                       Label="@Field.DisplayName"
                       Value="@(Value as string)"
                       ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                       Variant="Variant.Outlined"
                       Required="@Field.IsRequired"
                       Error="@hasError"
                       ErrorText="@errorText"
                       FullWidth="true">
                @if (!Field.IsRequired)
                {
                    <MudSelectItem T="string" Value="@((string?)null)">（未选择）</MudSelectItem>
                }
                @foreach (var opt in options)
                {
                    <MudSelectItem T="string" Value="@opt">@opt</MudSelectItem>
                }
            </MudSelect>
        }
        else
        {
            <MudTextField T="string"
                          Label="@(Field.DisplayName + " (无选项，自由输入)")"
                          Value="@(Value as string)"
                          ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                          Variant="Variant.Outlined"
                          Required="@Field.IsRequired"
                          Error="@hasError"
                          ErrorText="@errorText"
                          Immediate="true"
                          FullWidth="true" />
        }
    }
}
else
{
    @* json / file / 其它 *@
    <MudTextField T="string"
                  Label="@(Field.DisplayName + " (JSON)")"
                  Value="@(Value is JsonElement je ? je.GetRawText()
                            : Value is null ? null
                            : JsonSerializer.Serialize(Value))"
                  ValueChanged="@OnJsonTextChanged"
                  Variant="Variant.Outlined"
                  Lines="3"
                  Required="@Field.IsRequired"
                  Error="@hasError"
                  ErrorText="@errorText"
                  Immediate="true"
                  FullWidth="true" />
}

@code {
    [Parameter, EditorRequired] public CompositeFieldSchemaDto Field { get; set; } = null!;
    [Parameter] public object? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }

    /// <summary>属于该叶子的错误（Path 已由父级剥离，Path 为 "" 或 "[i]" 等）。</summary>
    [Parameter] public IReadOnlyList<FieldValidationError>? Errors { get; set; }

    private void OnDatetimeTextChanged(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
            ValueChanged.InvokeAsync(null);
        else if (DateTimeOffset.TryParse(v, out var parsed))
            ValueChanged.InvokeAsync(parsed);
    }

    private void OnJsonTextChanged(string? v)
    {
        if (string.IsNullOrWhiteSpace(v))
        {
            ValueChanged.InvokeAsync(null);
            return;
        }
        try
        {
            using var doc = JsonDocument.Parse(v);
            ValueChanged.InvokeAsync(doc.RootElement.Clone());
        }
        catch (JsonException) { /* 用户继续输入 */ }
    }

    private List<string> ExtractAllowedValues()
    {
        var result = new List<string>();
        if (Field.AllowedValues is JsonElement av && av.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in av.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                    result.Add(item.GetString()!);
            }
        }
        return result;
    }
}
```

## 文件 46/46 TreeGraph.Blazor/Components/Shared/ValueInput.razor

```razor
@using System.Globalization
@using System.Text.Json
@using TreeGraph.Blazor.Services
@using TreeGraph.Shared.Eav.Dtos
@* ValueInput.razor *@
@* 单值输入组件，按属性类型渲染。 *@

@if (Attribute.DataType == "string")
{
    <MudTextField T="string"
                  Value="@(Value as string)"
                  ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                  Variant="Variant.Outlined"
                  Margin="Margin.Dense"
                  Placeholder="值"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Attribute.DataType == "int")
{
    <MudNumericField T="long?"
                     Value="@(Value switch { long l => l, int i => i, _ => null })"
                     ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                     Variant="Variant.Outlined"
                     Margin="Margin.Dense"
                     Placeholder="值"
                     FullWidth="true" />
}
else if (Attribute.DataType == "decimal")
{
    @if (Attribute.Unit is not null)
    {
        var numeric = Value as NumericInput ?? new NumericInput();
        <div class="d-flex" style="gap: 4px;">
            <MudNumericField T="decimal?"
                             Value="@numeric.Value"
                             ValueChanged="@(v =>
                             {
                                 var n = Value as NumericInput ?? new NumericInput();
                                 n.Value = v ?? 0;
                                 ValueChanged.InvokeAsync(n);
                             })"
                             Variant="Variant.Outlined"
                             Margin="Margin.Dense"
                             Placeholder="值"
                             FullWidth="true" />
            <MudSelect T="Guid?"
                       Value="@numeric.UnitId"
                       ValueChanged="@(u =>
                       {
                           var n = Value as NumericInput ?? new NumericInput();
                           n.UnitId = u;
                           ValueChanged.InvokeAsync(n);
                       })"
                       Variant="Variant.Outlined"
                       Margin="Margin.Dense"
                       Style="min-width: 100px;">
                @foreach (var u in Attribute.AvailableUnits ?? Array.Empty<UnitSchemaDto>())
                {
                    <MudSelectItem T="Guid?" Value="@u.Id">@u.Symbol</MudSelectItem>
                }
            </MudSelect>
        </div>
    }
    else
    {
        <MudNumericField T="decimal?"
                         Value="@(Value switch
                         {
                             decimal d => d,
                             double db => (decimal)db,
                             int i => i,
                             long l => l,
                             _ => null
                         })"
                         ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                         Variant="Variant.Outlined"
                         Margin="Margin.Dense"
                         Placeholder="值"
                         FullWidth="true" />
    }
}
else if (Attribute.DataType == "bool")
{
    <MudSelect T="bool?"
               Value="@(Value as bool?)"
               ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
               Variant="Variant.Outlined"
               Margin="Margin.Dense"
               FullWidth="true">
        <MudSelectItem T="bool?" Value="@((bool?)null)">（未选择）</MudSelectItem>
        <MudSelectItem T="bool?" Value="@((bool?)true)">是</MudSelectItem>
        <MudSelectItem T="bool?" Value="@((bool?)false)">否</MudSelectItem>
    </MudSelect>
}
else if (Attribute.DataType == "datetime")
{
    <MudTextField T="string"
                  Value="@(Value is DateTimeOffset dto ? dto.ToString("O") : Value as string)"
                  ValueChanged="@(v =>
                  {
                      if (string.IsNullOrWhiteSpace(v))
                          ValueChanged.InvokeAsync(null);
                      else if (DateTimeOffset.TryParse(v, out var parsed))
                          ValueChanged.InvokeAsync(parsed);
                  })"
                  Variant="Variant.Outlined"
                  Margin="Margin.Dense"
                  Placeholder="ISO 8601"
                  Immediate="true"
                  FullWidth="true" />
}
else if (Attribute.DataType == "date")
{
    <MudDatePicker Date="@(Value is string s && DateOnly.TryParse(s, out var d)
                            ? d.ToDateTime(TimeOnly.MinValue) : null)"
                   DateChanged="@(v => ValueChanged.InvokeAsync(
                       v.HasValue ? v.Value.ToString("yyyy-MM-dd") : null))"
                   Variant="Variant.Outlined"
                   Margin="Margin.Dense" />
}
else if (Attribute.DataType == "time")
{
    <MudTimePicker Time="@(Value is string s && TimeOnly.TryParse(s, out var t)
                            ? t.ToTimeSpan() : null)"
                   TimeChanged="@(v => ValueChanged.InvokeAsync(
                       v.HasValue
                           ? new TimeOnly(v.Value.Hours, v.Value.Minutes, v.Value.Seconds)
                               .ToString("HH:mm:ss")
                           : null))"
                   Variant="Variant.Outlined"
                   Margin="Margin.Dense" />
}
else if (Attribute.DataType == "single_choice")
{
    <MudSelect T="string"
               Value="@(Value as string)"
               ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
               Variant="Variant.Outlined"
               Margin="Margin.Dense"
               FullWidth="true">
        <MudSelectItem T="string" Value="@((string?)null)">（未选择）</MudSelectItem>
        @foreach (var item in Attribute.OptionSet?.Items ?? Array.Empty<OptionItemSchemaDto>())
        {
            <MudSelectItem T="string" Value="@item.Value">@item.Label</MudSelectItem>
        }
    </MudSelect>
}
else
{
    <MudTextField T="string"
                  Value="@(Value?.ToString())"
                  ValueChanged="@(v => ValueChanged.InvokeAsync(v))"
                  Variant="Variant.Outlined"
                  Margin="Margin.Dense"
                  Placeholder="值"
                  Immediate="true"
                  FullWidth="true" />
}

@code {
    [Parameter, EditorRequired] public AttributeSchemaDto Attribute { get; set; } = null!;
    [Parameter] public object? Value { get; set; }
    [Parameter] public EventCallback<object?> ValueChanged { get; set; }
}
```

