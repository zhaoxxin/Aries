using Aris.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aries.Lib
{
    public class PortForwardingService
    {
        public WarpMessage WarpMessage;

        private Dictionary<int, PortForwardingWorker> workers;
        private PortForwardingProxyWorker loginWorker;

        public PortForwardingService()
        {
            this.workers = new Dictionary<int, PortForwardingWorker>();
        }

        #region LifeCycle

        public async void Launch(Action<bool> callback)
        {
            await Task.Run(() =>
            {
                SendMessage("正在开启端口映射...");
                if (loginWorker != null)
                {
                    loginWorker.Start();
                }
                foreach (PortForwardingWorker worker in workers.Values)
                {
                    if (!worker.IsRunning)
                    {
                        worker.Start();
                    }
                }
                callback(true);
            });

        }

        public void Stop()
        {
            SendMessage("正在停止端口映射...");
            foreach (PortForwardingWorker worker in workers.Values)
            {
                try
                {
                    worker.Stop();
                }
                catch (Exception ex)
                {
                    SendErrorMessage($"停止端口映射出错：{ex}");
                }
            }
            workers.Clear();

            if (loginWorker != null)
            {
                loginWorker.Stop();
                loginWorker = null;
            }
            SendMessage("端口映射已停止");
        }

        #endregion

        #region Manage

        public void AddForwarding(int localPort, string host, int port)
        {
            var worker = workers.ContainsKey(localPort) ? workers[localPort] : null;
            if (worker == null)
            {
                worker = new PortForwardingWorker(localPort, host, port);
                worker.show += SendMessage;
                workers.Add(localPort, worker);
            }
        }

        public void SetLoginForwarding(int localPort, string host, int port)
        {
            if (loginWorker != null)
            {
                loginWorker.Stop();
            }

            loginWorker = new PortForwardingProxyWorker(localPort, host, port);
            loginWorker.WarpMessage += ForwardLoginWorkerMessage;
        }

        public bool sendPacket(byte[] packet)
        {
            if (loginWorker == null)
            {
                SendErrorMessage("登录代理尚未初始化，请先点击启动");
                return false;
            }

            return loginWorker.TrySendPacket(packet);
        }

        #endregion


        /// <summary>
        /// 发送消息
        /// </summary>
        /// <param name="Msg"></param>
        private void SendMessage(string Msg)
        {
            WarpMessage?.Invoke(MessageType.Tips, Msg);
        }
        /// <summary>
        /// 发送错误消息
        /// </summary>
        /// <param name="Msg"></param>
        private void SendErrorMessage(string Msg)
        {
            WarpMessage?.Invoke(MessageType.Error, Msg);
        }

        private void ForwardLoginWorkerMessage(MessageType type, string message)
        {
            if (type == MessageType.Tips)
            {
                SendMessage(message);
                return;
            }

            SendErrorMessage(message);
        }

    }
}
