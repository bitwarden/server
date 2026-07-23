namespace Bit.Core.Auth.Models.Business.Tokenables;

public interface ISalesAssistedRegistrationTokenableFactory
{
    SalesAssistedRegistrationTokenable CreateToken(string email, string? name);
}
