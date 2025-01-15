using System.Net;
using Bit.Api.IntegrationTest.Factories;
using Bit.Api.IntegrationTest.Helpers;
using Bit.Api.Models.Response;
using Bit.Api.NotificationCenter.Models.Response;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Billing.Enums;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Api;
using Bit.Core.NotificationCenter.Entities;
using Bit.Core.NotificationCenter.Enums;
using Bit.Core.NotificationCenter.Repositories;
using Bit.Core.Repositories;
using Xunit;

namespace Bit.Api.IntegrationTest.NotificationCenter.Controllers;

public class NotificationsControllerTests : IClassFixture<ApiApplicationFactory>, IAsyncLifetime
{
    private static readonly string _mockEncryptedBody =
        "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private static readonly string _mockEncryptedTitle =
        "2.06CDSJjTZaigYHUuswIq5A==|trxgZl2RCkYrrmCvGE9WNA==|w5p05eI5wsaYeSyWtsAPvBX63vj798kIMxBTfSB0BQg=";

    private static readonly Random _random = new();

    private static TimeSpan OneMinuteTimeSpan => TimeSpan.FromMinutes(1);

    private readonly HttpClient _client;
    private readonly ApiApplicationFactory _factory;
    private readonly LoginHelper _loginHelper;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationStatusRepository _notificationStatusRepository;
    private readonly IUserRepository _userRepository;
    private Organization _organization = null!;
    private OrganizationUser _organizationUserOwner = null!;
    private string _ownerEmail = null!;
    private List<(Notification, NotificationStatus?)> _notificationsWithStatuses = null!;

    public NotificationsControllerTests(ApiApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _loginHelper = new LoginHelper(_factory, _client);
        _notificationRepository = _factory.GetService<INotificationRepository>();
        _notificationStatusRepository = _factory.GetService<INotificationStatusRepository>();
        _userRepository = _factory.GetService<IUserRepository>();
    }

    public async Task InitializeAsync()
    {
        // Create the owner account
        _ownerEmail = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(_ownerEmail);

        // Create the organization
        (_organization, _organizationUserOwner) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);

