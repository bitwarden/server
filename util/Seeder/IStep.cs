using Bit.Seeder.Pipeline;

namespace Bit.Seeder;

public interface IStep
{
    void Execute(SeederContext context);
}
