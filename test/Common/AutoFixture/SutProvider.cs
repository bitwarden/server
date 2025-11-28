// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.Reflection;
using AutoFixture;
using AutoFixture.Kernel;

namespace Bit.Test.Common.AutoFixture;

/// <summary>
/// A utility class that encapsulates a system under test (sut) and its dependencies.
/// By default, all dependencies are initialized as mocks using the NSubstitute library.
/// SutProvider provides an interface for accessing these dependencies in the arrange and assert stages of your tests.
/// </summary>
/// <typeparam name="TSut">The concrete implementation of the class being tested.</typeparam>
public class SutProvider<TSut> : ISutProvider
{
    /// <summary>
    /// A record of the configured dependencies (constructor parameters). The outer Dictionary is keyed by the dependency's
    /// type, and the inner dictionary is keyed by the parameter name (optionally used to disambiguate parameters with the same type).
    /// The inner dictionary value is the dependency.
    /// </summary>
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

    /// <summary>
    /// Registers a dependency to be injected when the sut is created. You must call <see cref="Create"/> after
    /// this method to (re)create the sut with the dependency.
    /// </summary>
    /// <param name="dependency">The dependency to register.</param>
    /// <param name="parameterName">An optional parameter name to disambiguate the dependency if there are multiple of the same type. You generally don't need this.</param>
    /// <typeparam name="T">The type to register the dependency under - usually an interface. This should match the type expected by the sut's constructor.</typeparam>
    /// <returns></returns>
    public SutProvider<TSut> SetDependency<T>(T dependency, string parameterName = "")
        => SetDependency(typeof(T), dependency, parameterName);

    /// <summary>
    /// An overload for <see cref="SetDependency{T}"/> which takes a runtime <see cref="Type"/> object rather than a compile-time type.
    /// </summary>
    private SutProvider<TSut> SetDependency(Type dependencyType, object dependency, string parameterName = "")
    {
        if (_dependencies.TryGetValue(dependencyType, out var dependencyForType))
        {
            dependencyForType[parameterName] = dependency;
        }
        else
        {
            _dependencies[dependencyType] = new Dictionary<string, object> { { parameterName, dependency } };
        }

        return this;
    }

    /// <summary>
    /// Gets a dependency of the sut. Can only be called after the dependency has been set, either explicitly with
    /// <see cref="SetDependency{T}"/> or automatically with <see cref="Create"/>.
    /// As dependencies are initialized with NSubstitute mocks by default, this is often used to retrieve those mocks in order to
    /// configure them during the arrange stage, or check received calls in the assert stage.
    /// </summary>
    /// <param name="parameterName">An optional parameter name to disambiguate the dependency if there are multiple of the same type. You generally don't need this.</param>
    /// <typeparam name="T">The type of the dependency you want to get - usually an interface.</typeparam>
    /// <returns>The dependency.</returns>
    public T GetDependency<T>(string parameterName = "") => (T)GetDependency(typeof(T), parameterName);

    /// <summary>
    /// An overload for <see cref="GetDependency{T}"/> which takes a runtime <see cref="Type"/> object rather than a compile-time type.
    /// </summary>
    private object GetDependency(Type dependencyType, string parameterName = "")
    {
        if (DependencyIsSet(dependencyType, parameterName))
        {
            return _dependencies[dependencyType][parameterName];
        }

        if (_dependencies.TryGetValue(dependencyType, out var knownDependencies))
        {
            if (knownDependencies.Values.Count == 1)
            {
                return knownDependencies.Values.Single();
            }

            throw new ArgumentException(string.Concat($"Dependency of type {dependencyType.Name} and name ",
                $"{parameterName} does not exist. Available dependency names are: ",
                string.Join(", ", knownDependencies.Keys)));
        }

        throw new ArgumentException($"Dependency of type {dependencyType.Name} and name {parameterName} has not been set.");
    }

    /// <summary>
    /// Clear all the dependencies and the sut. This reverts the SutProvider back to a fully uninitialized state.
    /// </summary>
    public void Reset()
    {
        _dependencies = new Dictionary<Type, Dictionary<string, object>>();
        Sut = default;
    }

    /// <summary>
    /// Recreate a new sut with all new dependencies. This will reset all dependencies, including mocked return values
    /// and any dependencies set with <see cref="SetDependency{T}"/>.
    /// </summary>
    public void Recreate()
    {
        _dependencies = new Dictionary<Type, Dictionary<string, object>>();
        Sut = _fixture.Create<TSut>();
    }

    /// <inheritdoc cref="Create()"/>>
    ISutProvider ISutProvider.Create() => Create();

    /// <summary>
    /// Creates the sut, injecting any dependencies configured via <see cref="SetDependency{T}"/> and falling back to
    /// NSubstitute mocks for any dependencies that have not been explicitly configured.
    /// </summary>
    /// <returns></returns>
    public SutProvider<TSut> Create()
    {
        Sut = _fixture.Create<TSut>();
        return this;
    }

    private bool DependencyIsSet(Type dependencyType, string parameterName = "")
        => _dependencies.ContainsKey(dependencyType) && _dependencies[dependencyType].ContainsKey(parameterName);

    private object GetDefault(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

    /// <summary>
    /// A specimen builder which tells Autofixture to use the dependency registered in <see cref="SutProvider{T}"/>
    /// when creating test data. If no matching dependency exists in <see cref="SutProvider{TSut}"/>, it creates
    /// an NSubstitute mock and registers it using <see cref="SutProvider{TSut}.SetDependency{T}"/>
    /// so it can be retrieved later.
    /// This is the link between <see cref="SutProvider{T}"/> and Autofixture.
    /// </summary>
    /// <remarks>
    /// Autofixture knows how to create sample data of simple types (such as an int or string) but not more complex classes.
    /// We create our own <see cref="ISpecimenBuilder"/> and register it with the <see cref="Fixture"/> in
    /// <see cref="SutProvider{TSut}"/> to provide that instruction.
    /// </remarks>
    /// <typeparam name="T">The type of the sut.</typeparam>
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
            // Basic checks to filter out irrelevant requests from Autofixture
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

            // Use the dependency set under this parameter name, if any
            if (_sutProvider.DependencyIsSet(parameterInfo.ParameterType, parameterInfo.Name))
            {
                return _sutProvider.GetDependency(parameterInfo.ParameterType, parameterInfo.Name);
            }

            // Use the default dependency set for this type, if any (i.e. no parameter name has been specified)
            if (_sutProvider.DependencyIsSet(parameterInfo.ParameterType, ""))
            {
                return _sutProvider.GetDependency(parameterInfo.ParameterType, "");
            }

            // Fallback: pass the request down the chain. This lets another fixture customization populate the value.
            // If you haven't added any customizations, this should be an NSubstitute mock.
            // It is registered with SetDependency so you can retrieve it later.

            // This is the equivalent of _fixture.Create<parameterInfo.ParameterType>, but no overload for
            // Create(Type type) exists.
            var dependency = new SpecimenContext(_fixture).Resolve(new SeededRequest(parameterInfo.ParameterType,
                _sutProvider.GetDefault(parameterInfo.ParameterType)));
            _sutProvider.SetDependency(parameterInfo.ParameterType, dependency, parameterInfo.Name);
            return dependency;
        }
    }
}
