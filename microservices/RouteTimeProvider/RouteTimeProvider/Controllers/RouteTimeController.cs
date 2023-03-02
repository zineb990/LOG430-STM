using Ambassador;
using Ambassador.Usecases;
using ApplicationLogic.Usecases;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace PLACEHOLDER.Controllers
{
    [EnableCors("AllowOrigin")]
    [ApiController]
    [Route("[controller]/[action]")]
    public class RouteTimeController : ControllerBase
    {
        private readonly ILogger<RouteTimeController> _logger;

        private readonly TravelUC _travelUc = new ();

        private static readonly RegistrationUC _registration = new();

        public RouteTimeController(ILogger<RouteTimeController> logger)
        {
            _logger = logger;
            _registration.Register(ServiceTypes.RouteTimeProvider.ToString(), logger);
        }

        [HttpGet]
        [ActionName(nameof(Get))]
        public async Task<ActionResult<int>> Get(string startingCoordinates, string destinationCoordinates)
        {
            _logger.LogInformation($"Fetching car travel time from {startingCoordinates} to {destinationCoordinates}");

            var travelTime = await _travelUc.GetTravelTimeInSeconds(RemoveWhiteSpaces(startingCoordinates), RemoveWhiteSpaces(destinationCoordinates));

            return Ok(travelTime);

            string RemoveWhiteSpaces(string s)
                => s.Replace(" ", "");
        }
    }
}