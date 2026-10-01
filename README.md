# eShop catalog on Graftcode

A .NET class library that exposes the eShop catalog, basket, and ordering as a Graftcode module. PostgreSQL persists brands, types, items, stock, baskets, and orders. There is no REST API or generated client to maintain. There are no user accounts and no payments: a buyer id is a string the caller passes in, and checkout confirms stock immediately.

Seed names, descriptions, brands, and types come from the [dotnet/eShop](https://github.com/dotnet/eShop) catalog (`src/Catalog.API/Setup/catalog.json`), which is licensed under the MIT License.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) with Docker Compose installed and running
- [.NET SDK 9](https://dotnet.microsoft.com/download)

## 1. Run the backend and database

From this repository root:

```bash
docker compose up --build -d
docker compose ps
```

Compose starts PostgreSQL on port 5432 and waits for it to become healthy before starting the backend. Port 80 serves calls (`ws://localhost/ws`). Port 81 serves Graftcode Vision.

The schema and the eight-item seed are in [`database/init.sql`](database/init.sql). PostgreSQL stores data in the `postgres-data` named volume, so catalog changes, baskets, orders, and stock survive container restarts. Stop the containers with `docker compose down`. To delete all data and recreate the seed on the next start, use:

```bash
docker compose down -v
```

Wait until the install command is available:

```bash
curl http://localhost:80/nuget
```

Open Graftcode Vision at [http://localhost:81/GV](http://localhost:81/GV). You should see `Catalog`, `Basket`, and `Ordering`. `Catalog` methods: `ListItems`, `GetItem`, `GetItemsByIds`, `ListBrands`, `ListTypes`, `GetFacets`, `CreateItem`, `UpdateItem`, `DeleteItem`, `RemoveStock`. `Basket` methods: `GetBasket`, `UpdateBasket`, `DeleteBasket`. `Ordering` methods: `Checkout`, `GetOrder`, `ListOrders`, `Ship`, `Cancel`. The gateway build used while writing this sample also serves that page at [http://localhost:80/GV](http://localhost:80/GV). If `docker compose logs backend` prints a different Vision URL, use that one.

The registry GUID changes every time the container starts. Copy the current GUID and the `dotnet add package` command from `http://localhost:80/nuget` (Vision, Configuration tab). Do not reuse a GUID from an earlier run.

## 2. Install the graft and call one method

The feed at `grft.dev` serves grafts only. [`CatalogConsumer/NuGet.config`](CatalogConsumer/NuGet.config) maps `graft.nuget.*` and `Hypertube.*` to that feed and everything else to nuget.org. Replace `YOUR_GUID` in that file with the GUID from the install command.

From `CatalogConsumer`, paste the command from `http://localhost:80/nuget`. It looks like this (the GUID and version come from that response):

```bash
dotnet add package graft.nuget.catalogservice -v 1.0.0 --source https://grft.dev/YOUR_GUID__free
dotnet run
```

`CatalogConsumer/Program.cs` points the graft at the local gateway, loads the first catalog item, asks for a missing one, then checks out a basket and tries to buy the sold-out item:

```csharp
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
```

`GraftConfig.Stateless = true` returns the whole DTO in one round trip.

`UpdateBasket` copies the current name, price, and picture from the catalog. `Checkout` reads the price again, removes stock for every line or leaves the catalog and the basket unchanged, then stores an order already in `StockConfirmed`. There is no `Paid` status. `Ship` moves that order to `Shipped`. `Cancel` is allowed only from `StockConfirmed` and puts the units back.

Expected output:

```text
Getting item 1: Wanderer Black Hiking Boots
Getting item 999: Item with id 999 not found.
Basket: Wanderer Black Hiking Boots x 1
Order 1: StockConfirmed
Order 1: Shipped
Checkout item 6: Empty stock, product item Carbon Fiber Trekking Poles is sold out
```

The first line is the seeded item `Id` 1. A missing item and an empty warehouse come back as a plain `Exception`; the message is preserved. Item 6 is seeded with no stock, and that failed checkout leaves the basket in place.

A step-by-step manual smoke test (in Polish) is in [SMOKE-TEST.md](SMOKE-TEST.md).

## Dev container

[`.devcontainer/devcontainer.json`](.devcontainer/devcontainer.json) uses the .NET SDK 9 image and installs Graftcode Gateway with:

```bash
curl -fsSL grft.dev/get/gg | sh
```

## Follow-up

Basket and ordering in this module skip identity, payment, and the event bus. Still to come, and not in this repository:

- pgvector and semantic search
- the event bus (`ProductPriceChanged`)
- binary item pictures
- the rest of eShop (identity, payment, Blazor, mobile)
- Academy hub UI, if it only lists the numbered Quick Start courses
