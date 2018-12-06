using IotHubSdkless;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Reflection;
using System.Linq;
using uPLibrary.Networking.M2Mqtt;

namespace SimpleMqttIotDeviceConsoleApp
{
    class Program
    {   static void Main(string[] args)
        {
            string versionNumber = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine(" Simple MQTT IoT Device Console App v " + versionNumber);
            Console.WriteLine("-------------------------------------------");
            Console.WriteLine();
            
            var deviceConfigurationJsonFile = args.FirstOrDefault();

            if (string.IsNullOrEmpty(deviceConfigurationJsonFile))
            {
                Console.WriteLine("This tool requires a command line parameter (the path of the Device configuration JSON");
            }
            
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile(deviceConfigurationJsonFile, optional: true, reloadOnChange: true);

            IConfigurationRoot configuration = builder.Build();
            
            var deviceConfiuration = configuration.GetSection("DeviceConfiuration").Get<DeviceConfiuration>();

            if (deviceConfiuration != null)
            {
                Console.WriteLine("Device Configuration loaded!");

                startDevice(deviceConfiuration); 
            }
            else
            {
                Console.WriteLine("Something Wrong happened while loading Device Configuration!");
            }
        }



        private static void startDevice(DeviceConfiuration deviceConfiuration)
        {
            //MqttClient mqttClient = new MqttClient()


        }
    }
}
