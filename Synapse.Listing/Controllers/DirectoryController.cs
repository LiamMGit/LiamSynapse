using Microsoft.AspNetCore.Mvc;
using Synapse.Listing.Services;

namespace Synapse.Listing.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class DirectoryController : ControllerBase
{
    private readonly ListingService _listingService;

    public DirectoryController(ListingService listingService)
    {
        _listingService = listingService;
    }

    [HttpGet]
    public JsonResult Get()
    {
        return _listingService.Listing;
    }
}
