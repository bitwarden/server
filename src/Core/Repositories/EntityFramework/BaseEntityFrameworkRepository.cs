using AutoMapper;

namespace Bit.Core.Repositories.EntityFramework
{
    public abstract class BaseEntityFrameworkRepository
    {
        public BaseEntityFrameworkRepository(DatabaseContext databaseContext, IMapper mapper)
        {
            DatabaseContext = databaseContext;
            Mapper = mapper;
        }

        protected DatabaseContext DatabaseContext { get; private set; }
        protected IMapper Mapper { get; private set; }
    }
}
