using System.Reflection;
using DocMaster.Api.Configuration;
using DocMaster.Api.Data;
using DocMaster.Api.Services;
using DocMaster.ErasureCoding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;

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
builder.Services.AddSingleton<IErasureCoder>(sp =>
{
    var options = sp.GetRequiredService<IOptions<ErasureCodingOptions>>().Value;
    return new IsaLErasureCoder(options.DataShards, options.ParityShards);
});

// Background services
builder.Services.AddHostedService<NodeHealthService>();

// API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "DocMaster API",
        Description = "Distributed Object Storage with Erasure Coding - A fault-tolerant storage system built with .NET 9.0",
        Contact = new OpenApiContact
        {
            Name = "DocMaster",
            Url = new Uri("https://github.com/Langusia/DocMaster")
        },
        License = new OpenApiLicense
        {
            Name = "MIT",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

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
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DocMaster API v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseRouting();
app.MapControllers();

await app.RunAsync();

// Make Program accessible for integration tests
public partial class Program { }
