using Host.Worker;
using Microsoft.Extensions.Hosting;
using Gma.Framework.Logging.Serilog;
using Gma.Framework.ModuleComposition;

HostApplicationBuilder builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.AddConfiguredSerilog();
builder.AddWorkerHost();
builder.ValidateModuleComposition();

using IHost host = builder.Build();
host.LogWorkerStartupSummary();
await host.RunAsync().ConfigureAwait(false);
