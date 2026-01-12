using DocMaster.Api.Configuration;
using DocMaster.Api.Data;
using DocMaster.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<ErasureCodingOptions>(
    builder.Configuration.GetSection(ErasureCodingOptions.SectionName));
builder.Services.Configure<NodeHealthOptions>(
    builder.Configuration.GetSection(NodeHealthOptions.SectionName));
builder.Services.Configure<UploadOptions>(
    builder.Configuration.GetSection(UploadOptions.SectionName));
builder.Services.Configure<MimeDetectionOptions>(
    builder.Configuration.GetSection(MimeDetectionOptions.SectionName));

// Database
builder.Services.AddDbContext<DocMasterDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Services
builder.Services.AddSingleton<IGrpcChannelFactory, GrpcChannelFactory>();
builder.Services.AddSingleton<INodeCache, NodeCache>();
builder.Services.AddScoped<IMimeDetector, MimeDetector>();
builder.Services.AddScoped<IStreamProcessor, StreamProcessor>();
builder.Services.AddScoped<INodeSelector, NodeSelector>();
builder.Services.AddScoped<IShardUploader, ShardUploader>();
builder.Services.AddScoped<IShardDownloader, ShardDownloader>();
builder.Services.AddScoped<IObjectService, ObjectService>();
builder.Services.AddScoped<IBucketService, BucketService>();
builder.Services.AddScoped<INodeService, NodeService>();

// Background services
builder.Services.AddHostedService<NodeHealthService>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DocMasterDbContext>();
    await db.Database.MigrateAsync();
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseRouting();
app.MapControllers();

await app.RunAsync();

// Make Program accessible for integration tests
public partial class Program { }
