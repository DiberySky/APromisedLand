using System.Text.Json;
using APromisedLand.Razor.DiberyTree.Attributes.Values;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Enums;
using APromisedLand.Shared.DiberyTree.Attributes.Models;
using APromisedLand.Shared.Helper;
using Microsoft.AspNetCore.Components;

namespace APromisedLand.Razor.DiberyTree.Attributes;

public partial class NodeAttributeItemsSky : ComponentBase
{
    private AttributeJsonValueDto? _tableDefValue;
    private AttributeDefinition? _tableDef;
    
    private DateTimeOffset? _dateOffset;
    private DateTimeOffset? _dateTimeOffset;
    private AttributeItemDto? _defDateItemDto;
    private AttributeItemDto? _defDateTimeItemDto;

    private async Task LoadTableAsync()
    {
        if (ParentId == null) return;

        _loading = true;

        try
        {
            _tableDefValue = await AttributeApiClient.GetSingleValueAsync(NodeId!, ParentId);
            
            _tableDef = await AttributeApiClient.GetDefinitionByIdAsync(_tableDefValue?.DefinitionId!);

            if (_tableDef == null) return;

            TableRowDto? tableRowDto;

            if (RowId != null)
            {
                tableRowDto = await AttributeApiClient.GetRowAsync(RowId);
            }
            else
            {
                var columns = await AttributeApiClient.ListTableColumnsAsync(_tableDef.Id);

                tableRowDto = new TableRowDto
                {
                    CellValues =
                    [
                        .. columns.Select(c => new TableCellDto
                        {
                            ColumnId = c.Id,
                            ColumnDef = c,
                            Value = null,
                        })
                    ]
                };
            }

            _dateOffset = tableRowDto?.CreatedAt.LocalDateTime.Date;
            _dateTimeOffset = tableRowDto?.CreatedAt.LocalDateTime;

            _defDateItemDto = _tableDef.DefDateItemDto(_dateOffset!.Value);
            _defDateTimeItemDto = _tableDef.DefDateTimeItemDto(_dateTimeOffset!.Value);

            var cellValues = tableRowDto?.CellValues ?? [];

            foreach (var cellValue in cellValues)
            {
                var item = SetItemDto(cellValue);

                if (item != null)
                {
                    DefItemDtos.Add(item);
                }
            }
        }
        catch
            (Exception ex)
        {
            Message.Details("加载列失败", ex.Message);
        }
        finally
        {
            _loading = false;
        }
    }

    private AttributeItemDto? SetItemDto(TableCellDto cell)
    {
        var item = new AttributeItemDto
        {
            Id = cell.ColumnId,
            Def = cell.ColumnDef,
            ParentId = ParentId,
        };

        if (cell.Value is not JsonElement je) return item;

        // var je = cell.Value;
        switch (cell.ColumnDef?.TypeEnum)
        {
            case AttributeTypeEnum.文本:
                item.TextValue = je.ToString();
                break;
            case AttributeTypeEnum.整数:
                item.IntegerValue = je.GetInt32();
                break;
            case AttributeTypeEnum.小数:
                item.DecimalValue = je.GetDecimal();
                break;
            case AttributeTypeEnum.日期:
                item.DateValue = je.GetValue<DateTimeOffset>();
                break;
            case AttributeTypeEnum.时间:
                item.TimeValue = je.GetValue<TimeSpan>();
                break;
            case AttributeTypeEnum.日期时间:
                item.DateTimeValue = je.GetValue<DateTimeOffset>();
                break;
            case AttributeTypeEnum.文件:
                break;
            case AttributeTypeEnum.定位:
                break;
            case AttributeTypeEnum.表格:
                item.TableName = je.GetValue<string>();
                break;
            default:
                Message.Warning($"未知属性类型:{cell.ColumnDef?.TypeEnum}");
                item = null;
                break;
        }

        return item;
    }

    public async Task SubmitTableAsync()
    {
        await _form.ValidateAsync();
        if (!_form.IsValid) return;

        var itemDict = DefItemDtos.ToDictionary(kv => kv.Id, kv => kv.ToJsonElement());

        try
        {
            // //_defDateItemDto
            // DateTimeOffset result = _dateOffset.HasValue && _timeSpan.HasValue
            //     ? new DateTimeOffset(
            //         _dateOffset.Value.Date + _timeSpan.Value, // 日期部分 + 时刻部分
            //         _dateOffset.Value.Offset) // 保留原时区
            //     : DateTimeOffset.UtcNow;
            
            var dateOffset = _defDateItemDto?.DateValue;
            var dateTimeOffset = _defDateTimeItemDto?.DateTimeValue;
            
            DateTimeOffset result = dateTimeOffset.HasValue
                ? dateTimeOffset.Value // 直接返回 DateTimeOffset，偏移与原相同
                : dateOffset.HasValue ? dateOffset.Value : DateTimeOffset.UtcNow;

            if (RowId != null)
            {
                // DateTime combinedDateTime = _dateOffset.Value + _timeSpan.Value;


                var updateDto = new UpdateTableRowDto
                {
                    NodeId = NodeId!,
                    DefinitionId = _tableDef!.Id,
                    TableId = ParentId!,
                    RowId = RowId!,
                    CreatedAt = result.LocalDateTime,
                    Values = itemDict
                };

                await AttributeApiClient.UpdateRowAsync(updateDto);
            }
            else
            {
                var addDto = new AddTableRowDto
                {
                    NodeId = NodeId!,
                    DefinitionId = _tableDef!.Id,
                    TableId = ParentId!,
                    CreatedAt = result.LocalDateTime,
                    Values = itemDict
                };

                await AttributeApiClient.AddRowAsync(addDto);
            }
        }
        catch (ArgumentException ex)
        {
            Message.Details("保存失败.", ex.Message);
        }
        catch (Exception ex)
        {
            Message.Details("保存失败.", ex.Message);
        }
    }
}