using Bit.Core.Repositories.EntityFramework;
using Bit.Core.Test.AutoFixture;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.EventFixtures;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Bit.Core.Models.Table;
using EfRepo = Bit.Core.Repositories.EntityFramework;
using SqlRepo = Bit.Core.Repositories.SqlServer;
using System.Collections.Generic;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using System.Linq;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class EventRepositoryTests
    {
        [CiSkippedTheory, EfEventAutoData]
        public async void CreateAsync_Works_DataMatches(Event user, EventCompare equalityComparer,
            List<EfRepo.EventRepository> suts, SqlRepo.EventRepository sqlEventRepo)
        {
            var savedEvents = new List<Event>();
            foreach (var sut in suts)
            {

                var postEfEvent = await sut.CreateAsync(user) as Event;
                var savedEvent = await sut.GetByIdAsync(postEfEvent.Id);
                savedEvents.Add(savedEvent);
            }

            var sqlEvent = await sqlEventRepo.CreateAsync(user) as Event;
            savedEvents.Add(await sqlEventRepo.GetByIdAsync(sqlEvent.Id));

            var distinctItems = savedEvents.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }
    }
}
