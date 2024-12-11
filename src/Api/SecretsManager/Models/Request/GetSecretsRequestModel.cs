using System.ComponentModel.DataAnnotations;

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
            var duplicateGuids = Ids.GroupBy(x => x).Where(g => g.Count() > 1).Select(g => g.Key);

            yield return new ValidationResult(
                $"The following GUIDs were duplicated {string.Join(", ", duplicateGuids)} ",
                new[] { nameof(GetSecretsRequestModel) }
            );
        }
    }
}
