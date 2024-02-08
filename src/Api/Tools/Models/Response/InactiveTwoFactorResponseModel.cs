using Bit.Core.Models.Api;

namespace Bit.Api.Tools.Models.Response;

public class InactiveTwoFactorResponseModel : ResponseModel
{
    public InactiveTwoFactorResponseModel() : base("inactive-two-factor") { }

    public IReadOnlyDictionary<string, string> Services { get; set; }
}
