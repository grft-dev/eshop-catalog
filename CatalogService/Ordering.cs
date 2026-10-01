namespace CatalogService;

public static class Ordering
{
    public static OrderDto Checkout(string buyerId, AddressDto address)
    {
        return OrderStore.Checkout(buyerId, address);
    }

    public static OrderDto GetOrder(int orderId)
    {
        return OrderStore.GetOrder(orderId);
    }

    public static OrderDto[] ListOrders(string buyerId)
    {
        return OrderStore.ListOrders(buyerId);
    }

    public static OrderDto Ship(int orderId)
    {
        return OrderStore.Ship(orderId);
    }

    public static OrderDto Cancel(int orderId)
    {
        return OrderStore.Cancel(orderId);
    }
}
