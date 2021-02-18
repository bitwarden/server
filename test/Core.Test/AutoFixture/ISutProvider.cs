using System;

namespace Bit.Core.Test.AutoFixture
{
    public interface ISutProvider
    {
        Type SutType { get; }
        ISutProvider Create();
    }
}
