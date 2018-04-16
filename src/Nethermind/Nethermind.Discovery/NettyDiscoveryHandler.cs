﻿/*
 * Copyright (c) 2018 Demerzel Solutions Limited
 * This file is part of the Nethermind library.
 *
 * The Nethermind library is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * The Nethermind library is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Net;
using DotNetty.Buffers;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Nethermind.Core;
using Nethermind.Discovery.Messages;
using Nethermind.Network;

namespace Nethermind.Discovery
{
    public class NettyDiscoveryHandler : SimpleChannelInboundHandler<DatagramPacket>, IMessageSender, INettyChannel
    {
        private readonly ILogger _logger;
        private readonly IDiscoveryManager _discoveryManager;
        private readonly IDatagramChannel _channel;
        private readonly IMessageSerializationService _messageSerializationService;

        public NettyDiscoveryHandler(ILogger logger, IDiscoveryManager discoveryManager, IDatagramChannel channel, IMessageSerializationService messageSerializationService)
        {
            _logger = logger;
            _discoveryManager = discoveryManager;
            _channel = channel;
            _messageSerializationService = messageSerializationService;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            OnChannelActivated?.Invoke(this, EventArgs.Empty);
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            _logger.Error("Exception when processing discovery messages", exception);
            base.ExceptionCaught(context, exception);
        }

        public override void ChannelReadComplete(IChannelHandlerContext context)
        {
            context.Flush();
        }

        public async void SendMessage(DiscoveryMessage discoveryMessage)
        {
            byte[] message;

            try
            {
                _logger.Info($"Sending message: {discoveryMessage}");
                message = Seserialize(discoveryMessage);
            }
            catch (Exception e)
            {
                _logger.Error($"Error during serialization of the message: {discoveryMessage}", e);
                return;
            }
            
            IAddressedEnvelope<IByteBuffer> packet = new DatagramPacket(Unpooled.CopiedBuffer(message), discoveryMessage.FarAddress);
            await _channel.WriteAndFlushAsync(packet);
        }

        public event EventHandler OnChannelActivated;

        protected override void ChannelRead0(IChannelHandlerContext ctx, DatagramPacket packet)
        {
            _logger.Info("Received message");

            var content = packet.Content;
            var address = packet.Sender;

            byte[] msg = new byte[content.ReadableBytes];
            content.ReadBytes(msg);

            if (msg.Length < 98)
            {
                _logger.Error($"Incorrect message, length: {msg.Length}, sender: {address}");
                return;
            }
            var typeRaw = msg[97];
            if (!Enum.IsDefined(typeof(MessageType), (int)typeRaw))
            {
                _logger.Error($"Unsupported message type: {typeRaw}, sender: {address}");
                return;
            }

            var type = (MessageType)typeRaw;
            DiscoveryMessage message;

            try
            {
                message = Deserialize(type, msg);
                message.FarAddress = (IPEndPoint)address;
            }
            catch (Exception e)
            {
                _logger.Error($"Error during deserialization of the message, type: {type}, sender: {address}", e);
                return;
            }

            try
            {
                _discoveryManager.OnIncomingMessage(message);
            }
            catch (Exception e)
            {
                _logger.Error($"Error while processing message, type: {type}, sender: {address}, message: {message}", e);
            }
        }

        private DiscoveryMessage Deserialize(MessageType type, byte[] msg)
        {
            switch (type)
            {
                case MessageType.Ping:
                    return _messageSerializationService.Deserialize<PingMessage>(msg);
                case MessageType.Pong:
                    return _messageSerializationService.Deserialize<PongMessage>(msg);
                case MessageType.FindNode:
                    return _messageSerializationService.Deserialize<FindNodeMessage>(msg);
                case MessageType.Neighbors:
                    return _messageSerializationService.Deserialize<NeighborsMessage>(msg);
                default:
                    throw new Exception($"Unsupported messageType: {type}");
            }
        }

        private byte[] Seserialize(DiscoveryMessage message)
        {
            switch (message.MessageType)
            {
                case MessageType.Ping:
                    return _messageSerializationService.Serialize((PingMessage)message);
                case MessageType.Pong:
                    return _messageSerializationService.Serialize((PongMessage)message);
                case MessageType.FindNode:
                    return _messageSerializationService.Serialize((FindNodeMessage)message);
                case MessageType.Neighbors:
                    return _messageSerializationService.Serialize((NeighborsMessage)message);
                default:
                    throw new Exception($"Unsupported messageType: {message.MessageType}");
            }
        }
    }
}