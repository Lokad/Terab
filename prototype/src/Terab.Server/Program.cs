using System;
using System.Collections.Concurrent;
using System.Net;
using System.Threading;
using Terab.UTXO.Core.Networking;

namespace Terab.Server
{
    internal class Program
    {
        private static void Main()
        {
            Console.WriteLine(" ### Running the server ### ");
            var queue = new ConcurrentQueue<ClientConnection>();
            var config = new Configuration
            {
                ListenAddress = IPAddress.Any,
                ListenPort = 15000 
                // TODO: [vermorel] Magic number should be isolated in file name 'Config.cs'
                // (no need to go for App.config).
            };
            var theListener = new Listener(queue, config);
            var cancellationToken = new CancellationToken(false);
            Console.WriteLine($"[INFO] {DateTime.Now} >>> " +
                              $"Starting the server listening to {config.ListenAddress} on port {config.ListenPort}");
            theListener.Listen(cancellationToken);
        }
    }
}
