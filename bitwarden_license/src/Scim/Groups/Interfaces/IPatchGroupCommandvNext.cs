﻿using Bit.Core.AdminConsole.Entities;
using Bit.Scim.Models;

namespace Bit.Scim.Groups.Interfaces;

public interface IPatchGroupCommandvNext
{
    Task PatchGroupAsync(Organization organization, Guid id, ScimPatchModel model);
}
