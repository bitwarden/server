using Bit.Core.Settings;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class SalesAssistedRegistrationTokenableFactory : ISalesAssistedRegistrationTokenableFactory
{
    private readonly GlobalSettings _globalSettings;

    public SalesAssistedRegistrationTokenableFactory(GlobalSettings globalSettings)
    {
        _globalSettings = globalSettings;
    }

    public SalesAssistedRegistrationTokenable CreateToken(string email, string? name) =>
        new(email, name, _globalSettings.SalesAssistedRegistrationTokenLifetimeDays);
}
