namespace Bit.Test.Common.AutoFixture;

public interface ISutProvider
{
    Type SutType { get; }
    ISutProvider Create();
}
