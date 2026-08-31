using Bit.Services.Pam.Api.Models.Response;
using Bit.Services.Pam.Enums;
using Bit.Services.Pam.Models;
using Xunit;

namespace Bit.Services.Pam.Test.Api.Models;

public class AccessPreCheckResponseModelTests
{
    [Fact]
    public void Constructor_MarksSlotFreesAtAsUtcWithoutShiftingIt()
    {
        // Dapper materialises AccessLease.NotAfter with Kind.Unspecified. Serialized as-is it carries no designator
        // and a browser reads it as local time, so the retry time this ticket exists to give the requester would be
        // wrong by their UTC offset -- in the past for anyone east of UTC.
        var slotFreesAt = new DateTime(2026, 8, 31, 10, 52, 0, DateTimeKind.Unspecified);
        var result = new AccessPreCheckResult(AccessApprovalMode.Automatic, CanStartLease: false,
            SlotFreesAt: slotFreesAt);

        var model = new AccessPreCheckResponseModel(Guid.NewGuid(), result);

        Assert.NotNull(model.SlotFreesAt);
        Assert.Equal(DateTimeKind.Utc, model.SlotFreesAt.Value.Kind);
        // Relabelled, not converted: the clock reading must be untouched.
        Assert.Equal(slotFreesAt.TimeOfDay, model.SlotFreesAt.Value.TimeOfDay);
    }

    [Fact]
    public void Constructor_LeavesSlotFreesAtNullWhenTheSlotIsFree()
    {
        var cipherId = Guid.NewGuid();
        var result = new AccessPreCheckResult(AccessApprovalMode.Automatic);

        var model = new AccessPreCheckResponseModel(cipherId, result);

        Assert.Equal("accessPreCheck", model.Object);
        Assert.Equal(cipherId, model.CipherId);
        Assert.True(model.CanStartLease);
        Assert.Null(model.SlotFreesAt);
    }

    [Fact]
    public void DefaultConstructed_ReadsAsStartable()
    {
        // The field's polarity is "absence means startable" all the way down, so even the parameterless
        // (de)serialization constructor must not produce a model that looks blocked.
        Assert.True(new AccessPreCheckResponseModel().CanStartLease);
    }

    [Fact]
    public void TheWireContractIsExactlyAvailability_WithNoHolderIdentity()
    {
        // PM-42446 chose Alternative A: the requester learns THAT the slot is taken and when it frees, never by whom.
        // Pinned as the exact property set rather than a list of forbidden names, so that ANY added field trips this
        // test and forces a deliberate disclosure decision -- a name-blocklist would wave through the same data under
        // a different name.
        var properties = typeof(AccessPreCheckResponseModel).GetProperties().Select(p => p.Name).ToHashSet();

        Assert.Equal(
            new HashSet<string>
            {
                nameof(AccessPreCheckResponseModel.Object),
                nameof(AccessPreCheckResponseModel.CipherId),
                nameof(AccessPreCheckResponseModel.ApprovalMode),
                nameof(AccessPreCheckResponseModel.HasActiveLease),
                nameof(AccessPreCheckResponseModel.DefaultDurationSeconds),
                nameof(AccessPreCheckResponseModel.MaxDurationSeconds),
                nameof(AccessPreCheckResponseModel.CanStartLease),
                nameof(AccessPreCheckResponseModel.SlotFreesAt),
            },
            properties);
    }
}
