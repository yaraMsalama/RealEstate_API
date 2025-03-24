using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using RealEstate.Models;
using RealEstate.Services;
using RealEStateProject.Services.Interfaces;

namespace RealEstate.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PropertyController : ControllerBase
    {
        private readonly IPropertyService _propertyService;
        private readonly IFavoriteService _favoriteService;
        private readonly IMessageService _messageService;
        private readonly IAgentService _agentService;
        private readonly IMapper _mapper;
        private readonly ILogger<PropertyController> _logger;

        public PropertyController(
            IPropertyService propertyService,
            IFavoriteService favoriteService,
            IMessageService messageService,
            IAgentService agentService,
            IMapper mapper,
            ILogger<PropertyController> logger)
        {
            _propertyService = propertyService;
            _favoriteService = favoriteService;
            _messageService = messageService;
            _agentService = agentService;
            _mapper = mapper;
            _logger = logger;
        }

        // GET: api/Property/agent-properties
        [HttpGet("agent-properties")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Agent")]
        public async Task<IActionResult> GetAgentProperties()
        {
            var userId = GetUserId();
            var properties = await _propertyService.GetPropertiesByAgentIdAsync(userId, userId);
            return Ok(properties);
        }

        // GET: api/Property/search
        [HttpGet("search")]
        public async Task<IActionResult> SearchProperties([FromQuery] PropertySearchFilterViewModel filter,
                                                         [FromQuery] int page = 1,
                                                         [FromQuery] int pageSize = 10)
        {
            var userId = User.Identity?.IsAuthenticated == true ? GetUserId() : null;
            var searchResults = await _propertyService.SearchPropertiesAsync(filter, userId, page, pageSize);

            if (userId != null)
            {
                var favorites = await _favoriteService.GetFavoritesByUserIdAsync(userId);
                // Add favorite status to each property in the results
                foreach (var property in searchResults.Properties)
                {
                    property.IsFavorite = favorites.Any(f => f.PropertyId == property.Id);
                }
            }

            return Ok(searchResults);
        }

        // GET: api/Property/5
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProperty(int id)
        {
            var userId = User.Identity?.IsAuthenticated == true ? GetUserId() : null;
            var property = await _propertyService.GetPropertyByIdAsync(id, userId);

            if (property == null)
            {
                return NotFound();
            }

            property.Agent = await _agentService.GetAgentByPropertyIdAsync(id);

            if (userId != null)
            {
                property.IsFavorite = await _favoriteService.IsFavoriteAsync(id, userId);
            }

            return Ok(property);
        }

        // GET: api/Property/types
        [HttpGet("types")]
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

        // GET: api/Property/statuses
        [HttpGet("statuses")]
        public IActionResult GetPropertyStatuses()
        {
            var propertyStatuses = Enum.GetValues(typeof(PropertyStatus))
                .Cast<PropertyStatus>()
                .Select(e => new
                {
                    Value = (int)e,
                    Name = e.ToString()
                });

            return Ok(propertyStatuses);
        }

        // POST: api/Property
        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Agent")]
        public async Task<IActionResult> CreateProperty([FromBody] PropertyViewModel viewModel)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            var propertyId = await _propertyService.AddPropertyAsync(viewModel, userId);

            return CreatedAtAction(nameof(GetProperty), new { id = propertyId }, null);
        }

        // PUT: api/Property/5
        [HttpPut("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Agent")]
        public async Task<IActionResult> UpdateProperty(int id, [FromBody] PropertyViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return BadRequest("Property ID mismatch");
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserId();
            try
            {
                await _propertyService.UpdatePropertyAsync(viewModel, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating property");
                return StatusCode(500, "An error occurred while updating the property");
            }

            return NoContent();
        }

        // DELETE: api/Property/5
        [HttpDelete("{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Agent")]
        public async Task<IActionResult> DeleteProperty(int id)
        {
            var userId = GetUserId();
            var property = await _propertyService.GetPropertyByIdAsync(id, userId);

            if (property == null)
            {
                return NotFound();
            }

            await _propertyService.DeleteAsync(id);
            return NoContent();
        }

        // POST: api/Property/favorite/5
        [HttpPost("favorite/{propertyId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> ToggleFavorite(int propertyId)
        {
            var userId = GetUserId();
            var isFavorite = await _favoriteService.IsFavoriteAsync(propertyId, userId);

            if (isFavorite)
            {
                await _favoriteService.RemoveFavoriteAsync(propertyId, userId);
                isFavorite = false;
            }
            else
            {
                await _favoriteService.AddFavoriteAsync(propertyId, userId);
                isFavorite = true;
            }

            return Ok(new { isFavorite });
        }

        // DELETE: api/Property/favorite/5
        [HttpDelete("favorite/{propertyId}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> RemoveFavorite(int propertyId)
        {
            var userId = GetUserId();
            await _favoriteService.RemoveFavoriteAsync(propertyId, userId);
            return NoContent();
        }

        // GET: api/Property/favorites
        [HttpGet("favorites")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetFavorites()
        {
            var userId = GetUserId();
            var favorites = await _favoriteService.GetFavoritesByUserIdAsync(userId);
            return Ok(favorites);
        }

        // POST: api/Property/contact
        [HttpPost("contact")]
        public async Task<IActionResult> ContactAgent([FromBody] ContactAgentViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Make sure the AgentId is set if it's not coming from the request
                if (string.IsNullOrEmpty(model.AgentId))
                {
                    // Get the property to find its agent
                    var propertyViewModel = await _propertyService.GetPropertyByIdAsync(model.PropertyId, null);

                    if (propertyViewModel != null)
                    {
                        // Use the AgentId from the PropertyViewModel
                        model.AgentId = propertyViewModel.AgentId;
                    }
                    else
                    {
                        return NotFound("Property not found");
                    }
                }

                await _messageService.SendContactMessageAsync(model);
                return Ok(new { success = true, message = "Message sent successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending contact message");
                return StatusCode(500, "An error occurred while sending your message");
            }
        }

        // GET: api/Property/messages
        [HttpGet("messages")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetMessages()
        {
            var userId = GetUserId();
            var messages = await _messageService.GetMessagesByAgentIdAsync(userId);
            return Ok(messages);
        }

        // PUT: api/Property/messages/5/read
        [HttpPut("messages/{id}/read")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> MarkMessageAsRead(int id)
        {
            await _messageService.MarkAsReadAsync(id);
            return NoContent();
        }

        // DELETE: api/Property/messages/5
        [HttpDelete("messages/{id}")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            await _messageService.DeleteAsync(id);
            return NoContent();
        }

        private string GetUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier);
        }
    }
}