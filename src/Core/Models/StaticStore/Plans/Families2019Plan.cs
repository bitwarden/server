﻿using Bit.Core.Enums;

namespace Bit.Core.Models.StaticStore.Plans;

public record Families2019Plan : Models.StaticStore.Plan
{
    public Families2019Plan()
    {
        Type = PlanType.FamiliesAnnually2019;
        Product = ProductType.Families;
        Name = "Families 2019";
        IsAnnual = true;
        NameLocalizationKey = "planNameFamilies";
        DescriptionLocalizationKey = "planDescFamilies";

        TrialPeriodDays = 7;

        HasSelfHost = true;
        HasTotp = true;
        UsersGetPremium = true;

        UpgradeSortOrder = 1;
        DisplaySortOrder = 1;
        LegacyYear = 2020;

        PasswordManager = new Families2019PasswordManagerFeatures();
    }

    private record Families2019PasswordManagerFeatures : PasswordManagerPlanFeatures
    {
        public Families2019PasswordManagerFeatures()
        {
            BaseSeats = 6;
            BaseStorageGb = 1;
            MaxSeats = 6;

            HasAdditionalStorageOption = true;
            HasPremiumAccessOption = true;

            StripePlanId = "personal-org-annually";
            StripeStoragePlanId = "storage-gb-annually";
            StripePremiumAccessPlanId = "personal-org-premium-access-annually";
            BasePrice = 12;
            AdditionalStoragePricePerGb = 4;
            PremiumAccessOptionPrice = 40;

            AllowSeatAutoscale = false;
        }
    }
}
