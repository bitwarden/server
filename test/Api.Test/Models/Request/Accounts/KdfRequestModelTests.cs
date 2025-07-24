﻿using System.ComponentModel.DataAnnotations;
using Bit.Api.Auth.Models.Request.Accounts;
using Bit.Core.Enums;
using Xunit;

namespace Bit.Api.Test.Models.Request.Accounts;

public class KdfRequestModelTests
{
    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 1_000_000, null, null)] // Somewhere in the middle
    [InlineData(KdfType.PBKDF2_SHA256, 600_000, null, null)] // Right on the lower boundary
    [InlineData(KdfType.PBKDF2_SHA256, 2_000_000, null, null)] // Right on the upper boundary
    [InlineData(KdfType.Argon2id, 5, 500, 8)] // Somewhere in the middle
    [InlineData(KdfType.Argon2id, 2, 15, 1)] // Right on the lower boundary
    [InlineData(KdfType.Argon2id, 10, 1024, 16)] // Right on the upper boundary
    public void Validate_IsValid(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism)
    {
        var model = new KdfRequestModel
        {
            Kdf = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            Key = "TEST",
            NewMasterPasswordHash = "TEST",
        };

        var results = Validate(model);
        Assert.Empty(results);
    }

    [Theory]
    [InlineData(KdfType.PBKDF2_SHA256, 500_000, null, null, 1)] // Too few iterations
    [InlineData(KdfType.PBKDF2_SHA256, 2_000_001, null, null, 1)] // Too many iterations
    [InlineData(KdfType.Argon2id, 0, 30, 8, 1)] // Iterations must be greater than 0
    [InlineData(KdfType.Argon2id, 10, 14, 8, 1)] // Too little memory
    [InlineData(KdfType.Argon2id, 10, 14, 0, 1)] // Too small of a parallelism value
    [InlineData(KdfType.Argon2id, 10, 1025, 8, 1)] // Too much memory
    [InlineData(KdfType.Argon2id, 10, 512, 17, 1)] // Too big of a parallelism value
    public void Validate_Fails(KdfType kdfType, int kdfIterations, int? kdfMemory, int? kdfParallelism, int expectedFailures)
    {
        var model = new KdfRequestModel
        {
            Kdf = kdfType,
            KdfIterations = kdfIterations,
            KdfMemory = kdfMemory,
            KdfParallelism = kdfParallelism,
            Key = "TEST",
            NewMasterPasswordHash = "TEST",
        };

        var results = Validate(model);
        Assert.NotEmpty(results);
        Assert.Equal(expectedFailures, results.Count);
    }

    public static List<ValidationResult> Validate(KdfRequestModel model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results);
        return results;
    }
}
