using Bit.Core.Enums;
using Bit.Core.KeyManagement.Models.Data;

namespace Bit.Api.KeyManagement.Models.Requests;

/// <summary>
/// Note: This should not be confused with the auth owned KDF request model, which represents a change to the KDF requested by a client. This model
/// is merely a sub-model for other request models to transport KDF information in a non-flat deeply nested object.
/// </summary>
public class InnerKdfRequestModel
{
    public required KdfType KdfType { get; init; }
    public required int Iterations { get; init; }
    public int? Memory { get; init; }
    public int? Parallelism { get; init; }

    public KdfSettings ToData()
    {
        return new KdfSettings
        {
            KdfType = KdfType,
            Iterations = Iterations,
            Memory = Memory,
            Parallelism = Parallelism
        };
    }
}
