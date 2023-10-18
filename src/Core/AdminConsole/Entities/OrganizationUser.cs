﻿using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Models;
using Bit.Core.Models.Data;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationUser : ITableObject<Guid>, IExternal
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid? UserId { get; set; }
    [MaxLength(256)]
    public string Email { get; set; }
    public string Key { get; set; }
    public string ResetPasswordKey { get; set; }
    public OrganizationUserStatusType Status { get; set; }
    public OrganizationUserType Type { get; set; }
    public bool AccessAll { get; set; }
    [MaxLength(300)]
    public string ExternalId { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;
    public string Permissions { get; set; }
    public bool AccessSecretsManager { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }

    public Permissions GetPermissions()
    {
        return string.IsNullOrWhiteSpace(Permissions) ? null
            : CoreHelpers.LoadClassFromJsonData<Permissions>(Permissions);
    }
}
