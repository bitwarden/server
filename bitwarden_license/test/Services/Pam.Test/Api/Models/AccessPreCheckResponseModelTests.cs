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
        // Dapper materializes NotAfter as Kind.Unspecified; serialized as-is a browser would read it as local time.
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
        // "Absence means startable" must hold even for the parameterless constructor.
        Assert.True(new AccessPreCheckResponseModel().CanStartLease);
    }

    [Fact]
    public void TheWireContractIsExactlyAvailability_WithNoHolderIdentity()
    {
        // Pinned to the exact property set, not a forbidden-names list, so any added field forces a review.
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
