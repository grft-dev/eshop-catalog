using Npgsql;

namespace CatalogService;

internal static class Database
{
    private const string DefaultConnectionString =
        "Host=localhost;Port=5432;Database=eshop;Username=eshop;Password=eshop";

    internal static NpgsqlConnection OpenConnection()
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Catalog")
            ?? DefaultConnectionString;

        var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        return connection;
    }
}
