using Microsoft.SemanticKernel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Manufactron.Client.Plugins;

namespace Manufactron.Client.Services;

public static class PluginRegistration
{
    public static void RegisterManufacturingPlugins(this Kernel kernel, IServiceProvider serviceProvider, string aggregatorUrl)
    {
        var httpClient = serviceProvider.GetRequiredService<HttpClient>();
        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        // Register Equipment Plugin
        var equipmentPlugin = new EquipmentPlugin(
            httpClient,
            loggerFactory.CreateLogger<EquipmentPlugin>(),
            aggregatorUrl);
        kernel.ImportPluginFromObject(equipmentPlugin, "Equipment");

        // Register Context Plugin
        var contextPlugin = new ContextPlugin(
            httpClient,
            loggerFactory.CreateLogger<ContextPlugin>(),
            aggregatorUrl);
        kernel.ImportPluginFromObject(contextPlugin, "Context");

        // Register Analytics Plugin
        var analyticsPlugin = new AnalyticsPlugin(
            httpClient,
            loggerFactory.CreateLogger<AnalyticsPlugin>(),
            aggregatorUrl);
        kernel.ImportPluginFromObject(analyticsPlugin, "Analytics");
    }
}