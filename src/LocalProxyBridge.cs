using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DejavuBrowser
{
    public enum UpstreamKind
    {
        Socks5,
        HttpConnect,
        Socks4
    }

    public sealed class UpstreamEndpoint
    {
        public UpstreamKind Kind;
        public string Host;
        public int Port;
        public string ExitIp;
        public string City;
        public string Country;
        public string CountryCode;
        public int LatencyMs;

        public string Key
        {
            get { return Kind.ToString() + "://" + (Host ?? "") + ":" + Port; }
        }
    }

    public sealed class HttpsProbeResult
    {
        public int StatusCode;
        public string Body;
        public int LatencyMs;
        public bool Success { get { return StatusCode > 0; } }
    }

    /// <summary>
    /// Ultra-fast loopback HTTP/CONNECT proxy bridge for WebView2.
    /// When VPN is OFF: relays TCP sockets directly with zero restart.
    /// When VPN is ON: tunnels traffic through either:
    ///   - Smart Clean-IP Global Relay Pool (SOCKS5 / HTTPS CONNECT / SOCKS4 with automatic zero-drop failover), OR
    ///   - 3-Hop Tor Onion SOCKS5 tunnel (127.0.0.1:socksPort, remote DNS ATYP 0x03)
    /// with a fail-closed kill-switch when connecting.
    /// </summary>
    internal sealed class LocalProxyBridge : IDisposable
    {
        private sealed class DnsCacheEntry
        {
            public byte[] Ipv4;
            public DateTime ExpiresUtc;
        }

        private static readonly ConcurrentDictionary<string, DnsCacheEntry> DnsV4Cache =
            new ConcurrentDictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _sync = new object();
        private readonly HashSet<TcpClient> _activeVpnClients = new HashSet<TcpClient>();

        private volatile bool _vpnEnabled;
        private List<UpstreamEndpoint> _upstreamPool = new List<UpstreamEndpoint>();
        private int _primaryIndex;
        private long _vpnBytesSent;
        private long _vpnBytesReceived;
        private bool _disposed;

        public int ListenPort { get; private set; }

        public bool VpnEnabled
        {
            get { return _vpnEnabled; }
        }

        public long VpnBytesSent
        {
            get { return Interlocked.Read(ref _vpnBytesSent); }
        }

        public long VpnBytesReceived
        {
            get { return Interlocked.Read(ref _vpnBytesReceived); }
        }

        public void ResetVpnCounters()
        {
            Interlocked.Exchange(ref _vpnBytesSent, 0);
            Interlocked.Exchange(ref _vpnBytesReceived, 0);
        }

        public LocalProxyBridge()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start(256);
            ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public void SetDirectMode()
        {
            lock (_sync)
            {
                _vpnEnabled = false;
                _upstreamPool = new List<UpstreamEndpoint>();
                _primaryIndex = 0;
            }
            ResetTunneledConnections();
        }

        public void SetVpnKillSwitchEngaged()
        {
            lock (_sync)
            {
                _vpnEnabled = true;
                _upstreamPool = new List<UpstreamEndpoint>();
                _primaryIndex = 0;
            }
            ResetTunneledConnections();
        }

        public void SetSocks5Tunnel(int socksPort)
        {
            List<UpstreamEndpoint> pool = new List<UpstreamEndpoint>();
            if (socksPort > 0)
            {
                pool.Add(new UpstreamEndpoint
                {
                    Kind = UpstreamKind.Socks5,
                    Host = "127.0.0.1",
                    Port = socksPort
                });
            }
            SetUpstreamPool(pool);
        }

        public void SetUpstreamPool(List<UpstreamEndpoint> endpoints)
        {
            lock (_sync)
            {
                _upstreamPool = (endpoints != null) ? new List<UpstreamEndpoint>(endpoints) : new List<UpstreamEndpoint>();
                _primaryIndex = 0;
                _vpnEnabled = true;
            }
            ResetTunneledConnections();
        }

        public UpstreamEndpoint GetPrimaryEndpoint()
        {
            lock (_sync)
            {
                if (_upstreamPool == null || _upstreamPool.Count == 0) return null;
                int idx = _primaryIndex % _upstreamPool.Count;
                return _upstreamPool[idx];
            }
        }

        private List<UpstreamEndpoint> SnapshotOrderedPool()
        {
            lock (_sync)
            {
                if (_upstreamPool == null || _upstreamPool.Count == 0) return null;
                int count = _upstreamPool.Count;
                int start = _primaryIndex % count;
                List<UpstreamEndpoint> ordered = new List<UpstreamEndpoint>(count);
                for (int i = 0; i < count; i++)
                {
                    ordered.Add(_upstreamPool[(start + i) % count]);
                }
                return ordered;
            }
        }

        private void PromoteEndpointIfInPool(UpstreamEndpoint winner)
        {
            if (winner == null) return;
            lock (_sync)
            {
                if (_upstreamPool == null || _upstreamPool.Count <= 1) return;
                for (int i = 0; i < _upstreamPool.Count; i++)
                {
                    if (string.Equals(_upstreamPool[i].Key, winner.Key, StringComparison.OrdinalIgnoreCase))
                    {
                        _primaryIndex = i;
                        return;
                    }
                }
            }
        }

        public void ResetTunneledConnections()
        {
            List<TcpClient> toClose;
            lock (_sync)
            {
                toClose = new List<TcpClient>(_activeVpnClients);
                _activeVpnClients.Clear();
            }
            for (int i = 0; i < toClose.Count; i++)
            {
                try { toClose[i].Close(); } catch { }
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient client = null;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                    client.NoDelay = true;
                    TcpClient captured = client;
                    Task ignored = Task.Run(() => HandleClientAsync(captured, token));
                }
                catch
                {
                    if (client != null) { try { client.Close(); } catch { } }
                    if (token.IsCancellationRequested) break;
                }
            }
        }

        private async Task<TcpClient> ConnectUsingPoolAsync(string targetHost, int targetPort, CancellationToken token)
        {
            List<UpstreamEndpoint> pool = SnapshotOrderedPool();
            if (pool == null || pool.Count == 0) return null;

            int maxTries = Math.Min(pool.Count, 3);
            for (int i = 0; i < maxTries; i++)
            {
                if (token.IsCancellationRequested) return null;
                UpstreamEndpoint ep = pool[i];
                bool isLocalTor = (ep.Kind == UpstreamKind.Socks5 && ep.Host == "127.0.0.1");
                int timeoutMs = isLocalTor ? 24000 : (i == 0 ? 7500 : 6000);
                TcpClient tcp = await ConnectViaEndpointAsync(ep, targetHost, targetPort, timeoutMs, token).ConfigureAwait(false);
                if (tcp != null)
                {
                    if (i > 0) PromoteEndpointIfInPool(ep);
                    return tcp;
                }
            }
            return null;
        }

        private async Task HandleClientAsync(TcpClient client, CancellationToken token)
        {
            TcpClient upstream = null;
            bool trackedAsVpn = false;
            try
            {
                using (client)
                {
                    NetworkStream clientStream = client.GetStream();
                    byte[] headerBuf = new byte[32768];
                    int totalRead = 0;
                    int headerEnd = -1;

                    while (totalRead < headerBuf.Length)
                    {
                        int n = await ReadWithTimeoutAsync(clientStream, headerBuf, totalRead, headerBuf.Length - totalRead, 15000, token).ConfigureAwait(false);
                        if (n <= 0) return;
                        totalRead += n;
                        headerEnd = FindHeaderEnd(headerBuf, totalRead);
                        if (headerEnd >= 0) break;
                    }

                    if (headerEnd < 0) return;

                    string headerText = Encoding.ASCII.GetString(headerBuf, 0, headerEnd);
                    string[] lines = headerText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                    if (lines.Length == 0) return;

                    string[] reqParts = lines[0].Split(' ');
                    if (reqParts.Length < 3) return;

                    string method = reqParts[0].ToUpperInvariant();
                    string target = reqParts[1];
                    string httpVer = reqParts[2];

                    int bodyStart = headerEnd + 4;
                    int leftoverLen = totalRead - bodyStart;

                    bool useVpn = _vpnEnabled;
                    bool hasPool = (GetPrimaryEndpoint() != null);

                    if (method == "CONNECT")
                    {
                        string host;
                        int port;
                        if (!ParseHostPort(target, 443, out host, out port))
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        if (useVpn && !hasPool)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        upstream = useVpn
                            ? await ConnectUsingPoolAsync(host, port, token).ConfigureAwait(false)
                            : await ConnectDirectAsync(host, port, 15000, token).ConfigureAwait(false);

                        if (upstream == null)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        if (!useVpn && _vpnEnabled)
                        {
                            try { upstream.Close(); } catch { }
                            upstream = null;
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        trackedAsVpn = true;
                        TrackVpnSocket(upstream, client);

                        await WriteAsciiAsync(clientStream, "HTTP/1.1 200 Connection Established\r\n\r\n").ConfigureAwait(false);

                        NetworkStream upStream = upstream.GetStream();
                        if (leftoverLen > 0)
                        {
                            await upStream.WriteAsync(headerBuf, bodyStart, leftoverLen, token).ConfigureAwait(false);
                            if (useVpn) Interlocked.Add(ref _vpnBytesSent, leftoverLen);
                        }

                        await RelayBidirectionalAsync(client, clientStream, upstream, upStream, useVpn, token).ConfigureAwait(false);
                    }
                    else
                    {
                        Uri uri;
                        if (!Uri.TryCreate(target, UriKind.Absolute, out uri))
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        string host = uri.Host;
                        int port = uri.Port > 0 ? uri.Port : 80;
                        string relPath = string.IsNullOrEmpty(uri.PathAndQuery) ? "/" : uri.PathAndQuery;

                        if (useVpn && !hasPool)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        // For plain HTTP over an HttpConnect upstream, forward directly to proxy with absolute URI!
                        UpstreamEndpoint primary = useVpn ? GetPrimaryEndpoint() : null;
                        bool forwardAsHttpProxy = (useVpn && primary != null && primary.Kind == UpstreamKind.HttpConnect);

                        if (forwardAsHttpProxy)
                        {
                            upstream = await ConnectDirectAsync(primary.Host, primary.Port, 7500, token).ConfigureAwait(false);
                        }
                        else
                        {
                            upstream = useVpn
                                ? await ConnectUsingPoolAsync(host, port, token).ConfigureAwait(false)
                                : await ConnectDirectAsync(host, port, 15000, token).ConfigureAwait(false);
                        }

                        if (upstream == null)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        if (!useVpn && _vpnEnabled)
                        {
                            try { upstream.Close(); } catch { }
                            upstream = null;
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        trackedAsVpn = true;
                        TrackVpnSocket(upstream, client);

                        StringBuilder sb = new StringBuilder();
                        sb.Append(method).Append(' ').Append(forwardAsHttpProxy ? target : relPath).Append(' ').Append(httpVer).Append("\r\n");
                        bool hasHost = false;
                        for (int i = 1; i < lines.Length; i++)
                        {
                            string ln = lines[i];
                            if (string.IsNullOrEmpty(ln)) continue;
                            if (ln.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase) ||
                                ln.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase) ||
                                ln.StartsWith("Connection:", StringComparison.OrdinalIgnoreCase))
                            {
                                continue;
                            }
                            if (ln.StartsWith("Host:", StringComparison.OrdinalIgnoreCase)) hasHost = true;
                            sb.Append(ln).Append("\r\n");
                        }
                        if (!hasHost)
                        {
                            sb.Append("Host: ").Append(port == 80 ? host : (host + ":" + port)).Append("\r\n");
                        }
                        sb.Append("Connection: close\r\n\r\n");

                        byte[] rewritten = Encoding.ASCII.GetBytes(sb.ToString());
                        NetworkStream upStream = upstream.GetStream();
                        await upStream.WriteAsync(rewritten, 0, rewritten.Length, token).ConfigureAwait(false);
                        if (useVpn) Interlocked.Add(ref _vpnBytesSent, rewritten.Length);
                        if (leftoverLen > 0)
                        {
                            await upStream.WriteAsync(headerBuf, bodyStart, leftoverLen, token).ConfigureAwait(false);
                            if (useVpn) Interlocked.Add(ref _vpnBytesSent, leftoverLen);
                        }

                        await RelayBidirectionalAsync(client, clientStream, upstream, upStream, useVpn, token).ConfigureAwait(false);
                    }
                }
            }
            catch { }
            finally
            {
                if (trackedAsVpn)
                {
                    UntrackVpnSocket(upstream, client);
                }
                if (upstream != null)
                {
                    try { upstream.Close(); } catch { }
                }
            }
        }

        private void TrackVpnSocket(TcpClient a, TcpClient b)
        {
            lock (_sync)
            {
                if (a != null) _activeVpnClients.Add(a);
                if (b != null) _activeVpnClients.Add(b);
            }
        }

        private void UntrackVpnSocket(TcpClient a, TcpClient b)
        {
            lock (_sync)
            {
                if (a != null) _activeVpnClients.Remove(a);
                if (b != null) _activeVpnClients.Remove(b);
            }
        }

        private static async Task<TcpClient> ConnectDirectAsync(string host, int port, int timeoutMs, CancellationToken token)
        {
            TcpClient tcp = new TcpClient();
            tcp.NoDelay = true;
            try
            {
                Task connectTask = tcp.ConnectAsync(host, port);
                Task delayTask = Task.Delay(timeoutMs, token);
                Task done = await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false);
                if (done != connectTask)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                await connectTask.ConfigureAwait(false);
                return tcp;
            }
            catch
            {
                try { tcp.Close(); } catch { }
                return null;
            }
        }

        public static Task<TcpClient> ConnectViaEndpointAsync(
            UpstreamEndpoint ep, string targetHost, int targetPort, int timeoutMs, CancellationToken token)
        {
            if (ep == null || string.IsNullOrEmpty(ep.Host) || ep.Port <= 0)
                return Task.FromResult<TcpClient>(null);

            if (ep.Kind == UpstreamKind.Socks5)
                return ConnectViaSocks5HostAsync(ep.Host, ep.Port, targetHost, targetPort, timeoutMs, token);
            if (ep.Kind == UpstreamKind.HttpConnect)
                return ConnectViaHttpConnectAsync(ep.Host, ep.Port, targetHost, targetPort, timeoutMs, token);
            if (ep.Kind == UpstreamKind.Socks4)
                return ConnectViaSocks4Async(ep.Host, ep.Port, targetHost, targetPort, timeoutMs, token);

            return Task.FromResult<TcpClient>(null);
        }

        public static Task<TcpClient> ConnectViaSocks5Async(int socksPort, string host, int port, int timeoutMs, CancellationToken token)
        {
            return ConnectViaSocks5HostAsync("127.0.0.1", socksPort, host, port, timeoutMs, token);
        }

        public static async Task<TcpClient> ConnectViaSocks5HostAsync(
            string proxyHost, int proxyPort, string host, int port, int timeoutMs, CancellationToken token)
        {
            TcpClient tcp = await ConnectDirectAsync(proxyHost, proxyPort, timeoutMs, token).ConfigureAwait(false);
            if (tcp == null) return null;
            try
            {
                NetworkStream s = tcp.GetStream();

                // 1. SOCKS5 Greeting: Version 5, 1 Auth Method, No Auth (0x00)
                byte[] greeting = new byte[] { 0x05, 0x01, 0x00 };
                await s.WriteAsync(greeting, 0, greeting.Length, token).ConfigureAwait(false);

                byte[] greetResp = new byte[2];
                if (!await ReadExactAsync(s, greetResp, 0, 2, timeoutMs, token).ConfigureAwait(false))
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                if (greetResp[0] != 0x05 || greetResp[1] != 0x00)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }

                // 2. SOCKS5 CONNECT with ATYP=0x03 (Domain Name -> Remote DNS inside VPN exit!)
                byte[] hostBytes = Encoding.ASCII.GetBytes(host);
                if (hostBytes.Length > 255)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                byte[] req = new byte[7 + hostBytes.Length];
                req[0] = 0x05;
                req[1] = 0x01;
                req[2] = 0x00;
                req[3] = 0x03;
                req[4] = (byte)hostBytes.Length;
                Buffer.BlockCopy(hostBytes, 0, req, 5, hostBytes.Length);
                req[5 + hostBytes.Length] = (byte)((port >> 8) & 0xFF);
                req[6 + hostBytes.Length] = (byte)(port & 0xFF);
                await s.WriteAsync(req, 0, req.Length, token).ConfigureAwait(false);

                byte[] repHead = new byte[4];
                if (!await ReadExactAsync(s, repHead, 0, 4, timeoutMs, token).ConfigureAwait(false))
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                if (repHead[0] != 0x05 || repHead[1] != 0x00)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }

                int skipBytes = 0;
                if (repHead[3] == 0x01) skipBytes = 6;
                else if (repHead[3] == 0x04) skipBytes = 18;
                else if (repHead[3] == 0x03)
                {
                    byte[] lenB = new byte[1];
                    if (!await ReadExactAsync(s, lenB, 0, 1, timeoutMs, token).ConfigureAwait(false))
                    {
                        try { tcp.Close(); } catch { }
                        return null;
                    }
                    skipBytes = lenB[0] + 2;
                }

                if (skipBytes > 0)
                {
                    byte[] dump = new byte[skipBytes];
                    if (!await ReadExactAsync(s, dump, 0, skipBytes, timeoutMs, token).ConfigureAwait(false))
                    {
                        try { tcp.Close(); } catch { }
                        return null;
                    }
                }

                return tcp;
            }
            catch
            {
                try { tcp.Close(); } catch { }
                return null;
            }
        }

        public static async Task<TcpClient> ConnectViaHttpConnectAsync(
            string proxyHost, int proxyPort, string targetHost, int targetPort, int timeoutMs, CancellationToken token)
        {
            TcpClient tcp = await ConnectDirectAsync(proxyHost, proxyPort, timeoutMs, token).ConfigureAwait(false);
            if (tcp == null) return null;
            try
            {
                NetworkStream s = tcp.GetStream();
                string connectReq =
                    "CONNECT " + targetHost + ":" + targetPort + " HTTP/1.1\r\n" +
                    "Host: " + targetHost + ":" + targetPort + "\r\n" +
                    "Proxy-Connection: keep-alive\r\n" +
                    "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36\r\n\r\n";
                byte[] reqBytes = Encoding.ASCII.GetBytes(connectReq);
                await s.WriteAsync(reqBytes, 0, reqBytes.Length, token).ConfigureAwait(false);

                // Read HTTP response header byte-by-byte up to \r\n\r\n so zero TLS bytes are over-read
                byte[] buf = new byte[4096];
                int len = 0;
                int state = 0;
                while (len < buf.Length)
                {
                    int n = await ReadWithTimeoutAsync(s, buf, len, 1, timeoutMs, token).ConfigureAwait(false);
                    if (n <= 0)
                    {
                        try { tcp.Close(); } catch { }
                        return null;
                    }
                    byte b = buf[len];
                    len++;
                    if (state == 0 && b == 13) state = 1;
                    else if (state == 1 && b == 10) state = 2;
                    else if (state == 2 && b == 13) state = 3;
                    else if (state == 3 && b == 10) break;
                    else state = (b == 13) ? 1 : 0;
                }

                string respHeader = Encoding.ASCII.GetString(buf, 0, len);
                int firstNl = respHeader.IndexOf('\n');
                string statusLine = firstNl > 0 ? respHeader.Substring(0, firstNl) : respHeader;
                if (statusLine.IndexOf(" 200", StringComparison.Ordinal) < 0)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }

                return tcp;
            }
            catch
            {
                try { tcp.Close(); } catch { }
                return null;
            }
        }

        private static async Task<byte[]> ResolveIpv4CachedAsync(string host, int timeoutMs, CancellationToken token)
        {
            IPAddress parsed;
            if (IPAddress.TryParse(host, out parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
            {
                return parsed.GetAddressBytes();
            }

            DnsCacheEntry entry;
            if (DnsV4Cache.TryGetValue(host, out entry) && entry.ExpiresUtc > DateTime.UtcNow)
            {
                return entry.Ipv4;
            }

            try
            {
                Task<IPAddress[]> dnsTask = Dns.GetHostAddressesAsync(host);
                Task delayTask = Task.Delay(Math.Min(timeoutMs, 3500), token);
                if (await Task.WhenAny(dnsTask, delayTask).ConfigureAwait(false) != dnsTask)
                {
                    return null;
                }
                IPAddress[] addrs = await dnsTask.ConfigureAwait(false);
                if (addrs != null)
                {
                    for (int i = 0; i < addrs.Length; i++)
                    {
                        if (addrs[i].AddressFamily == AddressFamily.InterNetwork)
                        {
                            byte[] ipv4 = addrs[i].GetAddressBytes();
                            DnsV4Cache[host] = new DnsCacheEntry
                            {
                                Ipv4 = ipv4,
                                ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
                            };
                            return ipv4;
                        }
                    }
                }
            }
            catch { }
            return null;
        }

        public static async Task<TcpClient> ConnectViaSocks4Async(
            string proxyHost, int proxyPort, string targetHost, int targetPort, int timeoutMs, CancellationToken token)
        {
            byte[] ipv4 = await ResolveIpv4CachedAsync(targetHost, timeoutMs, token).ConfigureAwait(false);
            if (ipv4 == null || ipv4.Length != 4) return null;

            TcpClient tcp = await ConnectDirectAsync(proxyHost, proxyPort, timeoutMs, token).ConfigureAwait(false);
            if (tcp == null) return null;
            try
            {
                NetworkStream s = tcp.GetStream();
                byte[] req = new byte[9];
                req[0] = 0x04; // SOCKS4
                req[1] = 0x01; // CONNECT
                req[2] = (byte)((targetPort >> 8) & 0xFF);
                req[3] = (byte)(targetPort & 0xFF);
                req[4] = ipv4[0];
                req[5] = ipv4[1];
                req[6] = ipv4[2];
                req[7] = ipv4[3];
                req[8] = 0x00; // empty userid terminator
                await s.WriteAsync(req, 0, req.Length, token).ConfigureAwait(false);

                byte[] rep = new byte[8];
                if (!await ReadExactAsync(s, rep, 0, 8, timeoutMs, token).ConfigureAwait(false))
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                // SOCKS4 reply: VN=0x00, CD=0x5A (90 = request granted)
                if (rep[1] != 0x5A)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                return tcp;
            }
            catch
            {
                try { tcp.Close(); } catch { }
                return null;
            }
        }

        /// <summary>
        /// Performs a real HTTPS GET directly through ANY upstream endpoint (SOCKS5 / HTTP CONNECT / SOCKS4)
        /// using SslStream and returns HTTP status code, decoded body, and measured round-trip latency!
        /// </summary>
        public static async Task<HttpsProbeResult> HttpsProbeViaEndpointAsync(
            UpstreamEndpoint ep, string host, string path, int timeoutMs, CancellationToken token)
        {
            Stopwatch sw = Stopwatch.StartNew();
            TcpClient tcp = await ConnectViaEndpointAsync(ep, host, 443, timeoutMs, token).ConfigureAwait(false);
            if (tcp == null) return null;
            try
            {
                using (tcp)
                using (SslStream ssl = new SslStream(tcp.GetStream(), false))
                {
                    SslProtocols protos = SslProtocols.Tls12;
                    try { protos = SslProtocols.Tls12 | (SslProtocols)12288; } catch { }

                    Task authTask = ssl.AuthenticateAsClientAsync(host, null, protos, false);
                    if (await Task.WhenAny(authTask, Task.Delay(timeoutMs, token)).ConfigureAwait(false) != authTask)
                    {
                        return null;
                    }
                    await authTask.ConfigureAwait(false);

                    string req = "GET " + path + " HTTP/1.1\r\n" +
                                 "Host: " + host + "\r\n" +
                                 "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/134.0.0.0 Safari/537.36\r\n" +
                                 "Accept: application/json,text/html,*/*\r\n" +
                                 "Accept-Encoding: identity\r\n" +
                                 "Connection: close\r\n\r\n";
                    byte[] reqBytes = Encoding.ASCII.GetBytes(req);
                    await ssl.WriteAsync(reqBytes, 0, reqBytes.Length, token).ConfigureAwait(false);
                    await ssl.FlushAsync(token).ConfigureAwait(false);

                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buf = new byte[8192];
                        while (true)
                        {
                            int n = await ReadWithTimeoutAsync(ssl, buf, 0, buf.Length, timeoutMs, token).ConfigureAwait(false);
                            if (n <= 0) break;
                            ms.Write(buf, 0, n);
                            if (ms.Length > 49152) break;
                        }
                        byte[] raw = ms.ToArray();
                        int sep = FindHeaderEnd(raw, raw.Length);
                        if (sep < 0) return null;
                        string headers = Encoding.ASCII.GetString(raw, 0, sep);
                        int statusCode = ParseHttpStatusCode(headers);
                        int bodyOffset = sep + 4;
                        string body;
                        if (headers.IndexOf("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            byte[] decoded = DecodeChunkedBytes(raw, bodyOffset);
                            body = Encoding.UTF8.GetString(decoded);
                        }
                        else
                        {
                            body = Encoding.UTF8.GetString(raw, bodyOffset, raw.Length - bodyOffset);
                        }
                        return new HttpsProbeResult
                        {
                            StatusCode = statusCode,
                            Body = body.Trim(),
                            LatencyMs = Math.Max(1, (int)sw.ElapsedMilliseconds)
                        };
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static async Task<string> HttpsGetViaEndpointAsync(
            UpstreamEndpoint ep, string host, string path, int timeoutMs, CancellationToken token)
        {
            HttpsProbeResult res = await HttpsProbeViaEndpointAsync(ep, host, path, timeoutMs, token).ConfigureAwait(false);
            if (res == null || res.StatusCode < 200 || res.StatusCode >= 300) return null;
            return res.Body;
        }

        public static Task<string> HttpsGetViaSocks5Async(int socksPort, string host, string path, int timeoutMs, CancellationToken token)
        {
            UpstreamEndpoint ep = new UpstreamEndpoint { Kind = UpstreamKind.Socks5, Host = "127.0.0.1", Port = socksPort };
            return HttpsGetViaEndpointAsync(ep, host, path, timeoutMs, token);
        }

        private static int ParseHttpStatusCode(string headers)
        {
            if (string.IsNullOrEmpty(headers)) return 0;
            int nl = headers.IndexOf('\n');
            string line = (nl > 0 ? headers.Substring(0, nl) : headers).Trim();
            string[] parts = line.Split(' ');
            int code;
            if (parts.Length >= 2 && int.TryParse(parts[1], out code)) return code;
            return 0;
        }

        private static byte[] DecodeChunkedBytes(byte[] raw, int startOffset)
        {
            using (MemoryStream outMs = new MemoryStream())
            {
                int pos = startOffset;
                while (pos < raw.Length)
                {
                    int crlf = FindCrlf(raw, pos, raw.Length);
                    if (crlf < 0) break;
                    string sizeHex = Encoding.ASCII.GetString(raw, pos, crlf - pos).Trim();
                    int semi = sizeHex.IndexOf(';');
                    if (semi >= 0) sizeHex = sizeHex.Substring(0, semi);
                    int chunkLen;
                    if (!int.TryParse(sizeHex, System.Globalization.NumberStyles.HexNumber, null, out chunkLen) || chunkLen <= 0)
                    {
                        break;
                    }
                    pos = crlf + 2;
                    if (pos + chunkLen > raw.Length) chunkLen = raw.Length - pos;
                    if (chunkLen > 0) outMs.Write(raw, pos, chunkLen);
                    pos += chunkLen + 2;
                }
                return outMs.ToArray();
            }
        }

        private static int FindCrlf(byte[] buf, int start, int len)
        {
            for (int i = start; i <= len - 2; i++)
            {
                if (buf[i] == 13 && buf[i + 1] == 10) return i;
            }
            return -1;
        }

        private async Task RelayBidirectionalAsync(TcpClient a, NetworkStream aStream, TcpClient b, NetworkStream bStream, bool countVpnTraffic, CancellationToken token)
        {
            Task t1 = PumpHalfAsync(aStream, bStream, b, countVpnTraffic, true, token);
            Task t2 = PumpHalfAsync(bStream, aStream, a, countVpnTraffic, false, token);
            await Task.WhenAll(t1, t2).ConfigureAwait(false);
        }

        private async Task PumpHalfAsync(NetworkStream src, NetworkStream dst, TcpClient dstClient, bool countVpnTraffic, bool isUpload, CancellationToken token)
        {
            byte[] buf = new byte[65536];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int n = await src.ReadAsync(buf, 0, buf.Length, token).ConfigureAwait(false);
                    if (n <= 0) break;
                    await dst.WriteAsync(buf, 0, n, token).ConfigureAwait(false);
                    if (countVpnTraffic)
                    {
                        if (isUpload) Interlocked.Add(ref _vpnBytesSent, n);
                        else Interlocked.Add(ref _vpnBytesReceived, n);
                    }
                }
            }
            catch { }
            finally
            {
                try { dstClient.Client.Shutdown(SocketShutdown.Send); } catch { }
            }
        }

        private static async Task<int> ReadWithTimeoutAsync(Stream s, byte[] buf, int offset, int count, int timeoutMs, CancellationToken token)
        {
            Task<int> readTask = s.ReadAsync(buf, offset, count, token);
            Task delayTask = Task.Delay(timeoutMs, token);
            Task done = await Task.WhenAny(readTask, delayTask).ConfigureAwait(false);
            if (done != readTask) return -1;
            return await readTask.ConfigureAwait(false);
        }

        private static async Task<bool> ReadExactAsync(Stream s, byte[] buf, int offset, int count, int timeoutMs, CancellationToken token)
        {
            int read = 0;
            while (read < count)
            {
                int n = await ReadWithTimeoutAsync(s, buf, offset + read, count - read, timeoutMs, token).ConfigureAwait(false);
                if (n <= 0) return false;
                read += n;
            }
            return true;
        }

        private static async Task WriteAsciiAsync(Stream s, string text)
        {
            byte[] b = Encoding.ASCII.GetBytes(text);
            await s.WriteAsync(b, 0, b.Length).ConfigureAwait(false);
        }

        private static int FindHeaderEnd(byte[] buf, int len)
        {
            for (int i = 0; i <= len - 4; i++)
            {
                if (buf[i] == 13 && buf[i + 1] == 10 && buf[i + 2] == 13 && buf[i + 3] == 10)
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool ParseHostPort(string target, int defaultPort, out string host, out int port)
        {
            host = null;
            port = defaultPort;
            if (string.IsNullOrWhiteSpace(target)) return false;
            target = target.Trim();

            if (target.StartsWith("["))
            {
                int close = target.IndexOf(']');
                if (close < 0) return false;
                host = target.Substring(1, close - 1);
                if (close + 2 < target.Length && target[close + 1] == ':')
                {
                    int.TryParse(target.Substring(close + 2), out port);
                }
                return !string.IsNullOrEmpty(host) && port > 0 && port <= 65535;
            }

            int colon = target.LastIndexOf(':');
            if (colon > 0)
            {
                host = target.Substring(0, colon);
                int.TryParse(target.Substring(colon + 1), out port);
            }
            else
            {
                host = target;
            }
            return !string.IsNullOrEmpty(host) && port > 0 && port <= 65535;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try { _cts.Cancel(); } catch { }
            try { _listener.Stop(); } catch { }
            ResetTunneledConnections();
            try { _cts.Dispose(); } catch { }
        }
    }
}
