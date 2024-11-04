using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class PasswordHealthReportApplication : Core.Tools.Entities.PasswordHealthReportApplication
{
    public virtual Organization Organization { get; set; }
}

public class PasswordHealthReportApplicationProfile : Profile
{
    public PasswordHealthReportApplicationProfile()
    {
        CreateMap<Core.Tools.Entities.PasswordHealthReportApplication, PasswordHealthReportApplication>()
            .ReverseMap();
    }
}
