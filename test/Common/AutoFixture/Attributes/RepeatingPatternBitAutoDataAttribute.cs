using System.Reflection;

namespace Bit.Test.Common.AutoFixture.Attributes;

public class RepeatingPatternBitAutoDataAttribute : BitAutoDataAttribute
{
    private readonly List<object[]> _repeatingDataList;

    public RepeatingPatternBitAutoDataAttribute(object[] first)
    {
        _repeatingDataList = AllValues([first]);
    }

    public RepeatingPatternBitAutoDataAttribute(object[] first, object[] second)
    {
        _repeatingDataList = AllValues([first, second]);
    }

    public RepeatingPatternBitAutoDataAttribute(object[] first, object[] second, object[] third)
    {
        _repeatingDataList = AllValues([first, second, third]);
    }

    public override IEnumerable<object[]> GetData(MethodInfo testMethod)
    {
        foreach (var repeatingData in _repeatingDataList)
        {
            var bitData = base.GetData(testMethod).First();
            for (var i = 0; i < repeatingData.Length; i++)
            {
                bitData[i] = repeatingData[i];
            }

            yield return bitData;
        }
    }

    private static List<object[]> AllValues(object[][] parameterToPatternValues)
    {
        var result = new List<object[]>();
        GenerateCombinations(parameterToPatternValues, new object[parameterToPatternValues.Length], 0, result);
        return result;
    }

    private static void GenerateCombinations(object[][] parameterToPatternValues, object[] current, int index,
        List<object[]> result)
    {
        if (index == current.Length)
        {
            result.Add((object[])current.Clone());
            return;
        }

        var patternValues = parameterToPatternValues[index];

        foreach (var value in patternValues)
        {
            current[index] = value;
            GenerateCombinations(parameterToPatternValues, current, index + 1, result);
        }
    }
}
