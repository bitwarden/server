﻿using Bit.Core.Entities;
using Bit.Core.Models.Api;

namespace Bit.Api.AdminConsole.Models.Response.Organizations;

public class OrganizationDomainResponseModel : ResponseModel
{
    public OrganizationDomainResponseModel(
        OrganizationDomain organizationDomain,
        string obj = "organizationDomain"
    )
        : base(obj)
    {
        ArgumentNullException.ThrowIfNull(organizationDomain);

        Id = organizationDomain.Id;
        OrganizationId = organizationDomain.OrganizationId;
        Txt = organizationDomain.Txt;
        DomainName = organizationDomain.DomainName;
        CreationDate = organizationDomain.CreationDate;
        NextRunDate = organizationDomain.NextRunDate;
        JobRunCount = organizationDomain.JobRunCount;
        VerifiedDate = organizationDomain.VerifiedDate;
        LastCheckedDate = organizationDomain.LastCheckedDate;
    }

    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Txt { get; set; }
    public string DomainName { get; set; }
    public DateTime CreationDate { get; set; }
    public DateTime NextRunDate { get; set; }
    public int JobRunCount { get; set; }
    public DateTime? VerifiedDate { get; set; }
    public DateTime? LastCheckedDate { get; set; }
}
