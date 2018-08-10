using System;

namespace Terab.Client
{
    public static class Api
    {
        public static void Init()
        {
            var err = PInvokes.terab_initialize();
            if (err != PInvokes.ReturnCode.SUCCESS)
            {
                throw new Exception("Terab initialization failed with code " + err);
            }
        }

        public static void Shutdown()
        {
            PInvokes.terab_shutdown();
        }

        public static Connection Connect(string connectionString)
        {
            return new Connection(connectionString);
        }
    }
}