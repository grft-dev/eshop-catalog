namespace CatalogService;

public class OrderItemDto
{
    public int ProductId { get; set; }

    public string ProductName { get; set; } = "";

    public decimal UnitPrice { get; set; }

    public int Units { get; set; }

    public string PictureFileName { get; set; } = "";
}
