using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models;

public class Transaction : Core.Entities.Transaction
{
    public virtual Organization Organization { get; set; }
    public virtual User User { get; set; }
}

public class TransactionMapperProfile : Profile
{
    public TransactionMapperProfile()
    {
        CreateMap<Core.Entities.Transaction, Transaction>().ReverseMap();
    }
}
