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
