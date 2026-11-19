using System.ComponentModel.DataAnnotations;
using Bit.Api.SecretsManager.Models.Request;
using Xunit;

namespace Bit.Api.Test.SecretsManager.Models.Request;

public class SMImportRequestModelTests
{
    private const string EncString = "2.AOs41Hd8OQiCPXjyJKCiDA==|O6OHgt2U2hJGBSNGnimJmg==|iD33s8B69C8JhYYhSa4V1tArjvLr8eEaGqOV7BRo5Jk=";

    private static List<ValidationResult> Validate(SMImportRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void Validate_SecretsOmitted_Fails()
    {
        Assert.NotEmpty(Validate(new SMImportRequestModel()));
    }

    [Fact]
    public void Validate_NullProjectEntry_Fails()
    {
        var model = new SMImportRequestModel
        {
            Projects = new SMImportRequestModel.InnerProjectImportRequestModel[] { null! },
            Secrets = Array.Empty<SMImportRequestModel.InnerSecretImportRequestModel>(),
        };

        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void Validate_NullSecretEntry_Fails()
    {
        var model = new SMImportRequestModel { Secrets = new SMImportRequestModel.InnerSecretImportRequestModel[] { null! } };

        Assert.NotEmpty(Validate(model));
    }

    [Fact]
    public void Validate_Valid_Succeeds()
    {
        var model = new SMImportRequestModel
        {
            Projects = new[] { new SMImportRequestModel.InnerProjectImportRequestModel { Id = Guid.NewGuid(), Name = EncString } },
            Secrets = Array.Empty<SMImportRequestModel.InnerSecretImportRequestModel>(),
        };

        Assert.Empty(Validate(model));
    }
}
