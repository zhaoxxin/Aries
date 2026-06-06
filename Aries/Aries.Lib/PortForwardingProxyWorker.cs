using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Aries.Lib
{
    internal sealed class PortForwardingProxyWorker
    {
        private readonly int localPort;
        private readonly string remoteHost;
        private readonly int remotePort;
        private readonly object sessionLock = new object();

        private CancellationTokenSource cancellationTokenSource;
        private TcpListener listener;
        private NetworkStream activeUpstreamStream;

        public event WarpMessage WarpMessage;

        public PortForwardingProxyWorker(int localPort, string remoteHost, int remotePort)
        {
            this.localPort = localPort;
            this.remoteHost = remoteHost;
            this.remotePort = remotePort;
        }

        public void Start()
        {
            if (listener != null)
            {
                return;
            }

            cancellationTokenSource = new CancellationTokenSource();
            listener = new TcpListener(IPAddress.Any, localPort);
            listener.Start();
            SendMessage("登录代理入口已启动 本机:" + localPort + " -> " + remoteHost + ":" + remotePort);
            Task.Run(() => AcceptLoopAsync(cancellationTokenSource.Token));
        }

        public void Stop()
        {
            try
            {
                cancellationTokenSource?.Cancel();
                listener?.Stop();
            }
            catch
            {
            }
            finally
            {
                listener = null;
                ClearSession();
            }
        }

        public bool TrySendPacket(byte[] packet)
        {
            if (packet == null || packet.Length == 0)
            {
                SendErrorMessage("登录包为空，sendPacket 已拒绝");
                return false;
            }

            NetworkStream stream;
            lock (sessionLock)
            {
                stream = activeUpstreamStream;
            }

            if (stream == null)
            {
                SendErrorMessage("请在客户端进入到官方登录页面后再点击登陆");
                return false;
            }

            try
            {
                stream.Write(packet, 0, packet.Length);
                stream.Flush();
                SendMessage("登录包已注入 " + packet.Length + " bytes");
                return true;
            }
            catch (Exception ex)
            {
                SendErrorMessage("登录包注入失败：" + ex.Message);
                ClearSession();
                return false;
            }
        }

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                TcpClient downstream = null;
                try
                {
                    downstream = await listener.AcceptTcpClientAsync();
                    Task ignored = Task.Run(() => HandleClientAsync(downstream, token));
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (InvalidOperationException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        SendErrorMessage("登录代理监听失败：" + ex.Message);
                    }

                    downstream?.Close();
                    break;
                }
            }
        }

        private async Task HandleClientAsync(TcpClient downstream, CancellationToken token)
        {
            using (downstream)
            using (TcpClient upstream = new TcpClient())
            {
                try
                {
                    await upstream.ConnectAsync(remoteHost, remotePort);
                    SetSession(upstream.GetStream());
                    Task uplink = PumpAsync(downstream.GetStream(), upstream.GetStream(), token);
                    Task downlink = PumpAsync(upstream.GetStream(), downstream.GetStream(), token);
                    await Task.WhenAny(uplink, downlink);
                }
                catch (Exception ex)
                {
                    if (!token.IsCancellationRequested)
                    {
                        SendErrorMessage("登录代理链路失败：" + ex.Message);
                    }
                }
                finally
                {
                    ClearSession();
                }
            }
        }

        private static async Task PumpAsync(NetworkStream input, NetworkStream output, CancellationToken token)
        {
            byte[] buffer = new byte[4096];

            while (!token.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await input.ReadAsync(buffer, 0, buffer.Length, token);
                }
                catch
                {
                    break;
                }

                if (read <= 0)
                {
                    break;
                }

                try
                {
                    await output.WriteAsync(buffer, 0, read, token);
                    output.Flush();
                }
                catch
                {
                    break;
                }
            }
        }

        private void SetSession(NetworkStream upstreamStream)
        {
            lock (sessionLock)
            {
                activeUpstreamStream = upstreamStream;
            }

            SendMessage("登录代理会话已建立");
        }

        private void ClearSession()
        {
            bool hadSession;
            lock (sessionLock)
            {
                hadSession = activeUpstreamStream != null;
                activeUpstreamStream = null;
            }

            if (hadSession)
            {
                SendMessage("登录代理会话已关闭");
            }
        }

        private void SendMessage(string message)
        {
            WarpMessage?.Invoke(MessageType.Tips, message);
        }

        private void SendErrorMessage(string message)
        {
            WarpMessage?.Invoke(MessageType.Error, message);
        }
    }
}
