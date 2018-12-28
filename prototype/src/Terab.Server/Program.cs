// Copyright Lokad 2018 under MIT BCH.
using System;
using System.Diagnostics;
using System.Threading;
using CommandLine;
using Terab.Lib;

namespace Terab.Server
{
    internal class Program
    {
        private static ILog _log;

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments<InitOptions, RunOptions>(args)
                .WithParsed<InitOptions>(Init)
                .WithParsed<RunOptions>(Run);
        }

        public static void Init(InitOptions options)
        {
            const long gb = 1_000_000_000;
            _log = new ConsoleLog();
            _log.Log(LogSeverity.Info, " ### Initialize terab using config file ### ");
            var config = TerabConfigReader.Read(options.ConfigFullPath);
            TerabInstance.InitializeFiles(config, options.Layer1SizeInGB * gb, options.Layer2SizeInGB * gb, _log);
            var instance = new TerabInstance(_log);
            instance.SetupStores(config);
            _log.Log(LogSeverity.Info, " ### Initialization done. ### ");
        }

        public static void Run(RunOptions options)
        {
            _log = new ConsoleLog();
            _log.Log(LogSeverity.Info, " ### Running the server ### ");
            _log.Log(LogSeverity.Info, $"   ProcessID: {Process.GetCurrentProcess().Id}");

            var config = TerabConfigReader.Read(options.ConfigFullPath);

            var terab = new TerabInstance(_log);
            terab.SetupNetwork(config);
            terab.SetupStores(config);
            terab.SetupControllers();

            _log.Log(LogSeverity.Info, $"Starting the server listening to {config.IpAddress} on port {config.Port}");
            terab.Start();

            _log.Log(LogSeverity.Info, $"Server started - press Ctrl-C to exit");

            AppDomain.CurrentDomain.ProcessExit += (sender, args) => _log.Log(LogSeverity.Info, "App domain exit");

            var readLoop = new CancellationTokenSource();
            Console.CancelKeyPress += (o, args) =>
            {
                terab.Stop();
                readLoop.Cancel();
            };
            try
            {
                while (!readLoop.IsCancellationRequested)
                {
                    Console.ReadKey();
                }
            }
            catch (Exception e)
            {
                _log.Log(LogSeverity.Error, "an error occurred");
                _log.Log(LogSeverity.Error, e.Message);
            }
            finally
            {
                _log.Log(LogSeverity.Info, "bye bye");
            }
        }
    }
}