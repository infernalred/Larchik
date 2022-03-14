using Larchik.Application.Helpers;

namespace Larchik.Application.Services;

public static class AssetOperation
{
    private static readonly Dictionary<string, Func<int, int>> MakeAssetOperations = new()
    {
        { ListOperations.Purchase, quantity => quantity },
        { ListOperations.Sale, quantity => -quantity }
    };

    public static int CreateAssetDeal(string operation, int quantity) => MakeAssetOperations[operation](quantity);
}