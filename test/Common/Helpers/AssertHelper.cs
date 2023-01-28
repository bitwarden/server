using System.Collections;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Bit.Core.Utilities;
using Microsoft.AspNetCore.Http;
using Xunit;
using Xunit.Sdk;

namespace Bit.Test.Common.Helpers;

public static class AssertHelper
{
    public static void AssertPropertyEqual(object expected, object actual, params string[] excludedPropertyStrings)
    {
        var relevantExcludedProperties = excludedPropertyStrings.Where(name => !name.Contains('.')).ToList();
        if (expected == null)
        {
            Assert.Null(actual);
            return;
        }

        if (actual == null)
        {
            throw new Exception("Actual object is null but expected is not");
        }

        foreach (var expectedPropInfo in expected.GetType().GetProperties().Where(pi => !relevantExcludedProperties.Contains(pi.Name) && !pi.GetIndexParameters().Any()))
        {
            var actualPropInfo = actual.GetType().GetProperty(expectedPropInfo.Name);

            if (actualPropInfo == null)
            {
                throw new Exception(string.Concat($"Expected actual object to contain a property named {expectedPropInfo.Name}, but it does not\n",
                $"Expected:\n{JsonSerializer.Serialize(expected, JsonHelpers.Indented)}\n",
                $"Actual:\n{JsonSerializer.Serialize(actual, JsonHelpers.Indented)}"));
            }

            if (typeof(IComparable).IsAssignableFrom(expectedPropInfo.PropertyType) || expectedPropInfo.PropertyType.IsPrimitive || expectedPropInfo.PropertyType.IsValueType)
            {
                Assert.Equal(expectedPropInfo.GetValue(expected), actualPropInfo.GetValue(actual));
            }
            else if (expectedPropInfo.PropertyType == typeof(JsonDocument) && actualPropInfo.PropertyType == typeof(JsonDocument))
            {
                static string JsonDocString(PropertyInfo info, object obj) => JsonSerializer.Serialize(info.GetValue(obj));
                Assert.Equal(JsonDocString(expectedPropInfo, expected), JsonDocString(actualPropInfo, actual));
            }
            else if (typeof(IEnumerable).IsAssignableFrom(expectedPropInfo.PropertyType) && typeof(IEnumerable).IsAssignableFrom(actualPropInfo.PropertyType))
            {
                var expectedItems = ((IEnumerable)expectedPropInfo.GetValue(expected)).Cast<object>();
                var actualItems = ((IEnumerable)actualPropInfo.GetValue(actual)).Cast<object>();

                AssertPropertyEqualPredicate(expectedItems, excludedPropertyStrings)(actualItems);
            }
            else
            {
                var prefix = $"{expectedPropInfo.PropertyType.Name}.";
                var nextExcludedProperties = excludedPropertyStrings.Where(name => name.StartsWith(prefix))
                    .Select(name => name[prefix.Length..]).ToArray();
                AssertPropertyEqual(expectedPropInfo.GetValue(expected), actualPropInfo.GetValue(actual), nextExcludedProperties);
            }
        }
    }

    private static Predicate<T> AssertPropertyEqualPredicate<T>(T expected, params string[] excludedPropertyStrings) => (actual) =>
    {
        AssertPropertyEqual(expected, actual, excludedPropertyStrings);
        return true;
    };

    public static Expression<Predicate<T>> AssertPropertyEqual<T>(T expected, params string[] excludedPropertyStrings) =>
        (T actual) => AssertPropertyEqualPredicate(expected, excludedPropertyStrings)(actual);

    private static Predicate<IEnumerable<T>> AssertPropertyEqualPredicate<T>(IEnumerable<T> expected, params string[] excludedPropertyStrings) => (actual) =>
    {
        // IEnumerable.Zip doesn't account for different lengths, we need to check this ourselves
        if (actual.Count() != expected.Count())
        {
            throw new Exception(string.Concat($"Actual IEnumerable does not have the expected length.\n",
            $"Expected: {expected.Count()}\n",
            $"Actual: {actual.Count()}"));
        }

        var elements = expected.Zip(actual);
        foreach (var (expectedEl, actualEl) in elements)
        {
            AssertPropertyEqual(expectedEl, actualEl, excludedPropertyStrings);
        }

        return true;
    };

    public static Expression<Predicate<IEnumerable<T>>> AssertPropertyEqual<T>(IEnumerable<T> expected, params string[] excludedPropertyStrings) =>
        (actual) => AssertPropertyEqualPredicate(expected, excludedPropertyStrings)(actual);

    private static Predicate<T> AssertEqualExpectedPredicate<T>(T expected) => (actual) =>
    {
        Assert.Equal(expected, actual);
        return true;
    };

