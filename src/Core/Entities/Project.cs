#nullable enable
using Bit.Core.Utilities;

namespace Bit.Core.Entities
{
    public class Project : ITableObject<Guid>
    {
        public Guid Id { get; set; }

        public string? Name {get; set;}

        public void SetNewId()
        {
            if (Id == default(Guid))
            {
                Id = CoreHelpers.GenerateComb();
            }
        }

    }
}
