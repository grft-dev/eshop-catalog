using Npgsql;

namespace CatalogService;

internal static class OrderStore
{
    private const string StockConfirmed = "StockConfirmed";
    private const string Shipped = "Shipped";
    private const string Cancelled = "Cancelled";

    internal static OrderDto Checkout(string buyerId, AddressDto address)
    {
        BasketStore.RequireBuyerId(buyerId);
        ValidateAddress(address);

        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var basket = BasketStore.ReadBasket(connection, transaction, buyerId, forUpdate: true);
        if (basket.Items.Length == 0)
        {
            throw new CatalogException("Basket is empty.");
        }

        var items = new List<OrderItemDto>(basket.Items.Length);
        foreach (var basketItem in basket.Items.OrderBy(item => item.ProductId))
        {
            var product = CatalogStore.RequireItem(
                connection,
                transaction,
                basketItem.ProductId,
                forUpdate: true);

            if (product.AvailableStock == 0)
            {
                throw new CatalogException($"Empty stock, product item {product.Name} is sold out");
            }

            if (product.AvailableStock < basketItem.Quantity)
            {
                throw new CatalogException($"Not enough stock for product item {product.Name}.");
            }

            items.Add(new OrderItemDto
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Units = basketItem.Quantity,
                PictureFileName = product.PictureFileName
            });
        }

        var orderId = InsertOrder(connection, transaction, buyerId, address);
        foreach (var item in items)
        {
            InsertOrderItem(connection, transaction, orderId, item);
            ChangeStock(connection, transaction, item.ProductId, -item.Units);
        }

        using (var deleteBasket = new NpgsqlCommand(
            "DELETE FROM baskets WHERE buyer_id = @buyer_id",
            connection,
            transaction))
        {
            deleteBasket.Parameters.AddWithValue("buyer_id", buyerId);
            deleteBasket.ExecuteNonQuery();
        }

