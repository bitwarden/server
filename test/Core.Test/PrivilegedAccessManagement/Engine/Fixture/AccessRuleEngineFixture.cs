using System.Net;
using Bit.Core.Enums;
using Bit.Core.PrivilegedAccessManagement.Engine;
using Bit.Core.Vault.Models.Data;
using Microsoft.Extensions.Time.Testing;

namespace Bit.Core.Test.PrivilegedAccessManagement.Engine;

public sealed class AccessRuleEngineFixture
{
    public const string RequestingUser = "alice";
    public const string AnotherUser = "bob";
    public static readonly DateTimeOffset Now = new(2026, 5, 29, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _time = new(Now);
    private readonly FakeAccessRuleResolver _resolver = new();
    private readonly FakeAccessRuleRequestRepository _requests = new();
    private readonly FakeAccessRuleLeaseRepository _leases;

    private IPAddress _ipAddress = IPAddress.Parse("10.0.0.5");

    private AccessRule? _rule = new() { Name = "test-rule", Duration = TimeSpan.FromHours(1) };

    public AccessRuleEngineFixture()
    {
        _leases = new FakeAccessRuleLeaseRepository(_time);
    }

    public CipherDetails Cipher { get; } = new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111") };

    public AccessRuleSignals Signals => new()
    {
        Username = RequestingUser,
        IpAddress = _ipAddress,
        MultifactorEnabled = true,
        UserTime = Now,
        Device = DeviceType.ChromeBrowser,
    };

    public int LeasesCreated => _leases.CreatedCount;
    public bool RequestWasCreated => _requests.CreatedCount > 0;
    public int RequestsCreated => _requests.CreatedCount;

    public AccessRuleEngineFixture WithNoRules()
    {
        _rule = null;
        return this;
    }

    public AccessRuleEngineFixture RequiringApproval()
    {
        _rule = EnsureRuleExist();
        _rule = _rule with { RequireApproval = true };
        return this;
    }

    public AccessRuleEngineFixture RequiringSingleton()
    {
        _rule = EnsureRuleExist();
        _rule = _rule with { RequireSingleton = true };
        return this;
    }

    public AccessRuleEngineFixture RestrictedToCidr(params string[] cidrs)
    {
        _rule = EnsureRuleExist();
        _rule.RequiredCidr.AddRange(cidrs);
        return this;
    }

    public AccessRuleEngineFixture RestrictedToTimeWindow(string timeZone, TimeOnly from, TimeOnly to, params DayOfWeek[] days)
    {
        _rule = EnsureRuleExist();
        _rule = _rule with
        {
            TimeOfDay = new TimeOfDayConfig
            {
                TimeZone = timeZone,
                Windows = [new AccessTimeWindow { Days = days, From = from, To = to }],
            },
        };
        return this;
    }

    public AccessRuleEngineFixture WithActiveLease()
    {
        return SeedLease(RequestingUser, Now.UtcDateTime.AddHours(1));
    }

    public AccessRuleEngineFixture WithExpiredLease()
    {
        return SeedLease(RequestingUser, Now.UtcDateTime.AddHours(-1));
    }

    public AccessRuleEngineFixture WithActiveLeaseHeldBy(string username)
    {
        return SeedLease(username, Now.UtcDateTime.AddHours(1));
    }

    public AccessRuleEngineFixture WithApprovedRequest()
    {
        return SeedRequest(approved: true);
    }

    public AccessRuleEngineFixture WithPendingRequest()
    {
        return SeedRequest(approved: false);
    }

    public AccessRuleEngineFixture ApproveRequest()
    {
        _requests.Approve(Cipher.Id, RequestingUser);
        return this;
    }

    public AccessRuleEngineFixture WhereLeaseCreationFails()
    {
        _leases.FailCreate = true;
        return this;
    }

    public AccessRuleEngineFixture FromIpAddress(string ip)
    {
        _ipAddress = IPAddress.Parse(ip);
        return this;
    }

    public AccessRuleEngineResult Check(CipherDetails cipher)
    {
        if (_rule != null)
        {
            _resolver.SetRule(cipher.Id, _rule);
        }

        var engine = new AccessRuleEngine(_time, _resolver, _requests, _leases);
        return engine.Check(cipher, Signals);
    }

    private AccessRuleEngineFixture SeedLease(string username, DateTime expires)
    {
        _leases.Seed(new AccessRuleLease { CipherId = Cipher.Id, Username = username, Expires = expires });
        return this;
    }

    private AccessRuleEngineFixture SeedRequest(bool approved)
    {
        _requests.Seed(new AccessRuleRequest { CipherId = Cipher.Id, Username = RequestingUser, Approved = approved });
        return this;
    }

    private AccessRule EnsureRuleExist()
    {
        return _rule ??= new AccessRule { Name = "test-rule", Duration = TimeSpan.FromHours(1) };
    }
}
