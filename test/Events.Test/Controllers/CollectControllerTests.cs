using AutoFixture.Xunit2;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.Context;
using Bit.Core.Enums;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Vault.Entities;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;
using Bit.Events.Controllers;
using Bit.Events.Models;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace Events.Test.Controllers;

public class CollectControllerTests
{
    private readonly CollectController _sut;
    private readonly ICurrentContext _currentContext;
    private readonly IEventService _eventService;
    private readonly ICipherRepository _cipherRepository;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IFeatureService _featureService;
    private readonly IApplicationCacheService _applicationCacheService;

    public CollectControllerTests()
    {
        _currentContext = Substitute.For<ICurrentContext>();
        _eventService = Substitute.For<IEventService>();
        _cipherRepository = Substitute.For<ICipherRepository>();
        _organizationRepository = Substitute.For<IOrganizationRepository>();
        _featureService = Substitute.For<IFeatureService>();
        _applicationCacheService = Substitute.For<IApplicationCacheService>();

        _sut = new CollectController(
            _currentContext,
            _eventService,
            _cipherRepository,
            _organizationRepository,
            _featureService,
            _applicationCacheService
        );
    }

    [Fact]
    public async Task Post_NullModel_ReturnsBadRequest()
    {
        var result = await _sut.Post(null);

        Assert.IsType<BadRequestResult>(result);
    }

    [Fact]
    public async Task Post_EmptyModel_ReturnsBadRequest()
    {
        var result = await _sut.Post(new List<EventModel>());

        Assert.IsType<BadRequestResult>(result);
    }

