using System.ComponentModel.DataAnnotations;
using Bit.Core.Utilities;

#nullable enable

namespace Bit.Core.Entities;

public class OrganizationDomain : ITableObject<Guid>
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string Txt { get; set; } = null!;

    [MaxLength(255)]
    public string DomainName { get; set; } = null!;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public DateTime? VerifiedDate { get; private set; }
    public DateTime NextRunDate { get; private set; }
    public DateTime? LastCheckedDate { get; private set; }
    public int JobRunCount { get; private set; }

    public void SetNewId() => Id = CoreHelpers.GenerateComb();

    public void SetNextRunDate(int interval)
    {
        //verification can take up to 72 hours
        //1st job runs after 12hrs, 2nd after 24hrs and 3rd after 36hrs
        NextRunDate =
            JobRunCount == 0
                ? CreationDate.AddHours(interval)
                : NextRunDate.AddHours((JobRunCount + 1) * interval);
    }

    public void SetJobRunCount()
    {
        if (JobRunCount == 3)
        {
            return;
        }

        JobRunCount++;
    }

    public void SetVerifiedDate()
    {
        VerifiedDate = DateTime.UtcNow;
    }

    public void SetLastCheckedDate()
    {
        LastCheckedDate = DateTime.UtcNow;
    }
}
