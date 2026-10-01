using Npgsql;

namespace CatalogService;

internal static class CatalogStore
{
    private const string ItemColumns = """
        SELECT i.id, i.name, i.description, i.price, i.picture_file_name,
               i.catalog_type_id, t.name, i.catalog_brand_id, b.name,
               i.available_stock, i.restock_threshold, i.max_stock_threshold, i.on_reorder
        FROM catalog_items i
        JOIN catalog_types t ON t.id = i.catalog_type_id
        JOIN catalog_brands b ON b.id = i.catalog_brand_id
        """;

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

        using var connection = Database.OpenConnection();
        const string filter = """
            WHERE (@name = '' OR left(lower(i.name), length(@name)) = lower(@name))
              AND (cardinality(@type_ids) = 0 OR i.catalog_type_id = ANY(@type_ids))
              AND (cardinality(@brand_ids) = 0 OR i.catalog_brand_id = ANY(@brand_ids))
            """;

        using var countCommand = new NpgsqlCommand($"SELECT count(*) FROM catalog_items i {filter}", connection);
        AddFilters(countCommand, name, typeIds, brandIds);
        var totalItems = Convert.ToInt32(countCommand.ExecuteScalar());

        using var command = new NpgsqlCommand(
            $"{ItemColumns} {filter} ORDER BY i.name COLLATE \"C\" OFFSET @offset LIMIT @limit",
            connection);
        AddFilters(command, name, typeIds, brandIds);
        command.Parameters.AddWithValue("offset", (long)pageIndex * pageSize);
        command.Parameters.AddWithValue("limit", pageSize);

