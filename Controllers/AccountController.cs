using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Models;
using RealEstate.API.Models;
using RealEstate.API.Services.Interfaces;
using RealEstate.API.ViewModels.User;

namespace RealEStateProject.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAgentService _agentService;
        private readonly IJwtService _jwtService; // You'll need to create this service

        public AccountController(
                 UserManager<ApplicationUser> userManager,
                 SignInManager<ApplicationUser> signInManager,
                 IAgentService agentService,
                 IJwtService jwtService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _agentService = agentService;
            _jwtService = jwtService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(RegisterUserViewModel userFromRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userModel = new ApplicationUser
            {
                FirstName = userFromRequest.FirstName,
                LastName = userFromRequest.LastName,
                Email = userFromRequest.Email,
                UserName = userFromRequest.Username,
                UserType = userFromRequest.UserType,
                PhoneNumber = userFromRequest.PhoneNumber ?? "0000000"
            };

            IdentityResult result = await _userManager.CreateAsync(userModel, userFromRequest.Password);

            if (result.Succeeded)
            {
                // Assign role based on UserType
                if (userFromRequest.UserType == UserType.Agent)
                {
                    await _userManager.AddToRoleAsync(userModel, "Agent");

                    // Create the Agent entity
                    var agent = new Agent
                    {
                        UserId = userModel.Id,
                        LicenseNumber = "Pending",
                        Agency = "Pending",
                        Biography = "",
                        YearsOfExperience = 0,
                        User = userModel
                    };

                    await _agentService.AddAgentAsync(agent);
                }
                else
                {
                    await _userManager.AddToRoleAsync(userModel, "User");
                }

                // Generate JWT token
                var token = await _jwtService.GenerateJwtToken(userModel);

                return Ok(new
                {
                    Token = token,
                    UserId = userModel.Id,
                    Username = userModel.UserName,
                    UserType = userModel.UserType.ToString()
                });
            }

            // Return errors if registration failed
            var errors = new List<string>();
            foreach (var error in result.Errors)
            {
                errors.Add(error.Description);
            }

            return BadRequest(new { Errors = errors });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginUserViewModel userFromRequest)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userFromDatabase = await _userManager.FindByNameAsync(userFromRequest.Username);

            if (userFromDatabase == null)
                return Unauthorized(new { Message = "Invalid username or password" });

            var passwordValid = await _userManager.CheckPasswordAsync(userFromDatabase, userFromRequest.Password);

            if (!passwordValid)
                return Unauthorized(new { Message = "Invalid username or password" });

            // Add custom claims
            var claims = new List<Claim>
            {
                new Claim("UserType", userFromDatabase.UserType.ToString())
            };

            // Generate JWT token
            var token = await _jwtService.GenerateJwtToken(userFromDatabase, claims);

            return Ok(new
            {
                Token = token,
                UserId = userFromDatabase.Id,
                Username = userFromDatabase.UserName,
                UserType = userFromDatabase.UserType.ToString()
            });
        }

        [HttpPost("logout")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public IActionResult Logout()
        {
            // In JWT-based authentication, the client handles logout by removing the token
            return Ok(new { Message = "Logged out successfully" });
        }

        [HttpPost("external-login")]
        public IActionResult ExternalLogin(string provider)
        {
            // External login needs to be reimplemented for API - this is a placeholder
            // In a SPA/API scenario, this would typically be handled differently
            return BadRequest(new { Message = "External login not implemented for the API" });
        }

        [HttpGet("user-info")]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
        public async Task<IActionResult> GetUserInfo()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = await _userManager.FindByIdAsync(userId);

            if (user == null)
                return NotFound();

            return Ok(new
            {
                Id = user.Id,
                Username = user.UserName,
                Email = user.Email,
                FirstName = user.FirstName,
                LastName = user.LastName,
                UserType = user.UserType.ToString(),
                PhoneNumber = user.PhoneNumber
            });
        }

        [HttpGet("user-types")]
        public IActionResult GetUserTypes()
        {
            var userTypes = Enum.GetValues(typeof(UserType))
                .Cast<UserType>()
                .Select(e => new {
                    Value = (int)e,
                    Name = e.ToString()
                });

            return Ok(userTypes);
        }
    }
}