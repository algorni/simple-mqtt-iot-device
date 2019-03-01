using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Newtonsoft.Json;
using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace IoTCertificateHelper
{
    public class IoTCertificateHelper
    {

        public static void StoreCertificateInVault(KeyVaultClient keyVaultClient, string vaultBaseUrl, X509Certificate2 certificate, string certificateName)
        {
            byte[] certificateBytes = certificate.Export(X509ContentType.Pkcs12);

            string base64EncodedCertificate = System.Convert.ToBase64String(certificateBytes);

            //save as KV Secret the wole root certificate
            SecretBundle certSetResult = keyVaultClient.SetSecretAsync(vaultBaseUrl, $"cert-{certificateName}", base64EncodedCertificate).Result;
        }

        public static X509Certificate2 GetCertificateFromVault(KeyVaultClient keyVaultClient, string vaultBaseUrl, string certificateName)
        {
            SecretBundle getCertResult = keyVaultClient.GetSecretAsync(vaultBaseUrl, $"cert-{certificateName}").Result;

            byte[] certificateBytes = System.Convert.FromBase64String(getCertResult.Value);

            X509Certificate2 certificateFromBytes = new X509Certificate2(certificateBytes);

            return certificateFromBytes;
        }




        public static X509Certificate2 CreateSelfSignedCertificate(RSA keys, string CN)
        {
            X509Certificate2 certificate = null;

            var certRequest = new CertificateRequest(
               $"CN={CN}",
               keys,
               HashAlgorithmName.SHA256,
               RSASignaturePadding.Pkcs1);
            
            // It is a CA.
            certRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(true, false, 0, false));

            certRequest.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature
                    | X509KeyUsageFlags.KeyEncipherment
                    | X509KeyUsageFlags.KeyCertSign,
                    true));

            // TLS Server EKU
            certRequest.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection
                    {
                        new Oid("1.3.6.1.5.5.7.3.1")
                    },
                    false));

            //Server Authentication(1.3.6.1.5.5.7.3.1)
            //Client Authentication(1.3.6.1.5.5.7.3.2)
            
            DateTimeOffset now = DateTimeOffset.UtcNow;

            certificate = certRequest.CreateSelfSigned(now, now.AddDays(365.25));

            return certificate;
        }

        public static byte[] GenerateSerialNumberFromCN(string CN)
        {
            MD5 md5 = System.Security.Cryptography.MD5.Create();

            byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(CN);

            byte[] hash = md5.ComputeHash(inputBytes);

            return hash;
        }

        public static CertificateRequest CreateCSR(RSA keys, string CN, bool canSignCertificates)
        {  
            var certRequest = new CertificateRequest(
               $"CN={CN}",
               keys,
               HashAlgorithmName.SHA256,
               RSASignaturePadding.Pkcs1);
            
            // Explicitly not a CA.
            certRequest.CertificateExtensions.Add(
                new X509BasicConstraintsExtension(canSignCertificates, false, 0, false));

            if (canSignCertificates)
            {
                certRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature
                        | X509KeyUsageFlags.KeyEncipherment
                        | X509KeyUsageFlags.KeyCertSign,
                        true));

                // TLS Server EKU
                certRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection
                        {
                            new Oid("1.3.6.1.5.5.7.3.1"),
                            new Oid("1.3.6.1.5.5.7.3.2"),
                        },
                        false));
            }
            else
            {
                certRequest.CertificateExtensions.Add(
                    new X509KeyUsageExtension(
                        X509KeyUsageFlags.DigitalSignature
                        | X509KeyUsageFlags.KeyEncipherment,
                        true));

                // TLS Server EKU
                certRequest.CertificateExtensions.Add(
                    new X509EnhancedKeyUsageExtension(
                        new OidCollection
                        {
                            new Oid("1.3.6.1.5.5.7.3.2"),
                        },
                        false));
            }

            //Server Authentication(1.3.6.1.5.5.7.3.1)
            //Client Authentication(1.3.6.1.5.5.7.3.2)

            return certRequest;
        }

        public static X509Certificate2 RespondCSR(CertificateRequest certificateSigningRequest, byte[] serialNumber, X509Certificate2 signingCertificate, DateTimeOffset notBefore, DateTimeOffset notAfter)
        {
            return certificateSigningRequest.Create(signingCertificate, notBefore, notAfter, serialNumber);
        }

        public static RSA GetKeysFromVault(KeyVaultClient keyVaultClient, string vaultBaseUrl, string keyName)
        {
            SecretBundle getKeyResult = keyVaultClient.GetSecretAsync(vaultBaseUrl, $"key-{keyName}").Result;

            Microsoft.Azure.KeyVault.WebKey.JsonWebKey jsonWebKey =
                JsonConvert.DeserializeObject<Microsoft.Azure.KeyVault.WebKey.JsonWebKey>(getKeyResult.Value);

            RSA rsaKeys = jsonWebKey.ToRSA(true);

            return rsaKeys;
        }

        public static RSA CreateRSAKeysAndSaveInVault(KeyVaultClient keyVaultClient, string vaultBaseUrl, string keyName)
        {
            //Generate teh RSA Key
            RSA rsaKeys = RSA.Create(2048);

            Microsoft.Azure.KeyVault.WebKey.JsonWebKey rsaKeysJson = new Microsoft.Azure.KeyVault.WebKey.JsonWebKey(rsaKeys, true);

            //save as KV Secret both public / private part of the Keys
            SecretBundle keySetResult = keyVaultClient.SetSecretAsync(vaultBaseUrl, $"key-{keyName}", rsaKeysJson.ToString()).Result;

            return rsaKeys;
        }
    }
}
