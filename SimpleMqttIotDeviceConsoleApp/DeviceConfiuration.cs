using System;

namespace IotHubSdkless
{
    public class DeviceConfiuration
    {
        public string DeviceId { get; set; }

        public string IoTHubBrokerHostname { get; set; }
        public int IoTHubBrokerPort { get; set; }

        public string IoTHubCertificatePath { get; set; }

        public string DeviceCertificatePath { get; set; }
    }
}
