using System.Text.Json;
using System.Text.Json.Nodes;
using VpnSc.Helpers;
using VpnSc.Models;

namespace VpnSc.Services;

public static class XrayConfigBuilder
{
    public static JsonObject Build(
        VpnService.ServerInfo server,
        string userUuid,
        FragmentationSettings fragmentationSettings,
        string? fingerprintOverride)
    {
        var network = string.IsNullOrEmpty(server.Type) ? "tcp" : server.Type;
        var security = string.IsNullOrEmpty(server.Security) ? "reality" : server.Security;

        var streamSettings = new JsonObject
        {
            ["network"] = network,
            ["security"] = security
        };

        var fingerprint = ResolveFingerprint(server, fingerprintOverride);

        if (security == "reality")
        {
            streamSettings["realitySettings"] = new JsonObject
            {
                ["publicKey"] = server.Pbk,
                ["fingerprint"] = fingerprint,
                ["serverName"] = server.Sni,
                ["shortId"] = server.Sid,
                ["spiderX"] = string.IsNullOrEmpty(server.Spx) ? "/" : server.Spx
            };
        }
        else if (security == "tls")
        {
            streamSettings["tlsSettings"] = new JsonObject
            {
                ["serverName"] = string.IsNullOrEmpty(server.Sni) ? server.Host : server.Sni,
                ["fingerprint"] = fingerprint,
                ["allowInsecure"] = false
            };
        }

        if (network == "ws")
        {
            streamSettings["wsSettings"] = new JsonObject
            {
                ["path"] = string.IsNullOrEmpty(server.Path)
                    ? (string.IsNullOrEmpty(server.Spx) ? "/" : server.Spx)
                    : server.Path,
                ["headers"] = new JsonObject
                {
                    ["Host"] = string.IsNullOrEmpty(server.HostHeader)
                        ? (string.IsNullOrEmpty(server.Sni) ? server.Host : server.Sni)
                        : server.HostHeader
                }
            };
        }
        else if (network == "grpc")
        {
            var grpc = new JsonObject
            {
                ["serviceName"] = string.IsNullOrEmpty(server.ServiceName)
                    ? server.Spx ?? ""
                    : server.ServiceName
            };
            if (!string.IsNullOrEmpty(server.Authority))
                grpc["authority"] = server.Authority;
            streamSettings["grpcSettings"] = grpc;
        }
        else if (network is "h2" or "http")
        {
            streamSettings["httpSettings"] = new JsonObject
            {
                ["path"] = string.IsNullOrEmpty(server.Path)
                    ? (string.IsNullOrEmpty(server.Spx) ? "/" : server.Spx)
                    : server.Path,
                ["host"] = new JsonArray(JsonValue.Create(
                    string.IsNullOrEmpty(server.HostHeader)
                        ? (string.IsNullOrEmpty(server.Sni) ? server.Host : server.Sni)
                        : server.HostHeader))
            };
        }

        ApplyFinalMask(streamSettings, security, server.FinalMask, fragmentationSettings);

        var userSettings = new JsonObject
        {
            ["id"] = string.IsNullOrEmpty(server.Id) ? userUuid : server.Id,
            ["encryption"] = "none"
        };
        if (!string.IsNullOrEmpty(server.Flow) && server.Flow != "none")
            userSettings["flow"] = server.Flow;

        return new JsonObject
        {
            ["log"] = new JsonObject { ["loglevel"] = "warning" },
            ["api"] = new JsonObject
            {
                ["tag"] = "api",
                ["listen"] = "127.0.0.1:10085",
                ["services"] = JsonNode.Parse(
                    """["StatsService","LoggerService","HandlerService","ReflectionService"]""")
            },
            ["stats"] = new JsonObject(),
            ["policy"] = new JsonObject
            {
                ["levels"] = new JsonObject
                {
                    ["0"] = new JsonObject
                    {
                        ["statsUserUplink"] = true,
                        ["statsUserDownlink"] = true,
                        ["statsUserOnline"] = true
                    }
                },
                ["system"] = new JsonObject
                {
                    ["statsInboundUplink"] = true,
                    ["statsInboundDownlink"] = true,
                    ["statsOutboundUplink"] = true,
                    ["statsOutboundDownlink"] = true
                }
            },
            ["inbounds"] = new JsonArray(new JsonObject
            {
                ["port"] = 1080,
                ["protocol"] = "socks",
                ["settings"] = new JsonObject
                {
                    ["auth"] = "noauth",
                    ["udp"] = true
                },
                ["tag"] = "socks-inbound"
            }),
            ["outbounds"] = new JsonArray(
                new JsonObject
                {
                    ["protocol"] = "vless",
                    ["settings"] = new JsonObject
                    {
                        ["vnext"] = new JsonArray(new JsonObject
                        {
                            ["address"] = server.Host,
                            ["port"] = server.Port,
                            ["users"] = new JsonArray(userSettings)
                        })
                    },
                    ["streamSettings"] = streamSettings,
                    ["tag"] = "proxy"
                },
                new JsonObject
                {
                    ["protocol"] = "freedom",
                    ["settings"] = new JsonObject(),
                    ["tag"] = "direct"
                }),
            ["routing"] = new JsonObject
            {
                ["domainStrategy"] = "IPIfNonMatch",
                ["rules"] = new JsonArray(
                    new JsonObject
                    {
                        ["type"] = "field",
                        ["outboundTag"] = "direct",
                        ["domain"] = new JsonArray(
                            JsonValue.Create("regexp:.*\\.ru$"),
                            JsonValue.Create("regexp:.*\\.xn--p1ai$"))
                    },
                    new JsonObject
                    {
                        ["type"] = "field",
                        ["outboundTag"] = "proxy",
                        ["ip"] = new JsonArray(JsonValue.Create("geoip:!cn"))
                    },
                    new JsonObject
                    {
                        ["type"] = "field",
                        ["outboundTag"] = "direct",
                        ["ip"] = new JsonArray(JsonValue.Create("geoip:private"))
                    })
            }
        };
    }

