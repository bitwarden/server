#nullable enable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class SubscriptionDiscount : Core.Billing.Subscriptions.Entities.SubscriptionDiscount
{
}

public class SubscriptionDiscountMapperProfile : Profile
{
    public SubscriptionDiscountMapperProfile()
    {
        CreateMap<Core.Billing.Subscriptions.Entities.SubscriptionDiscount, SubscriptionDiscount>().ReverseMap();
    }
}
