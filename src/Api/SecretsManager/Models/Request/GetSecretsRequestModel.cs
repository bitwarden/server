using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace Bit.Api.SecretsManager.Models.Request;

public class GetSecretsRequestModel : IValidatableObject
{
    [Required]
    public IEnumerable<Guid> Ids { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var isDistinct = Ids.Distinct().Count() == Ids.Count();
        if (!isDistinct)
        {
           var duplicateGuids = Ids.GroupBy(x => x)
                                  .Where(g => g.Count() > 1)
                                  .SelectMany(g => g);

            yield return new ValidationResult(
                $"The following GUIDs were duplicated {string.Join(", ", duplicateGuids.Distinct())} ",
                new[] { nameof(GetSecretsRequestModel) });
        }
    }
}
