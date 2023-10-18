﻿using System.ComponentModel.DataAnnotations;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.Entities;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.Entities;

public class OrganizationApiKey : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public OrganizationApiKeyType Type { get; set; }
    [MaxLength(30)]
    public string ApiKey { get; set; }
    public DateTime RevisionDate { get; set; }

    public void SetNewId()
    {
        Id = CoreHelpers.GenerateComb();
    }
}
