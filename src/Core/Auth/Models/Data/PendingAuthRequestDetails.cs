
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Enums;

namespace Bit.Core.Auth.Models.Data;

public class PendingAuthRequestDetails : AuthRequest
{
    public Guid? RequestDeviceId { get; set; }

    /**
     * Constructor for EF response.
     */
    public PendingAuthRequestDetails(
        AuthRequest authRequest,
        Guid? deviceId)
    {
        ArgumentNullException.ThrowIfNull(authRequest);

        Id = authRequest.Id;
        UserId = authRequest.UserId;
        OrganizationId = authRequest.OrganizationId;
        Type = authRequest.Type;
        RequestDeviceIdentifier = authRequest.RequestDeviceIdentifier;
        RequestDeviceType = authRequest.RequestDeviceType;
        RequestIpAddress = authRequest.RequestIpAddress;
        RequestCountryName = authRequest.RequestCountryName;
        ResponseDeviceId = authRequest.ResponseDeviceId;
        AccessCode = authRequest.AccessCode;
        PublicKey = authRequest.PublicKey;
        Key = authRequest.Key;
        MasterPasswordHash = authRequest.MasterPasswordHash;
        Approved = authRequest.Approved;
        CreationDate = authRequest.CreationDate;
        ResponseDate = authRequest.ResponseDate;
        AuthenticationDate = authRequest.AuthenticationDate;
        RequestDeviceId = deviceId;
    }

    /**
     * Constructor for dapper response.
     * Note: if the DeviceId is null it comes back as an empty guid That could change if the stored
     * procedure runs on a different database provider.
     * In order to maintain the flexibility of the wildcard (*) in SQL, the constructor accepts a"row number" rn of type long
     * parameter. 'rn' was used to order the results in the SQL query. Also, SQL complains about the constructor not
     * having the same parameters as the SELECT statement and since the SELECT uses the wildcard we need to include everything.
     * Order matters when mapping from the Stored Procedure, so the columns are in the order they come back from the query.
     */
    public PendingAuthRequestDetails(
        Guid id,
        Guid userId,
        short type,
        string requestDeviceIdentifier,
        short requestDeviceType,
        string requestIpAddress,
        Guid? responseDeviceId,
        string accessCode,
        string publicKey,
        string key,
        string masterPasswordHash,
        DateTime creationDate,
        DateTime? responseDate,
        DateTime? authenticationDate,
        bool? approved,
        Guid organizationId,
        string requestCountryName,
        Guid deviceId,
        long rn) // see comment above about rn parameter
    {
        Id = id;
        UserId = userId;
        Type = (AuthRequestType)type;
        RequestDeviceIdentifier = requestDeviceIdentifier;
        RequestDeviceType = (DeviceType)requestDeviceType;
        RequestIpAddress = requestIpAddress;
        ResponseDeviceId = responseDeviceId;
        AccessCode = accessCode;
        PublicKey = publicKey;
        Key = key;
        MasterPasswordHash = masterPasswordHash;
        Approved = approved;
        CreationDate = creationDate;
        ResponseDate = responseDate;
        AuthenticationDate = authenticationDate;
        OrganizationId = organizationId;
        RequestCountryName = requestCountryName;
        RequestDeviceId = deviceId;
    }
}
