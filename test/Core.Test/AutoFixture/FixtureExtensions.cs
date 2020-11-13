using System;
using System.Reflection;
using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Kernel;

namespace Bit.Core.Test.AutoFixture
{
    public static class FixtureExtensions
    {
        // Based on https://stackoverflow.com/a/58170719
        public static FreezeCreateProvider<TTypeToConstruct> For<TTypeToConstruct>(this IFixture fixture)
        {
            return new FreezeCreateProvider<TTypeToConstruct>(fixture);
        }

        public static IFixture WithAutoNSubstitutions(this IFixture fixture)
            => fixture.Customize(new AutoNSubstituteCustomization());
    }

    #region ConstructorParameter fluent classes
    public class FreezeCreateProvider<TTypeToConstruct>
    {
        private readonly IFixture _fixture;

        public FreezeCreateProvider(IFixture fixture)
        {
            _fixture = fixture;
        }

        public FreezeCreateProvider<TTypeToConstruct> Freeze<TTypeOfParam>(out TTypeOfParam parameterValue, string parameterName = null)
        {
            parameterValue = _fixture.Create<TTypeOfParam>();
            AddConstructorParameter(parameterName, parameterValue);
            return this;
        }

        public TTypeToConstruct Create()
        {
            var instance = _fixture.Create<TTypeToConstruct>();
            return instance;
        }

        internal void AddConstructorParameter<TTypeOfParam>(string parameterName, TTypeOfParam parameterValue)
        {
            _fixture.Customizations.Add(new ConstructorParameterRelay<TTypeToConstruct, TTypeOfParam>(parameterName, parameterValue));
        }
    }

    public class ConstructorParameterRelay<TTypeToConstruct, TValueType> : ISpecimenBuilder
    {
        private readonly string _paramName;
        private readonly TValueType _paramValue;

        public ConstructorParameterRelay(string paramName, TValueType paramValue)
        {
            _paramName = paramName;
            _paramValue = paramValue;
        }

        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (!(request is ParameterInfo parameterInfo))
                return new NoSpecimen();
            if (parameterInfo.Member.DeclaringType != typeof(TTypeToConstruct) ||
                parameterInfo.Member.MemberType != MemberTypes.Constructor ||
                parameterInfo.ParameterType != typeof(TValueType) ||
                (_paramName != null && parameterInfo.Name != _paramName))
                return new NoSpecimen();
            return _paramValue;
        }
    }
    #endregion
}
