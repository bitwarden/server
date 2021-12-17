using AutoMapper;

namespace Bit.Infrastructure.EntityFramework.Models
{
    public class Transaction : Core.Models.Table.Transaction
    {
        public virtual Organization Organization { get; set; }
        public virtual User User { get; set; }
    }

    public class TransactionMapperProfile : Profile
    {
        public TransactionMapperProfile()
        {
            CreateMap<Core.Models.Table.Transaction, Transaction>().ReverseMap();
        }
    }
}
