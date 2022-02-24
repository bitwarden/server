using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bit.Core.Enums;

namespace Bit.Core.Models.Business
{
    // Derived from https://gist.github.com/jbtule/4336842#file-aesthenhmac-cs
    public class SymmetricKeyProtectedString
    {
        public string EncryptedString =>
            $"{EncryptionType.AesCbc256_HmacSha256_B64}.{Convert.ToBase64String(IV)}|{Convert.ToBase64String(Data)}|{Convert.ToBase64String(Mac)}";
        public byte[] IV { get; private set; }
        public byte[] Data { get; private set; }
        public byte[] Mac { get; private set; }

        private SymmetricKeyProtectedString(byte[] data, byte[] iv, byte[] mac)
        {
            IV = iv;
            Data = data;
            Mac = mac;
        }

        public SymmetricKeyProtectedString(string encryptedString)
        {
            if (!encryptedString.StartsWith($"{EncryptionType.AesCbc256_HmacSha256_B64}."))
            {
                throw new Exception("Invalid installation protected string");
            }

            var encPieces = encryptedString[$"{EncryptionType.AesCbc256_HmacSha256_B64}.".Length..].Split("|");
            if (encPieces.Length != 3)
            {
                throw new Exception("Invalid installation protected string");
            }

            try
            {
                IV = Convert.FromBase64String(encPieces[0]);
                Data = Convert.FromBase64String(encPieces[1]);
                Mac = Convert.FromBase64String(encPieces[2]);
            }
            catch (Exception e)
            {
                throw new Exception("Invalid installation protected string", e);
            }
        }

        public string Decrypt(string password)
        {
            var (encKey, macKey) = DeriveKey(password);

            using (var hmac = new HMACSHA256(macKey))
            {
                var calcMac = hmac.ComputeHash(Data);

                //Compare Tag with constant time comparison
                var compare = 0;
                for (var i = 0; i < Mac.Length; i++)
                {
                    compare |= Mac[i] ^ calcMac[i];
                }

                if (compare != 0)
                {
                    return null;
                }

                using (var aes = GetAesManaged())
                {
                    using (var decryptor = aes.CreateDecryptor(encKey, IV))
                    using (var clearStream = new MemoryStream())
                    using (var reader = new StreamReader(clearStream))
                    using (var decryptorStream = new CryptoStream(clearStream, decryptor, CryptoStreamMode.Write))
                    using (var binaryWriter = new BinaryWriter(decryptorStream))
                    {
                        binaryWriter.Write(Data);
                        binaryWriter.Flush();
                        decryptorStream.FlushFinalBlock();
                        clearStream.Position = 0;

                        return reader.ReadToEnd();
                    }
                }
            }
        }

        public static SymmetricKeyProtectedString Encrypt(string clearText, string password)
        {
            var (encKey, macKey) = DeriveKey(password);
            byte[] data;
            byte[] mac;
            byte[] iv;

            // Encrypt data
            using (var aes = GetAesManaged())
            {
                aes.GenerateIV();
                iv = aes.IV;

                using (var encryptor = aes.CreateEncryptor(encKey, iv))
                using (var encStream = new MemoryStream())
                using (var cryptoStream = new CryptoStream(encStream, encryptor, CryptoStreamMode.Write))
                using (var binaryWriter = new BinaryWriter(cryptoStream))
                {
                    binaryWriter.Write(Encoding.UTF8.GetBytes(clearText));
                    binaryWriter.Flush();
                    cryptoStream.FlushFinalBlock();

                    data = encStream.ToArray();
                }

            }

            // Create hmac
            using (var hmac = new HMACSHA256(macKey))
            {
                mac = hmac.ComputeHash(data);
            }

            return new SymmetricKeyProtectedString(data, iv, mac);
        }

        private static (byte[] encKey, byte[] macKey) DeriveKey(string password)
        {
            var key = HKDF.DeriveKey(HashAlgorithmName.SHA256, Encoding.UTF8.GetBytes(password), 512);
            return (key.Take(32).ToArray(), key.Skip(32).ToArray());
        }

        private static AesManaged GetAesManaged() =>
            new AesManaged
            {
                KeySize = 256,
                BlockSize = 128,
                Mode = CipherMode.CBC,
                Padding = PaddingMode.PKCS7
            };
    }
}
