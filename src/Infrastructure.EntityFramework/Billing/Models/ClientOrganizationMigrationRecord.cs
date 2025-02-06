using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Billing.Models;

public class ClientOrganizationMigrationRecord : Core.Billing.Entities.ClientOrganizationMigrationRecord
{

}

public class ClientOrganizationMigrationRecordProfile : Profile
{
    public ClientOrganizationMigrationRecordProfile()
    {
        CreateMap<Core.Billing.Entities.ClientOrganizationMigrationRecord, ClientOrganizationMigrationRecord>().ReverseMap();
    }
}
