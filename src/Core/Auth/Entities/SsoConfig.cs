﻿// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.Auth.Models.Data;
using Bit.Core.Entities;

namespace Bit.Core.Auth.Entities;

public class SsoConfig : ITableObject<long>
{
    public long Id { get; set; }
    public bool Enabled { get; set; } = true;
    public Guid OrganizationId { get; set; }
    public string Data { get; set; }
    public DateTime CreationDate { get; internal set; } = DateTime.UtcNow;
    public DateTime RevisionDate { get; internal set; } = DateTime.UtcNow;

    public void SetNewId()
    {
        // int will be auto-populated
        Id = 0;
    }

    public SsoConfigurationData GetData()
    {
        return SsoConfigurationData.Deserialize(Data);
    }

    public void SetData(SsoConfigurationData data)
    {
        Data = data.Serialize();
    }
}
