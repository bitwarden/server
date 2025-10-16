// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.Models;
using Bit.Core.Context;
using Bit.Core.Exceptions;
using Bit.Core.Utilities;

namespace Bit.Api.AdminConsole.Models.Request;

public class PolicyRequestModel
{
    [Required]
    public PolicyType? Type { get; set; }
    [Required]
    public bool? Enabled { get; set; }
    public Dictionary<string, object> Data { get; set; }

    public async Task<PolicyUpdate> ToPolicyUpdateAsync(Guid organizationId, ICurrentContext currentContext)
    {
        var serializedData = ValidateAndSerializePolicyData();
        var performedBy = new StandardUser(currentContext.UserId!.Value, await currentContext.OrganizationOwner(organizationId));

        return new()
        {
            Type = Type!.Value,
            OrganizationId = organizationId,
            Data = serializedData,
            Enabled = Enabled.GetValueOrDefault(),
            PerformedBy = performedBy
        };
    }

    private string ValidateAndSerializePolicyData()
    {
        if (Data == null)
        {
            return null;
        }

        try
        {
            var json = JsonSerializer.Serialize(Data);

            switch (Type!.Value)
            {
                case PolicyType.MasterPassword:
                    CoreHelpers.LoadClassFromJsonData<MasterPasswordPolicyData>(json);
                    break;
                case PolicyType.SendOptions:
                    CoreHelpers.LoadClassFromJsonData<SendOptionsPolicyData>(json);
                    break;
                case PolicyType.ResetPassword:
                    CoreHelpers.LoadClassFromJsonData<ResetPasswordDataModel>(json);
                    break;
            }

            return json;
        }
        catch
        {
            throw new BadRequestException($"Invalid data for {Type!.Value} policy.");
        }
    }
}
