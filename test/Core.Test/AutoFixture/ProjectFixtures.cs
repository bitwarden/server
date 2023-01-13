using AutoFixture;
using Bit.Core.Entities;
using Bit.Test.Common.AutoFixture.Attributes;

namespace Bit.Core.Test.AutoFixture.ProjectsFixture;

public class ProjectCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        var projectId = Guid.NewGuid();

        fixture.Customize<Project>(composer => composer
            .With(p => p.Id, projectId)
            .Without(s => s.Secrets));
    }
}

public class ProjectCustomizeAttribute : BitCustomizeAttribute
{
    public override ICustomization GetCustomization() => new ProjectCustomization();
}
