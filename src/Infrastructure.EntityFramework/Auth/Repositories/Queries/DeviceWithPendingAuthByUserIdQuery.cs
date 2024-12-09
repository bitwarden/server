using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Models.Api.Response;
using Bit.Infrastructure.EntityFramework.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Bit.Infrastructure.EntityFramework.Auth.Repositories.Queries;

public class DeviceWithPendingAuthByUserIdQuery
{
    public IQueryable<DeviceAuthRequestResponseModel> GetQuery(
        DatabaseContext dbContext,
        Guid userId,
        int expirationMinutes)
    {
        // Handle sqlite differently because it cannot convert some linq queries into sql syntax.
        // This error is thrown when trying to run the join else clause below with sqlite:
        // 'Translating this query requires the SQL APPLY operation, which is not supported on SQLite.'
        if (dbContext.Database.IsSqlite())
        {
            var query =
                (from device in dbContext.Devices
                 where device.UserId == userId && device.Active
                 select new
                 {
                     device,
                     authRequest =
                         (from authRequest in dbContext.AuthRequests
                          where authRequest.RequestDeviceIdentifier == device.Identifier
                          where authRequest.Type == AuthRequestType.AuthenticateAndUnlock ||
                                   authRequest.Type == AuthRequestType.Unlock
                          where authRequest.Approved == null
                          where authRequest.UserId == userId
                          where authRequest.CreationDate.AddMinutes(expirationMinutes) > DateTime.UtcNow
                          orderby authRequest.CreationDate descending
                          select authRequest).First()
                 }
                ).Select(deviceWithAuthRequest => new DeviceAuthRequestResponseModel(
                    deviceWithAuthRequest.device,
                    deviceWithAuthRequest.authRequest != null ? deviceWithAuthRequest.authRequest.Id : Guid.Empty,
                    deviceWithAuthRequest.authRequest != null
                        ? deviceWithAuthRequest.authRequest.CreationDate
                        : DateTime.MinValue));

            return query;
        }
        else
        {
            var query =
                from device in dbContext.Devices
                join authRequest in dbContext.AuthRequests
                    on device.Identifier equals authRequest.RequestDeviceIdentifier
                    into deviceRequests
                from authRequest in deviceRequests
                    .Where(ar => ar.Type == AuthRequestType.AuthenticateAndUnlock || ar.Type == AuthRequestType.Unlock)
                    .Where(ar => ar.Approved == null)
                    .Where(ar => ar.CreationDate.AddMinutes(expirationMinutes) > DateTime.UtcNow)
                    .OrderByDescending(ar => ar.CreationDate)
                    .Take(1)
                where device.UserId == userId && device.Active == true
                select new
                {
                    Device = device,
                    AuthRequestId = authRequest.Id,
                    AuthRequestCreationDate = authRequest.CreationDate
                };

            var devicesWithAuthQuery = query.Select(deviceAndAuthProperty => new DeviceAuthRequestResponseModel(
                deviceAndAuthProperty.Device, deviceAndAuthProperty.AuthRequestId, deviceAndAuthProperty.AuthRequestCreationDate));

            return devicesWithAuthQuery;
        }
    }
}
