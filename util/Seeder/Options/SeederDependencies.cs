using AutoMapper;
using Bit.Core.Entities;
using Bit.Infrastructure.EntityFramework.Repositories;
using Bit.Seeder.Services;
using Microsoft.AspNetCore.Identity;

namespace Bit.Seeder.Options;

/// <summary>
/// Bundles the infrastructure services that all recipes require.
/// </summary>
public sealed record SeederDependencies(
    DatabaseContext Db,
    IMapper Mapper,
    IPasswordHasher<User> PasswordHasher,
    IManglerService ManglerService);
