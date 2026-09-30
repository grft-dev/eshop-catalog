namespace CatalogService;

public class CatalogItemDto
{
    public int Id { get; set; }

    public string Name { get; set; } = "";

    public string Description { get; set; } = "";

    public decimal Price { get; set; }

    public string PictureFileName { get; set; } = "";

    public int CatalogTypeId { get; set; }

    public string CatalogType { get; set; } = "";

    public int CatalogBrandId { get; set; }

    public string CatalogBrand { get; set; } = "";

    public int AvailableStock { get; set; }

    public int RestockThreshold { get; set; }

    public int MaxStockThreshold { get; set; }

    public bool OnReorder { get; set; }
}
