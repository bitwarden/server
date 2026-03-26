#nullable enable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class AutofillTriageReport : Core.Entities.AutofillTriageReport
{
}

public class AutofillTriageReportMapperProfile : Profile
{
    public AutofillTriageReportMapperProfile()
    {
        CreateMap<Core.Entities.AutofillTriageReport, AutofillTriageReport>().ReverseMap();
    }
}
