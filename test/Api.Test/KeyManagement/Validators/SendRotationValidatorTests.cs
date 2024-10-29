using System.Text.Json;
using Bit.Api.KeyManagement.Validators;
using Bit.Api.Tools.Models;
using Bit.Api.Tools.Models.Request;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.Services;
using Bit.Core.Utilities;
using Bit.Test.Common.AutoFixture.Attributes;
using NSubstitute;
using Xunit;

namespace Bit.Api.Test.KeyManagement.Validators;

[SutProviderCustomize]
public class SendRotationValidatorTests
{
    [Fact]
    public async Task ValidateAsync_Success()
    {
        // Arrange
        var sendService = Substitute.For<ISendService>();
        var sendRepository = Substitute.For<ISendRepository>();

        var sut = new SendRotationValidator(
            sendService,
            sendRepository
        );

        var user = new User { Id = new Guid() };
        var sends = CreateInputSendRequests();

        sendRepository.GetManyByUserIdAsync(user.Id).Returns(MockUserSends(user));

        // Act
        var result = await sut.ValidateAsync(user, sends);

        // Assert
        var sendIds = new Guid[]
        {
            new("72e9ac6d-05f4-4227-ae0d-8a5207623a1a"), new("6b55836c-9280-4589-8762-01b0d8172c97"),
            new("9a65bbfb-8138-4aa5-a572-e5c0a41b540e"),
        };
        Assert.All(result, c => Assert.Contains(c.Id, sendIds));
    }

    [Fact]
    public async Task ValidateAsync_SendNotReturnedFromRepository_NotIncludedInOutput()
    {
        // Arrange
        var sendService = Substitute.For<ISendService>();
        var sendRepository = Substitute.For<ISendRepository>();

        var sut = new SendRotationValidator(
            sendService,
            sendRepository
        );

        var user = new User { Id = new Guid() };
        var sends = CreateInputSendRequests();

        var userSends = MockUserSends(user);
        userSends.RemoveAll(c => c.Id == new Guid("72e9ac6d-05f4-4227-ae0d-8a5207623a1a"));
        sendRepository.GetManyByUserIdAsync(user.Id).Returns(userSends);

        var result = await sut.ValidateAsync(user, sends);

        Assert.DoesNotContain(result, c => c.Id == new Guid("72e9ac6d-05f4-4227-ae0d-8a5207623a1a"));
    }

    [Fact]
    public async Task ValidateAsync_InputMissingUserSend_Throws()
    {
        // Arrange
        var sendService = Substitute.For<ISendService>();
        var sendRepository = Substitute.For<ISendRepository>();

        var sut = new SendRotationValidator(
            sendService,
            sendRepository
        );

        var user = new User { Id = new Guid() };
        var sends = CreateInputSendRequests();

        var userSends = MockUserSends(user);
        userSends.Add(new Send { Id = new Guid(), Data = "{}" });
        sendRepository.GetManyByUserIdAsync(user.Id).Returns(userSends);

        // Act, Assert
        await Assert.ThrowsAsync<BadRequestException>(async () =>
            await sut.ValidateAsync(user, sends));
    }

    private IEnumerable<SendWithIdRequestModel> CreateInputSendRequests()
    {
        return new[]
        {
            new SendWithIdRequestModel
            {
                DeletionDate = new DateTime(2080, 12, 31),
                Disabled = false,
                Id = new Guid("72e9ac6d-05f4-4227-ae0d-8a5207623a1a"),
                Key = "Send1Key",
                Name = "Send 1",
                Type = SendType.Text,
                Text = new SendTextModel(new SendTextData("Text name", "Notes", "Encrypted text for Send 1", false))
            },
            new SendWithIdRequestModel
            {
                DeletionDate = new DateTime(2080, 12, 31),
                Disabled = true,
                Id = new Guid("6b55836c-9280-4589-8762-01b0d8172c97"),
                Key = "Send2Key",
                Name = "Send 2",
                Type = SendType.Text,
                Text = new SendTextModel(new SendTextData("Text name", "Notes", "Encrypted text for Send 2",
                    false)),
            },
            new SendWithIdRequestModel
            {
                DeletionDate = new DateTime(2080, 12, 31),
                Disabled = false,
                Id = new Guid("9a65bbfb-8138-4aa5-a572-e5c0a41b540e"),
                Key = "Send3Key",
                Name = "Send 3",
                Type = SendType.File,
                File = new SendFileModel(new SendFileData("File name", "Notes", "File name here")),
                HideEmail = true
            }
        };
    }

    private List<Send> MockUserSends(User user)
    {
        return new List<Send>(new[]
        {
            new Send
            {
                DeletionDate = new DateTime(2080, 12, 31),
                Disabled = false,
                Id = new Guid("72e9ac6d-05f4-4227-ae0d-8a5207623a1a"),
                UserId = user.Id,
                Key = "Send1Key",
                Type = SendType.Text,
                Data = JsonSerializer.Serialize(
                    new SendTextModel(new SendTextData("Text name", "Notes", "Encrypted text for Send 1", false)),
                    JsonHelpers.IgnoreWritingNull),
            },
            new Send
            {
                DeletionDate = new DateTime(2080, 12, 31),
                Disabled = true,
                Id = new Guid("6b55836c-9280-4589-8762-01b0d8172c97"),
                UserId = user.Id,
                Key = "Send2Key",
                Type = SendType.Text,
                Data = JsonSerializer.Serialize(
                    new SendTextModel(new SendTextData("Text name", "Notes", "Encrypted text for Send 2",
                        false)),
                    JsonHelpers.IgnoreWritingNull),
            },
            new Send
            {
                DeletionDate = new DateTime(2080, 12, 31),
                Disabled = false,
                Id = new Guid("9a65bbfb-8138-4aa5-a572-e5c0a41b540e"),
                UserId = user.Id,
                Key = "Send3Key",
                Type = SendType.File,
                Data = JsonSerializer.Serialize(
                    new SendFileModel(new SendFileData("File name", "Notes", "File name here")),
                    JsonHelpers.IgnoreWritingNull),
                HideEmail = true
            }
        });
    }


}
