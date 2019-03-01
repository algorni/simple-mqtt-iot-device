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
using Newtonsoft.Json;

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
            //here you should validate Microsoft IoT Hub Certificate against the expected one... (Baltimora Root CA + Micrsoft Intermediate certificates)
            //in this sample assume that iot hub certificates are valid
            return true;
        }

        private static X509Certificate deviceCertificateSelection(object sender, string targetHost, 
            X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        {
            //To authenticate in the IoT Hub just use the Client Certificate that was signed by the root or intermediate certificate
            //uploaded in IoT Hub Certificates list and Validated

            //Just load the certificate from file
            X509Certificate2 deviceCertificate = new X509Certificate2(deviceConfiuration.DeviceCertificatePath);

            return deviceCertificate;
        }

        private static void startDevice()
        {
            //Need to use TLS secure channel to connect to IoT Hub and handle the certificate validation & selection
            MqttClient mqttClient = new MqttClient(deviceConfiuration.IoTHubBrokerHostname, deviceConfiuration.IoTHubBrokerPort,
                true, MqttSslProtocols.TLSv1_2, iotHubCertificateValidation, deviceCertificateSelection);

            //when using Certificates to authenticate into IoT Hub the Password must be empty
            var connectResult = mqttClient.Connect(deviceConfiuration.DeviceId,
                $"{deviceConfiuration.IoTHubBrokerHostname}/{deviceConfiuration.DeviceId}/?api-version=2018-06-30", 
                string.Empty);

            Console.WriteLine($"Is connected: {mqttClient.IsConnected}");


            //Subscribe to Cloud 2 Device Message
            mqttClient.MqttMsgPublishReceived += MqttClient_MqttMsgPublishReceived;
            mqttClient.Subscribe(new[] { $"devices/{deviceConfiuration.DeviceId}/messages/devicebound/#" },
                new[] { uPLibrary.Networking.M2Mqtt.Messages.MqttMsgBase.QOS_LEVEL_AT_LEAST_ONCE });


            //you can even subscribe to Device Twin topic to handle device twin desired state changes....
            //it requires JSON parser as soon as Device Twin are encoded in JSON

            //you can also subscribe to Direct Method topic to handle Direct Methods call from IoT Hub
            //it requires JSON parser as soon as Direct Method payload must be encoded in JSON

            //please visit this doc page to add these functionalities:
            //https://docs.microsoft.com/en-gb/azure/iot-hub/iot-hub-mqtt-support


            //Send a sample Device 2 Cloud message
            string sampleDevice2CloudMessage = JsonConvert.SerializeObject(new { MessagePayload = "Test Message" });
            byte[] sampleDevice2CloudMessageBytes = System.Text.Encoding.UTF8.GetBytes(sampleDevice2CloudMessage);
            mqttClient.Publish($"devices/{deviceConfiuration.DeviceId}/messages/events/", sampleDevice2CloudMessageBytes);


            bool cancelation = false;
            
            //start an infinite loop
            while (!cancelation)
            {
                Task.Delay(1000).Wait();
            }
        }


        /// <summary>
        /// Cloud 2 Device Message received
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void MqttClient_MqttMsgPublishReceived(object sender, uPLibrary.Networking.M2Mqtt.Messages.MqttMsgPublishEventArgs e)
        {
            //Message received from cloud
            string message = System.Text.Encoding.UTF8.GetString(e.Message);



        }
        
    }
}
