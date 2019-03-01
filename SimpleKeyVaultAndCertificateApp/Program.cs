using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace SimpleKeyVaultAndCertificateApp
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World from SimpleKeyVaultAccessApp.");

            var builder = new ConfigurationBuilder()
               .SetBasePath(Directory.GetCurrentDirectory())
               .AddJsonFile("appsettings.json", optional: true)
               .AddEnvironmentVariables();
            
            IConfiguration configuration = builder.Build();

            
            //to authenticate to KeyVault leverage the AzureServiceTokenProvider class
            //it fall back from Connection String / Azure Managed Identity and other ways to being able to get a token!
            //this is super-easy way to get a token for many Azure Services
            var azureServiceTokenProvider = new AzureServiceTokenProvider();

            //create KV client
            var keyVaultClient = new KeyVaultClient(
                new KeyVaultClient.AuthenticationCallback(
                    azureServiceTokenProvider.KeyVaultTokenCallback));

            //that's the KV instance we would like to use 
            string vaultBaseUrl = configuration["VaultBaseUrl"].ToString();

            if (string.IsNullOrEmpty(vaultBaseUrl))
            {
                Console.WriteLine("This tool requires the 'VaultBaseUrl' parameter (as Environment Variable or into the appsettings.json)");
                return;
            }

            DateTimeOffset now;

            //Get the first parameter of the app...  hopeful the operating mode
            string appMode = args.FirstOrDefault();

            if (string.IsNullOrEmpty(appMode))
            {
                Console.WriteLine("This tool requires a first parameter (the application mode)");
                return;
            }

            switch (appMode)
            {
                case "GenerateRootCertificate":
                    {
                        RSA rootKeys;

                        rootKeys = IoTCertificateHelper.IoTCertificateHelper.CreateRSAKeysAndSaveInVault(keyVaultClient, vaultBaseUrl, "root");

                        //rootKeys = IoTCertificateHelper.IoTCertificateHelper.GetKeysFromVault(keyVaultClient, vaultBaseUrl, "root");

                        X509Certificate2 rootCertificate = IoTCertificateHelper.IoTCertificateHelper.CreateSelfSignedCertificate(rootKeys, "root");

                        //store in KV the full certificate including the private key (with self signed it include the private key as well)
                        IoTCertificateHelper.IoTCertificateHelper.StoreCertificateInVault(keyVaultClient, vaultBaseUrl, rootCertificate, "root");

                        //let's save on disk teh root certificate, this will not include the private key and it will be uploaded into IoT Hub
                        byte[] rootCertificateBytes = rootCertificate.Export(X509ContentType.Cert);
                        File.WriteAllBytes("root.cer", rootCertificateBytes);

                        break;
                    }


                case "GenerateIntermediateCertificate":
                    {
                        //Now generate an intermediate certificate
                        RSA intermediateKeys;

                        intermediateKeys = IoTCertificateHelper.IoTCertificateHelper.CreateRSAKeysAndSaveInVault(keyVaultClient, vaultBaseUrl, "intermediate");

                        //intermediateKeys = IoTCertificateHelper.IoTCertificateHelper.GetKeysFromVault(keyVaultClient, vaultBaseUrl, "intermediate");

                        var intermediateCSR = IoTCertificateHelper.IoTCertificateHelper.CreateCSR(intermediateKeys, "intermediate", true);

                        now = DateTimeOffset.UtcNow;

                        byte[] intermediateCertificateSerialNumber = IoTCertificateHelper.IoTCertificateHelper.GenerateSerialNumberFromCN("intermediate");

                        //load teh root certificate from Vault
                        X509Certificate2 rootCertificateFromVault = IoTCertificateHelper.IoTCertificateHelper.GetCertificateFromVault(keyVaultClient, vaultBaseUrl, "root");

                        //Sign the Intermediate CSR with the root certificate
                        X509Certificate2 intermediateCertificate = IoTCertificateHelper.IoTCertificateHelper.RespondCSR(intermediateCSR, intermediateCertificateSerialNumber, rootCertificateFromVault, now, now.AddDays(180.0));

                        //merge the intermediate certificate with its own private key!!
                        X509Certificate2 intermediateCertificateWithPrivateKey = intermediateCertificate.CopyWithPrivateKey(intermediateKeys);

                        //store in KV with the private key
                        IoTCertificateHelper.IoTCertificateHelper.StoreCertificateInVault(keyVaultClient, vaultBaseUrl, intermediateCertificateWithPrivateKey, "intermediate");

                        //let's save on disk the intermediate certificate, this will not include the private key and it will be uploaded into IoT Hub 
                        byte[] intermediateCertificateBytes = intermediateCertificate.Export(X509ContentType.Cert);
                        File.WriteAllBytes("intermediate.cer", intermediateCertificateBytes);

                        break;
                    }


                case "VerifyCertificate":
                    {

                        //IoT hub requires verification of Root Certificate using a verification code
                        //basically you need to create a Certificate for a CN = <your verification code>

                        //the second parameter should be the NAME of the certificate that need to sign the verification code
                        //the thirdh parameter is the verification code (used as CN for the certificate generated)
                        string signingCertificateName = args[1];
                        string verificationCode = args[2];

                        X509Certificate2 signingCertificate = IoTCertificateHelper.IoTCertificateHelper.GetCertificateFromVault(keyVaultClient, vaultBaseUrl, signingCertificateName);

                        //Generate teh RSA Key (just generate and then throw away)
                        RSA verificationCodeKey = RSA.Create(2048);

                        var verificationCodeCSR = IoTCertificateHelper.IoTCertificateHelper.CreateCSR(verificationCodeKey, verificationCode, false);

                        now = DateTimeOffset.UtcNow;

                        byte[] verificationCodeSerialNumber = IoTCertificateHelper.IoTCertificateHelper.GenerateSerialNumberFromCN(verificationCode);

                        X509Certificate2 verificationCodeCertificate = IoTCertificateHelper.IoTCertificateHelper.RespondCSR(verificationCodeCSR, verificationCodeSerialNumber, signingCertificate, now, now.AddDays(10.0));

                        //let's write to a file that certificate and upload to IoT Hub to verify the certificate!

                        byte[] verificationCodeCertificateBytes = verificationCodeCertificate.Export(X509ContentType.Cert);
                        //File.WriteAllBytes("rootVerification.cer", verificationCodeCertificateBytes);
                        File.WriteAllBytes($"{signingCertificateName}-verification.cer", verificationCodeCertificateBytes);

                        break;
                    }


                case "CreateDeviceCertificate":
                    {
                        //the second parameter should be the device id 
                        //the thirdh parameter is the name of the certificate that need to sign the certificate
                        string deviceId = args[1];
                        string signingCertificateName = args[2];

                        X509Certificate2 signingCertificate = IoTCertificateHelper.IoTCertificateHelper.GetCertificateFromVault(keyVaultClient, vaultBaseUrl, signingCertificateName);

                        //Now generate keys and sign the certificate for a device using the intermediate

                        RSA deviceKey = IoTCertificateHelper.IoTCertificateHelper.CreateRSAKeysAndSaveInVault(keyVaultClient, vaultBaseUrl, deviceId);

                        CertificateRequest csr = IoTCertificateHelper.IoTCertificateHelper.CreateCSR(deviceKey, deviceId, false);

                        now = DateTimeOffset.UtcNow;

                        byte[] deviceSerialNumber =  IoTCertificateHelper.IoTCertificateHelper.GenerateSerialNumberFromCN(deviceId);

                        X509Certificate2 deviceCertificate = IoTCertificateHelper.IoTCertificateHelper.RespondCSR(csr, deviceSerialNumber, signingCertificate, now, now.AddDays(90.0));

                        //this will merge also the private key of the certificate!
                        X509Certificate2 fullDeviceCertificate = deviceCertificate.CopyWithPrivateKey(deviceKey);

                        IoTCertificateHelper.IoTCertificateHelper.StoreCertificateInVault(keyVaultClient, vaultBaseUrl, fullDeviceCertificate, deviceId);

                        //this time use Pkcs12 format including the private key!
                        byte[] deviceCertificateBytes = fullDeviceCertificate.Export(X509ContentType.Pkcs12);
                        File.WriteAllBytes($"{deviceId}.pfx", deviceCertificateBytes);
      
                        break;
                    }
                    
            }

        }

    }
}
