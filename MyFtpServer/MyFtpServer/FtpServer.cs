//-----------------------------------------------------------------------
// <copyright file="FtpServer.cs" company="DyaVan Production">
//     Copyright (c) DyaVan Production. All rights reserved.
// </copyright>
// <author>John Doe</author>
//-----------------------------------------------------------------------

using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace MyFtpServer
{
    public class FtpServer
    {
        private TcpListener listener;

        public TcpListener Listener
        {
            get
            {
                return listener;
            }

            set
            {
                listener = value;
            }
        }

        public FtpServer()
        {
        }

        public void Start()
        {
            Listener = new TcpListener(IPAddress.Any, 21);
            Listener.Start();

            #region BeginAcceptTcpClient & AcceptTcpClient
            // Разница между методами в том, что AcceptTcpClient не вернет управление до тех пор пока клиент не подключится, 
            // а BeginAcceptTcpClient вернет управление сразу же, а подключение клиента, если произойдет, то произойдет асинхронно,
            // основной поток не будет заблокирован.Методы AcceptSocket и AcceptTcpClient блокируют выполняющий поток,
            // пока сервер не обслужит подключенного клиента. Затем через методы, определенные в классах TcpClient и Socket,
            // можно взаимодействовать с подключенным клиентом: получать от него данные или, наоборот, отправлять ему.
            #endregion

            Listener.BeginAcceptTcpClient(HandleAcceptTcpClient, Listener);
        }

        public void Stop()
        {
            if (Listener == null)
            {
                Listener.Stop();
            }
        }

        private void HandleAcceptTcpClient(IAsyncResult result)
        {
            TcpClient client = Listener.EndAcceptTcpClient(result);
            Listener.BeginAcceptTcpClient(HandleAcceptTcpClient, Listener);

            ClientConnection connection = new ClientConnection(client);

            ThreadPool.QueueUserWorkItem(connection.HandleClient, client);
        }
    }
}
