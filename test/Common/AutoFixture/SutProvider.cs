using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;

namespace Bit.Test.Common.AutoFixture;

public class SutProvider<TSut> : ISutProvider
{
    private Dictionary<Type, Dictionary<string, object>> _dependencies;
    private readonly IFixture _fixture;
    private readonly ConstructorParameterRelay<TSut> _constructorParameterRelay;

    public TSut Sut { get; private set; }
    public Type SutType => typeof(TSut);

    public SutProvider() : this(new Fixture()) { }

    public SutProvider(IFixture fixture)
    {
        _dependencies = new Dictionary<Type, Dictionary<string, object>>();
        _fixture = (fixture ?? new Fixture()).WithAutoNSubstitutions().Customize(new GlobalSettings());
        _constructorParameterRelay = new ConstructorParameterRelay<TSut>(this, _fixture);
        _fixture.Customizations.Add(_constructorParameterRelay);
    }

    public SutProvider<TSut> SetDependency<T>(T dependency, string parameterName = "")
        => SetDependency(typeof(T), dependency, parameterName);
    public SutProvider<TSut> SetDependency(Type dependencyType, object dependency, string parameterName = "")
    {
        if (_dependencies.ContainsKey(dependencyType))
        {
            _dependencies[dependencyType][parameterName] = dependency;
        }
        else
        {
            _dependencies[dependencyType] = new Dictionary<string, object> { { parameterName, dependency } };
        }

        return this;
    }

    public T GetDependency<T>(string parameterName = "") => (T)GetDependency(typeof(T), parameterName);
    public object GetDependency(Type dependencyType, string parameterName = "")
    {
        if (DependencyIsSet(dependencyType, parameterName))
        {
            return _dependencies[dependencyType][parameterName];
        }
        else if (_dependencies.ContainsKey(dependencyType))
        {
            var knownDependencies = _dependencies[dependencyType];
            if (knownDependencies.Values.Count == 1)
            {
                return _dependencies[dependencyType].Values.Single();
            }
            else
            {
                throw new ArgumentException(string.Concat($"Dependency of type {dependencyType.Name} and name ",
                    $"{parameterName} does not exist. Available dependency names are: ",
                    string.Join(", ", knownDependencies.Keys)));
            }
        }
        else
        {
            throw new ArgumentException($"Dependency of type {dependencyType.Name} and name {parameterName} has not been set.");
        }
    }

    /// <summary>
    /// Creates a new instance of the given type using the SutProvider's inner Fixture.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    public T Create<T>() => _fixture.Create<T>();

    public void Reset()
    {
        _dependencies = new Dictionary<Type, Dictionary<string, object>>();
        Sut = default;
    }

    ISutProvider ISutProvider.Create() => Create();
    public SutProvider<TSut> Create()
    {
        Sut = _fixture.Create<TSut>();
        return this;
    }

    private bool DependencyIsSet(Type dependencyType, string parameterName = "")
        => _dependencies.ContainsKey(dependencyType) && _dependencies[dependencyType].ContainsKey(parameterName);

    private object GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    private class ConstructorParameterRelay<T> : ISpecimenBuilder
    {
        private readonly SutProvider<T> _sutProvider;
        private readonly IFixture _fixture;

        public ConstructorParameterRelay(SutProvider<T> sutProvider, IFixture fixture)
        {
            _sutProvider = sutProvider;
            _fixture = fixture;
        }

        public object Create(object request, ISpecimenContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            if (!(request is ParameterInfo parameterInfo))
            {
                return new NoSpecimen();
            }
            if (parameterInfo.Member.DeclaringType != typeof(T) ||
                parameterInfo.Member.MemberType != MemberTypes.Constructor)
            {
                return new NoSpecimen();
            }

            if (_sutProvider.DependencyIsSet(parameterInfo.ParameterType, parameterInfo.Name))
            {
                return _sutProvider.GetDependency(parameterInfo.ParameterType, parameterInfo.Name);
            }
            // Return default type if set
            else if (_sutProvider.DependencyIsSet(parameterInfo.ParameterType, ""))
            {
                return _sutProvider.GetDependency(parameterInfo.ParameterType, "");
            }


            // This is the equivalent of _fixture.Create<parameterInfo.ParameterType>, but no overload for
            // Create(Type type) exists.
            var dependency = new SpecimenContext(_fixture).Resolve(new SeededRequest(parameterInfo.ParameterType,
                _sutProvider.GetDefault(parameterInfo.ParameterType)));
            _sutProvider.SetDependency(parameterInfo.ParameterType, dependency, parameterInfo.Name);
            return dependency;
        }
    }
}
