using System;
using System.Configuration;

namespace MonaLisaConsole
{
    class MonaLisaConsole
    {
        static void StartConsole()
        {
            int port = 5555;            // default Port 
            Int32.TryParse(ConfigurationManager.AppSettings["Port"], out port);

            string hostName = ConfigurationManager.AppSettings["HostName"];
            if (string.IsNullOrWhiteSpace(hostName))
                hostName = "*LoopBack"; // default Host Name

            Controller controller = new Controller(hostName, port);
            controller.Execute();
        }

        static void Main(string[] args)
        {
            StartConsole();
        }
    }
}
