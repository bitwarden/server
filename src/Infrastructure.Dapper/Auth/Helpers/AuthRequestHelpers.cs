using System.Data;
using Bit.Core.Auth.Entities;

namespace Bit.Infrastructure.Dapper.Auth.Helpers;

public static class AuthRequestHelpers
{
    public static DataTable ToDataTable(this IEnumerable<AuthRequest> authRequests)
    {
        var table = new DataTable();

        var columnData = new List<(string name, Type type, Func<AuthRequest, object> getter)>
        {
            (nameof(AuthRequest.Id), typeof(Guid), ar => ar.Id),
            (nameof(AuthRequest.UserId), typeof(Guid), ar => ar.UserId),
            (nameof(AuthRequest.Type), typeof(byte), ar => ar.Type),
            (nameof(AuthRequest.RequestDeviceIdentifier), typeof(string), ar => ar.RequestDeviceIdentifier),
            (nameof(AuthRequest.RequestDeviceType), typeof(byte), ar => ar.RequestDeviceType),
            (nameof(AuthRequest.RequestIpAddress), typeof(string), ar => ar.RequestIpAddress),
            (nameof(AuthRequest.ResponseDeviceId), typeof(Guid), ar => ar.ResponseDeviceId),
            (nameof(AuthRequest.AccessCode), typeof(string), ar => ar.AccessCode),
            (nameof(AuthRequest.PublicKey), typeof(string), ar => ar.PublicKey),
            (nameof(AuthRequest.Key), typeof(string), ar => ar.Key),
            (nameof(AuthRequest.MasterPasswordHash), typeof(string), ar => ar.MasterPasswordHash),
            (nameof(AuthRequest.Approved), typeof(bool), ar => ar.Approved),
            (nameof(AuthRequest.CreationDate), typeof(DateTime), ar => ar.CreationDate),
            (nameof(AuthRequest.ResponseDate), typeof(DateTime), ar => ar.ResponseDate),
            (nameof(AuthRequest.AuthenticationDate), typeof(DateTime), ar => ar.AuthenticationDate),
            (nameof(AuthRequest.OrganizationId), typeof(Guid), ar => ar.OrganizationId),
        };

        return authRequests.BuildTable(table, columnData);
    }
}

