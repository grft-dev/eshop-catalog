namespace CatalogService;

public class CustomerBasketDto
{
    public string BuyerId { get; set; } = "";

    public string ProductName { get; set; } = "";

    public int Quantity { get; set; }

    public BasketItemDto[] Items { get; set; } = [];
}
