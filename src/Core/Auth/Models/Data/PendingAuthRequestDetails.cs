
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
     */
    public PendingAuthRequestDetails(
        Guid id,
        Guid userId,
        Guid organizationId,
        short type,
        string requestDeviceIdentifier,
        short requestDeviceType,
        string requestIpAddress,
        string requestCountryName,
        Guid? responseDeviceId,
        string accessCode,
        string publicKey,
        string key,
        string masterPasswordHash,
        bool? approved,
        DateTime creationDate,
        DateTime? responseDate,
        DateTime? authenticationDate,
        Guid deviceId)
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
        RequestDeviceId = deviceId;
    }
}