        var order = ReadOrder(connection, transaction, orderId);
        transaction.Commit();
        return order;
    }

    internal static OrderDto GetOrder(int orderId)
    {
        using var connection = Database.OpenConnection();
        return ReadOrder(connection, null, orderId);
    }

    internal static OrderDto[] ListOrders(string buyerId)
    {
        BasketStore.RequireBuyerId(buyerId);
        using var connection = Database.OpenConnection();
        using var command = new NpgsqlCommand(
            "SELECT id FROM orders WHERE buyer_id = @buyer_id ORDER BY id",
            connection);
        command.Parameters.AddWithValue("buyer_id", buyerId);

        var ids = new List<int>();
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                ids.Add(reader.GetInt32(0));
            }
        }

        return ids.Select(id => ReadOrder(connection, null, id)).ToArray();
    }

    internal static OrderDto Ship(int orderId)
    {
        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var order = ReadOrder(connection, transaction, orderId, forUpdate: true);
        RequireTransition(order.Status, Shipped);

        UpdateStatus(connection, transaction, orderId, Shipped, "The order was shipped.");
        var shipped = ReadOrder(connection, transaction, orderId);
        transaction.Commit();
        return shipped;
    }

    internal static OrderDto Cancel(int orderId)
    {
        using var connection = Database.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var order = ReadOrder(connection, transaction, orderId, forUpdate: true);
        RequireTransition(order.Status, Cancelled);

        foreach (var item in order.Items.OrderBy(item => item.ProductId))
        {
            CatalogStore.RequireItem(connection, transaction, item.ProductId, forUpdate: true);
            ChangeStock(connection, transaction, item.ProductId, item.Units);
        }

        UpdateStatus(connection, transaction, orderId, Cancelled, "The order was cancelled.");
        var cancelled = ReadOrder(connection, transaction, orderId);
        transaction.Commit();
        return cancelled;
    }

    private static int InsertOrder(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string buyerId,
        AddressDto address)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO orders
                (buyer_id, order_date, status, description, street, city, state, country, zip_code)
            VALUES
                (@buyer_id, @order_date, @status, @description, @street, @city, @state, @country, @zip_code)
            RETURNING id
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("buyer_id", buyerId);
        command.Parameters.AddWithValue("order_date", DateTime.UtcNow);
        command.Parameters.AddWithValue("status", StockConfirmed);
        command.Parameters.AddWithValue("description", "All the items were confirmed with available stock.");
        command.Parameters.AddWithValue("street", address.Street);
        command.Parameters.AddWithValue("city", address.City);
        command.Parameters.AddWithValue("state", address.State);
        command.Parameters.AddWithValue("country", address.Country);
        command.Parameters.AddWithValue("zip_code", address.ZipCode);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static void InsertOrderItem(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int orderId,
        OrderItemDto item)
    {
        using var command = new NpgsqlCommand(
            """
            INSERT INTO order_items
                (order_id, product_id, product_name, unit_price, units, picture_file_name)
            VALUES
                (@order_id, @product_id, @product_name, @unit_price, @units, @picture)
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("order_id", orderId);
        command.Parameters.AddWithValue("product_id", item.ProductId);
        command.Parameters.AddWithValue("product_name", item.ProductName);
        command.Parameters.AddWithValue("unit_price", item.UnitPrice);
        command.Parameters.AddWithValue("units", item.Units);
        command.Parameters.AddWithValue("picture", item.PictureFileName);
        command.ExecuteNonQuery();
    }

    private static void ChangeStock(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int productId,
        int change)
    {
        using var command = new NpgsqlCommand(
            """
            UPDATE catalog_items
            SET available_stock = available_stock + @change,
                on_reorder = available_stock + @change = 0
            WHERE id = @product_id
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("product_id", productId);
        command.Parameters.AddWithValue("change", change);
        command.ExecuteNonQuery();
    }

    private static void UpdateStatus(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int orderId,
        string status,
        string description)
    {
        using var command = new NpgsqlCommand(
            "UPDATE orders SET status = @status, description = @description WHERE id = @id",
            connection,
            transaction);
        command.Parameters.AddWithValue("id", orderId);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("description", description);
        command.ExecuteNonQuery();
    }

    private static OrderDto ReadOrder(
        NpgsqlConnection connection,
        NpgsqlTransaction? transaction,
        int orderId,
        bool forUpdate = false)
    {
        if (orderId <= 0)
        {
            throw new CatalogException("Id is not valid.");
        }

        using var command = new NpgsqlCommand(
            $"""
            SELECT id, buyer_id, order_date, status, description, street, city, state, country, zip_code
            FROM orders
            WHERE id = @id{(forUpdate ? " FOR UPDATE" : "")}
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("id", orderId);

        OrderDto order;
        using (var reader = command.ExecuteReader())
        {
            if (!reader.Read())
            {
                throw new CatalogException($"Order with id {orderId} not found.");
            }

            order = new OrderDto
            {
                Id = reader.GetInt32(0),
                BuyerId = reader.GetString(1),
                OrderDate = reader.GetDateTime(2),
                Status = reader.GetString(3),
                Description = reader.GetString(4),
                Address = new AddressDto
                {
                    Street = reader.GetString(5),
                    City = reader.GetString(6),
                    State = reader.GetString(7),
                    Country = reader.GetString(8),
                    ZipCode = reader.GetString(9)
                }
            };
        }

        using var itemsCommand = new NpgsqlCommand(
            """
            SELECT product_id, product_name, unit_price, units, picture_file_name
            FROM order_items
            WHERE order_id = @order_id
            ORDER BY product_id
            """,
            connection,
            transaction);
        itemsCommand.Parameters.AddWithValue("order_id", orderId);
        using var itemsReader = itemsCommand.ExecuteReader();
        var items = new List<OrderItemDto>();
        while (itemsReader.Read())
        {
            items.Add(new OrderItemDto
            {
                ProductId = itemsReader.GetInt32(0),
                ProductName = itemsReader.GetString(1),
                UnitPrice = itemsReader.GetDecimal(2),
                Units = itemsReader.GetInt32(3),
                PictureFileName = itemsReader.GetString(4)
            });
        }

        order.Items = items.ToArray();
        order.Total = items.Sum(item => item.UnitPrice * item.Units);
        return order;
    }

    private static void RequireTransition(string currentStatus, string targetStatus)
    {
        if (currentStatus != StockConfirmed)
        {
            throw new CatalogException(
                $"Is not possible to change the order status from {currentStatus} to {targetStatus}.");
        }
    }

    private static void ValidateAddress(AddressDto address)
    {
        if (address is null)
        {
            throw new CatalogException("Address must be provided.");
        }

        RequirePart(address.Street, "Street");
        RequirePart(address.City, "City");
        RequirePart(address.State, "State");
        RequirePart(address.Country, "Country");
        RequirePart(address.ZipCode, "Zip code");
    }

    private static void RequirePart(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new CatalogException($"{name} must be provided.");
        }
    }
}
