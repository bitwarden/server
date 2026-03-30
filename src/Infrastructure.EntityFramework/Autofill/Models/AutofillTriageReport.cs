#nullable enable

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Autofill.Models;

// ReSharper disable once ClassWithVirtualMembersNeverInherited.Global
public class AutofillTriageReport : Core.Autofill.Entities.AutofillTriageReport
{
}

public class AutofillTriageReportMapperProfile : Profile
{
    public AutofillTriageReportMapperProfile()
    {
        CreateMap<Core.Autofill.Entities.AutofillTriageReport, AutofillTriageReport>().ReverseMap();
    }
}
