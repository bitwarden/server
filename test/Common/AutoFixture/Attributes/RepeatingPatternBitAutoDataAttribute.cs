#nullable enable
using System.Reflection;

namespace Bit.Test.Common.AutoFixture.Attributes;

/// <summary>
/// This attribute helps to generate all possible combinations of the provided pattern values for a given number of parameters.
/// <remarks>
/// <para>
/// The repeating pattern values should be provided as an array for each parameter. Currently supports up to 3 parameters.
/// </para>
/// <para>
/// The attribute is a variation of the <see cref="BitAutoDataAttribute"/> attribute and can be used in the same way, except that all fixed value parameters needs to be provided as an array.
/// </para>
/// <para>
/// Note: Use it with caution. While this attribute is useful for handling repeating parameters, having too many parameters should be avoided as it is considered a code smell in most of the cases.
/// If your test requires more than 2 repeating parameters, or the test have too many conditions that change the behavior of the test, consider refactoring the test by splitting it into multiple smaller ones.
/// </para>
/// </remarks>
/// <example>
/// 1st example:
/// <code>
/// [RepeatingPatternBitAutoData([false], [1,2,3])]
/// public void TestMethod(bool first, int second, SomeOtherData third, ...)
/// </code>
/// Would generate the following test cases:
/// <list type="bullet">
/// <item>false, 1</item>
/// <item>false, 2</item>
/// <item>false, 3</item>
/// </list>
/// 2nd example:
/// <code>
/// [RepeatingPatternBitAutoData([false, true], [false, true], [false, true])]
/// public void TestMethod(bool first, bool second, bool third)
/// </code>
/// Would generate the following test cases:
/// <list type="bullet">
/// <item>false, false, false</item>
/// <item>false, false, true</item>
/// <item>false, true, false</item>
/// <item>false, true, true</item>
/// <item>true, false, false</item>
/// <item>true, false, true</item>
/// <item>true, true, false</item>
/// <item>true, true, true</item>
/// </list>
/// </example>
/// </summary>
public class RepeatingPatternBitAutoDataAttribute : BitAutoDataAttribute
{
    private readonly List<object?[]> _repeatingDataList;

    public RepeatingPatternBitAutoDataAttribute(object?[] first)
    {
        _repeatingDataList = AllValues([first]);
    }

    public RepeatingPatternBitAutoDataAttribute(object?[] first, object?[] second)
    {
        _repeatingDataList = AllValues([first, second]);
    }

    public RepeatingPatternBitAutoDataAttribute(object?[] first, object?[] second, object?[] third)
    {
        _repeatingDataList = AllValues([first, second, third]);
    }

    public override IEnumerable<object?[]> GetData(MethodInfo testMethod)
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

    private static List<object?[]> AllValues(object?[][] parameterToPatternValues)
    {
        var result = new List<object?[]>();
        GenerateCombinations(parameterToPatternValues, new object[parameterToPatternValues.Length], 0, result);
        return result;
    }

    private static void GenerateCombinations(object?[][] parameterToPatternValues, object?[] current, int index,
        List<object?[]> result)
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
