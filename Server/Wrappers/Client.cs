﻿namespace Server.Wrappers
{
    using System;
    using System.Net.Sockets;

    using ServerUtils;

    public class Client : IDisposable
    {
        private static readonly byte[] PingByte = new byte[] { 1 };

        public Socket Socket { get; }

        public AuthDataSecure AuthData { get; set; }

        public bool Validated { get; set; }

        public bool Connected { get; set; }

        public bool IsConnected()
        {
            try
            {
                int sent = this.Socket.Send(PingByte);
                return sent != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public Client(Socket socket)
        {
            this.Socket = socket;
            this.Validated = false;
        }

        public void Dispose()
        {
            this.Socket.Close();
            this.Socket.Dispose();
        }
    }
}