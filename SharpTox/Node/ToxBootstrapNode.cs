using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;

using SharpTox.Core;

namespace SharpTox.Node
{
    public class ToxBootstrapNode : IDisposable
    {
        private IntPtr _dht;
        private IntPtr _netCore;
        private IntPtr _onion;
        private IntPtr _onionAnnounce;
        private IntPtr _tcpServer;

        private CancellationTokenSource _cancelTokenSource = new CancellationTokenSource();

        private bool _enableLanDiscovery = false;
        private bool _enableTcpRelay = false;
        private int _port;

        private bool _disposed = false;
        private bool _running = false;

        private int _sleepTime = 30; //in milliseconds
        private int _lanDiscoveryInterval = 10; //in seconds
        private Stopwatch _stopWatch = new Stopwatch();

        public bool IsConnected
        {
            get
            {
                return NativeMethods.DhtIsConnected(_dht) == 1;
            }
        }

        public ToxBootstrapNode(bool ipv6Enabled, int port, ushort[] tcpRelayPorts, ToxKeyPair keyPair)
        {
            _port = port;

            _netCore = NativeMethods.NewNetworking(IntPtr.Zero, (ushort)_port);
            _dht = NativeMethods.NewDht(_netCore);
            _onion = NativeMethods.NewOnion(_dht);
            _onionAnnounce = NativeMethods.NewOnionAnnounce(_dht);

            if (_enableTcpRelay)
                _tcpServer = NativeMethods.NewTcpServer(ipv6Enabled ? (byte)1 : (byte)0, (ushort)tcpRelayPorts.Length, tcpRelayPorts, keyPair.PublicKey.GetBytes(), keyPair.SecretKey.GetBytes(), _onion);

            if (_enableLanDiscovery)
                NativeMethods.LanDiscoveryInit(_dht);

            if (_netCore == IntPtr.Zero || _dht == IntPtr.Zero || _onion == IntPtr.Zero || _onionAnnounce == IntPtr.Zero || (_enableTcpRelay && _tcpServer == IntPtr.Zero))
                throw new Exception("Failed to initialise tox bootstrap node");
        }

        public void Start()
        {
            if (_running)
                return;

            Loop();
        }

        private void Loop()
        {
            _running = true;

            Task.Factory.StartNew(() =>
            {
                while (_running)
                {
                    if (_cancelTokenSource.IsCancellationRequested)
                        break;

                    NativeMethods.DoDht(_dht);

                    if (_enableLanDiscovery && _stopWatch.ElapsedMilliseconds >= _lanDiscoveryInterval || !_stopWatch.IsRunning)
                    {
                        NativeMethods.SendLanDiscovery((ushort)System.Net.IPAddress.HostToNetworkOrder(_port), _dht);
                        _stopWatch.Restart();
                    }

                    if (_enableTcpRelay)
                        NativeMethods.DoTcpServer(_tcpServer);

                    NativeMethods.NetworkingPoll(_netCore);

#if IS_PORTABLE
                    Task.Delay((int)ToxFunctions.DoInterval(_sleepTime));
#else
                    Thread.Sleep(_sleepTime);
#endif
                }
            }, _cancelTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
