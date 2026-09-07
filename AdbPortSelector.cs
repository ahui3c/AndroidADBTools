using System;
using System.Net;
using System.Net.Sockets;

namespace AndroidADBTools
{
    public sealed class AdbPortSelection
    {
        public int Port { get; private set; }
        public bool UsedFallback { get; private set; }
        public string Reason { get; private set; }

        public AdbPortSelection(int port, bool usedFallback, string reason)
        {
            Port = port;
            UsedFallback = usedFallback;
            Reason = reason ?? "";
        }
    }

    public static class AdbPortSelector
    {
        public const int DefaultPort = 5037;

        public static AdbPortSelection Select()
        {
            SocketError error;
            if (CanBind(DefaultPort, out error) || error == SocketError.AddressAlreadyInUse)
                return new AdbPortSelection(DefaultPort, false, "");

            int fallback = FindAvailableFallbackPort(-1, true);
            if (fallback <= 0)
                return new AdbPortSelection(DefaultPort, false,
                    "找不到可供 ADB 使用的本機 TCP 連接埠。");

            string reason = error == SocketError.AccessDenied
                ? "Windows 已保留或禁止 ADB 的預設 TCP 5037 連接埠"
                : "ADB 的預設 TCP 5037 連接埠目前無法使用";
            return new AdbPortSelection(fallback, true,
                reason + "，已自動改用 " + fallback + "。");
        }

        public static AdbPortSelection SelectFallback(int failedPort)
        {
            int fallback = FindAvailableFallbackPort(failedPort, false);
            if (fallback <= 0 || fallback == failedPort)
                return new AdbPortSelection(failedPort, false,
                    "ADB 無法使用 TCP " + failedPort + "，且找不到其他可用連接埠。");
            return new AdbPortSelection(fallback, true,
                "ADB 無法從 TCP " + failedPort + " 啟動，已自動改用 " + fallback + " 重試。");
        }

        public static bool IsDaemonStartupFailure(string detail)
        {
            string value = detail ?? "";
            return value.IndexOf("cannot bind", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("10013", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("could not read ok from ADB Server", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("failed to start daemon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("cannot connect to daemon", StringComparison.OrdinalIgnoreCase) >= 0 ||
                value.IndexOf("protocol fault", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int FindAvailableFallbackPort(int skipPort, bool reuseFirstListener)
        {
            int[][] ranges =
            {
                new[] { 5200, 5299 },
                new[] { 5400, 5499 },
                new[] { 5600, 5699 }
            };
            foreach (int[] range in ranges)
            {
                for (int port = range[0]; port <= range[1]; port++)
                {
                    if (port == skipPort) continue;
                    SocketError error;
                    if (CanBind(port, out error)) return port;
                    if (reuseFirstListener && port == 5200 &&
                        error == SocketError.AddressAlreadyInUse) return port;
                }
            }
            return -1;
        }

        private static bool CanBind(int port, out SocketError error)
        {
            TcpListener listener = null;
            error = SocketError.Success;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Server.ExclusiveAddressUse = true;
                listener.Start();
                return true;
            }
            catch (SocketException ex)
            {
                error = ex.SocketErrorCode;
                return false;
            }
            catch
            {
                error = SocketError.SocketError;
                return false;
            }
            finally
            {
                if (listener != null)
                {
                    try { listener.Stop(); } catch { }
                }
            }
        }
    }
}
