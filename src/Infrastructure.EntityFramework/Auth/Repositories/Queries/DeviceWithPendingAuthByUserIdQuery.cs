using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Data;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;

public class DeviceWithPendingAuthByUserIdQuery
{
    public IQueryable<DeviceAuthDetails> GetQuery(
        DatabaseContext dbContext,
        Guid userId,
        int expirationMinutes)
    {
        var devicesWithAuthQuery = (
            from device in dbContext.Devices
            where device.UserId == userId && device.Active
            select new
            {
                device,
                authRequest =
                (
                    from authRequest in dbContext.AuthRequests
                    where authRequest.RequestDeviceIdentifier == device.Identifier
                    where authRequest.Type == AuthRequestType.AuthenticateAndUnlock || authRequest.Type == AuthRequestType.Unlock
                    where authRequest.Approved == null
                    where authRequest.UserId == userId
                    where authRequest.CreationDate.AddMinutes(expirationMinutes) > DateTime.UtcNow
                    orderby authRequest.CreationDate descending
                    select authRequest
                ).First()
            }).Select(deviceWithAuthRequest => new DeviceAuthDetails(
                deviceWithAuthRequest.device,
                deviceWithAuthRequest.authRequest.Id,
                deviceWithAuthRequest.authRequest.CreationDate));

        return devicesWithAuthQuery;
    }
}
