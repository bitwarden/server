

using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.AdminConsole.Models;

public class PasswordHealthReportApplication : Core.AdminConsole.Entities.PasswordHealthReportApplication
{
    public virtual Organization Organization { get; set; }
}

public class PasswordHealthReportApplicationProfile : Profile
{
    public PasswordHealthReportApplicationProfile()
    {
        CreateMap<Core.AdminConsole.Entities.PasswordHealthReportApplication, PasswordHealthReportApplication>()
            .ReverseMap();
    }
}
