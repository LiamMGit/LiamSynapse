using Microsoft.AspNetCore.Mvc;
using Synapse.Listing.Services;

namespace Synapse.Listing.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DirectoryController(ListingService listingService) : ControllerBase
{
    [HttpGet]
    public JsonResult Get()
    {
        return listingService.Listing;
    }
}
