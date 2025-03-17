using System.ComponentModel.DataAnnotations;

namespace Bit.Core.Auth.Models.Api.Request.Opaque;


public class OpaqueRegistrationStartRequest
{
    [Required]
    public string RegistrationRequest { get; set; }
    [Required]
    public CipherConfiguration CipherConfiguration { get; set; }
}

public class CipherConfiguration
{
    static string OpaqueKe3Ristretto3DHArgonSuite = "OPAQUE_3_RISTRETTO255_OPRF_RISTRETTO255_KEGROUP_3DH_KEX_ARGON2ID13_KSF";

    [Required]
    public string CipherSuite { get; set; }
    [Required]
    public Argon2KsfParameters Argon2Parameters { get; set; }

    public Bitwarden.OPAQUE.CipherConfiguration ToNativeConfiguration()
    {
        if (CipherSuite == OpaqueKe3Ristretto3DHArgonSuite)
        {
            return new Bitwarden.OPAQUE.CipherConfiguration
            {
                OprfCS = Bitwarden.OPAQUE.OprfCS.Ristretto255,
                KeGroup = Bitwarden.OPAQUE.KeGroup.Ristretto255,
                KeyExchange = Bitwarden.OPAQUE.KeyExchange.TripleDH,
                KSF = new Bitwarden.OPAQUE.Argon2id(Argon2Parameters.Iterations, Argon2Parameters.Memory, Argon2Parameters.Parallelism)
            };
        }
        else
        {
            throw new Exception("Unsupported cipher suite");
        }
    }
}

public class Argon2KsfParameters
{
    // Memory in KiB
    [Required]
    public int Memory { get; set; }
    [Required]
    public int Iterations { get; set; }
    [Required]
    public int Parallelism { get; set; }
}