        return new CatalogPage
        {
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalItems = totalItems,
            Items = ReadItems(command)
        };
    }

    internal static CatalogItemDto GetItem(int id)
    {
        using var connection = Database.OpenConnection();
        return RequireItem(connection, null, id);
    }

    internal static CatalogItemDto[] GetItemsByIds(int[] ids)
    {
        ids ??= [];
        if (ids.Any(id => id <= 0))
        {
            throw new CatalogException("Id is not valid.");
        }

        if (ids.Length == 0)
        {
            return [];
        }

        using var connection = Database.OpenConnection();
        using var command = new NpgsqlCommand($"{ItemColumns} WHERE i.id = ANY(@ids)", connection);
        command.Parameters.AddWithValue("ids", ids);
        var byId = ReadItems(command).ToDictionary(item => item.Id);
        return ids.Where(byId.ContainsKey).Select(id => byId[id]).ToArray();
    }

    internal static CatalogBrandDto[] ListBrands()
    {
        using var connection = Database.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT id, name FROM catalog_brands ORDER BY name COLLATE \"C\"",
            connection);
        using var reader = command.ExecuteReader();
        var result = new List<CatalogBrandDto>();
        while (reader.Read())
        {
            result.Add(new CatalogBrandDto { Id = reader.GetInt32(0), Brand = reader.GetString(1) });
        }

        return result.ToArray();
    }

    internal static CatalogTypeDto[] ListTypes()
    {
        using var connection = Database.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT id, name FROM catalog_types ORDER BY name COLLATE \"C\"",
            connection);
        using var reader = command.ExecuteReader();
        var result = new List<CatalogTypeDto>();
        while (reader.Read())
        {
            result.Add(new CatalogTypeDto { Id = reader.GetInt32(0), Type = reader.GetString(1) });
        }

        return result.ToArray();
    }

    internal static CatalogFacetsDto GetFacets(int[] typeIds, int[] brandIds)
    {
        typeIds ??= [];
        brandIds ??= [];
        using var connection = Database.OpenConnection();

        var brands = ReadFacetCounts(
            connection,
            """
            SELECT catalog_brand_id, count(*)::integer
            FROM catalog_items
            WHERE cardinality(@ids) = 0 OR catalog_type_id = ANY(@ids)
            GROUP BY catalog_brand_id
            ORDER BY catalog_brand_id
            """,
            typeIds);

        var types = ReadFacetCounts(
            connection,
            """
            SELECT catalog_type_id, count(*)::integer
            FROM catalog_items
            WHERE cardinality(@ids) = 0 OR catalog_brand_id = ANY(@ids)
            GROUP BY catalog_type_id
            ORDER BY catalog_type_id
            """,
            brandIds);

        return new CatalogFacetsDto
        {
            Brands = brands,
            Types = types,
            BrandTotal = brands.Sum(count => count.Count),
            TypeTotal = types.Sum(count => count.Count)
        };
    }

    internal static CatalogItemDto CreateItem(CatalogItemInput input)
    {
        ValidateInput(input);
        using var connection = Database.OpenConnection();
        RequireBrand(connection, null, input.CatalogBrandId);
        RequireType(connection, null, input.CatalogTypeId);

        using var command = new NpgsqlCommand(
            """
            INSERT INTO catalog_items
                (name, description, price, picture_file_name, catalog_type_id, catalog_brand_id,
                 available_stock, restock_threshold, max_stock_threshold, on_reorder)
            VALUES
                (@name, @description, @price, @picture, @type_id, @brand_id,
                 @stock, @restock, @max_stock, @on_reorder)
            RETURNING id
            """,
            connection);
        AddItemInput(command, input);
        var id = Convert.ToInt32(command.ExecuteScalar());
        return RequireItem(connection, null, id);
    }

    internal static CatalogItemDto UpdateItem(int id, CatalogItemInput input)
    {
        ValidateInput(input);
        using var connection = Database.OpenConnection();
        RequireItem(connection, null, id);
        RequireBrand(connection, null, input.CatalogBrandId);
        RequireType(connection, null, input.CatalogTypeId);

        using var command = new NpgsqlCommand(
            """
            UPDATE catalog_items
            SET name = @name, description = @description, price = @price,
                picture_file_name = @picture, catalog_type_id = @type_id,
                catalog_brand_id = @brand_id, available_stock = @stock,
                restock_threshold = @restock, max_stock_threshold = @max_stock,
                on_reorder = @on_reorder
            WHERE id = @id
            """,
            connection);
        AddItemInput(command, input);
        command.Parameters.AddWithValue("id", id);
        command.ExecuteNonQuery();
        return RequireItem(connection, null, id);
    }

    internal static bool DeleteItem(int id)
    {
        using var connection = Database.OpenConnection();
        RequireItem(connection, null, id);
        using var command = new NpgsqlCommand("DELETE FROM catalog_items WHERE id = @id", connection);
        command.Parameters.AddWithValue("id", id);
        command.ExecuteNonQuery();
        return true;
    }

    internal static int RemoveStock(int id, int quantity)
    {
        if (quantity <= 0)
        {
            throw new CatalogException("Item units desired should be greater than zero");
        }

        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var item = RequireItem(connection, transaction, id, forUpdate: true);
        if (item.AvailableStock == 0)
        {
            throw new CatalogException($"Empty stock, product item {item.Name} is sold out");
        }

        var removed = Math.Min(quantity, item.AvailableStock);
        using var command = new NpgsqlCommand(
            """
            UPDATE catalog_items
            SET available_stock = available_stock - @quantity,
                on_reorder = available_stock - @quantity = 0
            WHERE id = @id
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("quantity", removed);
        command.ExecuteNonQuery();
        transaction.Commit();
        return removed;
    }

    internal static CatalogItemDto RequireItem(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int id,
        bool forUpdate = false)
    {
        if (id <= 0)
        {
            throw new CatalogException("Id is not valid.");
        }

        var sql = $"{ItemColumns} WHERE i.id = @id{(forUpdate ? " FOR UPDATE OF i" : "")}";
        using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new CatalogException($"Item with id {id} not found.");
        }

        return ReadItem(reader);
    }

    private static void AddFilters(
        NpgsqlCommand command,
        string name,
        int[] typeIds,
        int[] brandIds)
    {
        command.Parameters.AddWithValue("name", name ?? "");
        command.Parameters.AddWithValue("type_ids", typeIds);
        command.Parameters.AddWithValue("brand_ids", brandIds);
    }

    private static void AddItemInput(NpgsqlCommand command, CatalogItemInput input)
    {
        command.Parameters.AddWithValue("name", input.Name);
        command.Parameters.AddWithValue("description", input.Description ?? "");
        command.Parameters.AddWithValue("price", input.Price);
        command.Parameters.AddWithValue("picture", input.PictureFileName ?? "");
        command.Parameters.AddWithValue("type_id", input.CatalogTypeId);
        command.Parameters.AddWithValue("brand_id", input.CatalogBrandId);
        command.Parameters.AddWithValue("stock", input.AvailableStock);
        command.Parameters.AddWithValue("restock", input.RestockThreshold);
        command.Parameters.AddWithValue("max_stock", input.MaxStockThreshold);
        command.Parameters.AddWithValue("on_reorder", input.AvailableStock == 0);
    }

    private static CatalogItemDto[] ReadItems(NpgsqlCommand command)
    {
        using var reader = command.ExecuteReader();
        var result = new List<CatalogItemDto>();
        while (reader.Read())
        {
            result.Add(ReadItem(reader));
        }

        return result.ToArray();
    }

    private static CatalogItemDto ReadItem(NpgsqlDataReader reader)
    {
        return new CatalogItemDto
        {
            Id = reader.GetInt32(0),
            Name = reader.GetString(1),
            Description = reader.GetString(2),
            Price = reader.GetDecimal(3),
            PictureFileName = reader.GetString(4),
            CatalogTypeId = reader.GetInt32(5),
            CatalogType = reader.GetString(6),
            CatalogBrandId = reader.GetInt32(7),
            CatalogBrand = reader.GetString(8),
            AvailableStock = reader.GetInt32(9),
            RestockThreshold = reader.GetInt32(10),
            MaxStockThreshold = reader.GetInt32(11),
            OnReorder = reader.GetBoolean(12)
        };
    }

    private static CatalogFacetCountDto[] ReadFacetCounts(
        NpgsqlConnection connection,
        string sql,
        int[] ids)
    {
        using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("ids", ids);
        using var reader = command.ExecuteReader();
        var result = new List<CatalogFacetCountDto>();
        while (reader.Read())
        {
            result.Add(new CatalogFacetCountDto { Id = reader.GetInt32(0), Count = reader.GetInt32(1) });
        }

        return result.ToArray();
    }

    private static void RequireBrand(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int id)
    {
        RequireReference(connection, transaction, "catalog_brands", id, $"Catalog brand {id} was not found.");
    }

    private static void RequireType(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int id)
    {
        RequireReference(connection, transaction, "catalog_types", id, $"Catalog type {id} was not found.");
    }

    private static void RequireReference(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string table,
        int id,
        string message)
    {
        using var command = new NpgsqlCommand($"SELECT EXISTS (SELECT 1 FROM {table} WHERE id = @id)", connection, transaction);
        command.Parameters.AddWithValue("id", id);
        if (!(bool)command.ExecuteScalar()!)
        {
            throw new CatalogException(message);
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
}
