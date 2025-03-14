using System.ComponentModel.DataAnnotations;

namespace Bit.Api.Auth.Models.Request.Opaque;


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
                KSF = new Bitwarden.OPAQUE.Argon2id(Argon2Parameters.iterations, Argon2Parameters.memory, Argon2Parameters.parallelism)
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
    public int memory;
    [Required]
    public int iterations;
    [Required]
    public int parallelism;
}
