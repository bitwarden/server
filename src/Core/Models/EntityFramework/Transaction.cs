using System.Collections.Generic;
using System.Text.Json;
using AutoMapper;

namespace Bit.Core.Models.EntityFramework
{
    public class Transaction : Table.Transaction
    {
    }

    public class TransactionMapperProfile : Profile
    {
        public TransactionMapperProfile()
        {
            CreateMap<Table.Transaction, Transaction>().ReverseMap();
        }
    }
}
