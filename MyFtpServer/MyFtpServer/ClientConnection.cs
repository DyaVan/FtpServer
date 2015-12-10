//-----------------------------------------------------------------------
// <copyright file="ClientConnection.cs" company="DyaVan Production">
//     Copyright (c) DyaVan Production. All rights reserved.
// </copyright>
// <author>John Doe</author>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace MyFtpServer
{
    public class ClientConnection : IDisposable
    {
        private TcpClient controlClient;
        private TcpClient dataClient;

        private NetworkStream controlStream;
        private StreamReader controlReader;
        private StreamWriter controlWriter;

        private TcpListener passiveListener;
        private IPEndPoint dataEndpoint;

        private string username = string.Empty;

        private string root = @"I:\Учеба";
        private string currentDirectory = @"I:\Учеба";
        
        private DataConnectionType dataConnectionType = DataConnectionType.Active;
        private DataTransferType dataTransferType = DataTransferType.ASCII;

        private X509Certificate cert = null;
        private SslStream sslStream;

        public ClientConnection(TcpClient client)
        {
            controlClient = client;
            controlStream = controlClient.GetStream();

            controlReader = new StreamReader(controlStream);
            controlWriter = new StreamWriter(controlStream);
        }

        private enum DataConnectionType
        {
            Passive,
            Active,
        }

        private enum DataTransferType
        {
            Image,
            ASCII,
        }

        public void HandleClient(object value)
        {
            controlWriter.WriteLine("220 Service Ready.");
            controlWriter.Flush();

            string line;

            try
            {
                while (!string.IsNullOrEmpty(line = controlReader.ReadLine()))
                {
                    string response = null;

                    string[] command = line.Split(' ');

                    string cmd = command[0].ToUpperInvariant();
                    string arguments = command.Length > 1 ? line.Substring(command[0].Length + 1) : null;

                    if (string.IsNullOrWhiteSpace(arguments))
                    {
                        arguments = null;
                    }

                    if (response == null)
                    {
                        switch (cmd)
                        {
                            case "USER":
                                response = User(arguments);
                                break;
                            case "PASS":
                                response = Password(arguments);
                                break;
                            case "TYPE":
                                string[] splitArgs = arguments.Split(' ');
                                response = Type(splitArgs[0], splitArgs.Length > 1 ? splitArgs[1] : null);
                                break;
                            case "CWD":
                                response = ChangeWorkingDirectory(arguments);
                                break;
                            case "CDUP":   //// CHANGE TO PARENT DIRECTORY
                                response = ChangeWorkingDirectory("..");
                                break;
                            case "PWD":     //// Print working dir
                                response = "257 \"/\" is current directory.";
                                break;
                            case "QUIT":
                                response = "221 Service closing control connection";
                                break;
                            case "PORT":
                                response = Port(arguments);
                                break;
                            case "PASV":
                                response = Passive();
                                break;
                            case "LIST":
                                response = List(arguments);
                                break;
                            case "RETR":
                                response = Retrieve(arguments);
                                break;
                            case "AUTH":
                                cert = new X509Certificate(@"I:\Учеба\Универ\5 семестр\СТП\Проекты\MyFtpServer\MyFtpServer\server2.cer");
                                sslStream = new SslStream(controlStream);
                                response = "234 Enabling TLS Connection";
                                controlWriter.WriteLine(response);
                                controlWriter.Flush();
                                sslStream.AuthenticateAsServer(cert);
                                controlReader = new StreamReader(sslStream);
                                controlWriter = new StreamWriter(sslStream);
                                break;

                            default:
                                response = "502 Command not implemented";
                                break;
                        }
                    }

                    if (controlClient == null || !controlClient.Connected)
                    {
                        break;
                    }
                    else
                    {
                        if (cmd != "AUTH")
                        {
                            controlWriter.WriteLine(response);
                            controlWriter.Flush();

                            if (response.StartsWith("221", StringComparison.Ordinal))
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

        #region FTP Commands

        private static long CopyStream(Stream input, Stream output, int bufferSize)
        {
            byte[] buffer = new byte[bufferSize];
            int count = 0;
            long total = 0;

            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                output.Write(buffer, 0, count);
                total += count;
            }

            return total;
        }

        private static long CopyStreamAscii(Stream input, Stream output, int bufferSize)
        {
            char[] buffer = new char[bufferSize];
            int count = 0;
            long total = 0;

            using (StreamReader rdr = new StreamReader(input))
            {
                using (StreamWriter wtr = new StreamWriter(output, Encoding.ASCII))
                {
                    while ((count = rdr.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        wtr.Write(buffer, 0, count);
                        total += count;
                    }
                }
            }

            return total;
        }

        private static string Password(string password)
        {
            if (password.HasValue())
            {
                return "230 User logged in";
            }
            else
            {
                return "Failed";
            }
        }

        private static string ChangeWorkingDirectory(string pathname)
        {
            return "250 Changed to new directory " + pathname;
        }

        private string Type(string typeCode, string formatControl)
        {
            string response = "500 ERROR";

            switch (typeCode)   
            { //// A = ASCII, I = Image, E = EBCDIC, and L = Local byte size
                case "A":
                    dataTransferType = DataTransferType.ASCII;
                    response = "200 OK";
                    break;
                case "I":
                    dataTransferType = DataTransferType.Image;
                    response = "200 OK";
                    break;
                case "E":
                case "L":
                default:
                    response = "504 Command not implemented for that parameter.";
                    break;
            }

            if (formatControl != null)
            {
                switch (formatControl)
                {
                    case "N":
                        response = "200 OK";
                        break;
                    case "T":
                    case "C":
                    default:
                        response = "504 Command not implemented for that parameter.";
                        break;
                }
            }

            return response;
        }

        private string User(string usrName)
        {
            this.username = this.username + usrName;

            return "331 Username ok, need password";
        }

        private string Port(string hostPort)
        {
            string[] ipAndPort = hostPort.Split(',');

            byte[] ipAddress = new byte[4];
            byte[] port = new byte[2];

            for (int i = 0; i < 4; i++)
            {
                ipAddress[i] = Convert.ToByte(ipAndPort[i], CultureInfo.CurrentCulture);
            }

            for (int i = 4; i < 6; i++)
            {
                port[i - 4] = Convert.ToByte(ipAndPort[i], CultureInfo.CurrentCulture);
            }

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(port);
            }

            dataEndpoint = new IPEndPoint(new IPAddress(ipAddress), BitConverter.ToInt16(port, 0));

            return "200 Data Connection Established";
        }

        private string Passive()
        {
            IPAddress localAddress = ((IPEndPoint)controlClient.Client.LocalEndPoint).Address;

            passiveListener = new TcpListener(localAddress, 0);
            passiveListener.Start();
            IPEndPoint localEndpoint = (IPEndPoint)passiveListener.LocalEndpoint;

            byte[] address = localEndpoint.Address.GetAddressBytes();
            short port = (short)localEndpoint.Port;

            byte[] portArray = BitConverter.GetBytes(port);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(portArray);
            }

            dataConnectionType = DataConnectionType.Passive;
            return string.Format(CultureInfo.CurrentCulture, "227 Entering Passive Mode ({0},{1},{2},{3},{4},{5})",
                          address[0], address[1], address[2], address[3], portArray[0], portArray[1]);
        }

        private string List(string pathname)
        {
            pathname = NormalizeFilename(pathname);
            if (pathname == null)
            {
                pathname = string.Empty;
            }

            pathname = new DirectoryInfo(Path.Combine(currentDirectory, pathname)).FullName;

            if (dataConnectionType == DataConnectionType.Active)
            {
                dataClient = new TcpClient();
                dataClient.BeginConnect(dataEndpoint.Address, dataEndpoint.Port, DoList, pathname);
               //// dataClient.BeginConnect(dataClient., dataEndpoint.Port, DoList, pathname);
            }
            else
            {
                passiveListener.BeginAcceptTcpClient(DoList, pathname);
            }

            return string.Format(CultureInfo.CurrentCulture, "150 Opening {0} mode data transfer for LIST", dataConnectionType);
        }

        private string Retrieve(string pathname)
        {
            pathname = NormalizeFilename(pathname);

            if (pathname != null)
            {
                if (File.Exists(pathname))
                {
                    if (dataConnectionType == DataConnectionType.Active)
                    {
                        dataClient = new TcpClient();
                        dataClient.BeginConnect(dataEndpoint.Address, dataEndpoint.Port, DoRetrieve, pathname);
                    }
                    else
                    {
                        passiveListener.BeginAcceptTcpClient(DoRetrieve, pathname);
                    }

                    return string.Format(CultureInfo.CurrentCulture, "150 Opening {0} mode data transfer for RETR", dataConnectionType);
                }
            }

            return "550 File Not Found";
        }

        #endregion

        private void DoList(IAsyncResult result)
        {
            if (dataConnectionType == DataConnectionType.Active)
            {
                dataClient.EndConnect(result);
            }
            else
            {
                dataClient = passiveListener.EndAcceptTcpClient(result);
            }

            string pathname = (string)result.AsyncState;

            using (NetworkStream dataStream = dataClient.GetStream())
            using (StreamReader dataReader = new StreamReader(dataStream, Encoding.ASCII))
            using (StreamWriter dataWriter = new StreamWriter(dataStream, Encoding.ASCII))
            {
                IEnumerable<string> directories = Directory.EnumerateDirectories(pathname);

                foreach (string dir in directories)
                {
                    DirectoryInfo d = new DirectoryInfo(dir);

                    string date = d.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                        d.LastWriteTime.ToString("MMM dd  yyyy", CultureInfo.CurrentCulture) :
                        d.LastWriteTime.ToString("MMM dd HH:mm", CultureInfo.CurrentCulture);

                    string line = string.Format(CultureInfo.CurrentCulture, "drwxr-xr-x    2 2003     2003     {0,8} {1} {2}", "4096", date, d.Name);

                    dataWriter.WriteLine(line);
                    dataWriter.Flush();
                }

                IEnumerable<string> files = Directory.EnumerateFiles(pathname);

                foreach (string file in files)
                {
                    FileInfo f = new FileInfo(file);

                    string date = f.LastWriteTime < DateTime.Now - TimeSpan.FromDays(180) ?
                        f.LastWriteTime.ToString("MMM dd  yyyy", CultureInfo.CurrentCulture) : 
                        f.LastWriteTime.ToString("MMM dd HH:mm", CultureInfo.CurrentCulture);

                    string line = string.Format(CultureInfo.CurrentCulture, "-rw-r--r--    2 2003     2003     {0,8} {1} {2}", f.Length, date, f.Name);

                    dataWriter.WriteLine(line);
                    dataWriter.Flush();
                }
            }

            dataClient.Close();
            dataClient = null;

            controlWriter.WriteLine("226 Transfer complete");
            controlWriter.Flush();
        }

        private void DoRetrieve(IAsyncResult result)
        {
            if (dataConnectionType == DataConnectionType.Active)
            {
                dataClient.EndConnect(result);
            }
            else
            {
                dataClient = passiveListener.EndAcceptTcpClient(result);
            }

            string pathname = (string)result.AsyncState;

            using (NetworkStream dataStream = dataClient.GetStream())
            using (FileStream fs = new FileStream(pathname, FileMode.Open, FileAccess.Read))
            {
                CopyStream(fs, dataStream);
                dataClient.Close();
                dataClient = null;
                controlWriter.WriteLine("226 Closing data connection, file transfer successful");
                controlWriter.Flush();
            }
        }       

        private long CopyStream(Stream input, Stream output)
        {
            switch (dataTransferType)
            {
                case DataTransferType.Image:
                    return CopyStream(input, output, 4096);
                case DataTransferType.ASCII:
                    return CopyStreamAscii(input, output, 4096);
                default:
                    return 0;
            }
        }

        private string NormalizeFilename(string path)
        {
            if (path == null)
            {
                path = string.Empty;
            }

            if (path == "/")
            {
                return root;
            }
            else if (path.StartsWith("/", StringComparison.Ordinal))
            {
                path = new FileInfo(Path.Combine(root, path.Substring(1))).FullName;
            }
            else
            {
                path = new FileInfo(Path.Combine(currentDirectory, path)).FullName;
            }

            if (path.StartsWith(root, StringComparison.Ordinal))
            {
                return path;
            }
            else
            {
                return null;
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // dispose managed resources
                controlReader.Close();
                controlStream.Close();
                controlWriter.Close();
                dataClient.Close();
                sslStream.Close();
            }
            //// free native resources
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
