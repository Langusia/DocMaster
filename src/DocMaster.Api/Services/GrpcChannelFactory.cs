using System.Collections.Concurrent;
using Grpc.Net.Client;

namespace DocMaster.Api.Services;

public class GrpcChannelFactory : IGrpcChannelFactory, IDisposable
{
    private readonly ConcurrentDictionary<string, GrpcChannel> _channels = new();
    private bool _disposed;

    public GrpcChannel GetChannel(string address)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var normalizedAddress = NormalizeAddress(address);

        return _channels.GetOrAdd(normalizedAddress, addr =>
        {
            var options = new GrpcChannelOptions
            {
                HttpHandler = new SocketsHttpHandler
                {
                    EnableMultipleHttp2Connections = true,
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(5),
                    KeepAlivePingDelay = TimeSpan.FromSeconds(60),
                    KeepAlivePingTimeout = TimeSpan.FromSeconds(30)
                }
            };

            return GrpcChannel.ForAddress(addr, options);
        });
    }

    private static string NormalizeAddress(string address)
    {
        if (!address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return $"http://{address}";
        }

        return address;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        foreach (var channel in _channels.Values)
        {
            channel.Dispose();
        }

        _channels.Clear();
    }
}
