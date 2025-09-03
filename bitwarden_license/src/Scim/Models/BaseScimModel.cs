// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

namespace Bit.Scim.Models;

public abstract class BaseScimModel
{
    public BaseScimModel()
    { }

    public BaseScimModel(string schema)
    {
        Schemas = new List<string> { schema };
    }

    public List<string> Schemas { get; set; }
}
