namespace CatalogService;

public class CatalogFacetsDto
{
    public CatalogFacetCountDto[] Brands { get; set; } = [];

    public CatalogFacetCountDto[] Types { get; set; } = [];

    public int BrandTotal { get; set; }

    public int TypeTotal { get; set; }
}
