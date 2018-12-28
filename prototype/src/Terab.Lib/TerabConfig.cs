// Copyright Lokad 2018 under MIT BCH.
using System;
using System.IO;
using System.Runtime.Serialization;

namespace Terab.Lib
{
    /// <summary>
    /// Permits reading out a well-formed xml document representing a Terab
    /// configuration, which will then be verified against minimum criteria
    /// for the values. A sample xml file would be:
    /// 
    /// <?xml version="1.0" encoding="utf-8" ?>
    /// <TerabConfig>
    ///     <ipAddress> 0.0.0.0 or 127.0.0.1 </ipAddress>
    ///     <port> 65536 >= x >= 1025 </port>
    ///     <layer1Path>/path/to/dir</layer1Path>
    ///     <layer2Path>/path/to/dir</layer2Path>
    ///     <layer3Path>/path/to/dir</layer3Path>
    /// </TerabConfig>
    ///
    /// None of the fields is obligatory, however their order has to be as above.
    /// </summary>
    [DataContract(Namespace = "", Name = "TerabConfig")]
    public class TerabConfig
    {
        [DataMember(Name = "ipAddress", IsRequired = false, Order = 2)]
        public string IpAddress { get; set; } = "127.0.0.1";

        [DataMember(Name = "port", IsRequired = false, Order = 3)]
        public int Port { get; set; } = Constants.DefaultPort;

        /// <summary> First layer of the Sozu table. Choose a path that
        /// provides maximal I/O performance. </summary>
        [DataMember(Name = "layer1Path", IsRequired = true, Order = 4)]
        public string Layer1Path { get; set; }

        /// <summary> Can be omitted. If omitted, there is no layer 2. </summary>
        [DataMember(Name = "layer2Path", IsRequired = false, Order = 5)]
        public string Layer2Path { get; set; } = string.Empty;

        /// <summary> Overflowing layer intended as the final layer.
        /// No pre-allocation. We suggest to use a sub-folder of the
        /// path of the layer 1.</summary>
        [DataMember(Name = "layer3Path", IsRequired = false, Order = 6)]
        public string Layer3Path { get; set; } = string.Empty;

    }

    public static class TerabConfigReader
    {
        public static TerabConfig Read(string configPath)
        {
            using (var configReader = new FileStream(configPath, FileMode.Open))
            {
                var ser = new DataContractSerializer(typeof(TerabConfig));
                var config = (TerabConfig) ser.ReadObject(configReader);

                ValidateConfig(config);

                return config;
            }
        }

        private static void ValidateConfig(TerabConfig config)
        {
            // ipAddress - 0.0.0.0 / 127.0.0.1
            if (!(config.IpAddress.Equals("0.0.0.0") || config.IpAddress.Equals("127.0.0.1")))
                throw new ArgumentException(
                    $"The only IP Addresses allowed are 0.0.0.0 and 127.0.0.1, but was {config.IpAddress}.");

            // port > 1024, < 2^16 (65536)
            if (config.Port < 1025 || config.Port > 65536)
                throw new ArgumentException($"The port has to be between 1025 and 65536, but was {config.Port}.");
        }
    }
}