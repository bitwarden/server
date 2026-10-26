
using Bit.Core.Vault.Models.Data;

namespace Dirt.Reports.ReportFeatures.Interfaces;

/// <summary>
///  All commands to update user browser health excluded ciphers.
/// </summary>
public interface IUpdateCipherBrowserHealthExclusionCommand
{
  /// <summary>
  /// Excludes the specified cipher from the user's browser health report. 
  /// <param name="cipherId">The ID of the cipher to exclude.</param>
  /// <param name="userId">The ID of the user.</param>
  /// </summary>
  Task<CipherDetails> ExcludeAsync(Guid cipherId, Guid userId);

  /// <summary>
  /// Unexcludes the specified cipher from the user's browser health report.
  /// <param name="cipherId">The ID of the cipher to unexclude.</param>
  /// <param name="userId">The ID of the user.</param>
  /// </summary>
  Task<CipherDetails> UnexcludeAsync(Guid cipherId, Guid userId);
}