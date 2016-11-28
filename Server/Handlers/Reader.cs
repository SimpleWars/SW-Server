﻿namespace Server.Handlers
{
    using System;
    using System.Net.Sockets;

    using ModelDTOs;
    using ModelDTOs.Enums;

    using Serialization;

    using Server.Constants;
    using Server.Services;
    using Server.Wrappers;

    public static class Reader
    {
        public static void ReadSingleMessage(Client client)
        {
            ReadLengthPrefix(client, false);
        }

        public static void ReadMessagesContinously(Client client)
        {
            ReadLengthPrefix(client, true);
        }

        private static void ReadMessage(Client client, int messageLength, bool continuous)
        {
            PacketAssembler packetAssembler = new PacketAssembler(messageLength);

            client.Socket.BeginReceive(
                packetAssembler.DataBuffer,
                0,
                messageLength,
                SocketFlags.None,
                MessageReceivedCallback,
                Tuple.Create(client, packetAssembler, continuous));
        }

        private static void MessageReceivedCallback(IAsyncResult result)
        {
            Tuple<Client, PacketAssembler, bool> state =
                result.AsyncState as Tuple<Client, PacketAssembler, bool>;
            if (state == null) return;

            Client client = state.Item1;
            PacketAssembler packetAssembler = state.Item2;
            bool listenForNextMessage = state.Item3;

            try
            {
                int bytesReceived = client.Socket.EndReceive(result);

                if (bytesReceived > 0)
                {
                    Message message =
                        SerManager.Deserialize<Message>(packetAssembler.DataBuffer);

                    packetAssembler.Dispose();

                    // handle the data
                    Parser.ParseReceived(client, message);

                    if (state.Item3)
                    {
                        ReadLengthPrefix(client, listenForNextMessage);
                    }
                }
            }
            catch (Exception e)
            {
                AuthenticationServices.TryLogout(client);

                if (client != null)
                {
                    Writer.SendToThenDropConnection(client, new Message<string>(Service.None, Messages.InternalErrorDrop));
                }

                Console.WriteLine(e.ToString());
            }
        }

        private static void ReadLengthPrefix(Client client, bool continuous)
        {
            LengthReceiver lengthReceiver = new LengthReceiver();

            client.Socket.BeginReceive(
                lengthReceiver.Buffer,
                0,
                LengthReceiver.LengthPrefixBytes,
                SocketFlags.None,
                LengthPrefixReceivedCallback,
                Tuple.Create(client, lengthReceiver, continuous));
        }

        private static void LengthPrefixReceivedCallback(IAsyncResult result)
        {
            Tuple<Client, LengthReceiver, bool> state = (Tuple<Client, LengthReceiver, bool>)result.AsyncState;

            try
            {
                int bytesRead = state.Item1.Socket.EndReceive(result);
                state.Item2.PushReceivedData(bytesRead);

                if (state.Item2.BytesToRead == 0)
                {
                    int messageLength = SerManager.GetLengthPrefix(state.Item2.LengthData);
                    ReadMessage(state.Item1, messageLength, state.Item3);
                }
                else
                {
                    ContinueReadingLengthPrefix(state);
                }
            }
            catch
            {
                Console.WriteLine("Error while reading length prefix");
            }
        }

        private static void ContinueReadingLengthPrefix(Tuple<Client, LengthReceiver, bool> state)
        {
            state.Item1.Socket.BeginReceive(
                state.Item2.Buffer,
                0,
                state.Item2.BytesToRead,
                SocketFlags.None,
                LengthPrefixReceivedCallback,
                state);
        }
    }
}