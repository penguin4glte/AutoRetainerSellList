using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoRetainerSellList.Application.Queries;

public class SearchItemsQuery
{
    public record ItemSearchResult(uint ItemId, string ItemName);

    public List<ItemSearchResult> Execute(string searchText, int maxResults = 50)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(searchText))
                return new List<ItemSearchResult>();

            var itemSheet = Svc.Data.GetExcelSheet<Item>();
            if (itemSheet == null)
                return new List<ItemSearchResult>();

            var results = itemSheet
                .Where(item => item.RowId > 0 && item.Name.ToString().Contains(searchText, StringComparison.OrdinalIgnoreCase))
                .Take(maxResults)
                .Select(item => new ItemSearchResult(item.RowId, item.Name.ToString()))
                .ToList();

            return results;
        }
        catch (Exception ex)
        {
            Svc.Log.Error($"[SearchItemsQuery] Error: {ex}");
            return new List<ItemSearchResult>();
        }
    }
}
