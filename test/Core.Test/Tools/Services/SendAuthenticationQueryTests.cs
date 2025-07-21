using Bit.Core.Tools.Entities;
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
    [MemberData(nameof(EmailParsingTestCases))]
    public async Task GetAuthenticationMethod_WithEmails_ParsesEmailsCorrectly(string emailString, string[] expectedEmails)
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = CreateSend(accessCount: 0, maxAccessCount: 10, emails: emailString, password: null);
        _sendRepository.GetByIdAsync(sendId).Returns(send);

        // Act
        var result = await _sendAuthenticationQuery.GetAuthenticationMethod(sendId);

        // Assert
        var emailOtp = Assert.IsType<EmailOtp>(result);
        Assert.Equal(expectedEmails, emailOtp.Emails);
    }

    [Fact]
    public async Task GetAuthenticationMethod_WithBothEmailsAndPassword_ReturnsEmailOtp()
    {
        // Arrange
        var sendId = Guid.NewGuid();
        var send = CreateSend(accessCount: 0, maxAccessCount: 10, emails: "test@example.com", password: "hashedpassword");
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
        var send = CreateSend(accessCount: 0, maxAccessCount: 10, emails: null, password: null);
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
        yield return new object[] { CreateSend(accessCount: 5, maxAccessCount: 5, emails: null, password: null), typeof(NeverAuthenticate) };
        yield return new object[] { CreateSend(accessCount: 6, maxAccessCount: 5, emails: null, password: null), typeof(NeverAuthenticate) };
        yield return new object[] { CreateSend(accessCount: 0, maxAccessCount: 10, emails: "test@example.com", password: null), typeof(EmailOtp) };
        yield return new object[] { CreateSend(accessCount: 0, maxAccessCount: 10, emails: null, password: "hashedpassword"), typeof(ResourcePassword) };
        yield return new object[] { CreateSend(accessCount: 0, maxAccessCount: 10, emails: null, password: null), typeof(NotAuthenticated) };
    }

    public static IEnumerable<object[]> EmailParsingTestCases()
    {
        yield return new object[] { "test@example.com", new[] { "test@example.com" } };
        yield return new object[] { "test1@example.com,test2@example.com", new[] { "test1@example.com", "test2@example.com" } };
        yield return new object[] { " test@example.com , other@example.com ", new[] { "test@example.com", "other@example.com" } };
        yield return new object[] { "test@example.com,,other@example.com", new[] { "test@example.com", "other@example.com" } };
        yield return new object[] { " , test@example.com,  ,other@example.com, ", new[] { "test@example.com", "other@example.com" } };
    }

    private static Send CreateSend(int accessCount, int? maxAccessCount, string? emails, string? password)
    {
        return new Send
        {
            Id = Guid.NewGuid(),
            AccessCount = accessCount,
            MaxAccessCount = maxAccessCount,
            Emails = emails,
            Password = password
        };
    }
}
