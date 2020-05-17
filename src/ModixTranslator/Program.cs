using Discord;
using Discord.Commands;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System.Threading.Tasks;

namespace TranslatorBot9000
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureHostConfiguration(builder => {
                    builder.AddEnvironmentVariables("BOT_");
                    builder.AddCommandLine(args);
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    if(context.HostingEnvironment.IsDevelopment())
                    {
                        config.AddUserSecrets("4d9d6bca-ddc0-45ee-a876-74d5a6b1d83c");
                    }
                })
                .ConfigureLogging((context, builder) => {

                    builder.ClearProviders();
                    if(context.HostingEnvironment.IsDevelopment())
                    {
                        builder.AddDebug();
                    }

                    builder.AddConsole(o =>
                    {
                        if (context.HostingEnvironment.IsProduction())
                        {
                            o.DisableColors = true;
                        }
                        o.Format = ConsoleLoggerFormat.Systemd;
                        o.TimestampFormat = "o";

                    });
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton(context.Configuration);
                    services.AddHttpClient("translationClient");
                    services.AddHttpClient("authnClient");
                    services.AddSingleton(services =>
                    {
                        return new CommandService(new CommandServiceConfig
                        {
                            CaseSensitiveCommands = false,
                            DefaultRunMode = RunMode.Sync,
                            IgnoreExtraArgs = false,
                            LogLevel = LogSeverity.Verbose,
                            ThrowOnError = false
                        });
                    });

                    services.AddSingleton<ITranslationService, TranslationService>();

                    services.AddSingleton<ITranslationTokenProvider, TranslationTokenProvider>();
                    services.AddHostedService(provider =>
                    {
                        return provider.GetRequiredService<ITranslationTokenProvider>();
                    });

                    services.AddSingleton<IBotService, BotHostedService>();
                    services.AddHostedService(provider =>
                    {
                        return provider.GetRequiredService<IBotService>();
                    });

                    services.AddSingleton<ILocalizerHostedService, LocalizerHostedService>();
                    services.AddHostedService(provider =>
                    {
                        return provider.GetRequiredService<ILocalizerHostedService>();
                    });

                    services.AddHostedService<HostedCommandService>();
                    services.AddHostedService<ServerConfigurationHostedService>();
                    services.AddHostedService<TranslationsCategoryMaintenance>();
                });

            using var builtHost = host.Build();
            await builtHost.StartAsync();
            await builtHost.WaitForShutdownAsync();
        }
    }
}
