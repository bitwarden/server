#nullable enable

using Bit.Core.Dirt.Models.Data.EventIntegrations;
using Bit.Core.Models.Data;

namespace Bit.Core.Dirt.Services;

public interface IIntegrationFilterService
{
    bool EvaluateFilterGroup(IntegrationFilterGroup group, EventMessage message);
}
