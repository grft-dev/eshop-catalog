# eShop catalog on Graftcode

A .NET class library that exposes the eShop catalog as a Graftcode module. The catalog keeps brands, types, and a handful of items in memory. There is no REST API, no database, and no generated client to maintain.

Seed names, descriptions, brands, and types come from the [dotnet/eShop](https://github.com/dotnet/eShop) catalog (`src/Catalog.API/Setup/catalog.json`), which is licensed under the MIT License.

## Prerequisites

- [Docker](https://docs.docker.com/get-docker/) installed and running
- [.NET SDK 9](https://dotnet.microsoft.com/download)

## 1. Build and run the catalog

From this repository root:

```bash
docker build -t eshop-catalog CatalogService
docker run -d --name eshop-catalog -p 80:80 -p 81:81 eshop-catalog
```

Port 80 serves calls (`ws://localhost/ws`). Port 81 serves Graftcode Vision.

Wait until the install command is available:

```bash
curl http://localhost:80/nuget
```

Open Graftcode Vision at [http://localhost:81/GV](http://localhost:81/GV). You should see the `Catalog` class and its methods (`ListItems`, `GetItem`, `GetItemsByIds`, `ListBrands`, `ListTypes`, `GetFacets`, `CreateItem`, `UpdateItem`, `DeleteItem`, `RemoveStock`). The gateway build used while writing this sample also serves that page at [http://localhost:80/GV](http://localhost:80/GV). If `docker logs eshop-catalog` prints a different Vision URL, use that one.

The registry GUID changes every time the container starts. Copy the current GUID and the `dotnet add package` command from `http://localhost:80/nuget` (Vision, Configuration tab). Do not reuse a GUID from an earlier run.

## 2. Install the graft and call one method

The feed at `grft.dev` serves grafts only. [`CatalogConsumer/NuGet.config`](CatalogConsumer/NuGet.config) maps `graft.nuget.*` and `Hypertube.*` to that feed and everything else to nuget.org. Replace `YOUR_GUID` in that file with the GUID from the install command.

From `CatalogConsumer`, paste the command from `http://localhost:80/nuget`. It looks like this (the GUID and version come from that response):

```bash
dotnet add package graft.nuget.catalogservice -v 1.0.0 --source https://grft.dev/YOUR_GUID__free
dotnet run
```

`CatalogConsumer/Program.cs` points the graft at the local gateway, loads the first catalog item, then asks for a missing one:

```csharp
using graft.nuget.CatalogService;

GraftConfig.Host = "ws://localhost/ws";
GraftConfig.Stateless = true;

var item = Catalog.GetItem(1);
Console.WriteLine($"Pobranie pozycji 1: {item.Name}");

try
{
    Catalog.GetItem(999);
}
catch (Exception ex)
{
    Console.WriteLine($"Pobranie pozycji 999: {ex.Message}");
}
```

`GraftConfig.Stateless = true` returns the whole DTO in one round trip.

Expected output:

```text
Pobranie pozycji 1: Wanderer Black Hiking Boots
Pobranie pozycji 999: Item with id 999 not found.
```

The first line is the seeded item `Id` 1. The second line is the gateway turning a missing item into a plain `Exception`; the message is preserved.

A missing item and an empty warehouse come back as a plain `Exception`. The message is preserved:

- `Catalog.GetItem(999)` — `Item with id 999 not found.`
- `Catalog.RemoveStock(6, 1)` — `Empty stock, product item Carbon Fiber Trekking Poles is sold out` (item 6 is seeded with no stock)

A step-by-step manual smoke test (in Polish) is in [SMOKE-TEST.md](SMOKE-TEST.md).

## Dev container

[`.devcontainer/devcontainer.json`](.devcontainer/devcontainer.json) uses the .NET SDK 9 image and installs Graftcode Gateway with:

```bash
curl -fsSL grft.dev/get/gg | sh
```

## Follow-up

This slice is the catalog facade only. Still to come, and not in this repository:

- PostgreSQL, pgvector, and semantic search
- the event bus (`ProductPriceChanged`)
- binary item pictures
- the rest of eShop (basket, ordering, identity, payment, Blazor, mobile)
- Academy hub UI, if it only lists the numbered Quick Start courses
