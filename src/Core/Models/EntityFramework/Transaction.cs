using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Transaction : Table.Transaction
    {
        public virtual Organization Organization { get; set; }
        public virtual User User { get; set; }
    }

    public class TransactionMapperProfile : Profile
    {
        public TransactionMapperProfile()
        {
            CreateMap<Table.Transaction, Transaction>().ReverseMap();
        }
    }
}
