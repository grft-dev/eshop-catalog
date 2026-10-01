namespace CatalogService;

public static class Basket
{
    public static CustomerBasketDto GetBasket(string buyerId)
    {
        return BasketStore.GetBasket(buyerId);
    }

    public static CustomerBasketDto UpdateBasket(string buyerId, BasketItemInput[] items)
    {
        return BasketStore.UpdateBasket(buyerId, items);
    }

    public static bool DeleteBasket(string buyerId)
    {
        return BasketStore.DeleteBasket(buyerId);
    }
}
