using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Enums;
using Bit.Core.Tools.Models.Data;
using Bit.Core.Tools.Repositories;
using Bit.Core.Tools.SendFeatures.Queries;
using NSubstitute;
using Xunit;

namespace Bit.Core.Test.Tools.Services;

public class SendAuthenticationQueryTests
{
    private readonly ISendRepository _sendRepository;
    private readonly SendAuthenticationQuery _sendAuthenticationQuery;

    public SendAuthenticationQueryTests()
    {
        _sendRepository = Substitute.For<ISendRepository>();
        _sendAuthenticationQuery = new SendAuthenticationQuery(_sendRepository);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() => new SendAuthenticationQuery(null));
        Assert.Equal("sendRepository", exception.ParamName);
    }

    [Theory]
    [MemberData(nameof(AuthenticationMethodTestCases))]
    public async Task GetAuthenticationMethod_ReturnsExpectedAuthenticationMethod(Send? send, Type expectedType)
    {
        // Arrange
        var sendId = Guid.NewGuid();
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType(expectedType, result);
    }

    [Theory]
    [MemberData(nameof(EmailsParsingTestCases))]
    public async Task GetAuthenticationMethod_WithEmails_ParsesEmailsCorrectly(string emailString, string[] expectedEmails)
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = CreateSend(accessCount: 0, maxAccessCount: 10, emails: emailString, password: null, AuthType.Email);
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        var emailOtp = Assert.IsType<EmailOtp>(result);
        Assert.Equal(expectedEmails, emailOtp.emails);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithBothEmailsAndPassword_ReturnsEmailOtp()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = CreateSend(accessCount: 0, maxAccessCount: 10, emails: "person@company.com", password: "hashedpassword", AuthType.Email);
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<EmailOtp>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_CallsRepositoryWithCorrectSendId()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = CreateSend(accessCount: 0, maxAccessCount: 10, emails: null, password: null, AuthType.None);
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        await _sendRepository.Received(1).GetByIdAsync(sendId);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WhenRepositoryThrows_PropagatesException()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var expectedException = new InvalidOperationException("Repository error");
        _sendRepository.GetByIdAsync(sendId).Returns(Task.FromException<Send?>(expectedException));

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _sendAuthenticationQuery.GetAuthenticationMethod(sendId));
        Assert.Same(expectedException, exception);
    }

    public static IEnumerable<object[]> AuthenticationMethodTestCases()
    {
        yield return new object[] { null, typeof(NeverAuthenticate) };
        yield return new object[] { CreateSend(accessCount: 5, maxAccessCount: 5, emails: null, password: null, AuthType.None), typeof(NeverAuthenticate) };
        yield return new object[] { CreateSend(accessCount: 6, maxAccessCount: 5, emails: null, password: null, AuthType.None), typeof(NeverAuthenticate) };
        yield return new object[] { CreateSend(accessCount: 0, maxAccessCount: 10, emails: "person@company.com", password: null, AuthType.Email), typeof(EmailOtp) };
        yield return new object[] { CreateSend(accessCount: 0, maxAccessCount: 10, emails: null, password: "hashedpassword", AuthType.Password), typeof(ResourcePassword) };
        yield return new object[] { CreateSend(accessCount: 0, maxAccessCount: 10, emails: null, password: null, AuthType.None), typeof(NotAuthenticated) };
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithDisabledSend_ReturnsNeverAuthenticate()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            AccessCount = 0,
            MaxAccessCount = 10,
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = true,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<NeverAuthenticate>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithExpiredSend_ReturnsNeverAuthenticate()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            AccessCount = 0,
            MaxAccessCount = 10,
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = DateTime.UtcNow.AddDays(-1) // Expired yesterday
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<NeverAuthenticate>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithDeletionDatePassed_ReturnsNeverAuthenticate()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            AccessCount = 0,
            MaxAccessCount = 10,
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(-1), // Should have been deleted yesterday
            ExpirationDate = null
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<NeverAuthenticate>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithDeletionDateEqualToNow_ReturnsNeverAuthenticate()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var send = new Send
        {
            Id = sendId,
            AccessCount = 0,
            MaxAccessCount = 10,
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = false,
            DeletionDate = now, // DeletionDate <= DateTime.UtcNow
            ExpirationDate = null
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<NeverAuthenticate>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithAccessCountEqualToMaxAccessCount_ReturnsNeverAuthenticate()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            AccessCount = 5,
            MaxAccessCount = 5,
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<NeverAuthenticate>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithNullMaxAccessCount_DoesNotRestrictAccess()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            AccessCount = 1000,
            MaxAccessCount = null, // No limit
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<EmailOtp>(result);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithNullExpirationDate_DoesNotExpire()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = new Send
        {
            Id = sendId,
            AccessCount = 0,
            MaxAccessCount = 10,
            Emails = "person@company.com",
            Password = null,
            AuthType = AuthType.Email,
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null // No expiration
        };
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        Assert.IsType<EmailOtp>(result);
    }

    public static IEnumerable<object[]> EmailsParsingTestCases()
    {
        yield return new object[] { "person@company.com", new[] { "person@company.com" } };
        yield return new object[] { "person1@company.com,person2@company.com", new[] { "person1@company.com", "person2@company.com" } };
        yield return new object[] { " person1@company.com , person2@company.com ", new[] { "person1@company.com", "person2@company.com" } };
        yield return new object[] { "person1@company.com,,person2@company.com", new[] { "person1@company.com", "person2@company.com" } };
        yield return new object[] { " , person1@company.com,  ,person2@company.com, ", new[] { "person1@company.com", "person2@company.com" } };
    }

    private static Send CreateSend(int accessCount, int? maxAccessCount, string? emails, string? password, AuthType? authType)
    {
        return new Send
        {
            Id = Guid.NewGuid(),
            AccessCount = accessCount,
            MaxAccessCount = maxAccessCount,
            Emails = emails,
            Password = password,
            AuthType = authType,
            Disabled = false,
            DeletionDate = DateTime.UtcNow.AddDays(7),
            ExpirationDate = null
        };
    }
}
