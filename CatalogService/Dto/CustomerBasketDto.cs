namespace CatalogService;

public class CustomerBasketDto
{
    public string BuyerId { get; set; } = "";

    public BasketItemDto[] Items { get; set; } = [];
}
