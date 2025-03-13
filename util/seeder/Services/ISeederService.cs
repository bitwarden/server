namespace Bit.Seeder.Services;

public interface ISeederService
{
    Task GenerateSeedsAsync(int userCount, int ciphersPerUser, string seedName);
    Task LoadSeedsAsync(string seedName, string? timestamp = null);
    Task GenerateAndLoadSeedsAsync(int userCount, int ciphersPerUser, string seedName);
    Task ExtractSeedsAsync(string seedName);
}
