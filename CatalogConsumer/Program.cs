using graft.nuget.CatalogService;

GraftConfig.Host = "ws://localhost/ws";
GraftConfig.Stateless = true;

var item = Catalog.GetItem(1);
Console.WriteLine($"Getting item 1: {item.Name}");

try
{
    Catalog.GetItem(999);
}
catch (Exception ex)
{
    Console.WriteLine($"Getting item 999: {ex.Message}");
}

var basket = Basket.UpdateBasket("demo", [new BasketItemInput { ProductId = 1, Quantity = 1 }]);
Console.WriteLine($"Basket: {basket.Items[0].ProductName} x {basket.Items[0].Quantity}");

var address = new AddressDto
{
    Street = "1 Adventure Works Way",
    City = "Redmond",
    State = "WA",
    Country = "USA",
    ZipCode = "98052"
};

var order = Ordering.Checkout("demo", address);
Console.WriteLine($"Order {order.Id}: {order.Status}");

var shipped = Ordering.Ship(order.Id);
Console.WriteLine($"Order {order.Id}: {shipped.Status}");

try
{
    Basket.UpdateBasket("demo", [new BasketItemInput { ProductId = 6, Quantity = 1 }]);
    Ordering.Checkout("demo", address);
}
catch (Exception ex)
{
    Console.WriteLine($"Checkout item 6: {ex.Message}");
}
