using Grpc.Net.Client;

namespace DocMaster.Api.Services;

public interface IGrpcChannelFactory
{
    GrpcChannel GetChannel(string address);
}
