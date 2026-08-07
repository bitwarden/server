using Bit.Core.Entities;
using Bit.Core.Models.Data;

namespace Bit.Api.AdminConsole.Authorization.Collections;

public record CollectionGroupAccessResource(
    Collection Collection,
    CollectionAccessDetails AccessDetails);
