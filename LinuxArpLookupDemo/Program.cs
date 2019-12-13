using System;
using System.Net;
using System.Threading.Tasks;
using System.Linq;

namespace LinuxArpLookupDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            if (args?.Length != 1)
            {
                Console.WriteLine("This program expects excalty one parameter, which is an IP address.");
                return;
            }

            var ipStr = args[0];
            if (!IPAddress.TryParse(ipStr, out var ip))
            {
                Console.WriteLine("The given parameter could not be parsed as IP address.");
                return;
            }

            Console.WriteLine($"Trying to find mac of {ip}...");
            var mac = await LinuxArp.PingThenReadArpTable(ip, TimeSpan.FromMilliseconds(1500)).ConfigureAwait(false);
            if (mac == null)
                Console.WriteLine("mac not found");
            else
                Console.WriteLine($"mac is {GetMacString(mac.GetAddressBytes())}");
        }

        public static string GetMacString(byte[] value)
        {
            return string.Join(":", from z in value select z.ToString("X2"));
        }
    }
}
