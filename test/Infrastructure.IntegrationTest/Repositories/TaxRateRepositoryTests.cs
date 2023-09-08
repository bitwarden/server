using Bit.Core.Entities;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Infrastructure.IntegrationTest.Repositories;

public class TaxRateRepositoryTests
{
    [DatabaseTheory, DatabaseData]
    public async Task CreateAsync_Works(ITaxRateRepository taxRateRepository)
    {
        var id = Guid.NewGuid().ToString();
        var createdTaxRate = await taxRateRepository.CreateAsync(new TaxRate
        {
            Id = id,
            Country = "US",
            Active = true,
            PostalCode = "12345",
            Rate = 5.1m,
            State = "AL",
        });

        // Assert that we can find one from the database
        var taxRate = await taxRateRepository.GetByIdAsync(createdTaxRate.Id);
        Assert.NotNull(taxRate);

        // Assert the found item has all the data we expect
        Assert.Equal(id, taxRate.Id);
        Assert.Equal("US", taxRate.Country);
        Assert.True(taxRate.Active);
        Assert.Equal("12345", taxRate.PostalCode);
        Assert.Equal(5.1m, taxRate.Rate);
        Assert.Equal("AL", taxRate.State);

        // Assert the items returned from CreateAsync match what is found by id
        Assert.Equal(createdTaxRate.Id, taxRate.Id);
        Assert.Equal(createdTaxRate.Country, taxRate.Country);
        Assert.Equal(createdTaxRate.Active, taxRate.Active);
        Assert.Equal(createdTaxRate.PostalCode, taxRate.PostalCode);
        Assert.Equal(createdTaxRate.Rate, taxRate.Rate);
        Assert.Equal(createdTaxRate.State, taxRate.State);
    }
}
