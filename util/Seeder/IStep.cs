using Bit.Seeder.Pipeline;

namespace Bit.Seeder;

internal interface IStep
{
    void Execute(SeederContext context);
}
