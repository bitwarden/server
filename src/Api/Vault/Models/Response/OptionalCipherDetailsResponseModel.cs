using Bit.Api.Vault.Models.Response;
using Bit.Core.Models.Api;

#nullable enable

public class OptionalCipherDetailsResponseModel : ResponseModel
{
    public bool Unavailable { get; set; }

    public CipherDetailsResponseModel? Cipher { get; set; }

    public OptionalCipherDetailsResponseModel()
        : base("optionalCipherDetails") { }
}
