namespace CatalogService;

internal static class CatalogStore
{
    private static readonly object Gate = new();
    private static readonly List<NamedRecord> Brands = CreateBrands();
    private static readonly List<NamedRecord> Types = CreateTypes();
    private static readonly List<ItemRecord> Items = CreateItems();

    internal static CatalogPage ListItems(int pageIndex, int pageSize, string name, int[] typeIds, int[] brandIds)
    {
        if (pageIndex < 0)
        {
            throw new CatalogException("Page index must be zero or greater.");
        }

        if (pageSize <= 0)
        {
            throw new CatalogException("Page size must be greater than zero.");
        }

        typeIds ??= [];
        brandIds ??= [];

        lock (Gate)
        {
            IEnumerable<ItemRecord> query = Items;
            if (!string.IsNullOrEmpty(name))
            {
                query = query.Where(item => item.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase));
            }

            if (typeIds.Length > 0)
            {
                query = query.Where(item => typeIds.Contains(item.CatalogTypeId));
            }

            if (brandIds.Length > 0)
            {
                query = query.Where(item => brandIds.Contains(item.CatalogBrandId));
            }

            var matched = query.OrderBy(item => item.Name, StringComparer.Ordinal).ToList();
            var skip = (long)pageSize * pageIndex;
            var page = skip >= matched.Count
                ? []
                : matched.Skip((int)skip).Take(pageSize).Select(ToDto).ToArray();

            return new CatalogPage
            {
                PageIndex = pageIndex,
                PageSize = pageSize,
                TotalItems = matched.Count,
                Items = page
            };
        }
    }

    internal static CatalogItemDto GetItem(int id)
    {
        lock (Gate)
        {
            return ToDto(RequireItem(id));
        }
    }

    internal static CatalogItemDto[] GetItemsByIds(int[] ids)
    {
        ids ??= [];
        lock (Gate)
        {
            var found = new List<CatalogItemDto>(ids.Length);
            foreach (var id in ids)
            {
                if (id <= 0)
                {
                    throw new CatalogException("Id is not valid.");
                }

                var item = Items.FirstOrDefault(candidate => candidate.Id == id);
                if (item is not null)
                {
                    found.Add(ToDto(item));
                }
            }

            return found.ToArray();
        }
    }

    internal static CatalogBrandDto[] ListBrands()
    {
        lock (Gate)
        {
            return Brands
                .OrderBy(brand => brand.Name, StringComparer.Ordinal)
                .Select(brand => new CatalogBrandDto { Id = brand.Id, Brand = brand.Name })
                .ToArray();
        }
    }

    internal static CatalogTypeDto[] ListTypes()
    {
        lock (Gate)
        {
            return Types
                .OrderBy(type => type.Name, StringComparer.Ordinal)
                .Select(type => new CatalogTypeDto { Id = type.Id, Type = type.Name })
                .ToArray();
        }
    }

    internal static CatalogFacetsDto GetFacets(int[] typeIds, int[] brandIds)
    {
        typeIds ??= [];
        brandIds ??= [];

        lock (Gate)
        {
            IEnumerable<ItemRecord> brandScope = Items;
            if (typeIds.Length > 0)
            {
                brandScope = brandScope.Where(item => typeIds.Contains(item.CatalogTypeId));
            }

            var brands = brandScope
                .GroupBy(item => item.CatalogBrandId)
                .Select(group => new CatalogFacetCountDto { Id = group.Key, Count = group.Count() })
                .OrderBy(count => count.Id)
                .ToArray();

            IEnumerable<ItemRecord> typeScope = Items;
            if (brandIds.Length > 0)
            {
                typeScope = typeScope.Where(item => brandIds.Contains(item.CatalogBrandId));
            }

            var types = typeScope
                .GroupBy(item => item.CatalogTypeId)
                .Select(group => new CatalogFacetCountDto { Id = group.Key, Count = group.Count() })
                .OrderBy(count => count.Id)
                .ToArray();

            return new CatalogFacetsDto
            {
                Brands = brands,
                Types = types,
                BrandTotal = brands.Sum(count => count.Count),
                TypeTotal = types.Sum(count => count.Count)
            };
        }
    }

    internal static CatalogItemDto CreateItem(CatalogItemInput input)
    {
        ValidateInput(input);
        lock (Gate)
        {
            RequireBrand(input.CatalogBrandId);
            RequireType(input.CatalogTypeId);

            var item = new ItemRecord
            {
                Id = Items.Count == 0 ? 1 : Items.Max(existing => existing.Id) + 1,
                Name = input.Name,
                Description = input.Description ?? "",
                Price = input.Price,
                PictureFileName = input.PictureFileName ?? "",
                CatalogTypeId = input.CatalogTypeId,
                CatalogBrandId = input.CatalogBrandId,
                AvailableStock = input.AvailableStock,
                RestockThreshold = input.RestockThreshold,
                MaxStockThreshold = input.MaxStockThreshold,
                OnReorder = input.AvailableStock == 0
            };
            Items.Add(item);
            return ToDto(item);
        }
    }

    internal static CatalogItemDto UpdateItem(int id, CatalogItemInput input)
    {
        ValidateInput(input);
        lock (Gate)
        {
            var item = RequireItem(id);
            RequireBrand(input.CatalogBrandId);
            RequireType(input.CatalogTypeId);

            item.Name = input.Name;
            item.Description = input.Description ?? "";
            item.Price = input.Price;
            item.PictureFileName = input.PictureFileName ?? "";
            item.CatalogTypeId = input.CatalogTypeId;
            item.CatalogBrandId = input.CatalogBrandId;
            item.AvailableStock = input.AvailableStock;
            item.RestockThreshold = input.RestockThreshold;
            item.MaxStockThreshold = input.MaxStockThreshold;
            item.OnReorder = input.AvailableStock == 0;
            return ToDto(item);
        }
    }

    internal static bool DeleteItem(int id)
    {
        lock (Gate)
        {
            var item = RequireItem(id);
            Items.Remove(item);
            return true;
        }
    }

    internal static int RemoveStock(int id, int quantity)
    {
        lock (Gate)
        {
            var item = RequireItem(id);
            if (item.AvailableStock == 0)
            {
                throw new CatalogException($"Empty stock, product item {item.Name} is sold out");
            }

            if (quantity <= 0)
            {
                throw new CatalogException("Item units desired should be greater than zero");
            }

            var removed = Math.Min(quantity, item.AvailableStock);
            item.AvailableStock -= removed;
            return removed;
        }
    }

    private static ItemRecord RequireItem(int id)
    {
        if (id <= 0)
        {
            throw new CatalogException("Id is not valid.");
        }

        var item = Items.FirstOrDefault(candidate => candidate.Id == id);
        if (item is null)
        {
            throw new CatalogException($"Item with id {id} not found.");
        }

        return item;
    }

    private static void RequireBrand(int id)
    {
        if (Brands.All(brand => brand.Id != id))
        {
            throw new CatalogException($"Catalog brand {id} was not found.");
        }
    }

    private static void RequireType(int id)
    {
        if (Types.All(type => type.Id != id))
        {
            throw new CatalogException($"Catalog type {id} was not found.");
        }
    }

    private static void ValidateInput(CatalogItemInput input)
    {
        if (input is null || string.IsNullOrWhiteSpace(input.Name))
        {
            throw new CatalogException("Item name must be provided.");
        }

        if (input.Price < 0)
        {
            throw new CatalogException("Price must be zero or greater.");
        }

        if (input.AvailableStock < 0 || input.RestockThreshold < 0 || input.MaxStockThreshold < 0)
        {
            throw new CatalogException("Stock values must be zero or greater.");
        }
    }

    private static CatalogItemDto ToDto(ItemRecord item)
    {
        var brand = Brands.First(candidate => candidate.Id == item.CatalogBrandId).Name;
        var type = Types.First(candidate => candidate.Id == item.CatalogTypeId).Name;
        return new CatalogItemDto
        {
            Id = item.Id,
            Name = item.Name,
            Description = item.Description,
            Price = item.Price,
            PictureFileName = item.PictureFileName,
            CatalogTypeId = item.CatalogTypeId,
            CatalogType = type,
            CatalogBrandId = item.CatalogBrandId,
            CatalogBrand = brand,
            AvailableStock = item.AvailableStock,
            RestockThreshold = item.RestockThreshold,
            MaxStockThreshold = item.MaxStockThreshold,
            OnReorder = item.OnReorder
        };
    }

    private static List<NamedRecord> CreateBrands()
    {
        return
        [
            new NamedRecord(1, "Daybird"),
            new NamedRecord(2, "Gravitator"),
            new NamedRecord(3, "WildRunner"),
            new NamedRecord(4, "Quester"),
            new NamedRecord(5, "B&R"),
            new NamedRecord(6, "Raptor Elite"),
            new NamedRecord(7, "Solstix"),
            new NamedRecord(8, "Grolltex")
        ];
    }

    private static List<NamedRecord> CreateTypes()
    {
        return
        [
            new NamedRecord(1, "Footwear"),
            new NamedRecord(2, "Climbing"),
            new NamedRecord(3, "Ski/boarding"),
            new NamedRecord(4, "Bags"),
            new NamedRecord(5, "Trekking"),
            new NamedRecord(6, "Jackets")
        ];
    }

    private static List<ItemRecord> CreateItems()
    {
        return
        [
            Item(1, "Wanderer Black Hiking Boots", "Daybird's Wanderer Hiking Boots in sleek black are perfect for all your outdoor adventures. These boots are made with a waterproof leather upper and a durable rubber sole for superior traction. With their cushioned insole and padded collar, these boots will keep you comfortable all day long.", 109.99m, 1, 1, 100, false),
            Item(2, "Summit Pro Harness", "Conquer new heights with the Summit Pro Harness by Gravitator. This lightweight and durable climbing harness features adjustable leg loops and waist belt for a customized fit. With its vibrant blue color, you'll look stylish while maneuvering difficult routes. Safety is a top priority with a reinforced tie-in point and strong webbing loops.", 89.99m, 2, 2, 50, false),
            Item(3, "Alpine Fusion Goggles", "Enhance your skiing experience with the Alpine Fusion Goggles from WildRunner. These goggles offer full UV protection and anti-fog lenses to keep your vision clear on the slopes. With their stylish silver frame and orange lenses, you'll stand out from the crowd. Adjustable straps ensure a secure fit, while the soft foam padding provides comfort all day long.", 79.99m, 3, 3, 40, false),
            Item(4, "Expedition Backpack", "The Expedition Backpack by Quester is a must-have for every outdoor enthusiast. With its spacious interior and multiple pockets, you can easily carry all your gear and essentials. Made with durable nylon fabric, this backpack is built to withstand the toughest conditions. The orange accents add a touch of style to this functional backpack.", 129.99m, 4, 4, 30, false),
            Item(5, "Blizzard Rider Snowboard", "Get ready to ride the slopes with the Blizzard Rider Snowboard by B&R. This versatile snowboard is perfect for riders of all levels with its medium flex and twin shape. Its black and blue color scheme gives it a sleek and cool look. Whether you're carving turns or hitting the terrain park, this snowboard will help you shred with confidence.", 299.99m, 3, 5, 12, false),
            Item(6, "Carbon Fiber Trekking Poles", "The Carbon Fiber Trekking Poles by Raptor Elite are the ultimate companion for your hiking adventures. Designed with lightweight carbon fiber shafts, these poles provide excellent support and durability. The comfortable and adjustable cork grips ensure a secure hold, while the blue accents add a stylish touch. Compact and collapsible, these trekking poles are easy to transport and store.", 69.99m, 5, 6, 0, true),
            Item(7, "Explorer 45L Backpack", "The Explorer 45L Backpack by Solstix is perfect for your next outdoor expedition. Made with waterproof and tear-resistant materials, this backpack can withstand even the harshest weather conditions. With its spacious main compartment and multiple pockets, you can easily organize your gear. The green and black color scheme adds a rugged and adventurous edge.", 149.99m, 4, 7, 20, false),
            Item(8, "Frostbite Insulated Jacket", "Stay warm and stylish with the Frostbite Insulated Jacket by Grolltex. Featuring a water-resistant outer shell and lightweight insulation, this jacket is perfect for cold weather adventures. The black and gray color combination and Grolltex logo add a touch of sophistication. With its adjustable hood and multiple pockets, this jacket offers both style and functionality.", 179.99m, 6, 8, 15, false)
        ];
    }

    private static ItemRecord Item(int id, string name, string description, decimal price, int typeId, int brandId, int stock, bool onReorder)
    {
        return new ItemRecord
        {
            Id = id,
            Name = name,
            Description = description,
            Price = price,
            PictureFileName = $"{id}.png",
            CatalogTypeId = typeId,
            CatalogBrandId = brandId,
            AvailableStock = stock,
            RestockThreshold = 10,
            MaxStockThreshold = 200,
            OnReorder = onReorder
        };
    }

    private sealed class NamedRecord
    {
        public NamedRecord(int id, string name)
        {
            Id = id;
            Name = name;
        }

        public int Id { get; }

        public string Name { get; }
    }

    private sealed class ItemRecord
    {
        public int Id { get; set; }

        public string Name { get; set; } = "";

        public string Description { get; set; } = "";

        public decimal Price { get; set; }

        public string PictureFileName { get; set; } = "";

        public int CatalogTypeId { get; set; }

        public int CatalogBrandId { get; set; }

        public int AvailableStock { get; set; }

        public int RestockThreshold { get; set; }

        public int MaxStockThreshold { get; set; }

        public bool OnReorder { get; set; }
    }
}
