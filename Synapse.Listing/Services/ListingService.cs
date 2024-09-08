using Microsoft.AspNetCore.Mvc;

namespace Synapse.Listing.Services;

public class ListingService
{
    public ListingService(ILogger<ListingService> logger, IConfiguration config)
    {
        Listing = new JsonResult(
            config.GetRequiredSection("Listing").Get<Networking.Models.Listing>()! with { Guid = Guid.NewGuid().ToString() });
        ////Listing = new JsonResult(config.GetRequiredSection("Listing").Get<Listing>());
        logger.LogInformation("{Listing}", Listing.Value);
    }

    public JsonResult Listing { get; }
}
