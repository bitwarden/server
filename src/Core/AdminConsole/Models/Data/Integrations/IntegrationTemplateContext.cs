﻿#nullable enable

using Bit.Core.AdminConsole.Entities;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data;

namespace Bit.Core.AdminConsole.Models.Data.Integrations;

public class IntegrationTemplateContext(EventMessage eventMessage)
{
    public EventMessage Event { get; } = eventMessage;

    public string DomainName => Event.DomainName;
    public string IpAddress => Event.IpAddress;
    public DeviceType? DeviceType => Event.DeviceType;
    public Guid? ActingUserId => Event.ActingUserId;
    public Guid? OrganizationUserId => Event.OrganizationUserId;
    public DateTime Date => Event.Date;
    public EventType Type => Event.Type;
    public Guid? UserId => Event.UserId;
    public Guid? OrganizationId => Event.OrganizationId;
    public Guid? CipherId => Event.CipherId;
    public Guid? CollectionId => Event.CollectionId;
    public Guid? GroupId => Event.GroupId;
    public Guid? PolicyId => Event.PolicyId;

    public User? User { get; set; }
    public string? UserName => User?.Name;
    public string? UserEmail => User?.Email;

    public User? ActingUser { get; set; }
    public string? ActingUserName => ActingUser?.Name;
    public string? ActingUserEmail => ActingUser?.Email;

    public Organization? Organization { get; set; }
    public string? OrganizationName => Organization?.DisplayName();
}
