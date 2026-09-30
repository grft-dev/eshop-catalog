namespace CatalogService;

public class CatalogPage
{
    public int PageIndex { get; set; }

    public int PageSize { get; set; }

    public int TotalItems { get; set; }

    public CatalogItemDto[] Items { get; set; } = [];
}
