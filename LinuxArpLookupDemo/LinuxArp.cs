using System;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LinuxArpLookupDemo
{
    public static class LinuxArp
    {
        private const string ArpTablePath = "/proc/net/arp";
        private static readonly Regex lineRegex = new Regex(@"^((?:[0-9]{1,3}\.){3}[0-9]{1,3})(?:\s+\w+){2}\s+((?:[0-9A-Fa-f]{2}[:-]){5}(?:[0-9A-Fa-f]{2}))");

        public static bool IsWindows() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static bool IsPlatformSupported()
        {
            if (IsWindows())
                return false;
            return File.Exists(ArpTablePath);
        }

        public static async Task<PhysicalAddress> PingThenReadArpTable(IPAddress ip, TimeSpan timeout)
        {
            if (!IsPlatformSupported())
                throw new PlatformNotSupportedException();
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(ip, (int)timeout.TotalMilliseconds).ConfigureAwait(false);
            return await TryReadFromArpTable(ip).ConfigureAwait(false);
        }

        public static async Task<PhysicalAddress> TryReadFromArpTable(IPAddress ip)
        {
            if (!IsPlatformSupported())
                throw new PlatformNotSupportedException();
            using var arpFile = new FileStream(ArpTablePath, FileMode.Open, FileAccess.Read);
            return await ParseProcNetArp(arpFile, ip).ConfigureAwait(false);
        }

        private static async Task<PhysicalAddress> ParseProcNetArp(Stream content, IPAddress ip)
        {
            using var reader = new StreamReader(content);
            await reader.ReadLineAsync().ConfigureAwait(false); // first line is header
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(line))
                    return null;
                try
                {
                    var mac = ParseIfMatch(line, ip);
                    if (mac != null)
                        return mac;
                }
                catch (FormatException)
                {
                    throw new PlatformNotSupportedException(); ;
                }
            }
            return null;
        }

        private static PhysicalAddress ParseIfMatch(string line, IPAddress ip)
        {
            var m = lineRegex.Match(line);
            if (!m.Success || m.Groups.Count != 3)
                throw new FormatException($"The given line '{line}' was not in the expected /proc/net/arp format.");
            var tableIpStr = m.Groups[1].Value;
            var tableMacStr = m.Groups[2].Value;
            var tableIp = IPAddress.Parse(tableIpStr);
            if (!tableIp.Equals(ip))
                return null;
            return ParseMacAddress(tableMacStr);
        }

        private static PhysicalAddress ParseMacAddress(this string mac)
        {
            var macString = mac?.Replace(":", "-", StringComparison.Ordinal)?.ToUpper() ?? throw new ArgumentNullException(nameof(mac));
            return PhysicalAddress.Parse(macString);
        }
    }
}
