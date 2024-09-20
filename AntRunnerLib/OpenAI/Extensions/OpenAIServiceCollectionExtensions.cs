using Microsoft.Extensions.DependencyInjection;
using OpenAI.Interfaces;
using OpenAI.Managers;

namespace OpenAI.Extensions;

public static class OpenAiServiceCollectionExtensions
{
    public static IHttpClientBuilder AddOpenAiService(this IServiceCollection services, Action<OpenAiOptions>? setupAction = null)
    {
        var optionsBuilder = services.AddOptions<OpenAiOptions>();
        optionsBuilder.BindConfiguration(OpenAiOptions.SettingKey);
        if (setupAction != null)
        {
            optionsBuilder.Configure(setupAction);
        }

        return services.AddHttpClient<IOpenAiService, OpenAiService>();
    }

    public static IHttpClientBuilder AddOpenAiService<TServiceInterface>(this IServiceCollection services, string name, Action<OpenAiOptions>? setupAction = null) where TServiceInterface : class, IOpenAiService
    {
        var optionsBuilder = services.AddOptions<OpenAiOptions>(name);
        optionsBuilder.BindConfiguration($"{OpenAiOptions.SettingKey}:{name}");
        if (setupAction != null)
        {
            optionsBuilder.Configure(setupAction);
        }

        return services.AddHttpClient<TServiceInterface>();
    }
}