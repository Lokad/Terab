using System;
using System.Linq;
using Terab.Client;

namespace Terab.SharpConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Api.Init();
            try
            {
                using (var cnx = Api.Connect("127.0.0.1"))
                {
                    //var block = cnx.UtxoGetBlock(Enumerable.Range(0,32).Select( i=>(byte)i).ToArray());
                    //Console.WriteLine("block handle is: " + block.Handle.Value);

                    var blockHandle = cnx.OpenBlock(BlockHandle.Zero, out BlockUcid ucid);
                    Console.WriteLine("block #" + blockHandle.Value+ ": " + ucid.Left.ToString("X8") + ucid.Right.ToString("X8"));
                }
                Console.WriteLine("press any key to exit");
                Console.ReadKey();

            }
            finally
            {
                Api.Shutdown();
            }
        }
    }
}
