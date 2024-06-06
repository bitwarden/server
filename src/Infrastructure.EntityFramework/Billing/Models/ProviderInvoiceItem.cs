using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class ProviderInvoiceItem : Core.Billing.Entities.ProviderInvoiceItem
{
    public virtual Provider Provider { get; set; }
}

public class ProviderInvoiceItemMapperProfile : Profile
{
    public ProviderInvoiceItemMapperProfile()
    {
        CreateMap<Core.Billing.Entities.ProviderInvoiceItem, ProviderInvoiceItem>().ReverseMap();
    }
}
