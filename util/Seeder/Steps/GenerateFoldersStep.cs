using Bit.Seeder.Data.Distributions;
using Bit.Seeder.Factories;
using Bit.Seeder.Pipeline;

namespace Bit.Seeder.Steps;

/// <summary>
/// Generates folders for each user based on a realistic distribution, encrypted with each user's symmetric key.
/// </summary>
internal sealed class GenerateFoldersStep : IStep
{
    public void Execute(SeederContext context)
    {
        var generator = context.RequireGenerator();
        var userDigests = context.Registry.UserDigests;
        var distribution = FolderCountDistributions.Realistic;

        for (var index = 0; index < userDigests.Count; index++)
        {
            var digest = userDigests[index];
            var range = distribution.Select(index, userDigests.Count);
            var count = range.Min + (index % Math.Max(range.Max - range.Min + 1, 1));
            var folderIds = new List<Guid>(count);

            for (var i = 0; i < count; i++)
            {
                var folder = FolderSeeder.Create(
                    digest.UserId,
                    digest.SymmetricKey,
                    generator.Folder.GetFolderName(i));
                context.Folders.Add(folder);
                folderIds.Add(folder.Id);
            }

            context.Registry.UserFolderIds[digest.UserId] = folderIds;
        }
    }
}
