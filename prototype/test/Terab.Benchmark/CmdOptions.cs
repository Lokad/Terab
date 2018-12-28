// Copyright Lokad 2018 under MIT BCH.
using CommandLine;
using Terab.Lib;

namespace Terab.Benchmark
{
    [Verb("init", HelpText = "Initialize storage files for a local benchmark.")]
    public class InitOptions
    {
        [Option("layer1Path", Required = true, HelpText = "Folder for layer1 storage.")]
        public string Layer1Path { get; set; }

        [Option("layer1", Required = false, Default = 48.0, HelpText = "First layer storage capacity in GB.")]
        public double Layer1SizeInGB { get; set; }
    }

    [Verb("run", HelpText = "Run a benchmark against a local pre-initialized storage.")]
    public class RunOptions
    {
        [Option("layer1Path", Required = true, HelpText = "Folder for layer1 storage.")]
        public string Layer1Path { get; set; }
    }

    [Verb("rrun", HelpText = "Run a benchmark against a remote Terab instance.")]
    public class RRunOptions
    {
        [Option("ipAddress", Required = true, HelpText = "IP Address of the Terab instance.")]
        public string IpAddress { get; set; }

        [Option("port", Required = false, HelpText = "Port of the Terab instance.", Default = Constants.DefaultPort)]
        public int Port { get; set; }
    }
}
