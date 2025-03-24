using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Models;
using RealEstate.Services;
using RealEStateProject.Models;
using RealEStateProject.Services.Interfaces;
using RealEState.api.ViewModels.User;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace RealEstate.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IAgentService _agentService;
        private readonly IPropertyService _propertyService;
        private readonly IFavoriteService _favoriteService;
        private readonly IReviewService _reviewService;
        private readonly IUserService _userService;

        public UserController(
            IAgentService agentService,
            IPropertyService propertyService,
            IFavoriteService favoriteService,
            IReviewService reviewService,
            IUserService userService)
        {
            _agentService = agentService;
            _propertyService = propertyService;
            _favoriteService = favoriteService;
            _reviewService = reviewService;
            _userService = userService;
        }

        // GET: api/User/Profile
        [HttpGet("Profile")]
        public async Task<IActionResult> GetUserProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
                return NotFound();

            // Check if the user is an agent
            var agent = await _agentService.GetAgentByUserIdAsync(userId);

            return Ok(new
            {
                User = user,
                IsAgent = agent != null,
                Agent = agent
            });
        }

        // POST: api/User/Reviews
        [HttpPost("Reviews")]
        public async Task<IActionResult> AddReview([FromBody] Review review)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Get the current user ID
                review.UserId = User.FindFirstValue(ClaimTypes.NameIdentifier);

                // Create the review
                await _reviewService.CreateAsync(review);

                return CreatedAtAction(nameof(GetReview), new { reviewId = review.Id }, review);
            }
            catch (Exception ex)
            {
                // Log the exception (ideally to a file or logging service)

                // Get inner exception message if it exists
                string errorMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;

                return StatusCode(500, new { message = "Error creating review: " + errorMessage });
            }
        }

        // GET: api/User/Reviews/{reviewId}
        [HttpGet("Reviews/{reviewId}")]
        public async Task<IActionResult> GetReview(int reviewId)
        {
            var review = await _reviewService.GetByIdAsync(reviewId);

            if (review == null)
                return NotFound();

            return Ok(review);
        }

        // DELETE: api/User/Reviews/{reviewId}
        [HttpDelete("Reviews/{reviewId}")]
        public async Task<IActionResult> RemoveReview(int reviewId)
        {
            var review = await _reviewService.GetByIdAsync(reviewId);
            if (review == null)
            {
                return NotFound();
            }

            // Ensure the review belongs to the current user
            string currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (review.UserId != currentUserId)
            {
                return Forbid();
            }

            int result = await _reviewService.DeleteAsync(reviewId);

            if (result > 0)
            {
                return NoContent();
            }

            return BadRequest(new { message = "Failed to delete review" });
        }

        // GET: api/User/Properties/{propertyId}/Reviews
        [HttpGet("Properties/{propertyId}/Reviews")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPropertyReviews(int propertyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 5)
        {
            // Get paginated reviews for the property
            var reviews = await _reviewService.GetReviewsByPropertyIdAsync(propertyId, page, pageSize);

            // Get total review count for pagination
            var totalReviews = await _reviewService.GetReviewCountForPropertyAsync(propertyId);

            // Calculate total pages
            var totalPages = (int)Math.Ceiling((double)totalReviews / pageSize);

            // Create a view model with reviews and pagination info
            var result = new
            {
                Reviews = reviews,
                CurrentPage = page,
                TotalPages = totalPages,
                TotalReviews = totalReviews
            };

            return Ok(result);
        }

        // GET: api/User/Profile/Edit
        [HttpGet("Profile/Edit")]
        public async Task<IActionResult> GetProfileForEdit()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userService.GetUserByIdAsync(userId);

            if (user == null)
                return NotFound();

            var agent = await _agentService.GetAgentByUserIdAsync(userId);

            var viewModel = new UserProfileViewModel
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                PhoneNumber = user.PhoneNumber,
                ProfilePicture = user.UserImageURL,
                IsAgent = agent != null
            };

            // Set agent properties if the user is an agent
            if (agent != null)
            {
                viewModel.LicenseNumber = agent.LicenseNumber;
                viewModel.Agency = agent.Agency;
                viewModel.Biography = agent.Biography;
                viewModel.YearsOfExperience = agent.YearsOfExperience;
            }

            return Ok(viewModel);
        }

        // PUT: api/User/Profile
        [HttpPut("Profile")]
        public async Task<IActionResult> UpdateProfile([FromForm] UserProfileViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                // Get the current user ID
                model.Id = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var user = await _userService.GetUserByIdAsync(model.Id);

                if (user == null)
                    return NotFound();

                // Handle profile image upload
                if (model.ProfileImageFile != null && model.ProfileImageFile.Length > 0)
                {
                    // Define allowed file extensions
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png" };
                    var extension = Path.GetExtension(model.ProfileImageFile.FileName).ToLowerInvariant();

                    if (!allowedExtensions.Contains(extension))
                    {
                        return BadRequest(new { message = "Only .jpg, .jpeg and .png files are allowed" });
                    }

                    // Save the file
                    var fileName = $"{Guid.NewGuid()}{extension}";
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "profiles");
                    Directory.CreateDirectory(uploadsFolder);
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await model.ProfileImageFile.CopyToAsync(fileStream);
                    }

                    user.UserImageURL = $"/uploads/profiles/{fileName}";
                }
                else if (!string.IsNullOrEmpty(model.ProfilePicture))
                {
                    user.UserImageURL = model.ProfilePicture;
                }

                // Update user properties
                user.FirstName = model.FirstName;
                user.LastName = model.LastName;
                user.Email = model.Email;
                user.PhoneNumber = model.PhoneNumber;
                await _userService.UpdateUserAsync(user);

                // Handle agent information
                var agent = await _agentService.GetAgentByUserIdAsync(model.Id);

                if (model.IsAgent)
                {
                    if (agent == null)
                    {
                        // Create new agent profile
                        agent = new Agent
                        {
                            UserId = model.Id,
                            LicenseNumber = model.LicenseNumber,
                            Agency = model.Agency,
                            Biography = model.Biography,
                            YearsOfExperience = model.YearsOfExperience ?? 0,
                            ProfileImageUrl = user.UserImageURL
                        };

                        await _agentService.CreateAgentAsync(agent);
                    }
                    else
                    {
                        // Update existing agent profile
                        agent.LicenseNumber = model.LicenseNumber;
                        agent.Agency = model.Agency;
                        agent.Biography = model.Biography;
                        agent.YearsOfExperience = model.YearsOfExperience ?? 0;
                        agent.ProfileImageUrl = user.UserImageURL;

                        await _agentService.UpdateAgentAsync(agent);
                    }
                }

                return Ok(new { message = "Profile updated successfully" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Error updating profile: " + ex.Message });
            }
        }
    }
}