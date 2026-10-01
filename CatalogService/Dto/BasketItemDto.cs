namespace CatalogService;

public class BasketItemDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = "";

    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; }

    public string PictureFileName { get; set; } = "";
}
