using System.Collections.Generic;
using System.Linq;
using Bit.Core.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Core.Test.AutoFixture.TaxRateFixtures;
using Bit.Core.Test.Repositories.EntityFramework.EqualityComparers;
using Xunit;
using EfRepo = Bit.Infrastructure.EntityFramework.Repositories;
using SqlRepo = Bit.Infrastructure.Dapper.Repositories;

namespace Bit.Core.Test.Repositories.EntityFramework
{
    public class TaxRateRepositoryTests
    {
        [CiSkippedTheory, EfTaxRateAutoData]
        public async void CreateAsync_Works_DataMatches(
            TaxRate taxRate,
            TaxRateCompare equalityComparer,
            List<EfRepo.TaxRateRepository> suts,
            SqlRepo.TaxRateRepository sqlTaxRateRepo
            )
        {
            var savedTaxRates = new List<TaxRate>();
            foreach (var sut in suts)
            {
                var i = suts.IndexOf(sut);
                var postEfTaxRate = await sut.CreateAsync(taxRate);
                sut.ClearChangeTracking();

                var savedTaxRate = await sut.GetByIdAsync(postEfTaxRate.Id);
                savedTaxRates.Add(savedTaxRate);
            }

            var sqlTaxRate = await sqlTaxRateRepo.CreateAsync(taxRate);
            var savedSqlTaxRate = await sqlTaxRateRepo.GetByIdAsync(sqlTaxRate.Id);
            savedTaxRates.Add(savedSqlTaxRate);

            var distinctItems = savedTaxRates.Distinct(equalityComparer);
            Assert.True(!distinctItems.Skip(1).Any());
        }
    }
}
