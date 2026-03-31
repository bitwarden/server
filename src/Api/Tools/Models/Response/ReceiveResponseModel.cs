using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Bit.Core.Models.Api;
using Bit.Core.Tools.Entities;
using Bit.Core.Tools.Models.Data;

namespace Bit.Api.Tools.Models.Response;

public class ReceiveResponseModel : ResponseModel
{
    [SetsRequiredMembers]
    public ReceiveResponseModel(Receive receive) : base("receive")
    {
        Id = receive.Id;

        Name = receive.Name;
        var receiveData = JsonSerializer.Deserialize<ReceiveData>(receive.Data);
        Files = receiveData!.Files.Select(f => new ReceiveFileModel(f));

        UserKeyWrappedSharedContentEncryptionKey = receive.UserKeyWrappedSharedContentEncryptionKey;
        UserKeyWrappedPrivateKey = receive.UserKeyWrappedPrivateKey;
        ScekWrappedPublicKey = receive.ScekWrappedPublicKey;
        Secret = receive.Secret;
        UploadCount = receive.UploadCount;
        CreationDate = receive.CreationDate;
        RevisionDate = receive.RevisionDate;
        ExpirationDate = receive.ExpirationDate;
    }
    /// <summary>
    /// Uniquely identifies this Receive.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Label for the Receive. Encrypted.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Metadata for each file uploaded to this Receive.
    /// </summary>
    public IEnumerable<ReceiveFileModel> Files { get; set; }

    /// <summary>
    /// The shared content encryption key (SCEK) wrapped by the owners userKey.
    /// </summary>
    public required string UserKeyWrappedSharedContentEncryptionKey { get; set; }

    /// <summary>
    /// The private key wrapped by the owners userKey.
    /// </summary>
    public required string UserKeyWrappedPrivateKey { get; set; }

    /// <summary>
    /// The public key wrapped by the shared content encryption key (SCEK).
    /// </summary>
    public required string ScekWrappedPublicKey { get; set; }

    /// <summary>
    /// A randomly generated value embedded in the Receive link.
    /// It restricts upload access to clients that possess the secret.
    /// Generated server-side, protected via ASP.NET Data Protection, and returned on a successful POST.
    /// </summary>
    [MaxLength(300)]
    public required string Secret { get; set; }

    /// <summary>
    /// Number of times the Receive has been used to upload a file.
    /// </summary>
    /// <remarks>
    /// This value is owned by the server. Clients cannot alter it.
    /// </remarks>
    public int UploadCount { get; set; }

    /// <summary>
    /// The date this Receive was created.
    /// </summary>
    public DateTime CreationDate { get; internal set; }

    /// <summary>
    /// The date this Receive was last modified.
    /// </summary>
    public DateTime RevisionDate { get; internal set; }

    /// <summary>
    /// The date this Receive becomes unavailable to potential uploaders.
    /// </summary>
    public DateTime ExpirationDate { get; set; }
}
