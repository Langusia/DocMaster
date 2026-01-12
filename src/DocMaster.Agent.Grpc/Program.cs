using DocMaster.Agent.Grpc.Configuration;
using DocMaster.Agent.Grpc.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<AgentOptions>(
    builder.Configuration.GetSection(AgentOptions.SectionName));

// Services
builder.Services.AddSingleton<IPathBuilder, PathBuilder>();

// gRPC
builder.Services.AddGrpc(options =>
{
    options.MaxReceiveMessageSize = 20 * 1024 * 1024; // 20MB
    options.MaxSendMessageSize = 20 * 1024 * 1024;
});

var app = builder.Build();

// Ensure data directory exists
var basePath = builder.Configuration.GetValue<string>("Agent:BasePath") ?? "/data";
Directory.CreateDirectory(basePath);

app.MapGrpcService<StorageServiceImpl>();

app.MapGet("/", () => "DocMaster Storage Agent - Use gRPC client to communicate");

await app.RunAsync();
