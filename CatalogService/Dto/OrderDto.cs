namespace CatalogService;

public class OrderDto
{
    public int Id { get; set; }

    public string BuyerId { get; set; } = "";

    public DateTime OrderDate { get; set; }

    public string Status { get; set; } = "";

    public string Description { get; set; } = "";

    public AddressDto Address { get; set; } = new();

    public OrderItemDto[] Items { get; set; } = [];

    public decimal Total { get; set; }
}
