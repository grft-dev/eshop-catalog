namespace CatalogService;

public static class Catalog
{
    public static CatalogPage ListItems(int pageIndex, int pageSize, string name, int[] typeIds, int[] brandIds)
    {
        return CatalogStore.ListItems(pageIndex, pageSize, name, typeIds, brandIds);
    }

    public static CatalogItemDto GetItem(int id)
    {
        return CatalogStore.GetItem(id);
    }

    public static CatalogItemDto[] GetItemsByIds(int[] ids)
    {
        return CatalogStore.GetItemsByIds(ids);
    }

    public static CatalogBrandDto[] ListBrands()
    {
        return CatalogStore.ListBrands();
    }

    public static CatalogTypeDto[] ListTypes()
    {
        return CatalogStore.ListTypes();
    }

    public static CatalogFacetsDto GetFacets(int[] typeIds, int[] brandIds)
    {
        return CatalogStore.GetFacets(typeIds, brandIds);
    }

    public static CatalogItemDto CreateItem(CatalogItemInput input)
    {
        return CatalogStore.CreateItem(input);
    }

    public static CatalogItemDto UpdateItem(int id, CatalogItemInput input)
    {
        return CatalogStore.UpdateItem(id, input);
    }

    public static bool DeleteItem(int id)
    {
        return CatalogStore.DeleteItem(id);
    }

    public static int RemoveStock(int id, int quantity)
    {
        return CatalogStore.RemoveStock(id, quantity);
    }
}
