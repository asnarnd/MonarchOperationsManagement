using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Collections;
using System.Text;
using System.Net;
using System.Net.Sockets;

namespace MonaLisaConsole
{
    public class SocketStream
    {
        public class ExceptionClosed : System.Exception
        {
        }


        Socket socket;

        public SocketStream(Socket socket)
        {
            this.socket = socket;
        }

        public bool Connected
        {
            get
            {
                return (socket != null && socket.Connected);
            }
        }

        public void Close()
        {
            if (Connected)
            {
                // socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket = null;
            }
        }

        public void Send(UInt32 num)
        {
            byte[] numBytes = new byte[] { (byte)(num >> 24), (byte)(num >> 16), (byte)(num >> 8), (byte)num };
            socket.Send(numBytes);
        }

        public void Send(Int32 num)
        {
            Send((UInt32)num);
        }

        public UInt32 ReceiveUInt32()
        {
            UInt32 num;
            byte[] bytes = ReceiveBytes(4);
            num = ((UInt32)bytes[0] << 24) + ((UInt32)bytes[1] << 16) + ((UInt32)bytes[2] << 8) + (UInt32)bytes[3];
            return num;
        }

        public Int32 ReceiveInt32()
        {
            return (Int32)ReceiveUInt32();
        }

        public void Send(string str)
        {
            byte[] bytes = System.Text.Encoding.UTF8.GetBytes(str);
            Send(bytes);
        }

        public string ReceiveString()
        {
            byte[] bytes = ReceiveBytes();
            string str = System.Text.Encoding.UTF8.GetString(bytes);
            return str;
        }

        public void Send(byte[] bytes)
        {
            Send(bytes.Length);
            socket.Send(bytes);
        }

        public byte[] ReceiveBytes(int length)
        {
            byte[] bytes = new byte[length];
            int offset = 0;
            while (length > 0)
            {
                if (!socket.Connected)
                    throw new ExceptionClosed();

                int received = socket.Receive(bytes, offset, length, SocketFlags.None);
                if (received == 0)
                    throw new ExceptionClosed();

                offset += received;
                length -= received;
            }
            return bytes;
        }

        public byte[] ReceiveBytes()
        {
            int length = ReceiveInt32();
            return ReceiveBytes(length);
        }

    }

    public class YellowSocketDevice
    {
        string assemblyPath;
        string myJobClass;
        string jobName;
        string hostName;
        int port;

        internal SocketStream socketStream;
        public int JobNumber { get; private set; }
        Exception lastSocketException;
        internal Exception LastSocketException { get { return lastSocketException; } }

        public bool IsConnected { get { return socketStream != null && socketStream.Connected; } }

        public YellowSocketDevice(string assemblyPath, string myJobClass, string jobName, string hostName, int port)
            : this(-1, hostName, port)
        {
            this.assemblyPath = assemblyPath;
            this.myJobClass = myJobClass;
            this.jobName = jobName;
        }

        public YellowSocketDevice(int jobNumber, string hostName, int port)
        {
            JobNumber = jobNumber;
            this.port = port;
            this.hostName = hostName;
            if (string.Compare(hostName, "*InProcess", true) == 0)
            {
                this.hostName = "*LoopBack";
            }
        }

        public bool Connect()
        {
            IPAddress hostIP;
            if (string.Compare(hostName, "*LoopBack", true) == 0)
            {
                hostIP = IPAddress.Loopback;
            }
            else if (string.Compare(hostName, "*LOCAL", true) == 0)
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
                IPHostEntry HostEntry = Dns.GetHostEntry(hostName);
                if (HostEntry.AddressList.Length == 1)
                    hostIP = HostEntry.AddressList[0];
                else
                    hostIP = HostEntry.AddressList[1];
            }

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
            }
            socketStream = new SocketStream(socket);
            if (!socketStream.Connected)
                return false;
            return true;
        }

        public void EndRequest()
        {
            socketStream.Close();
        }

        public void RequestShutdown(int controlledSeconds)
        {
            if (!Connect())
                return;

            string rjsRequestParms = string.Format("{0}", controlledSeconds);
            SendRequest("RJS", rjsRequestParms);    // Request Job Shutdown
            EndRequest();
        }

        public void SendRequest(string operation, string parameters)
        {
            if (JobNumber == -1)
                throw new Exception("Attempted to SendRequest but have no JobNumber");

            string request = string.Format("{0},{1},{2}", operation, JobNumber, parameters);
            socketStream.Send(request);
        }

        public int StartNewJob()
        {
            if (Connect())
            {
                string initialParms = "SNJ," + assemblyPath + "," + myJobClass + "," + jobName;
                socketStream.Send(initialParms);
                JobNumber = socketStream.ReceiveInt32();
                return JobNumber;
            }

            throw new System.Exception($"Could not connect to Monarch Application Server when attempting to Start New Job. (HostName={hostName}, Port={port})", LastSocketException);
        }

    }

}
