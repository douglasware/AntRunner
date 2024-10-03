using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        services.AddHttpClient();

        // Load assemblies from environment variable if it exists
        var assemblies = Environment.GetEnvironmentVariable("ANTRUNNER_LOCALTOOL_ASSEMBLIES");
        if (!string.IsNullOrEmpty(assemblies))
        {
            var assemblyNames = assemblies.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries);
            foreach (var assemblyName in assemblyNames)
            {
                try
                {
                    // Load assembly by name
                    Assembly.Load(assemblyName.Trim());
                    Console.WriteLine($"Loaded assembly: {assemblyName.Trim()}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error loading assembly '{assemblyName.Trim()}': {ex.Message}");
                }
            }
        }
    })
    .Build();

host.Run();