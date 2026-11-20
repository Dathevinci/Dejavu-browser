using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Ultra-fast loopback HTTP/CONNECT proxy bridge for WebView2.
    /// When VPN is OFF: relays TCP sockets directly with zero restart.
    /// When VPN is ON: tunnels all TCP + remote DNS (SOCKS5 ATYP 0x03) strictly
    /// through the encrypted local SOCKS5 tunnel with a fail-closed kill-switch.
    /// </summary>
    internal sealed class LocalProxyBridge : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly object _sync = new object();
        private readonly HashSet<TcpClient> _activeVpnClients = new HashSet<TcpClient>();

        private volatile bool _vpnEnabled;
        private volatile int _socksPort;
        private bool _disposed;

        public int ListenPort { get; private set; }

        public bool VpnEnabled
        {
            get { return _vpnEnabled; }
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
            _vpnEnabled = false;
            _socksPort = 0;
            ResetTunneledConnections();
        }

        public void SetVpnKillSwitchEngaged()
        {
            _vpnEnabled = true;
            _socksPort = 0;
            ResetTunneledConnections();
        }

        public void SetSocks5Tunnel(int socksPort)
        {
            _socksPort = socksPort;
            _vpnEnabled = true;
            ResetTunneledConnections();
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
                    int activeSocks = _socksPort;

                    if (method == "CONNECT")
                    {
                        string host;
                        int port;
                        if (!ParseHostPort(target, 443, out host, out port))
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        if (useVpn && activeSocks <= 0)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        upstream = useVpn
                            ? await ConnectViaSocks5Async(activeSocks, host, port, 25000, token).ConfigureAwait(false)
                            : await ConnectDirectAsync(host, port, 15000, token).ConfigureAwait(false);

                        if (upstream == null)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        trackedAsVpn = true;
                        TrackVpnSocket(upstream, client);

                        await WriteAsciiAsync(clientStream, "HTTP/1.1 200 Connection Established\r\n\r\n").ConfigureAwait(false);

                        NetworkStream upStream = upstream.GetStream();
                        if (leftoverLen > 0)
                        {
                            await upStream.WriteAsync(headerBuf, bodyStart, leftoverLen, token).ConfigureAwait(false);
                        }

                        await RelayBidirectionalAsync(client, clientStream, upstream, upStream, token).ConfigureAwait(false);
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

                        if (useVpn && activeSocks <= 0)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 503 Service Unavailable\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        upstream = useVpn
                            ? await ConnectViaSocks5Async(activeSocks, host, port, 25000, token).ConfigureAwait(false)
                            : await ConnectDirectAsync(host, port, 15000, token).ConfigureAwait(false);

                        if (upstream == null)
                        {
                            await WriteAsciiAsync(clientStream, "HTTP/1.1 502 Bad Gateway\r\nContent-Length: 0\r\n\r\n").ConfigureAwait(false);
                            return;
                        }

                        trackedAsVpn = true;
                        TrackVpnSocket(upstream, client);

                        StringBuilder sb = new StringBuilder();
                        sb.Append(method).Append(' ').Append(relPath).Append(' ').Append(httpVer).Append("\r\n");
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
                        if (leftoverLen > 0)
                        {
                            await upStream.WriteAsync(headerBuf, bodyStart, leftoverLen, token).ConfigureAwait(false);
                        }

                        await RelayBidirectionalAsync(client, clientStream, upstream, upStream, token).ConfigureAwait(false);
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

        public static async Task<TcpClient> ConnectViaSocks5Async(int socksPort, string host, int port, int timeoutMs, CancellationToken token)
        {
            TcpClient tcp = new TcpClient();
            tcp.NoDelay = true;
            try
            {
                Task connectTask = tcp.ConnectAsync(IPAddress.Loopback, socksPort);
                Task delayTask = Task.Delay(timeoutMs, token);
                if (await Task.WhenAny(connectTask, delayTask).ConfigureAwait(false) != connectTask)
                {
                    try { tcp.Close(); } catch { }
                    return null;
                }
                await connectTask.ConfigureAwait(false);

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
                req[0] = 0x05; // VER
                req[1] = 0x01; // CMD = CONNECT
                req[2] = 0x00; // RSV
                req[3] = 0x03; // ATYP = DOMAINNAME
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
                if (repHead[3] == 0x01) skipBytes = 6; // IPv4 (4) + port (2)
                else if (repHead[3] == 0x04) skipBytes = 18; // IPv6 (16) + port (2)
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

        /// <summary>
        /// Performs a real HTTPS GET directly through the SOCKS5 tunnel (using SslStream)
        /// so dejavu cryptographically verifies the actual exit IP, city, and country!
        /// </summary>
        public static async Task<string> HttpsGetViaSocks5Async(int socksPort, string host, string path, int timeoutMs, CancellationToken token)
        {
            TcpClient tcp = await ConnectViaSocks5Async(socksPort, host, 443, timeoutMs, token).ConfigureAwait(false);
            if (tcp == null) return null;
            try
            {
                using (tcp)
                using (SslStream ssl = new SslStream(tcp.GetStream(), false))
                {
                    Task authTask = ssl.AuthenticateAsClientAsync(host, null, SslProtocols.Tls12, false);
                    if (await Task.WhenAny(authTask, Task.Delay(timeoutMs, token)).ConfigureAwait(false) != authTask)
                    {
                        return null;
                    }
                    await authTask.ConfigureAwait(false);

                    string req = "GET " + path + " HTTP/1.1\r\n" +
                                 "Host: " + host + "\r\n" +
                                 "User-Agent: dejavu-vpn/2.4\r\n" +
                                 "Accept: application/json\r\n" +
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
                            if (ms.Length > 65536) break;
                        }
                        byte[] raw = ms.ToArray();
                        int sep = FindHeaderEnd(raw, raw.Length);
                        if (sep < 0) return null;
                        string headers = Encoding.ASCII.GetString(raw, 0, sep);
                        string body = Encoding.UTF8.GetString(raw, sep + 4, raw.Length - (sep + 4));
                        if (headers.IndexOf("Transfer-Encoding: chunked", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            body = DecodeChunkedBody(body);
                        }
                        return body.Trim();
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        private static string DecodeChunkedBody(string input)
        {
            StringBuilder sb = new StringBuilder();
            int pos = 0;
            while (pos < input.Length)
            {
                int crlf = input.IndexOf("\r\n", pos, StringComparison.Ordinal);
                if (crlf < 0) break;
                string sizeHex = input.Substring(pos, crlf - pos).Trim();
                int semi = sizeHex.IndexOf(';');
                if (semi >= 0) sizeHex = sizeHex.Substring(0, semi);
                int chunkLen;
                if (!int.TryParse(sizeHex, System.Globalization.NumberStyles.HexNumber, null, out chunkLen) || chunkLen <= 0)
                {
                    break;
                }
                pos = crlf + 2;
                if (pos + chunkLen > input.Length) chunkLen = input.Length - pos;
                sb.Append(input, pos, chunkLen);
                pos += chunkLen + 2;
            }
            return sb.ToString();
        }

        private static async Task RelayBidirectionalAsync(TcpClient a, NetworkStream aStream, TcpClient b, NetworkStream bStream, CancellationToken token)
        {
            Task t1 = PumpHalfAsync(aStream, bStream, b, token);
            Task t2 = PumpHalfAsync(bStream, aStream, a, token);
            await Task.WhenAll(t1, t2).ConfigureAwait(false);
        }

        private static async Task PumpHalfAsync(NetworkStream src, NetworkStream dst, TcpClient dstClient, CancellationToken token)
        {
            byte[] buf = new byte[65536];
            try
            {
                while (!token.IsCancellationRequested)
                {
                    int n = await src.ReadAsync(buf, 0, buf.Length, token).ConfigureAwait(false);
                    if (n <= 0) break;
                    await dst.WriteAsync(buf, 0, n, token).ConfigureAwait(false);
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
