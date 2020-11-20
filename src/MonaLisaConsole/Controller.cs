using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MonaLisaConsole
{
    public class Controller
    {
        string hostName;
        int port;
        Exception lastSocketException;
        SocketStream socketStream;

        public Controller(string hostName, int port)
        {
            this.hostName = hostName;
            this.port = port;
        }

        public bool Connect()
        {
            IPAddress hostIP;
            hostIP = ResolveIpAddress(hostName);

            Socket socket = new Socket(AddressFamily.InterNetwork,
                                         SocketType.Stream,
                                         ProtocolType.Tcp);

            IPEndPoint ep = new IPEndPoint(hostIP, port);
            try
            {
                socket.Connect(ep);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                lastSocketException = e;
                Console.WriteLine($"Could not Connect. {lastSocketException.Message}");
            }
            socketStream = new SocketStream(socket);
            if (!socketStream.Connected)
                return false;
            return true;
        }

        private static IPAddress ResolveIpAddress(string forHostName)
        {
            IPAddress hostIP;
            if (string.IsNullOrWhiteSpace(forHostName) || string.Compare(forHostName, "*LoopBack", true) == 0)
            {
                hostIP = IPAddress.Loopback;
            }
            else if (string.Compare(forHostName, "*LOCAL", true) == 0)
            {
                using (Socket throwAwaySocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    throwAwaySocket.Connect("8.8.8.8", 65530);
                    IPEndPoint endPoint = throwAwaySocket.LocalEndPoint as IPEndPoint;
                    hostIP = endPoint.Address;
                }
            }
            else
            {
                IPHostEntry HostEntry = Dns.GetHostEntry(forHostName);
                if (HostEntry.AddressList.Length == 1)
                    hostIP = HostEntry.AddressList[0];
                else
                    hostIP = HostEntry.AddressList[1];
            }

            return hostIP;
        }

        internal void Execute()
        {
            Console.WriteLine($"Consoling {hostName} using port {port}");

            string previousCommandLine = "HLP";
            while (true)
            {
                Console.Write($"Monarch Command (., BYE, GJL, HLP, RJS, SMS):> ");
                string commandLine = Console.ReadLine().Trim();
                if (string.IsNullOrWhiteSpace(commandLine) || commandLine == ".")
                    commandLine = previousCommandLine;
                else if (commandLine.Length < 3)
                    continue;
                else
                    previousCommandLine = commandLine;

                string command = commandLine.Substring(0, 3).ToUpper();
                if (command == "BYE")
                    break;

                string parms = commandLine.Substring(3).TrimStart();
                switch (command)
                {
                    case "GJL":
                        GetJobList();
                        break;
                    case "HLP":
                        Help();
                        break;
                    case "RJS":
                        RequestJobShutdown(parms);
                        break;
                    case "SMS":
                        StopMonaServer(parms);
                        break;
                }
            }
        }

        private void Help()
        {
            Console.WriteLine();
            Console.WriteLine("MonaLisa Console Help");
            Console.WriteLine("Available Commands:");
            Console.WriteLine("  BYE = Exit Mona Lisa Console");
            Console.WriteLine("  GJL = Get Job List");
            Console.WriteLine("  HLP = Print this Help");
            Console.WriteLine("  RJS = Request Job Shutdown");
            Console.WriteLine("        JobNumber [,Controlled Seconds]");
            Console.WriteLine("  SMS = Stop Mona Server");
            Console.WriteLine("        [Controlled Seconds]");
            Console.WriteLine();
        }


        private void StopMonaServer(string parms)
        {
            if (!Connect())
                return;
            SendRequest("SMS", parms);    // Request Stop Monarch Server ([controlledSeconds])
            EndRequest();
        }

        public void EndRequest()
        {
            socketStream.Close();
        }

        public void RequestJobShutdown(string parms)
        {
            if (!Connect())
                return;
            SendRequest("RJS", parms);    // Request Job Shutdown (JobID [,controlledSeconds])
            EndRequest();
        }

        public void GetJobList()
        {
            if (!Connect())
                return;

            string rjsRequestParms = $"";
            SendRequest("GJL", rjsRequestParms);    // Get Job List
            int rc = socketStream.ReceiveInt32();
            if (rc < 0)
            {
                Console.WriteLine("Error GJL ", rc);
                return;
            }
            for (int i=0; i<rc; i++)
            {
                string jobName    = socketStream.ReceiveString();
                string jobUser    = socketStream.ReceiveString();
                int jobNumber     = socketStream.ReceiveInt32();
                string status     = socketStream.ReceiveString();
                int masProcessId  = socketStream.ReceiveInt32();
                string webServer  = socketStream.ReceiveString();
                string browserClient = socketStream.ReceiveString();
                Console.WriteLine($">{jobName} {jobUser} {jobNumber} {status} ");
                Console.WriteLine($"    MAS Process Id: {masProcessId}");
                Console.WriteLine($"    {webServer}");
                Console.WriteLine($"    {browserClient}");
                Console.WriteLine();
            }
            Console.WriteLine();
            EndRequest();
        }

        public void SendRequest(string operation, string parameters)
        {
            socketStream.Send(operation);
            socketStream.Send(parameters);
        }
    }
}
