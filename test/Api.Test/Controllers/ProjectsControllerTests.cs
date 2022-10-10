using Bit.Api.Controllers;
using Bit.Core.SecretManagerFeatures.Projects.Interfaces;
using Bit.Test.Common.AutoFixture;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.Controllers;

[ControllerCustomize(typeof(ProjectsController))]
[SutProviderCustomize]
[JsonDocumentCustomize]
public class ProjectsControllerTests
{
    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_Success(SutProvider<ProjectsController> sutProvider, List<Guid> data)
    {
        var mockResult = new List<Tuple<Guid, string>>();
        foreach (var id in data)
        {
            mockResult.Add(new Tuple<Guid, string>(id, ""));
        }
        sutProvider.GetDependency<IDeleteProjectCommand>().DeleteProjects(data).ReturnsForAnyArgs(mockResult);

        var results = await sutProvider.Sut.BulkDeleteProjectsAsync(data);
        await sutProvider.GetDependency<IDeleteProjectCommand>().Received(1)
                     .DeleteProjects(Arg.Is(data));
        Assert.Equal(data.Count, results.Data.Count());
    }

    [Theory]
    [BitAutoData]
    public async void BulkDeleteProjects_NoGuids_ThrowsArgumentNullException(SutProvider<ProjectsController> sutProvider)
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => sutProvider.Sut.BulkDeleteProjectsAsync(new List<Guid>()));
    }
}