    public static async Task<string> WriteConfigFileAsync(JsonObject config)
    {
        var dir = FileManagerService.GetConnectDirectory();
        Directory.CreateDirectory(dir);
        var configPath = Path.Combine(dir, "config.json");
        var json = config.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        await FileCompat.WriteAllTextAsync(configPath, json, FileCompat.Utf8NoBom);
        return configPath;
    }

    public static JsonObject? ParseFinalMaskFromQuery(string? fmEncoded)
    {
        if (string.IsNullOrWhiteSpace(fmEncoded))
            return null;
        try
        {
            var decoded = Uri.UnescapeDataString(fmEncoded);
            var node = JsonNode.Parse(decoded);
            return node as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private static string ResolveFingerprint(VpnService.ServerInfo server, string? fingerprintOverride)
    {
        if (!string.IsNullOrWhiteSpace(fingerprintOverride))
            return fingerprintOverride;
        return string.IsNullOrEmpty(server.Fp) ? "chrome" : server.Fp;
    }

    private static void ApplyFinalMask(
        JsonObject streamSettings,
        string security,
        JsonObject? serverFinalMask,
        FragmentationSettings fragmentationSettings)
    {
        if (serverFinalMask != null && serverFinalMask.Count > 0)
        {
            streamSettings["finalmask"] = serverFinalMask.DeepClone();
            return;
        }

        if (!fragmentationSettings.Enabled)
            return;
        if (security != "tls" && security != "reality")
            return;

        if (streamSettings["sockopt"] is JsonObject sockopt &&
            sockopt["dialerProxy"] != null)
            return;

        streamSettings["finalmask"] = new JsonObject
        {
            ["tcp"] = new JsonArray(new JsonObject
            {
                ["type"] = "fragment",
                ["settings"] = new JsonObject
                {
                    ["packets"] = fragmentationSettings.Packets,
                    ["length"] = fragmentationSettings.Length,
                    ["delay"] = fragmentationSettings.Interval,
                    ["maxSplit"] = fragmentationSettings.MaxSplit
                }
            })
        };
    }
}