    [Theory]
    [AutoData]
    public async Task Post_UserClientExportedVault_LogsUserEvent(Guid userId)
    {
        _currentContext.UserId.Returns(userId);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.User_ClientExportedVault,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogUserEventAsync(userId, EventType.User_ClientExportedVault, eventDate);
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherAutofilled_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId, userId);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.Count() == 1 &&
                         tuples.First().Item1 == cipherDetails &&
                         tuples.First().Item2 == EventType.Cipher_ClientAutofilled &&
                         tuples.First().Item3 == eventDate
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientCopiedPassword_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedPassword,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientCopiedPassword
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientCopiedHiddenField_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedHiddenField,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientCopiedHiddenField
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientCopiedCardCode_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientCopiedCardCode,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientCopiedCardCode
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientToggledCardNumberVisible_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledCardNumberVisible,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientToggledCardNumberVisible
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientToggledCardCodeVisible_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledCardCodeVisible,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientToggledCardCodeVisible
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientToggledHiddenFieldVisible_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledHiddenFieldVisible,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientToggledHiddenFieldVisible
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientToggledPasswordVisible_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientToggledPasswordVisible,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientToggledPasswordVisible
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherClientViewed_WithValidCipher_LogsCipherEvent(Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipherId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item2 == EventType.Cipher_ClientViewed
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherEvent_WithoutCipherId_SkipsEvent(Guid userId)
    {
        _currentContext.UserId.Returns(userId);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = null,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _cipherRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default, default);
        await _eventService.DidNotReceiveWithAnyArgs().LogCipherEventsAsync(default);
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherEvent_WithNullCipher_WithoutOrgId_SkipsEvent(Guid userId, Guid cipherId)
    {
        _currentContext.UserId.Returns(userId);
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                OrganizationId = null,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId, userId);
        await _cipherRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(cipherId);
        await _eventService.DidNotReceiveWithAnyArgs().LogCipherEventsAsync(default);
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherEvent_WithNullCipher_WithOrgId_ChecksOrgCipher(
        Guid userId, Guid cipherId, Guid orgId, Cipher cipher, CurrentContextOrganization org)
    {
        _currentContext.UserId.Returns(userId);
        cipher.Id = cipherId;
        cipher.OrganizationId = orgId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);
        _cipherRepository.GetByIdAsync(cipherId).Returns(cipher);
        _currentContext.GetOrganization(orgId).Returns(org);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                OrganizationId = orgId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId, userId);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(
                tuples => tuples.First().Item1 == cipher
            )
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherEvent_WithNullCipher_OrgCipherNotFound_SkipsEvent(
        Guid userId, Guid cipherId, Guid orgId)
    {
        _currentContext.UserId.Returns(userId);
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);
        _cipherRepository.GetByIdAsync(cipherId).Returns((CipherDetails?)null);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                OrganizationId = orgId,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId, userId);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId);
        await _eventService.DidNotReceiveWithAnyArgs().LogCipherEventsAsync(default);
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherEvent_CipherDoesNotBelongToOrg_SkipsEvent(
        Guid userId, Guid cipherId, Guid orgId, Guid differentOrgId, Cipher cipher)
    {
        _currentContext.UserId.Returns(userId);
        cipher.Id = cipherId;
        cipher.OrganizationId = differentOrgId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);
        _cipherRepository.GetByIdAsync(cipherId).Returns(cipher);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                OrganizationId = orgId,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.DidNotReceiveWithAnyArgs().LogCipherEventsAsync(default);
    }

    [Theory]
    [AutoData]
    public async Task Post_CipherEvent_OrgNotFound_SkipsEvent(
        Guid userId, Guid cipherId, Guid orgId, Cipher cipher)
    {
        _currentContext.UserId.Returns(userId);
        cipher.Id = cipherId;
        cipher.OrganizationId = orgId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns((CipherDetails?)null);
        _cipherRepository.GetByIdAsync(cipherId).Returns(cipher);
        _currentContext.GetOrganization(orgId).Returns((CurrentContextOrganization)null);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                OrganizationId = orgId,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.DidNotReceiveWithAnyArgs().LogCipherEventsAsync(default);
    }

    [Theory]
    [AutoData]
    public async Task Post_MultipleCipherEvents_WithSameCipherId_UsesCachedCipher(
        Guid userId, Guid cipherId, CipherDetails cipherDetails)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                Date = DateTime.UtcNow
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientViewed,
                CipherId = cipherId,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _cipherRepository.Received(1).GetByIdAsync(cipherId, userId);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(tuples => tuples.Count() == 2)
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_OrganizationClientExportedVault_WithValidOrg_LogsOrgEvent(
        Guid userId, Guid orgId, Organization organization)
    {
        _currentContext.UserId.Returns(userId);
        organization.Id = orgId;
        _organizationRepository.GetByIdAsync(orgId).Returns(organization);
        var eventDate = DateTime.UtcNow;
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = orgId,
                Date = eventDate
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _organizationRepository.Received(1).GetByIdAsync(orgId);
        await _eventService.Received(1).LogOrganizationEventAsync(organization, EventType.Organization_ClientExportedVault, eventDate);
    }

    [Theory]
    [AutoData]
    public async Task Post_OrganizationClientExportedVault_WithoutOrgId_SkipsEvent(Guid userId)
    {
        _currentContext.UserId.Returns(userId);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = null,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _organizationRepository.DidNotReceiveWithAnyArgs().GetByIdAsync(default);
        await _eventService.DidNotReceiveWithAnyArgs().LogOrganizationEventAsync(default, default, default);
    }

    [Theory]
    [AutoData]
    public async Task Post_OrganizationClientExportedVault_WithNullOrg_SkipsEvent(Guid userId, Guid orgId)
    {
        _currentContext.UserId.Returns(userId);
        _organizationRepository.GetByIdAsync(orgId).Returns((Organization)null);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = orgId,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _organizationRepository.Received(1).GetByIdAsync(orgId);
        await _eventService.DidNotReceiveWithAnyArgs().LogOrganizationEventAsync(default, default, default);
    }

    [Theory]
    [AutoData]
    public async Task Post_UnsupportedEventType_SkipsEvent(Guid userId)
    {
        _currentContext.UserId.Returns(userId);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.User_LoggedIn,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.DidNotReceiveWithAnyArgs().LogUserEventAsync(default, default, default);
    }

    [Theory]
    [AutoData]
    public async Task Post_MixedEventTypes_ProcessesAllEvents(
        Guid userId, Guid cipherId, Guid orgId, CipherDetails cipherDetails, Organization organization)
    {
        _currentContext.UserId.Returns(userId);
        cipherDetails.Id = cipherId;
        organization.Id = orgId;
        _cipherRepository.GetByIdAsync(cipherId, userId).Returns(cipherDetails);
        _organizationRepository.GetByIdAsync(orgId).Returns(organization);
        var events = new List<EventModel>
        {
            new EventModel
            {
                Type = EventType.User_ClientExportedVault,
                Date = DateTime.UtcNow
            },
            new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipherId,
                Date = DateTime.UtcNow
            },
            new EventModel
            {
                Type = EventType.Organization_ClientExportedVault,
                OrganizationId = orgId,
                Date = DateTime.UtcNow
            }
        };

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogUserEventAsync(userId, EventType.User_ClientExportedVault, Arg.Any<DateTime?>());
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(tuples => tuples.Count() == 1)
        );
        await _eventService.Received(1).LogOrganizationEventAsync(organization, EventType.Organization_ClientExportedVault, Arg.Any<DateTime?>());
    }

    [Theory]
    [AutoData]
    public async Task Post_MoreThan50CipherEvents_LogsInBatches(Guid userId, List<CipherDetails> ciphers)
    {
        _currentContext.UserId.Returns(userId);
        var events = new List<EventModel>();

        for (int i = 0; i < 100; i++)
        {
            var cipher = ciphers[i % ciphers.Count];
            _cipherRepository.GetByIdAsync(cipher.Id, userId).Returns(cipher);
            events.Add(new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow
            });
        }

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(2).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(tuples => tuples.Count() == 50)
        );
    }

    [Theory]
    [AutoData]
    public async Task Post_Exactly50CipherEvents_LogsInSingleBatch(Guid userId, List<CipherDetails> ciphers)
    {
        _currentContext.UserId.Returns(userId);
        var events = new List<EventModel>();

        for (int i = 0; i < 50; i++)
        {
            var cipher = ciphers[i % ciphers.Count];
            _cipherRepository.GetByIdAsync(cipher.Id, userId).Returns(cipher);
            events.Add(new EventModel
            {
                Type = EventType.Cipher_ClientAutofilled,
                CipherId = cipher.Id,
                Date = DateTime.UtcNow
            });
        }

        var result = await _sut.Post(events);

        Assert.IsType<OkResult>(result);
        await _eventService.Received(1).LogCipherEventsAsync(
            Arg.Is<IEnumerable<Tuple<Cipher, EventType, DateTime?>>>(tuples => tuples.Count() == 50)
        );
    }
}
