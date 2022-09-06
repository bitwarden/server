namespace Bit.Test.Common.Helpers;

public static class TestCaseHelper
{
    public static IEnumerable<IEnumerable<T>> GetCombinations<T>(params T[] items)
    {
        var count = Math.Pow(2, items.Length);
        for (var i = 0; i < count; i++)
        {
            var str = Convert.ToString(i, 2).PadLeft(items.Length, '0');
            List<T> combination = new();
            for (var j = 0; j < str.Length; j++)
            {
                if (str[j] == '1')
                {
                    combination.Add(items[j]);
                }
            }
            yield return combination;
        }
    }

    public static IEnumerable<IEnumerable<object>> GetCombinationsOfMultipleLists(params IEnumerable<object>[] optionLists)
    {
        if (!optionLists.Any())
        {
            yield break;
        }

        foreach (var item in optionLists.First())
        {
            var itemArray = new[] { item };

            if (optionLists.Length == 1)
            {
                yield return itemArray;
            }

            foreach (var nextCombination in GetCombinationsOfMultipleLists(optionLists.Skip(1).ToArray()))
            {
                yield return itemArray.Concat(nextCombination);
            }
        }
    }
}
