using CLFunctionApp.Utility.cs;
using FunctionApp1.Utility.cs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    { ConfigureServices(context.Configuration, services); })
    .Build();

static void ConfigureServices(IConfiguration configuration,
    IServiceCollection services)
{
    services.AddScoped<ICraigsListScraper, CraigsListWebDriverScraper>();
    services.AddSingleton<DiscordLogger>();
}

host.Run();
