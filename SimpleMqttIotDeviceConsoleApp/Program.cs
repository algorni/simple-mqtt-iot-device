using IotHubSdkless;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using uPLibrary.Networking.M2Mqtt;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Threading.Tasks;

namespace SimpleMqttIotDeviceConsoleApp
{
    class Program
    {
        private static DeviceConfiuration deviceConfiuration;

        static void Main(string[] args)
        {
            string versionNumber = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            Console.WriteLine("-------------------------------------------");
            Console.WriteLine(" Simple MQTT IoT Device Console App v " + versionNumber);
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine();

           
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            IConfigurationRoot configuration = builder.Build();

            deviceConfiuration = configuration.GetSection("DeviceConfiuration").Get<DeviceConfiuration>();

            if (deviceConfiuration != null)
            {
                Console.WriteLine("Device Configuration loaded!");

                startDevice();
            }
            else
            {
                Console.WriteLine("Something Wrong happened while loading Device Configuration!");
            }
        }

        private static bool iotHubCertificateValidation(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            //here you should validate Microsoft IoT Hub Certificate against the expected one...
            return true;
        }

        private static X509Certificate deviceCertificateSelection(object sender, string targetHost, 
            X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            X509Certificate2 deviceCertificate = new X509Certificate2(deviceConfiuration.DeviceCertificatePath);

            return deviceCertificate;
        }

        private static void startDevice()
        {
            MqttClient mqttClient = new MqttClient(deviceConfiuration.IoTHubBrokerHostname, deviceConfiuration.IoTHubBrokerPort,
                true, MqttSslProtocols.TLSv1_2, iotHubCertificateValidation, deviceCertificateSelection);

            var connectResult = mqttClient.Connect(deviceConfiuration.DeviceId,
                $"{deviceConfiuration.IoTHubBrokerHostname}/{deviceConfiuration.DeviceId}/?api-version=2018-06-30", 
                string.Empty);

            Console.WriteLine($"Is connected: {mqttClient.IsConnected}");


            //Subscribe to Cloud 2 Device Message
            mqttClient.MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;
            mqttClient.Subscribe(new[] { $"devices/{deviceConfiuration.DeviceId}/messages/devicebound/#" },
                new[] { uPLibrary.Networking.M2Mqtt.Messages.MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });
           


            bool cancelation = false;
            
            //start an infinite loop
            while (!cancelation)
            {
                Task.Delay(1000).Wait();
            }
        }

        private static void MqttClient_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            string message = System.Text.Encoding.UTF8.GetString(e.Message);



        }
        
    }
}