        _notificationsWithStatuses = await CreateNotificationsWithStatusesAsync();
    }

    public Task DisposeAsync()
    {
        _client.Dispose();

        foreach (var (notification, _) in _notificationsWithStatuses)
        {
            _notificationRepository.DeleteAsync(notification);
        }

        return Task.CompletedTask;
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("-1")]
    [InlineData("0")]
    public async Task ListAsync_RequestValidationContinuationInvalidNumber_BadRequest(string continuationToken)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/notifications?continuationToken={continuationToken}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.NotNull(result);
        Assert.Contains("ContinuationToken", result.ValidationErrors);
        Assert.Contains("Continuation token must be a positive, non zero integer.",
            result.ValidationErrors["ContinuationToken"]);
    }

    [Fact]
    public async Task ListAsync_RequestValidationContinuationTokenMaxLengthExceeded_BadRequest()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync("/notifications?continuationToken=1234567890");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.NotNull(result);
        Assert.Contains("ContinuationToken", result.ValidationErrors);
        Assert.Contains("The field ContinuationToken must be a string with a maximum length of 9.",
            result.ValidationErrors["ContinuationToken"]);
    }

    [Theory]
    [InlineData("9")]
    [InlineData("1001")]
    public async Task ListAsync_RequestValidationPageSizeInvalidRange_BadRequest(string pageSize)
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var response = await _client.GetAsync($"/notifications?pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ErrorResponseModel>();
        Assert.NotNull(result);
        Assert.Contains("PageSize", result.ValidationErrors);
        Assert.Contains("The field PageSize must be between 10 and 1000.",
            result.ValidationErrors["PageSize"]);
    }

    [Fact]
    public async Task ListAsync_NotLoggedIn_Unauthorized()
    {
        var response = await _client.GetAsync("/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null, null, "2", 10)]
    [InlineData(10, null, "2", 10)]
    [InlineData(10, 2, "3", 10)]
    [InlineData(10, 3, null, 0)]
    [InlineData(15, null, "2", 15)]
    [InlineData(15, 2, null, 5)]
    [InlineData(20, null, "2", 20)]
    [InlineData(20, 2, null, 0)]
    [InlineData(1000, null, null, 20)]
    public async Task ListAsync_PaginationFilter_ReturnsNextPageOfNotificationsCorrectOrder(
        int? pageSize, int? pageNumber, string? expectedContinuationToken, int expectedCount)
    {
        var pageSizeWithDefault = pageSize ?? 10;

        await _loginHelper.LoginAsync(_ownerEmail);

        var skip = pageNumber == null ? 0 : (pageNumber.Value - 1) * pageSizeWithDefault;

        var notificationsInOrder = _notificationsWithStatuses.OrderByDescending(e => e.Item1.Priority)
            .ThenByDescending(e => e.Item1.CreationDate)
            .Skip(skip)
            .Take(pageSizeWithDefault)
            .ToList();

        var url = "/notifications";
        if (pageNumber != null)
        {
            url += $"?continuationToken={pageNumber}";
        }

        if (pageSize != null)
        {
            url += url.Contains('?') ? "&" : "?";
            url += $"pageSize={pageSize}";
        }

        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<NotificationResponseModel>>();
        Assert.NotNull(result?.Data);
        Assert.InRange(result.Data.Count(), 0, pageSizeWithDefault);
        Assert.Equal(expectedCount, notificationsInOrder.Count);
        Assert.Equal(notificationsInOrder.Count, result.Data.Count());
        AssertNotificationResponseModels(result.Data, notificationsInOrder);

        Assert.Equal(expectedContinuationToken, result.ContinuationToken);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(false, null)]
    [InlineData(true, null)]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ListAsync_ReadStatusDeletedStatusFilter_ReturnsFilteredNotificationsCorrectOrder(
        bool? readStatusFilter, bool? deletedStatusFilter)
    {
        await _loginHelper.LoginAsync(_ownerEmail);
        var notificationsInOrder = _notificationsWithStatuses.FindAll(e =>
                (readStatusFilter == null || readStatusFilter == (e.Item2?.ReadDate != null)) &&
                (deletedStatusFilter == null || deletedStatusFilter == (e.Item2?.DeletedDate != null)))
            .OrderByDescending(e => e.Item1.Priority)
            .ThenByDescending(e => e.Item1.CreationDate)
            .Take(10)
            .ToList();

        var url = "/notifications";
        if (readStatusFilter != null)
        {
            url += $"?readStatusFilter={readStatusFilter}";
        }

        if (deletedStatusFilter != null)
        {
            url += url.Contains('?') ? "&" : "?";
            url += $"deletedStatusFilter={deletedStatusFilter}";
        }

        var response = await _client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ListResponseModel<NotificationResponseModel>>();
        Assert.NotNull(result?.Data);
        Assert.InRange(result.Data.Count(), 0, 10);
        Assert.Equal(notificationsInOrder.Count, result.Data.Count());
        AssertNotificationResponseModels(result.Data, notificationsInOrder);
    }

    [Fact]
    private async void MarkAsDeletedAsync_NotLoggedIn_Unauthorized()
    {
        var url = $"/notifications/{Guid.NewGuid().ToString()}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    private async void MarkAsDeletedAsync_NonExistentNotificationId_NotFound()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{Guid.NewGuid()}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsDeletedAsync_UserIdNotMatching_NotFound()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = (await _userRepository.GetByEmailAsync(email))!;
        var notifications = await CreateNotificationsAsync(user.Id);

        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{notifications[0].Id}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsDeletedAsync_OrganizationIdNotMatchingUserNotPartOfOrganization_NotFound()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = (await _userRepository.GetByEmailAsync(email))!;
        var notifications = await CreateNotificationsAsync(user.Id, _organization.Id);

        await _loginHelper.LoginAsync(email);

        var url = $"/notifications/{notifications[0].Id}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsDeletedAsync_OrganizationIdNotMatchingUserPartOfDifferentOrganization_NotFound()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = (await _userRepository.GetByEmailAsync(email))!;
        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, email, OrganizationUserType.User);
        var notifications = await CreateNotificationsAsync(user.Id, _organization.Id);

        await _loginHelper.LoginAsync(email);

        var url = $"/notifications/{notifications[0].Id}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsDeletedAsync_NotificationStatusNotExisting_Created()
    {
        var notifications = await CreateNotificationsAsync(_organizationUserOwner.UserId);

        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{notifications[0].Id}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(
            notifications[0].Id, _organizationUserOwner.UserId!.Value);
        Assert.NotNull(notificationStatus);
        Assert.NotNull(notificationStatus.DeletedDate);
        Assert.Equal(DateTime.UtcNow, notificationStatus.DeletedDate.Value, OneMinuteTimeSpan);
        Assert.Null(notificationStatus.ReadDate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    private async void MarkAsDeletedAsync_NotificationStatusExisting_Updated(bool deletedDateNull)
    {
        var notifications = await CreateNotificationsAsync(_organizationUserOwner.UserId);
        await _notificationStatusRepository.CreateAsync(new NotificationStatus
        {
            NotificationId = notifications[0].Id,
            UserId = _organizationUserOwner.UserId!.Value,
            ReadDate = null,
            DeletedDate = deletedDateNull ? null : DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600))
        });

        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{notifications[0].Id}/delete";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(
            notifications[0].Id, _organizationUserOwner.UserId!.Value);
        Assert.NotNull(notificationStatus);
        Assert.NotNull(notificationStatus.DeletedDate);
        Assert.Equal(DateTime.UtcNow, notificationStatus.DeletedDate.Value, OneMinuteTimeSpan);
        Assert.Null(notificationStatus.ReadDate);
    }

    [Fact]
    private async void MarkAsReadAsync_NotLoggedIn_Unauthorized()
    {
        var url = $"/notifications/{Guid.NewGuid().ToString()}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    private async void MarkAsReadAsync_NonExistentNotificationId_NotFound()
    {
        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{Guid.NewGuid()}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsReadAsync_UserIdNotMatching_NotFound()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = (await _userRepository.GetByEmailAsync(email))!;
        var notifications = await CreateNotificationsAsync(user.Id);

        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{notifications[0].Id}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsReadAsync_OrganizationIdNotMatchingUserNotPartOfOrganization_NotFound()
    {
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = (await _userRepository.GetByEmailAsync(email))!;
        var notifications = await CreateNotificationsAsync(user.Id, _organization.Id);

        await _loginHelper.LoginAsync(email);

        var url = $"/notifications/{notifications[0].Id}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsReadAsync_OrganizationIdNotMatchingUserPartOfDifferentOrganization_NotFound()
    {
        var (organization, _) = await OrganizationTestHelpers.SignUpAsync(_factory,
            plan: PlanType.EnterpriseAnnually, ownerEmail: _ownerEmail, passwordManagerSeats: 10,
            paymentMethod: PaymentMethodType.Card);
        var email = $"integration-test{Guid.NewGuid()}@bitwarden.com";
        await _factory.LoginWithNewAccount(email);
        var user = (await _userRepository.GetByEmailAsync(email))!;
        await OrganizationTestHelpers.CreateUserAsync(_factory, organization.Id, email, OrganizationUserType.User);
        var notifications = await CreateNotificationsAsync(user.Id, _organization.Id);

        await _loginHelper.LoginAsync(email);

        var url = $"/notifications/{notifications[0].Id}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    private async void MarkAsReadAsync_NotificationStatusNotExisting_Created()
    {
        var notifications = await CreateNotificationsAsync(_organizationUserOwner.UserId);

        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{notifications[0].Id}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(
            notifications[0].Id, _organizationUserOwner.UserId!.Value);
        Assert.NotNull(notificationStatus);
        Assert.NotNull(notificationStatus.ReadDate);
        Assert.Equal(DateTime.UtcNow, notificationStatus.ReadDate.Value, OneMinuteTimeSpan);
        Assert.Null(notificationStatus.DeletedDate);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    private async void MarkAsReadAsync_NotificationStatusExisting_Updated(bool readDateNull)
    {
        var notifications = await CreateNotificationsAsync(_organizationUserOwner.UserId);
        await _notificationStatusRepository.CreateAsync(new NotificationStatus
        {
            NotificationId = notifications[0].Id,
            UserId = _organizationUserOwner.UserId!.Value,
            ReadDate = readDateNull ? null : DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600)),
            DeletedDate = null
        });

        await _loginHelper.LoginAsync(_ownerEmail);

        var url = $"/notifications/{notifications[0].Id}/read";
        var response = await _client.PatchAsync(url, new StringContent(""));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var notificationStatus = await _notificationStatusRepository.GetByNotificationIdAndUserIdAsync(
            notifications[0].Id, _organizationUserOwner.UserId!.Value);
        Assert.NotNull(notificationStatus);
        Assert.NotNull(notificationStatus.ReadDate);
        Assert.Equal(DateTime.UtcNow, notificationStatus.ReadDate.Value, OneMinuteTimeSpan);
        Assert.Null(notificationStatus.DeletedDate);
    }

    private static void AssertNotificationResponseModels(
        IEnumerable<NotificationResponseModel> notificationResponseModels,
        List<(Notification, NotificationStatus?)> expectedNotificationsWithStatuses)
    {
        var i = 0;
        foreach (var notificationResponseModel in notificationResponseModels)
        {
            Assert.Contains(expectedNotificationsWithStatuses, e => e.Item1.Id == notificationResponseModel.Id);
            var (expectedNotification, expectedNotificationStatus) = expectedNotificationsWithStatuses[i];
            Assert.NotNull(expectedNotification);
            Assert.Equal(expectedNotification.Priority, notificationResponseModel.Priority);
            Assert.Equal(expectedNotification.Title, notificationResponseModel.Title);
            Assert.Equal(expectedNotification.Body, notificationResponseModel.Body);
            Assert.Equal(expectedNotification.RevisionDate, notificationResponseModel.Date);
            if (expectedNotificationStatus != null)
            {
                Assert.Equal(expectedNotificationStatus.ReadDate, notificationResponseModel.ReadDate);
                Assert.Equal(expectedNotificationStatus.DeletedDate, notificationResponseModel.DeletedDate);
            }
            else
            {
                Assert.Null(notificationResponseModel.ReadDate);
                Assert.Null(notificationResponseModel.DeletedDate);
            }

            Assert.Equal("notification", notificationResponseModel.Object);
            i++;
        }
    }

    private async Task<List<(Notification, NotificationStatus?)>> CreateNotificationsWithStatusesAsync()
    {
        var userId = (Guid)_organizationUserOwner.UserId!;

        var globalNotifications = await CreateNotificationsAsync();
        var userWithoutOrganizationNotifications = await CreateNotificationsAsync(userId: userId);
        var organizationWithoutUserNotifications = await CreateNotificationsAsync(organizationId: _organization.Id);
        var userPartOrOrganizationNotifications = await CreateNotificationsAsync(userId: userId,
            organizationId: _organization.Id);

        var globalNotificationWithStatuses = await CreateNotificationStatusesAsync(globalNotifications, userId);
        var userWithoutOrganizationNotificationWithStatuses =
            await CreateNotificationStatusesAsync(userWithoutOrganizationNotifications, userId);
        var organizationWithoutUserNotificationWithStatuses =
            await CreateNotificationStatusesAsync(organizationWithoutUserNotifications, userId);
        var userPartOrOrganizationNotificationWithStatuses =
            await CreateNotificationStatusesAsync(userPartOrOrganizationNotifications, userId);

        return new List<List<(Notification, NotificationStatus?)>>
            {
                globalNotificationWithStatuses,
                userWithoutOrganizationNotificationWithStatuses,
                organizationWithoutUserNotificationWithStatuses,
                userPartOrOrganizationNotificationWithStatuses
            }
            .SelectMany(n => n)
            .ToList();
    }

    private async Task<List<Notification>> CreateNotificationsAsync(Guid? userId = null, Guid? organizationId = null,
        int numberToCreate = 5)
    {
        var priorities = Enum.GetValues<Priority>();
        var clientTypes = Enum.GetValues<ClientType>();

        var notifications = new List<Notification>();

        foreach (var clientType in clientTypes)
        {
            for (var i = 0; i < numberToCreate; i++)
            {
                var notification = new Notification
                {
                    Global = userId == null && organizationId == null,
                    UserId = userId,
                    OrganizationId = organizationId,
                    Title = _mockEncryptedTitle,
                    Body = _mockEncryptedBody,
                    Priority = (Priority)priorities.GetValue(_random.Next(priorities.Length))!,
                    ClientType = clientType,
                    CreationDate = DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600)),
                    RevisionDate = DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600))
                };

                notification = await _notificationRepository.CreateAsync(notification);

                notifications.Add(notification);
            }
        }

        return notifications;
    }

    private async Task<List<(Notification, NotificationStatus?)>> CreateNotificationStatusesAsync(
        List<Notification> notifications, Guid userId)
    {
        var readDateNotificationStatus = await _notificationStatusRepository.CreateAsync(new NotificationStatus
        {
            NotificationId = notifications[0].Id,
            UserId = userId,
            ReadDate = DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600)),
            DeletedDate = null
        });

        var deletedDateNotificationStatus = await _notificationStatusRepository.CreateAsync(new NotificationStatus
        {
            NotificationId = notifications[1].Id,
            UserId = userId,
            ReadDate = null,
            DeletedDate = DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600))
        });

        var readDateAndDeletedDateNotificationStatus = await _notificationStatusRepository.CreateAsync(
            new NotificationStatus
            {
                NotificationId = notifications[2].Id,
                UserId = userId,
                ReadDate = DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600)),
                DeletedDate = DateTime.UtcNow - TimeSpan.FromMinutes(_random.Next(3600))
            });

        return
        [
            (notifications[0], readDateNotificationStatus),
            (notifications[1], deletedDateNotificationStatus),
            (notifications[2], readDateAndDeletedDateNotificationStatus),
            (notifications[3], null),
            (notifications[4], null)
        ];
    }
}
