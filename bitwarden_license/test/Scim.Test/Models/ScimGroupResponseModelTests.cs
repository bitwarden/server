using System.Text.Json;
using System.Text.Json.Serialization;
using Bit.Core.AdminConsole.Entities;
using Bit.Scim.Models;
using Xunit;

namespace Bit.Scim.Test.Models;

public class ScimGroupResponseModelTests
{
    [Fact]
    public void ScimGroupResponseModel_IncludesMembersProperty()
    {
        // Arrange
        var group = new Group { Id = Guid.NewGuid(), Name = "Test Group", ExternalId = null };
        var model = new ScimGroupResponseModel(group);

        // Act & Assert
        Assert.NotNull(model.Members);
        Assert.IsType<List<ScimGroupResponseModel.GroupMemberReference>>(model.Members);
        Assert.Empty(model.Members);
    }

    [Fact]
    public void ScimGroupResponseModel_MembersSerializedInJson()
    {
        // Arrange
        var group = new Group { Id = Guid.NewGuid(), Name = "Test Group", ExternalId = null };
        var model = new ScimGroupResponseModel(group);
        
        // Add a member
        model.Members.Add(new ScimGroupResponseModel.GroupMemberReference
        {
            Value = Guid.NewGuid().ToString(),
            Display = "Alice Smith",
            Type = "User"
        });

        // Act
        var options = new JsonSerializerOptions { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
        var json = JsonSerializer.Serialize(model, options);

        // Assert
        Assert.Contains("\"members\"", json);
        Assert.Contains("\"Alice Smith\"", json);
        Assert.Contains("\"User\"", json);
    }

    [Fact]
    public void ScimGroupResponseModel_ConstructorInitializesMembersAsEmptyList()
    {
        // Act
        var model = new ScimGroupResponseModel();

        // Assert
        Assert.NotNull(model.Members);
        Assert.Empty(model.Members);
    }

    [Fact]
    public void GroupMemberReference_HasRequiredProperties()
    {
        // Act
        var member = new ScimGroupResponseModel.GroupMemberReference
        {
            Value = Guid.NewGuid().ToString(),
            Display = "Bob Jones",
            Type = "User"
        };

        // Assert
        Assert.NotNull(member.Value);
        Assert.NotNull(member.Display);
        Assert.Equal("User", member.Type);
    }
}

