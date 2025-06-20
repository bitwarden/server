using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;

namespace Bit.Infrastructure.EntityFramework.Dirt.Models;

public class PasswordHealthReportApplication : Core.Dirt.Entities.PasswordHealthReportApplication
{
    public virtual Organization Organization { get; set; }
}

public class PasswordHealthReportApplicationProfile : Profile
{
    public PasswordHealthReportApplicationProfile()
    {
        CreateMap<Core.Dirt.Entities.PasswordHealthReportApplication, PasswordHealthReportApplication>()
            .ReverseMap();
    }
}
