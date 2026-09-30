using graft.nuget.CatalogService;

GraftConfig.Host = "ws://localhost/ws";
GraftConfig.Stateless = true;

var item = Catalog.GetItem(1);
Console.WriteLine(item.Name);
