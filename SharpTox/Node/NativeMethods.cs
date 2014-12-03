using System;
using System.Runtime.InteropServices;

namespace SharpTox.Node
{
    internal class NativeMethods
    {
#if POSIX
		const string dll = "libtoxcore.so";
#else
        const string dll = "libtox";
#endif

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "new_networking")]
        internal static extern IntPtr NewNetworking(IntPtr ip, ushort port);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "ip_init")]
        internal static extern IntPtr IpInit(IntPtr ip, byte ipv6Enabled);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "new_DHT")]
        internal static extern IntPtr NewDht(IntPtr networkingCore);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "new_onion")]
        internal static extern IntPtr NewOnion(IntPtr dht);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "new_onion_announce")]
        internal static extern IntPtr NewOnionAnnounce(IntPtr dht);

        //motd
        //manage_keys

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "new_TCP_server")]
        internal static extern IntPtr NewTcpServer(byte ipv6Enabled, ushort numSockets, ushort[] ports, byte[] publicKey, byte[] secretKey, IntPtr onion);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DHT_bootstrap_from_address")]
        internal static extern int DhtBootstrapFromAddress(IntPtr dht, char[] address, byte enableIpv6, ushort port, byte[] publicKey);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "do_DHT")]
        internal static extern void DoDht(IntPtr dht);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "do_TCP_server")]
        internal static extern void DoTcpServer(IntPtr tcpServer);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "networking_poll")]
        internal static extern void NetworkingPoll(IntPtr networkingCore);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "DHT_isconnected")]
        internal static extern int DhtIsConnected(IntPtr dht); //1 if connected, 0 if not connected

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "send_LANdiscovery")]
        internal static extern int SendLanDiscovery(ushort port, IntPtr dht);

        [DllImport(dll, CallingConvention = CallingConvention.Cdecl, EntryPoint = "LANdiscovery_init")]
        internal static extern void LanDiscoveryInit(IntPtr dht);
    }
}
