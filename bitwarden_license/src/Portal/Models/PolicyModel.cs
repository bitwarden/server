using System;
using System.ComponentModel.DataAnnotations;
using Bit.Core.Enums;
using Bit.Core.Models.Table;

namespace Bit.Portal.Models
{
    public class PolicyModel
    {
        public PolicyModel() { }

        public PolicyModel(Policy policy)
            : this(policy.Type, policy.Enabled)
        { }

        public PolicyModel(PolicyType policyType, bool enabled)
        {
            switch (policyType)
            {
                case PolicyType.TwoFactorAuthentication:
                    NameKey = "TwoStepLogin";
                    DescriptionKey = "TwoStepLoginDescription";
                    break;

                case PolicyType.MasterPassword:
                    NameKey = "MasterPassword";
                    DescriptionKey = "MasterPasswordDescription";
                    break;

                case PolicyType.PasswordGenerator:
                    NameKey = "PasswordGenerator";
                    DescriptionKey = "PasswordGeneratorDescription";
                    break;

                case PolicyType.OnlyOrg:
                    NameKey = "OnlyOrg";
                    DescriptionKey = "OnlyOrganizationDescription";
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            PolicyType = policyType;
            Enabled = enabled;
        }

        public string NameKey { get; set; }
        public string DescriptionKey { get; set; }
        public PolicyType PolicyType { get; set; }
        [Display(Name = "Enabled")]
        public bool Enabled { get; set; }
    }
}
