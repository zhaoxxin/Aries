using System;
using System.Management;
using System.Text;

namespace Aries.Lib
{
    public static class LoginPacketBuilder
    {
        private static readonly byte[] EmptyMacAddress = new byte[6];

        public static byte[] Build(string username, string password, Action<string> log)
        {
            // 这个包只负责给官方登录页注入 CMS143 账号密码，不扩展其他协议字段。
            byte[] header = new byte[] { 0x69, 0x69, 0x00, 0x00 };
            byte[] usernameBytes = Encoding.ASCII.GetBytes(username ?? string.Empty);
            byte[] usernameLength = ToLittleEndianShort((short)usernameBytes.Length);
            byte[] passwordBytes = Encoding.ASCII.GetBytes(password ?? string.Empty);
            byte[] passwordLength = ToLittleEndianShort((short)passwordBytes.Length);
            byte[] macAddress = GetNetworkAdapterId(log);
            byte[] reservedMiddle = new byte[15];
            byte[] reservedAfterUser = new byte[2];

            int payloadLength = macAddress.Length
                + reservedMiddle.Length
                + usernameLength.Length
                + usernameBytes.Length
                + reservedAfterUser.Length
                + passwordLength.Length
                + passwordBytes.Length;

            header[2] = checked((byte)payloadLength);

            byte[] packet = new byte[header.Length + payloadLength];
            int offset = 0;

            Buffer.BlockCopy(header, 0, packet, offset, header.Length);
            offset += header.Length;
            Buffer.BlockCopy(macAddress, 0, packet, offset, macAddress.Length);
            offset += macAddress.Length;
            Buffer.BlockCopy(reservedMiddle, 0, packet, offset, reservedMiddle.Length);
            offset += reservedMiddle.Length;
            Buffer.BlockCopy(usernameLength, 0, packet, offset, usernameLength.Length);
            offset += usernameLength.Length;
            Buffer.BlockCopy(usernameBytes, 0, packet, offset, usernameBytes.Length);
            offset += usernameBytes.Length;
            Buffer.BlockCopy(reservedAfterUser, 0, packet, offset, reservedAfterUser.Length);
            offset += reservedAfterUser.Length;
            Buffer.BlockCopy(passwordLength, 0, packet, offset, passwordLength.Length);
            offset += passwordLength.Length;
            Buffer.BlockCopy(passwordBytes, 0, packet, offset, passwordBytes.Length);

            return packet;
        }

        private static byte[] GetNetworkAdapterId(Action<string> log)
        {
            try
            {
                foreach (ManagementObject instance in new ManagementClass("Win32_NetworkAdapterConfiguration").GetInstances())
                {
                    object ipEnabledValue = instance["IPEnabled"];
                    if (!(ipEnabledValue is bool) || !(bool)ipEnabledValue)
                    {
                        continue;
                    }

                    string macAddress = instance["MacAddress"] as string;
                    if (string.IsNullOrWhiteSpace(macAddress))
                    {
                        continue;
                    }

                    string[] chunks = macAddress.Trim().Split(new[] { ':', '-' }, StringSplitOptions.RemoveEmptyEntries);
                    if (chunks.Length != EmptyMacAddress.Length)
                    {
                        continue;
                    }

                    byte[] buffer = new byte[EmptyMacAddress.Length];
                    for (int index = 0; index < buffer.Length; index++)
                    {
                        buffer[index] = Convert.ToByte(chunks[index], 16);
                    }

                    return buffer;
                }

                log?.Invoke("未找到可用网卡 MAC，登录包按 6 字节零值退化");
            }
            catch (Exception ex)
            {
                log?.Invoke("读取网卡 MAC 失败，登录包按 6 字节零值退化：" + ex.Message);
            }

            return (byte[])EmptyMacAddress.Clone();
        }

        private static byte[] ToLittleEndianShort(short value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (!BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return bytes;
        }
    }
}
