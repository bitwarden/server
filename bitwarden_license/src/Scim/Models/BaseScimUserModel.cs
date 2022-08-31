using Bit.Scim.Utilities;

namespace Bit.Scim.Models;

public abstract class BaseScimUserModel : BaseScimModel
{
    public BaseScimUserModel(bool initSchema = false)
    {
        if (initSchema)
        {
            Schemas = new List<string> { ScimConstants.Scim2SchemaUser };
        }
    }

    public string UserName { get; set; }
    public NameModel Name { get; set; }
    public List<EmailModel> Emails { get; set; }
    public string PrimaryEmail => Emails?.FirstOrDefault(e => e.Primary)?.Value;
    public string WorkEmail => Emails?.FirstOrDefault(e => e.Type == "work")?.Value;
    public string DisplayName { get; set; }
    public bool Active { get; set; }
    public List<string> Groups { get; set; }
    public string ExternalId { get; set; }

    public class NameModel
    {
        public NameModel() { }

        public NameModel(string name)
        {
            Formatted = name;
        }

        public string Formatted { get; set; }
        public string GivenName { get; set; }
        public string MiddleName { get; set; }
        public string FamilyName { get; set; }
    }

    public class EmailModel
    {
        public EmailModel() { }

        public EmailModel(string email)
        {
            Primary = true;
            Value = email;
            Type = "work";
        }

        public bool Primary { get; set; }
        public string Value { get; set; }
        public string Type { get; set; }
    }
}
