using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Infrastructure.EntityFramework.Repositories;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;

public class DeviceWithPendingAuthByUserIdQuery
{
    public IQueryable<DeviceAuthRequestResponseModel> GetQuery(
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
            }).Select(deviceWithAuthRequest => new DeviceAuthRequestResponseModel(
                deviceWithAuthRequest.device,
                deviceWithAuthRequest.authRequest != null
                    ? deviceWithAuthRequest.authRequest.Id
                    : Guid.Empty,
                deviceWithAuthRequest.authRequest != null
                    ? deviceWithAuthRequest.authRequest.CreationDate
                    : DateTime.MinValue));

        return devicesWithAuthQuery;
    }
}
