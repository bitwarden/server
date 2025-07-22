#nullable enable

using Bit.Core.AdminConsole.Models.Data.EventIntegrations;
using Bit.Core.Models.Data;

namespace Bit.Core.Services;

public interface IIntegrationFilterService
{
    bool EvaluateFilterGroup(IntegrationFilterGroup group, EventMessage message);
}
