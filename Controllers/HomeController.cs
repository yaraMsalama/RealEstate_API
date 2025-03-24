using Microsoft.AspNetCore.Mvc;
using RealEstate.Models;
using RealEstate.Services;
using RealEStateProject.Models;
using System.Diagnostics;

namespace RealEstate.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IPropertyService _propertyService;

        public HomeController(ILogger<HomeController> logger, IPropertyService propertyService)
        {
            _logger = logger;
            _propertyService = propertyService;
        }

        // Get featured and recent properties
        [HttpGet]
        public async Task<IActionResult> GetHomeData()
        {
            try
            {
                // Get properties from the database
                var allProperties = await _propertyService.GetAllPropertiesAsync(null);
                var propertiesList = allProperties.ToList();

                var homeData = new
                {
                    // Take 3 featured properties
                    FeaturedProperties = propertiesList.Take(3).ToList(),
                    // Take most recent properties (sorted by creation date)
                    RecentProperties = propertiesList.OrderByDescending(p => p.CreatedAt).Take(6).ToList()
                };

                return Ok(homeData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving home data");
                return StatusCode(500, "An error occurred while retrieving property data");
            }
        }

        // Get property types
        [HttpGet("property-types")]
        public IActionResult GetPropertyTypes()
        {
            var propertyTypes = Enum.GetValues(typeof(PropertyType))
                .Cast<PropertyType>()
                .Select(e => new
                {
                    Value = (int)e,
                    Name = e.ToString()
                });

            return Ok(propertyTypes);
        }

        // Get cities
        [HttpGet("cities")]
        public IActionResult GetCities()
        {
            var cities = Enum.GetValues(typeof(City))
                .Cast<City>()
                .Select(e => new
                {
                    Value = (int)e,
                    Name = e.ToString()
                });

            return Ok(cities);
        }

        // Basic info endpoint
        [HttpGet("about")]
        public IActionResult GetAboutInfo()
        {
            // Replace with actual about information relevant to your API
            return Ok(new
            {
                ApiName = "RealEstate API",
                Version = "1.0",
                Description = "API for accessing real estate property information"
            });
        }

        [HttpGet("error")]
        [ApiExplorerSettings(IgnoreApi = true)]
        public IActionResult Error()
        {
            return StatusCode(500, new
            {
                RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                Message = "An unexpected error occurred"
            });
        }
    }
}