using System.Text.RegularExpressions;
using APromisedLand.Shared.Helper;
using APromisedLand.Shared.Projects.Nats.Models;
using APromisedLand.Shared.Projects.Typesense.Models;
using Typesense;

namespace APromisedLand.Shared.Projects.Typesense.Services;

public class TypesenseService(ITypesenseClient typesenseClient)
{
    public async Task<IEnumerable<TypesenseResult>> Search(string query)
    {
        string? tag = null;
        var tagMatch = Regex.Match(query, @"\[(.*?)\]");
        if (tagMatch.Success)
        {
            // tag = tagMatch.Groups[1].Value;
            query = query.Replace(tagMatch.Value, "").Trim();
        }

        var searchParams = new SearchParameters(query, "title,content");

        // if (!string.IsNullOrWhiteSpace(tag))
        // {
        //     // 原代码里 FilterBy 有多余的 ]，此处保持与最小 API 一致
        //     searchParams.FilterBy = $"tags:=[{tag}]]";
        // }

        try
        {
            var result = await typesenseClient.Search<DocumentData>(SolutionHelper.TypesenseCollectionName, searchParams);

            return result.Hits.Select(hit => new TypesenseResult
            {
                Id = hit.Document.Id,
                Title = hit.Document.Title,
                Content = hit.Document.Content,
            }).ToList();
        }
        catch (Exception e)
        {
            throw new Exception($"Typesense 搜索失败: {e.Message}");
        }
    }

    public async Task<IEnumerable<TypesenseResult>> SimilarTitles(string query)
    {
        var searchParams = new SearchParameters(query, "title");

        try
        {
            var result = await typesenseClient.Search<DocumentData>(SolutionHelper.TypesenseCollectionName, searchParams);

            return result.Hits.Select(hit => new TypesenseResult
            {
                Id = hit.Document.Id,
                Title = hit.Document.Title,
                Content = hit.Document.Content,
            }).ToList();
        }
        catch (Exception e)
        {
            throw new Exception($"Typesense 搜索失败: {e.Message}");
        }
    }
}