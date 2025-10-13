using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

public class ClientOrganizationMigrationRecord : Core.Billing.Providers.Entities.ClientOrganizationMigrationRecord
{

}

public class ClientOrganizationMigrationRecordProfile : Profile
{
    public ClientOrganizationMigrationRecordProfile()
    {
        CreateMap<Core.Billing.Providers.Entities.ClientOrganizationMigrationRecord, ClientOrganizationMigrationRecord>().ReverseMap();
    }
}
