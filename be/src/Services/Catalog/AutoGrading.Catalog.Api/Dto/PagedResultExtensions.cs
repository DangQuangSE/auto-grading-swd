using AutoGrading.Contracts.Pagination;

namespace AutoGrading.Catalog.Api.Dto;

internal static class PagedResultExtensions
{
    public static PagedResult<TResponse> MapItems<TDomain, TResponse>(
        this PagedResult<TDomain> source,
        Func<TDomain, TResponse> map) =>
        new(source.Items.Select(map).ToList(), source.Page, source.PageSize, source.TotalCount);
}
