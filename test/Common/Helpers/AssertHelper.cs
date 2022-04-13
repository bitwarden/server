using System;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using Bit.Core.Utilities;
using Xunit;
using Xunit.Sdk;

namespace Bit.Test.Common.Helpers
{
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

            foreach (var expectedPropInfo in expected.GetType().GetProperties().Where(pi => !relevantExcludedProperties.Contains(pi.Name)))
            {
                var actualPropInfo = actual.GetType().GetProperty(expectedPropInfo.Name);

                if (actualPropInfo == null)
                {
                    throw new Exception(string.Concat($"Expected actual object to contain a property named {expectedPropInfo.Name}, but it does not\n",
                    $"Expected:\n{JsonSerializer.Serialize(expected, JsonHelpers.Indented)}\n",
                    $"Actual:\n{JsonSerializer.Serialize(actual, JsonHelpers.Indented)}"));
                }

                if (expectedPropInfo.PropertyType == typeof(string) || expectedPropInfo.PropertyType.IsValueType)
                {
                    Assert.Equal(expectedPropInfo.GetValue(expected), actualPropInfo.GetValue(actual));
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
    }
}
