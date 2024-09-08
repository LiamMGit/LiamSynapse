using Microsoft.Extensions.Configuration;

namespace Synapse.Server.Services;

public interface IDirectoryService
{
    public string ActiveDirectory { get; }

    public string EventDirectory { get; }
}

public class DirectoryService : IDirectoryService
{
    public DirectoryService(IConfiguration config)
    {
        IConfigurationSection eventSection = config.GetRequiredSection("Event");
        string eventName = eventSection.GetRequiredSection("Title").Get<string>() ??
                           throw new InvalidOperationException();
        ActiveDirectory = config.GetValue<string>("Directory") ?? Directory.GetCurrentDirectory();
        string fileName = new(
            eventName
                .Select(
                    n =>
                    {
                        if (char.IsLetter(n) || char.IsNumber(n))
                        {
                            return n;
                        }

                        return '_';
                    })
                .ToArray());
        EventDirectory = Directory.CreateDirectory(Path.Combine(ActiveDirectory, fileName)).FullName;
    }

    public string ActiveDirectory { get; }

    public string EventDirectory { get; }
}
