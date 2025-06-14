
using Bit.Core.Auth.Entities;
using Bit.Core.Auth.Enums;
using Bit.Core.Enums;

namespace Bit.Core.Auth.Models.Data;

public class PendingAuthRequestDetails : AuthRequest
{
    public Guid? DeviceId { get; set; }

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
        DeviceId = deviceId;
    }

    /**
     * Constructor for dapper response.
     * Note: if the DeviceId is null it comes back as an empty guid That could change if the stored
     * procedure runs on a different kind of db.
     * In order to maintain the flexibility of the wildcard * in SQL the constrctor accepts a long "row number rn"
     * parameter that was used to order the results in the SQL query. Also SQL complains about the constructor not
     * having the same parameters as the SELECT statement.
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
        OrganizationId = organizationId;
        Type = (AuthRequestType)type;
        RequestDeviceIdentifier = requestDeviceIdentifier;
        RequestDeviceType = (DeviceType)requestDeviceType;
        RequestIpAddress = requestIpAddress;
        RequestCountryName = requestCountryName;
        ResponseDeviceId = responseDeviceId;
        AccessCode = accessCode;
        PublicKey = publicKey;
        Key = key;
        MasterPasswordHash = masterPasswordHash;
        Approved = approved;
        CreationDate = creationDate;
        ResponseDate = responseDate;
        AuthenticationDate = authenticationDate;
        DeviceId = deviceId;
    }
}
