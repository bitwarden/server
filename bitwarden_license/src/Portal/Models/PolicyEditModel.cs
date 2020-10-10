using System;
using System.Collections.Generic;
using System.Text.Json;
using Bit.Core.Enums;
using Bit.Core.Models.Table;
using Bit.Core.Services;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bit.Portal.Models
{
    public class PolicyEditModel : PolicyModel
    {
        public PolicyEditModel() { }

        public PolicyEditModel(PolicyType type, II18nService i18nService)
            : base(type, false)
        {
            // Inject service and create static lists
            BuildLists(i18nService);
        }

        public PolicyEditModel(Policy model, II18nService i18nService)
            : base(model)
        {
            if (model == null)
            {
                return;
            }

            // Inject service and create static lists
            BuildLists(i18nService);

            if (model.Data != null)
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                };

                switch (model.Type)
                {
                    case PolicyType.MasterPassword:
                        MasterPasswordDataModel = JsonSerializer.Deserialize<MasterPasswordDataModel>(model.Data, options);
                        break;
                    case PolicyType.PasswordGenerator:
                        PasswordGeneratorDataModel = JsonSerializer.Deserialize<PasswordGeneratorDataModel>(model.Data, options);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public MasterPasswordDataModel MasterPasswordDataModel { get; set; }
        public PasswordGeneratorDataModel PasswordGeneratorDataModel { get; set; }
        public List<SelectListItem> Complexities { get; set; }
        public List<SelectListItem> DefaultTypes { get; set; }

        public Policy ToPolicy(PolicyType type, Guid organizationId)
        {
            return ToPolicy(new Policy
            {
                Type = type,
                OrganizationId = organizationId
            });
        }

        public Policy ToPolicy(Policy existingPolicy)
        {
            existingPolicy.Enabled = Enabled;

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };
            switch (existingPolicy.Type)
            {
                case PolicyType.MasterPassword:
                    existingPolicy.Data = JsonSerializer.Serialize(MasterPasswordDataModel, options);
                    break;
                case PolicyType.PasswordGenerator:
                    existingPolicy.Data = JsonSerializer.Serialize(PasswordGeneratorDataModel, options);
                    break;
                case PolicyType.OnlyOrg: case PolicyType.TwoFactorAuthentication:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return existingPolicy;
        }

        public void BuildLists(II18nService i18nService)
        {
            Complexities = new List<SelectListItem>
            {
                new SelectListItem { Value = null, Text = "--" + i18nService.T("Select") + "--"},
                new SelectListItem { Value = "0", Text = i18nService.T("Weak") + " (0)" },
                new SelectListItem { Value = "1", Text = i18nService.T("Weak") + " (1)" },
                new SelectListItem { Value = "2", Text = i18nService.T("Weak") + " (2)" },
                new SelectListItem { Value = "3", Text = i18nService.T("Good") + " (3)" },
                new SelectListItem { Value = "4", Text = i18nService.T("Strong") + " (4)" },
            };
            DefaultTypes = new List<SelectListItem>
            {
                new SelectListItem { Value = null, Text = i18nService.T("UserPreference") },
                new SelectListItem { Value = "password", Text = i18nService.T("Password") },
                new SelectListItem { Value = "passphrase", Text = i18nService.T("Passphrase") },
            };
        }
    }
}
