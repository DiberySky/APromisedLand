using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.Api.Projects.DiberyTree.Interface;

public interface ITreeAttributeService
{
    Task<IReadOnlyList<AttributeDefinitionDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<AttributeDefinitionDto?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AttributeDefinitionDto> CreateAsync(AttributeDefinitionCreateDto dto, CancellationToken cancellationToken = default);
    Task<AttributeDefinitionDto> UpdateAsync(string id, AttributeDefinitionUpdateDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttributeType>> GetAttributeTypesAsync(CancellationToken cancellationToken = default);

    /// <summary>添加属性值，返回生成的值实体（含 Id）</summary>
    Task<AttributeValueBase> AddValueAsync(string nodeId, AddValueDto dto);
    /// <summary>获取单个属性值的 DTO</summary>
    Task<AttributeDto?> GetValueAsync(string nodeId, int id);
    /// <summary>获取节点所有属性值的聚合 DTO</summary>
    Task<NodeDto> GetAllValuesAsync(string nodeId);
    /// <summary>删除属性值，返回是否成功</summary>
    Task<bool> DeleteValueAsync(string nodeId, int id);
}