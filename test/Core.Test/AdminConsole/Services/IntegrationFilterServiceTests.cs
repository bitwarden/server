#nullable enable

using System.Text.Json;
using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Models.Data;
using Bit.Core.Services;
using Bit.Test.Common.AutoFixture.Attributes;
using Xunit;

namespace Bit.Core.Test.Services;

public class IntegrationFilterServiceTests
{
    private readonly IntegrationFilterService _service = new();

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_EqualsUserId_Matches(EventMessage eventMessage)
    {
        var userId = Guid.NewGuid();
        eventMessage.UserId = userId;

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = IntegrationFilterOperation.Equals,
                    Value = userId
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_EqualsUserIdString_Matches(EventMessage eventMessage)
    {
        var userId = Guid.NewGuid();
        eventMessage.UserId = userId;

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = IntegrationFilterOperation.Equals,
                    Value = userId.ToString()
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_EqualsUserId_DoesNotMatch(EventMessage eventMessage)
    {
        eventMessage.UserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = IntegrationFilterOperation.Equals,
                    Value = otherUserId
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.False(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NotEqualsUniqueUserId_ReturnsTrue(EventMessage eventMessage)
    {
        var otherId = Guid.NewGuid();
        eventMessage.UserId = otherId;

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = IntegrationFilterOperation.NotEquals,
                    Value = Guid.NewGuid()
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NotEqualsMatchingUserId_ReturnsFalse(EventMessage eventMessage)
    {
        var id = Guid.NewGuid();
        eventMessage.UserId = id;

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = IntegrationFilterOperation.NotEquals,
                    Value = id
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.False(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_InCollectionId_Matches(EventMessage eventMessage)
    {
        var id = Guid.NewGuid();
        eventMessage.CollectionId = id;

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.In,
                    Value = new Guid?[] { Guid.NewGuid(), id }
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_InCollectionId_DoesNotMatch(EventMessage eventMessage)
    {
        eventMessage.CollectionId = Guid.NewGuid();

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.In,
                    Value = new Guid?[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.False(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NotInCollectionIdUniqueId_ReturnsTrue(EventMessage eventMessage)
    {
        eventMessage.CollectionId = Guid.NewGuid();

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.NotIn,
                    Value = new Guid?[] { Guid.NewGuid(), Guid.NewGuid() }
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NotInCollectionIdPresent_ReturnsFalse(EventMessage eventMessage)
    {
        var matchId = Guid.NewGuid();
        eventMessage.CollectionId = matchId;

        var group = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.NotIn,
                    Value = new Guid?[] { Guid.NewGuid(), matchId }
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);

        var jsonGroup = JsonSerializer.Serialize(group);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.False(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NestedGroups_AllMatch(EventMessage eventMessage)
    {
        var id = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        eventMessage.UserId = id;
        eventMessage.CollectionId = collectionId;

        var nestedGroup = new IntegrationFilterGroup
        {
            AndOperator = true,
            Rules =
            [
                new() { Property = "UserId", Operation = IntegrationFilterOperation.Equals, Value = id },
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.In,
                    Value = new Guid?[] { collectionId, Guid.NewGuid() }
                }
            ]
        };

        var topGroup = new IntegrationFilterGroup
        {
            AndOperator = true,
            Groups = [nestedGroup]
        };

        var result = _service.EvaluateFilterGroup(topGroup, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(topGroup);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }


    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NestedGroups_AnyMatch(EventMessage eventMessage)
    {
        var id = Guid.NewGuid();
        var collectionId = Guid.NewGuid();
        eventMessage.UserId = id;
        eventMessage.CollectionId = collectionId;

        var nestedGroup = new IntegrationFilterGroup
        {
            AndOperator = false,
            Rules =
            [
                new() { Property = "UserId", Operation = IntegrationFilterOperation.Equals, Value = id },
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.In,
                    Value = new Guid?[] { Guid.NewGuid() }
                }
            ]
        };

        var topGroup = new IntegrationFilterGroup
        {
            AndOperator = false,
            Groups = [nestedGroup]
        };

        var result = _service.EvaluateFilterGroup(topGroup, eventMessage);
        Assert.True(result);

        var jsonGroup = JsonSerializer.Serialize(topGroup);
        var roundtrippedGroup = JsonSerializer.Deserialize<IntegrationFilterGroup>(jsonGroup);
        Assert.NotNull(roundtrippedGroup);
        Assert.True(_service.EvaluateFilterGroup(roundtrippedGroup, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_UnknownProperty_ReturnsFalse(EventMessage eventMessage)
    {
        var group = new IntegrationFilterGroup
        {
            Rules =
            [
                new() { Property = "NotARealProperty", Operation = IntegrationFilterOperation.Equals, Value = "test" }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_UnsupportedOperation_ReturnsFalse(EventMessage eventMessage)
    {
        var group = new IntegrationFilterGroup
        {
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = (IntegrationFilterOperation)999, // Unknown operation
                    Value = eventMessage.UserId
                }
            ]
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_WrongTypeForInList_ThrowsException(EventMessage eventMessage)
    {
        var group = new IntegrationFilterGroup
        {
            Rules =
            [
                new()
                {
                    Property = "CollectionId",
                    Operation = IntegrationFilterOperation.In,
                    Value = "not an array" // Should be Guid[]
                }
            ]
        };

        Assert.Throws<InvalidCastException>(() =>
            _service.EvaluateFilterGroup(group, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_NullValue_ThrowsException(EventMessage eventMessage)
    {
        var group = new IntegrationFilterGroup
        {
            Rules =
            [
                new()
                {
                    Property = "UserId",
                    Operation = IntegrationFilterOperation.Equals,
                    Value = null
                }
            ]
        };

        Assert.Throws<InvalidCastException>(() =>
            _service.EvaluateFilterGroup(group, eventMessage));
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_EmptyRuleList_ReturnsTrue(EventMessage eventMessage)
    {
        var group = new IntegrationFilterGroup
        {
            Rules = [],
            Groups = [],
            AndOperator = true
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.True(result); // Nothing to fail, returns true by design
    }

    [Theory, BitAutoData]
    public void EvaluateFilterGroup_InvalidNestedGroup_ReturnsFalse(EventMessage eventMessage)
    {
        var group = new IntegrationFilterGroup
        {
            Groups =
            [
                new()
                {
                    Rules =
                    [
                        new()
                        {
                            Property = "Nope",
                            Operation = IntegrationFilterOperation.Equals,
                            Value = "bad"
                        }
                    ]
                }
            ],
            AndOperator = true
        };

        var result = _service.EvaluateFilterGroup(group, eventMessage);
        Assert.False(result);
    }
}
