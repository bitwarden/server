// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ProviderInvoiceItem : Core.Billing.Providers.Entities.ProviderInvoiceItem
{
    public virtual Provider Provider { get; set; }
}

public class ProviderInvoiceItemMapperProfile : Profile
{
    public ProviderInvoiceItemMapperProfile()
    {
        CreateMap<Core.Billing.Providers.Entities.ProviderInvoiceItem, ProviderInvoiceItem>().ReverseMap();
    }
}
