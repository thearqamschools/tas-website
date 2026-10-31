using System;
using System.Linq;
using System.Net;
using System.Web;

namespace TheArqamSchools.Web.Infrastructure
{
    /// <summary>
    /// The visitor's real IP address, used for rate limits. Behind Cloudflare's proxy
    /// every request arrives from a Cloudflare address, so all visitors would share one
    /// limit. The CF-Connecting-IP header is therefore used, but only when the request
    /// really comes from Cloudflare's published ranges (https://www.cloudflare.com/ips/);
    /// from anywhere else the header could be forged and is ignored.
    /// </summary>
    public static class ClientAddress
    {
        private static readonly Range[] Cloudflare = new[]
        {
            "173.245.48.0/20", "103.21.244.0/22", "103.22.200.0/22", "103.31.4.0/22", "141.101.64.0/18",
            "108.162.192.0/18", "190.93.240.0/20", "188.114.96.0/20", "197.234.240.0/22", "198.41.128.0/17",
            "162.158.0.0/15", "104.16.0.0/13", "104.24.0.0/14", "172.64.0.0/13", "131.0.72.0/22",
            "2400:cb00::/32", "2606:4700::/32", "2803:f800::/32", "2405:b500::/32", "2405:8100::/32",
            "2a06:98c0::/29", "2c0f:f248::/32"
        }.Select(Range.Parse).ToArray();

        public static string Get(HttpRequestBase request)
        {
            var remote = request.UserHostAddress;
            IPAddress remoteIp;
            if (string.IsNullOrEmpty(remote) || !IPAddress.TryParse(remote, out remoteIp)) return remote ?? "unknown";

            var forwarded = request.Headers["CF-Connecting-IP"];
            IPAddress visitor;
            if (!string.IsNullOrEmpty(forwarded) && IPAddress.TryParse(forwarded.Trim(), out visitor) && IsCloudflare(remoteIp))
                return visitor.ToString();
            return remoteIp.ToString();
        }

        public static bool IsCloudflare(IPAddress ip)
        {
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            foreach (var r in Cloudflare) if (r.Contains(ip)) return true;
            return false;
        }

        private class Range
        {
            private byte[] _network;
            private int _bits;

            public static Range Parse(string cidr)
            {
                var parts = cidr.Split('/');
                return new Range { _network = IPAddress.Parse(parts[0]).GetAddressBytes(), _bits = int.Parse(parts[1]) };
            }

            public bool Contains(IPAddress ip)
            {
                var bytes = ip.GetAddressBytes();
                if (bytes.Length != _network.Length) return false;
                var full = _bits / 8;
                for (var i = 0; i < full; i++) if (bytes[i] != _network[i]) return false;
                var rest = _bits % 8;
                if (rest == 0) return true;
                var mask = (byte)(0xFF << (8 - rest));
                return (bytes[full] & mask) == (_network[full] & mask);
            }
        }
    }
}
