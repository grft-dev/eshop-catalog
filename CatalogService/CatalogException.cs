namespace CatalogService;

internal sealed class CatalogException : Exception
{
    public CatalogException(string message) : base(message)
    {
    }
}
