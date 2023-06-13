using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Bit.Core.Auth.Models.Data;
using Bit.Core.Models.Api;

namespace Bit.Api.Auth.Models.Response;

public class PendingOrganizationAuthRequestResponseModel : ResponseModel
{
    public PendingOrganizationAuthRequestResponseModel(OrganizationAdminAuthRequest authRequest, string obj = "pending-org-auth-request") : base(obj)
    {
        if (authRequest == null)
        {
            throw new ArgumentNullException(nameof(authRequest));
        }

        Id = authRequest.Id;
        UserId = authRequest.UserId;
        OrganizationUserId = authRequest.OrganizationUserId;
        Email = authRequest.Email;
        PublicKey = authRequest.PublicKey;
        RequestDeviceIdentifier = authRequest.RequestDeviceIdentifier;
        RequestDeviceType = authRequest.RequestDeviceType.GetType().GetMember(authRequest.RequestDeviceType.ToString())
            .FirstOrDefault()?.GetCustomAttribute<DisplayAttribute>()?.GetName();
        RequestIpAddress = authRequest.RequestIpAddress;
        CreationDate = authRequest.CreationDate;
    }

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid OrganizationUserId { get; set; }
    public string Email { get; set; }
    public string PublicKey { get; set; }
    public string RequestDeviceIdentifier { get; set; }
    public string RequestDeviceType { get; set; }
    public string RequestIpAddress { get; set; }
    public DateTime CreationDate { get; set; }
}
