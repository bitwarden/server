
using Bit.Core.Dirt.Repositories;
using Bit.Core.Dirt.Reports.ReportFeatures.Interfaces;
using Bit.Core.Vault.Models.Data;
using Bit.Core.Vault.Repositories;

namespace Bit.Core.Dirt.Reports.ReportFeatures;


public class UpdateCipherBrowserHealthExclusionCommand : IUpdateCipherBrowserHealthExclusionCommand
{

  private readonly ICipherRepository _cipherRepository;
  private readonly ICipherBrowserHealthExclusionRepository _cipherBrowserHealthExclusionRepository;

  public UpdateCipherBrowserHealthExclusionCommand(ICipherRepository cipherRepository, ICipherBrowserHealthExclusionRepository cipherBrowserHealthExclusionRepository)
  {
    _cipherRepository = cipherRepository;
    _cipherBrowserHealthExclusionRepository = cipherBrowserHealthExclusionRepository;
  }

  public async Task<CipherDetails> ExcludeAsync(Guid cipherId, Guid userId)
  {
    // TODO: update/move implementation here for exclude 
    throw new NotImplementedException();
  }

  public async Task<CipherDetails> UnexcludeAsync(Guid cipherId, Guid userId)
  {
    var cipher = await _cipherRepository.GetByIdAsync(cipherId, userId);

    if (cipher == null)
    {
      throw new Exception("Cipher not found for user.");
    }

    // early return to avoid unnecessary call to repository to clear exclusion when cipher is not currently excluded
    if (!cipher.ExcludedFromBrowserHealth)
    {
      return cipher;
    }

    var ciphersToExclude = new[] { cipherId };
    var result = await _cipherBrowserHealthExclusionRepository.UnexcludeAsync(ciphersToExclude, userId);

    if (result != ciphersToExclude.Length)
    {
      throw new Exception("Failed to remove cipher exclusion for user.");
    }

    cipher.ExcludedFromBrowserHealth = false;
    return cipher;
  }
}