using Bit.Core.Settings;

namespace Bit.Core.Auth.Models.Business.Tokenables;

public class SalesAssistedRegistrationTokenableFactory : ISalesAssistedRegistrationTokenableFactory
{
    private readonly GlobalSettings _globalSettings;
    private readonly TimeProvider _timeProvider;

    public SalesAssistedRegistrationTokenableFactory(GlobalSettings globalSettings, TimeProvider timeProvider)
    {
        _globalSettings = globalSettings;
        _timeProvider = timeProvider;
    }

    public SalesAssistedRegistrationTokenable CreateToken(string email, string? name)
    {
        var token = new SalesAssistedRegistrationTokenable(email, name)
        {
            ExpirationDate = _timeProvider.GetUtcNow().UtcDateTime
                .AddDays(_globalSettings.SalesAssistedRegistrationTokenLifetimeDays)
        };
        return token;
    }
}
