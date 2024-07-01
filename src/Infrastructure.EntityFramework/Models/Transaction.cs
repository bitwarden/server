using AutoMapper;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models;
using Bit.Infrastructure.EntityFramework.AdminConsole.Models.Provider;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Transaction : Core.Entities.Transaction
{
    public virtual Organization Organization { get; set; }
    public virtual User User { get; set; }
    public virtual Provider Provider { get; set; }
}

public class TransactionMapperProfile : Profile
{
    public TransactionMapperProfile()
    {
        CreateMap<Core.Entities.Transaction, Transaction>().ReverseMap();
    }
}
