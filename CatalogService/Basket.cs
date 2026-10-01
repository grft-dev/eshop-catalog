namespace CatalogService;

public static class Basket
{
    public static CustomerBasketDto GetBasket(string buyerId)
    {
        return BasketStore.GetBasket(buyerId);
    }

    public static CustomerBasketDto UpdateBasket(string buyerId, int[] productIds, int[] quantities)
    {
        return BasketStore.UpdateBasket(buyerId, productIds, quantities);
    }

    public static bool DeleteBasket(string buyerId)
    {
        return BasketStore.DeleteBasket(buyerId);
    }
}
