// Copyright © 2012-2020 VLINGO LABS. All rights reserved.
//
// This Source Code Form is subject to the terms of the
// Mozilla Public License, v. 2.0. If a copy of the MPL
// was not distributed with this file, You can obtain
// one at https://mozilla.org/MPL/2.0/.

using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Vlingo.Actors;
using Vlingo.Wire.Channel;
using Vlingo.Wire.Message;

namespace Vlingo.Wire.Multicast
{
    public class MulticastSubscriber : ChannelMessageDispatcher, IChannelReader, IDisposable
    {
        private readonly MemoryStream _buffer;
        private bool _closed;
        private readonly Socket _channel;
        private IChannelReaderConsumer? _consumer;
        private readonly ILogger _logger;
        private readonly int _maxReceives;
        private readonly RawMessage _message;
        private readonly string _name;
        private EndPoint _ipEndPoint;
        private bool _disposed;
        private readonly AutoResetEvent _readDone;
        private readonly int _port;

        public MulticastSubscriber(
            string name,
            Group group,
            int maxMessageSize,
            int maxReceives,
            ILogger logger)
        {
            _readDone = new AutoResetEvent(false);
            _name = name;
            _maxReceives = maxReceives;
            _logger = logger;
            _channel = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            _port = group.Port;
            var groupAddress = IPAddress.Parse(group.Address);
            
            var mcastOption = new MulticastOption(groupAddress, IPAddress.Any);
            _channel.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, mcastOption);
            _channel.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
            _channel.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _channel.ExclusiveAddressUse = false;
            _channel.Blocking = false;
            _ipEndPoint = new IPEndPoint(IPAddress.Any, group.Port);
            _channel.Bind(_ipEndPoint);

            _buffer = new MemoryStream(maxMessageSize);
            _message = new RawMessage(maxMessageSize);
            
            var networkInterface = NetworkInterface.GetAllNetworkInterfaces()[mcastOption.InterfaceIndex];
            logger.Info($"MulticastSubscriber joined: {networkInterface.Id}, {networkInterface.NetworkInterfaceType}, {networkInterface.OperationalStatus}");
        }
        
        //=========================================
        // ChannelMessageDispatcher
        //=========================================

        public override IChannelReaderConsumer? Consumer => _consumer;
        
        public override ILogger Logger => _logger;
        
        //=========================================
        // ChannelReader
        //=========================================
        
        public override string Name => _name;
        
        public int Port => _port;

        public void Close()
        {
            if (_closed)
            {
                return;
            }

            _closed = true;

            try
            {
                _channel.Close();
                _buffer.Dispose();
                _readDone.Dispose();
                Dispose(true);
            }
            catch (Exception e)
            {
                _logger.Error($"Failed to close channel for: '{_name}'", e);
            }
        }

        public void OpenFor(IChannelReaderConsumer consumer)
        {
            if (_closed)
            {
                return;
            }

            _consumer = consumer;
        }

        public void ProbeChannel()
        {
            if (_closed)
            {
                return;
            }

            try
            {
                // when nothing is received, receives represents retries
                // and possibly some number of receives
                for (var receives = 0; receives < _maxReceives; ++receives)
                {
                    if (_channel.Available > 0)
                    {
                        _buffer.SetLength(0); // clear
                        var bytes = new byte [_channel.Available];
                        // check for availability because otherwise surprisingly
                        // the call to _channel.ReceiveFromAsync is blocking and
                        // _channel.Blocking = false; is not taken into account
                        
                        var state = new StateObject(_channel, bytes);
                        _channel.BeginReceiveFrom(bytes, 0, bytes.Length, SocketFlags.None, ref _ipEndPoint, ReceiveCallback, state);
                        _readDone.WaitOne();
                    }
                }
            }
            catch (SocketException e)
            {
                _logger.Error($"Failed to read channel selector for: '{_name}'", e);
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);  
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }
      
            if (disposing) 
            {
                Close();
            }
      
            _disposed = true;
        }

        private void ReceiveCallback(IAsyncResult ar)
        {
            var state = (StateObject) ar.AsyncState;  
            var channel = state.WorkSocket;
            var buffer = state.Buffer;

            try
            {
                var bytesRead = channel.EndReceiveFrom(ar, ref _ipEndPoint);
                if (bytesRead > 0)
                {
                    _buffer.Clear();
                    _message.Reset();
                    _buffer.Write(buffer, 0, bytesRead);
                    _buffer.Flip();
                    _message.From(_buffer);

                    _consumer!.Consume(_message);
                }
                
                _readDone.Set();
            }
            catch (SocketException e)
            {
                _logger.Error($"Failed to receive callback: '{_name}'", e);
            }
        }
        
        private class StateObject
        {
            public StateObject(Socket workSocket, byte[] buffer)
            {
                WorkSocket = workSocket;
                Buffer = buffer;
            }

            // Client socket.  
            public Socket WorkSocket { get; }
            
            // Receive buffer.  
            public byte[] Buffer { get; }
        }
    }
}