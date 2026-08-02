// DiberyTreeApiClient.cs
using System.Net.Http.Json;
using APromisedLand.Shared.DiberyTree.Models;
using APromisedLand.Shared.DiberyTree.Attributes.DTOs;
using APromisedLand.Shared.DiberyTree.Attributes.Models;

namespace APromisedLand.MauiBlazor.DiberyTree.Services;

/// <summary>
/// 泛型树 API 客户端，用于调用后端的 TreeControllerBase<T>，
/// 包含树节点 CRUD 以及属性定义和值的操作。
/// </summary>
/// <typeparam name="T">节点值的类型</typeparam>
public class DiberyTreeApiClient<T>(HttpClient httpClient)
{
    private readonly string _basePath = typeof(T).Name; // 例如 "CategoryTree"

    // ==================== 树节点操作（原方法） ====================

    /// <summary>
    /// 获取所有根节点
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetRootNodesAsync(
        string? parentId = null,
        CancellationToken cancellationToken = default)
    {
        var url = parentId == null 
            ? $"{_basePath}/roots" 
            : $"{_basePath}/roots/{Uri.EscapeDataString(parentId)}";
        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TreeNodeDto<T>>>(
                   cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    /// <summary>
    /// 获取指定父节点的子节点（懒加载）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> GetChildrenAsync(
        string parentId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{_basePath}/children/{Uri.EscapeDataString(parentId)}", 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TreeNodeDto<T>>>(
                   cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    /// <summary>
    /// 获取从根节点到指定节点的祖先路径
    /// </summary>
    public async Task<IReadOnlyList<string>> GetAncestorPathAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/ancestors", 
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<string>>(
                   cancellationToken: cancellationToken)
               ?? new List<string>();
    }

    /// <summary>
    /// 条件查询节点（分页、搜索、过滤）
    /// </summary>
    public async Task<IReadOnlyList<TreeNodeDto<T>>> QueryNodesAsync(
        TreeQueryParams queryParams,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{_basePath}/query", queryParams, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<TreeNodeDto<T>>>(
                   cancellationToken: cancellationToken)
               ?? new List<TreeNodeDto<T>>();
    }

    /// <summary>
    /// 获取完整树（包含所有后代）
    /// </summary>
    public async Task<TreeNodeDto<T>?> GetFullTreeAsync(
        string? rootId = null,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/full";
        if (!string.IsNullOrEmpty(rootId))
            url += $"?rootId={Uri.EscapeDataString(rootId)}";

        var response = await httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建新节点
    /// </summary>
    public async Task<TreeNodeDto<T>> CreateNodeAsync(
        TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(_basePath, node, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 更新节点的子项（Reorder）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateChildrenAsync(
        TreeNodeDto<T> nodeDto,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{_basePath}/children", nodeDto, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 更新节点信息（不包括 ParentId，请使用 Move 方法移动）
    /// </summary>
    public async Task<TreeNodeDto<T>> UpdateNodeAsync(
        string id,
        TreeNodeDto<T> node,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"{_basePath}/{Uri.EscapeDataString(id)}", node, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TreeNodeDto<T>>(cancellationToken: cancellationToken))!;
    }

    /// <summary>
    /// 删除节点及其所有子节点
    /// </summary>
    public async Task<bool> DeleteNodeAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 移动节点到新的父节点（null 表示移至根）
    /// </summary>
    public async Task<bool> MoveNodeAsync(
        string nodeId,
        string? newParentId,
        CancellationToken cancellationToken = default)
    {
        var url = $"{_basePath}/move?nodeId={Uri.EscapeDataString(nodeId)}";
        if (!string.IsNullOrEmpty(newParentId))
            url += $"&newParentId={Uri.EscapeDataString(newParentId)}";

        var response = await httpClient.PostAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<bool>(cancellationToken: cancellationToken);
    }

    // ==================== 属性定义操作（新增） ====================

    /// <summary>
    /// 获取所有属性定义
    /// </summary>
    public async Task<IReadOnlyList<AttributeDefinition>> GetAllDefinitionsAsync(
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync($"{_basePath}/attributes/definitions", cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<AttributeDefinition>>(
                   cancellationToken: cancellationToken)
               ?? new List<AttributeDefinition>();
    }

    /// <summary>
    /// 根据 ID 获取属性定义
    /// </summary>
    public async Task<AttributeDefinition?> GetDefinitionByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{_basePath}/attributes/definitions/{id}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttributeDefinition>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 创建新的属性定义
    /// </summary>
    public async Task<AttributeDefinition> CreateDefinitionAsync(
        AttributeDefinition definition,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{_basePath}/attributes/definitions", definition, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AttributeDefinition>(cancellationToken: cancellationToken))!;
    }

    // ==================== 属性值操作（新增） ====================

    /// <summary>
    /// 为指定节点添加一个属性值
    /// </summary>
    public async Task AddValueAsync(
        string nodeId,
        AddValueDto dto,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values", dto, cancellationToken);
        response.EnsureSuccessStatusCode(); // 返回 201，无内容
    }

    /// <summary>
    /// 获取指定节点的单个属性值
    /// </summary>
    public async Task<AttributeDto?> GetSingleValueAsync(
        string nodeId,
        int valueId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{valueId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AttributeDto>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 获取指定节点的所有属性值（返回 NodeDto 包含属性列表）
    /// </summary>
    public async Task<NodeDto?> GetAllValuesAsync(
        string nodeId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.GetAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<NodeDto>(cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 删除指定节点的某个属性值
    /// </summary>
    public async Task<bool> DeleteValueAsync(
        string nodeId,
        int valueId,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.DeleteAsync(
            $"{_basePath}/{Uri.EscapeDataString(nodeId)}/attributes/values/{valueId}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            return false;
        response.EnsureSuccessStatusCode(); // 204 NoContent
        return true;
    }
}