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
    public static string OpaqueKe3Ristretto3DHArgonSuite = "OPAQUE_3_RISTRETTO255_OPRF_RISTRETTO255_KEGROUP_3DH_KEX_ARGON2ID13_KSF";

    [Required]
    public string CipherSuite { get; set; }
    [Required]
    public Argon2KsfParameters Argon2Parameters { get; set; }

    public Bitwarden.Opaque.CipherConfiguration ToNativeConfiguration()
    {
        if (CipherSuite == OpaqueKe3Ristretto3DHArgonSuite)
        {
            return new Bitwarden.Opaque.CipherConfiguration
            {
                OpaqueVersion = 3,
                OprfCs = Bitwarden.Opaque.OprfCs.Ristretto255,
                KeGroup = Bitwarden.Opaque.KeGroup.Ristretto255,
                KeyExchange = Bitwarden.Opaque.KeyExchange.TripleDH,
                Ksf = new Bitwarden.Opaque.Ksf
                {
                    Algorithm = Bitwarden.Opaque.KsfAlgorithm.Argon2id,
                    Parameters = new Bitwarden.Opaque.KsfParameters
                    {
                        Iterations = Argon2Parameters.Iterations,
                        Memory = Argon2Parameters.Memory,
                        Parallelism = Argon2Parameters.Parallelism
                    }
                }
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