    public static Expression<Predicate<T>> AssertEqualExpected<T>(T expected) =>
        (T actual) => AssertEqualExpectedPredicate(expected)(actual);

    public static JsonElement AssertJsonProperty(JsonElement element, string propertyName, JsonValueKind jsonValueKind)
    {
        if (!element.TryGetProperty(propertyName, out var subElement))
        {
            throw new XunitException($"Could not find property by name '{propertyName}'");
        }

        Assert.Equal(jsonValueKind, subElement.ValueKind);
        return subElement;
    }

    public static void AssertEqualJson(JsonElement a, JsonElement b)
    {
        switch (a.ValueKind)
        {
            case JsonValueKind.Array:
                Assert.Equal(JsonValueKind.Array, b.ValueKind);
                AssertEqualJsonArray(a, b);
                break;
            case JsonValueKind.Object:
                Assert.Equal(JsonValueKind.Object, b.ValueKind);
                AssertEqualJsonObject(a, b);
                break;
            case JsonValueKind.False:
                Assert.Equal(JsonValueKind.False, b.ValueKind);
                break;
            case JsonValueKind.True:
                Assert.Equal(JsonValueKind.True, b.ValueKind);
                break;
            case JsonValueKind.Number:
                Assert.Equal(JsonValueKind.Number, b.ValueKind);
                Assert.Equal(a.GetDouble(), b.GetDouble());
                break;
            case JsonValueKind.String:
                Assert.Equal(JsonValueKind.String, b.ValueKind);
                Assert.Equal(a.GetString(), b.GetString());
                break;
            case JsonValueKind.Null:
                Assert.Equal(JsonValueKind.Null, b.ValueKind);
                break;
            default:
                throw new XunitException($"Bad JsonValueKind '{a.ValueKind}'");
        }
    }

    private static void AssertEqualJsonObject(JsonElement a, JsonElement b)
    {
        Debug.Assert(a.ValueKind == JsonValueKind.Object && b.ValueKind == JsonValueKind.Object);

        var aObjectEnumerator = a.EnumerateObject();
        var bObjectEnumerator = b.EnumerateObject();

        while (true)
        {
            var aCanMove = aObjectEnumerator.MoveNext();
            var bCanMove = bObjectEnumerator.MoveNext();

            if (aCanMove)
            {
                Assert.True(bCanMove, $"a was able to enumerate over object '{a}' but b was NOT able to '{b}'");
            }
            else
            {
                Assert.False(bCanMove, $"a was NOT able to enumerate over object '{a}' but b was able to '{b}'");
            }

            if (aCanMove == false && bCanMove == false)
            {
                // They both can't continue to enumerate at the same time, that is valid
                break;
            }

            var aProp = aObjectEnumerator.Current;
            var bProp = bObjectEnumerator.Current;

            Assert.Equal(aProp.Name, bProp.Name);
            // Recursion!
            AssertEqualJson(aProp.Value, bProp.Value);
        }
    }

    private static void AssertEqualJsonArray(JsonElement a, JsonElement b)
    {
        Debug.Assert(a.ValueKind == JsonValueKind.Array && b.ValueKind == JsonValueKind.Array);

        var aArrayEnumerator = a.EnumerateArray();
        var bArrayEnumerator = b.EnumerateArray();

        while (true)
        {
            var aCanMove = aArrayEnumerator.MoveNext();
            var bCanMove = bArrayEnumerator.MoveNext();

            if (aCanMove)
            {
                Assert.True(bCanMove, $"a was able to enumerate over array '{a}' but b was NOT able to '{b}'");
            }
            else
            {
                Assert.False(bCanMove, $"a was NOT able to enumerate over array '{a}' but b was able to '{b}'");
            }

            if (aCanMove == false && bCanMove == false)
            {
                // They both can't continue to enumerate at the same time, that is valid
                break;
            }

            var aElement = aArrayEnumerator.Current;
            var bElement = bArrayEnumerator.Current;

            // Recursion!
            AssertEqualJson(aElement, bElement);
        }
    }

    public async static Task<T> AssertResponseTypeIs<T>(HttpContext context)
    {
        return await JsonSerializer.DeserializeAsync<T>(context.Response.Body);
    }

    public static TimeSpan AssertRecent(DateTime dateTime, int skewSeconds = 2)
        => AssertRecent(dateTime, TimeSpan.FromSeconds(skewSeconds));

    public static TimeSpan AssertRecent(DateTime dateTime, TimeSpan skew)
    {
        var difference = DateTime.UtcNow - dateTime;
        Assert.True(difference < skew);
        return difference;
    }
}
