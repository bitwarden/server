using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Pipeline;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Recipes;

public class IndividualUserRecipe(
    DatabaseContext db,
    IMapper mapper,
    IPasswordHasher<User> passwordHasher,
    IManglerService manglerService)
{
    private readonly RecipeOrchestrator _orchestrator = new(db, mapper);

    public IndividualSeedResult Seed(string presetName,
        string? password = null, int? kdfIterations = null)
    {
        var result = _orchestrator.Execute(
            presetName, passwordHasher, manglerService, password, kdfIterations);

        return new IndividualSeedResult(
            result.UserId!.Value,
            result.OwnerEmail,
            result.Premium,
            result.CiphersCount,
            result.FoldersCount);
    }
}

public record IndividualSeedResult(
    Guid UserId, string? Email, bool Premium, int CiphersCount, int FoldersCount);
