namespace Bit.Scim.Models
{
    public class ScimMetaModel
    {
        public ScimMetaModel(string resourceType)
        {
            ResourceType = resourceType;
        }

        public string ResourceType { get; set; }
    }
}
