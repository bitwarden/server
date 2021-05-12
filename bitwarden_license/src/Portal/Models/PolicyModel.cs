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
                case PolicyType.SingleOrg:
                    NameKey = "SingleOrganization";
                    DescriptionKey = "SingleOrganizationDescription";
                    break;
                case PolicyType.RequireSso:
                    NameKey = "RequireSso";
                    DescriptionKey = "RequireSsoDescription";
                    break;
                case PolicyType.PersonalOwnership:
                    NameKey = "PersonalOwnership";
                    DescriptionKey = "PersonalOwnershipDescription";
                    break;
                case PolicyType.DisableSend:
                    NameKey = "DisableSend";
                    DescriptionKey = "DisableSendDescription";
                    break;
                case PolicyType.SendOptions:
                    NameKey = "SendOptions";
                    DescriptionKey = "SendOptionsDescription";
                    break;
                case PolicyType.ResetPassword:
                    NameKey = "ResetPassword";
                    DescriptionKey = "ResetPasswordDescription";
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
        public bool Enabled { get; set; }
    }
}
