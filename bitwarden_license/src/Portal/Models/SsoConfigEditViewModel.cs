using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json;
using Bit.Core;
using Bit.Core.Enums;
using Bit.Core.Models.Data;
using Bit.Core.Models.Table;
using Bit.Core.Services;
using Bit.Core.Sso;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Bit.Portal.Models
{
    public class SsoConfigEditViewModel
    {
        public SsoConfigEditViewModel() { }

        public SsoConfigEditViewModel(SsoConfig ssoConfig, II18nService i18nService,
            GlobalSettings globalSettings)
        {
            if (ssoConfig != null)
            {
                Id = ssoConfig.Id;
                Enabled = ssoConfig.Enabled;
            }

            SsoConfigurationData configurationData;
            if (!string.IsNullOrWhiteSpace(ssoConfig?.Data))
            {
                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                };
                configurationData = JsonSerializer.Deserialize<SsoConfigurationData>(ssoConfig.Data, options);
            }
            else
            {
                configurationData = new SsoConfigurationData();
            }

            Data = new SsoConfigDataViewModel(configurationData, globalSettings);
            BuildLists(i18nService);
        }

        public long Id { get; set; }
        [Display(Name = "Enabled")]
        public bool Enabled { get; set; }
        public SsoConfigDataViewModel Data { get; set; }

        public List<SelectListItem> ConfigTypes { get; set; }
        public List<SelectListItem> SpNameIdFormats { get; set; }
        public List<SelectListItem> BindingTypes { get; set; }
        public List<SelectListItem> SigningBehaviors { get; set; }
        public List<SelectListItem> SigningAlgorithms { get; set; }

        public SsoConfig ToSsoConfig(Guid organizationId)
        {
            return ToSsoConfig(new SsoConfig { OrganizationId = organizationId });
        }

        public SsoConfig ToSsoConfig(SsoConfig existingConfig)
        {
            existingConfig.Enabled = Enabled;
            var configurationData = Data.ToConfigurationData();
            existingConfig.Data = JsonSerializer.Serialize(configurationData, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            return existingConfig;
        }

        public void BuildLists(II18nService i18nService)
        {
            ConfigTypes = Enum.GetNames(typeof(SsoType))
                .Select(configType => new SelectListItem
                {
                    Value = configType,
                    Text = i18nService.T(configType),
                }).ToList();

            SpNameIdFormats = Enum.GetNames(typeof(Saml2NameIdFormat))
                .Select(nameIdFormat => new SelectListItem
                {
                    Value = nameIdFormat,
                    Text = i18nService.T(nameIdFormat),
                }).ToList();

            BindingTypes = Enum.GetNames(typeof(Saml2BindingType))
                .Select(bindingType => new SelectListItem
                {
                    Value = bindingType,
                    Text = i18nService.T(bindingType),
                }).ToList();

            SigningBehaviors = Enum.GetNames(typeof(Saml2SigningBehavior))
                .Select(behavior => new SelectListItem
                {
                    Value = behavior,
                    Text = i18nService.T(behavior),
                }).ToList();

            SigningAlgorithms = SamlSigningAlgorithms.GetEnumerable().Select(a =>
                new SelectListItem(a, a)).ToList();
        }
    }
}
