using Npgsql;

namespace CatalogService;

internal static class BasketStore
{
    internal static CustomerBasketDto GetBasket(string buyerId)
    {
        RequireBuyerId(buyerId);
        using var connection = Database.OpenConnection();
        return ReadBasket(connection, null, buyerId);
    }

    internal static CustomerBasketDto UpdateBasket(string buyerId, int[] productIds, int[] quantities)
    {
        RequireBuyerId(buyerId);
        productIds ??= [];
        quantities ??= [];
        var requested = Normalize(productIds, quantities);

        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var basketCommand = new NpgsqlCommand(
            "INSERT INTO baskets (buyer_id) VALUES (@buyer_id) ON CONFLICT DO NOTHING",
            connection,
            transaction))
        {
            basketCommand.Parameters.AddWithValue("buyer_id", buyerId);
            basketCommand.ExecuteNonQuery();
        }

        using (var lockCommand = new NpgsqlCommand(
            "SELECT buyer_id FROM baskets WHERE buyer_id = @buyer_id FOR UPDATE",
            connection,
            transaction))
        {
            lockCommand.Parameters.AddWithValue("buyer_id", buyerId);
            lockCommand.ExecuteScalar();
        }

        var snapshots = new List<BasketItemDto>(requested.Count);
        foreach (var (productId, quantity) in requested.OrderBy(item => item.Key))
        {
            var product = CatalogStore.RequireItem(connection, transaction, productId, forUpdate: true);
            snapshots.Add(new BasketItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = quantity,
                PictureFileName = product.PictureFileName
            });
        }

        using (var deleteCommand = new NpgsqlCommand(
            "DELETE FROM basket_items WHERE basket_buyer_id = @buyer_id",
            connection,
            transaction))
        {
            deleteCommand.Parameters.AddWithValue("buyer_id", buyerId);
            deleteCommand.ExecuteNonQuery();
        }

        foreach (var item in snapshots)
        {
            using var insertCommand = new NpgsqlCommand(
                """
                INSERT INTO basket_items
                    (basket_buyer_id, product_id, product_name, unit_price, quantity, picture_file_name)
                VALUES
                    (@buyer_id, @product_id, @product_name, @unit_price, @quantity, @picture)
                """,
                connection,
                transaction);
            insertCommand.Parameters.AddWithValue("buyer_id", buyerId);
            insertCommand.Parameters.AddWithValue("product_id", item.ProductId);
            insertCommand.Parameters.AddWithValue("product_name", item.ProductName);
            insertCommand.Parameters.AddWithValue("unit_price", item.UnitPrice);
            insertCommand.Parameters.AddWithValue("quantity", item.Quantity);
            insertCommand.Parameters.AddWithValue("picture", item.PictureFileName);
            insertCommand.ExecuteNonQuery();
        }

        transaction.Commit();
        return ToBasket(buyerId, snapshots.ToArray());
    }

    internal static bool DeleteBasket(string buyerId)
    {
        RequireBuyerId(buyerId);
        using var connection = Database.OpenConnection();
        using var command = new NpgsqlCommand(
            "DELETE FROM baskets WHERE buyer_id = @buyer_id",
            connection);
        command.Parameters.AddWithValue("buyer_id", buyerId);
        if (command.ExecuteNonQuery() == 0)
        {
            throw new CatalogException($"Basket with buyer id {buyerId} does not exist");
        }

        return true;
    }

    internal static CustomerBasketDto ReadBasket(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        string buyerId,
        bool forUpdate = false)
    {
        using (var basketCommand = new NpgsqlCommand(
            $"SELECT buyer_id FROM baskets WHERE buyer_id = @buyer_id{(forUpdate ? " FOR UPDATE" : "")}",
            connection,
            transaction))
        {
            basketCommand.Parameters.AddWithValue("buyer_id", buyerId);
            if (basketCommand.ExecuteScalar() is null)
            {
                return ToBasket(buyerId, []);
            }
        }

        using var command = new NpgsqlCommand(
            """
            SELECT product_id, product_name, unit_price, quantity, picture_file_name
            FROM basket_items
            WHERE basket_buyer_id = @buyer_id
            ORDER BY product_id
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("buyer_id", buyerId);
        using var reader = command.ExecuteReader();
        var items = new List<BasketItemDto>();
        while (reader.Read())
        {
            items.Add(new BasketItemDto
            {
                ProductId = reader.GetInt32(0),
                ProductName = reader.GetString(1),
                UnitPrice = reader.GetDecimal(2),
                Quantity = reader.GetInt32(3),
                PictureFileName = reader.GetString(4)
            });
        }

        return ToBasket(buyerId, items.ToArray());
    }

    private static CustomerBasketDto ToBasket(string buyerId, BasketItemDto[] items)
    {
        return new CustomerBasketDto
        {
            BuyerId = buyerId,
            Items = items,
            ProductName = items.Length == 0 ? "" : items[0].ProductName,
            Quantity = items.Length == 0 ? 0 : items[0].Quantity
        };
    }

    internal static void RequireBuyerId(string buyerId)
    {
        if (string.IsNullOrWhiteSpace(buyerId))
        {
            throw new CatalogException("Buyer id must be provided.");
        }
    }

    private static Dictionary<int, int> Normalize(int[] productIds, int[] quantities)
    {
        if (productIds.Length != quantities.Length)
        {
            throw new CatalogException("Product ids and quantities must have the same length.");
        }

        var requested = new Dictionary<int, int>();
        for (var i = 0; i < productIds.Length; i++)
        {
            var productId = productIds[i];
            var quantity = quantities[i];
            if (productId <= 0)
            {
                throw new CatalogException("Id is not valid.");
            }

            if (quantity < 1)
            {
                throw new CatalogException("Invalid number of units");
            }

            requested[productId] = checked(requested.GetValueOrDefault(productId) + quantity);
        }

        return requested;
    }
}
