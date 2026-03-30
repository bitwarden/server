using AutoFixture;
using Bit.Core.Autofill.Entities;
using Bit.Core.Test.AutoFixture.Attributes;
using Bit.Infrastructure.EFIntegration.Test.AutoFixture;
using Xunit;
using EfAutofillRepo = Bit.Infrastructure.EntityFramework.Autofill.Repositories;

namespace Bit.Infrastructure.EFIntegration.Test.Autofill.Repositories;

public class AutofillTriageReportRepositoryTests
{
    [CiSkippedTheory, EfAutofillTriageReportAutoData]
    public async Task CreateAsync_Works_DataMatches(
        AutofillTriageReport report,
        List<EfAutofillRepo.AutofillTriageReportRepository> suts)
    {
        var createdRecords = new List<AutofillTriageReport>();

        foreach (var sut in suts)
        {
            var created = await sut.CreateAsync(report);
            sut.ClearChangeTracking();

            var fetched = await sut.GetByIdAsync(created.Id);
            createdRecords.Add(fetched);
        }

        Assert.Equal(suts.Count, createdRecords.Count);
        Assert.All(createdRecords, r =>
        {
            Assert.NotEqual(Guid.Empty, r.Id);
            Assert.Equal(report.PageUrl, r.PageUrl);
            Assert.Equal(report.ReportData, r.ReportData);
            Assert.False(r.Archived);
        });
    }

    [CiSkippedTheory, EfAutofillTriageReportAutoData]
    public async Task GetActiveAsync_ReturnsOnlyUnarchivedReports(
        List<EfAutofillRepo.AutofillTriageReportRepository> suts)
    {
        var fixture = new Fixture();

        foreach (var sut in suts)
        {
            var active = fixture.Build<AutofillTriageReport>()
                .With(r => r.PageUrl, "https://example.com")
                .With(r => r.ReportData, "{}")
                .With(r => r.Archived, false)
                .Without(r => r.Id)
                .Create();

            var archived = fixture.Build<AutofillTriageReport>()
                .With(r => r.PageUrl, "https://example.com")
                .With(r => r.ReportData, "{}")
                .With(r => r.Archived, true)
                .Without(r => r.Id)
                .Create();

            await sut.CreateAsync(active);
            await sut.CreateAsync(archived);
            sut.ClearChangeTracking();

            var results = (await sut.GetActiveAsync(0, 100)).ToList();

            Assert.Contains(results, r => r.Id == active.Id);
            Assert.DoesNotContain(results, r => r.Id == archived.Id);
        }
    }

    [CiSkippedTheory, EfAutofillTriageReportAutoData]
    public async Task ArchiveAsync_SetsArchivedTrue(
        AutofillTriageReport report,
        List<EfAutofillRepo.AutofillTriageReportRepository> suts)
    {
        foreach (var sut in suts)
        {
            var created = await sut.CreateAsync(report);
            sut.ClearChangeTracking();

            await sut.ArchiveAsync(created.Id);
            sut.ClearChangeTracking();

            var fetched = await sut.GetByIdAsync(created.Id);
            Assert.True(fetched.Archived);
        }
    }

    [CiSkippedTheory, EfAutofillTriageReportAutoData]
    public async Task GetActiveAsync_RespectsSkipAndTake(
        List<EfAutofillRepo.AutofillTriageReportRepository> suts)
    {
        var fixture = new Fixture();

        foreach (var sut in suts)
        {
            for (var i = 0; i < 5; i++)
            {
                var report = fixture.Build<AutofillTriageReport>()
                    .With(r => r.PageUrl, "https://example.com")
                    .With(r => r.ReportData, "{}")
                    .With(r => r.Archived, false)
                    .Without(r => r.Id)
                    .Create();
                await sut.CreateAsync(report);
            }

            sut.ClearChangeTracking();

            var page1 = (await sut.GetActiveAsync(0, 2)).ToList();
            var page2 = (await sut.GetActiveAsync(2, 2)).ToList();

            Assert.Equal(2, page1.Count);
            Assert.Equal(2, page2.Count);
            Assert.Empty(page1.IntersectBy(page2.Select(r => r.Id), r => r.Id));
        }
    }
}
