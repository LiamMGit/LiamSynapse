// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.SystemConsole.Themes;
using Synapse.Networking.Models;
using Synapse.TestClient;
using Synapse.TestClient.Extras;

using IHost host =  Host.CreateDefaultBuilder()
    .ConfigureServices((context, services) =>
    {
        services.AddSingleton<ListingService>();
        services.AddSingleton<ClientService>();

        services.AddHttpClient(
            "listing",
            (_, client) =>
            {
                string fullUrl = context.Configuration.GetRequiredSection("Listing").Get<string>() ??
                                 throw new InvalidOperationException();
                int id = fullUrl.LastIndexOf('/');
                client.BaseAddress = new Uri(fullUrl[..(id + 1)]);
            });
    })
    .UseSerilog(
        (context, config) =>
        {
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "server.log");
            config
                .ReadFrom.Configuration(context.Configuration)
                .WriteTo.Console(theme: AnsiConsoleTheme.Literate, applyThemeToRedirectedOutput: true)
                .WriteTo.File(filePath);
        })
    .Build();

ClientService clientService = host.Services.GetService<ClientService>() ?? throw new InvalidOperationException();
CancellationTokenSource tokenSource = new();

while (true)
{
    string? line = Console.ReadLine();
    if (string.IsNullOrEmpty(line))
    {
        continue;
    }

    line.SplitCommand(out string command, out string arguments);

    switch (command)
    {
        case "stop":
            tokenSource.Cancel();
            tokenSource = new CancellationTokenSource();
            break;

        case "deploy":
            arguments.SplitCommand(out string amountString, out string durationString);


            if (int.TryParse(amountString, out int amount))
            {
                if (!string.IsNullOrWhiteSpace(durationString) &&
                    int.TryParse(durationString, out int duration))
                {
                    duration *= 1000;
                }
                else
                {
                    duration = -1;
                }

                _ = clientService.MassDeploy(amount, duration, tokenSource.Token);
            }
            else
            {
                Console.WriteLine("Invalid parameters");
            }

            break;

        case "score":
            _ = clientService.Score(tokenSource.Token);
            break;

        case "send":
            clientService.SendRandomMessages();
            break;

        case "roll":
            foreach (Client client in clientService.Clients)
            {
                _ = client.Send(ServerOpcode.Command, "roll");
            }

            break;

        default:
            Console.WriteLine($"Unrecognized command: [{command}]");
            break;
    }
}
