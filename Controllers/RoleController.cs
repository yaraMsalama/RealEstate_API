using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RealEstate.Services;
using System.Threading.Tasks;

namespace RealEStateProject.Controllers
{
    [Authorize(Roles = "Admin")]
    [ApiController]
    [Route("api/[controller]")]
    public class RoleController : ControllerBase
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        // GET: api/Role
        [HttpGet]
        public async Task<IActionResult> GetAllRoles()
        {
            var roles = await _roleService.GetAllRolesAsync();
            return Ok(roles);
        }

        // POST: api/Role
        [HttpPost]
        public async Task<IActionResult> AddRole([FromBody] RoleViewModel roleFromRequest)
        {
            if (ModelState.IsValid)
            {
                var result = await _roleService.CreateRoleAsync(roleFromRequest);
                if (result)
                {
                    return Ok(new { Message = $"Role '{roleFromRequest.RoleName}' created successfully!" });
                }
                else
                {
                    return BadRequest(new { Message = $"Role '{roleFromRequest.RoleName}' already exists or couldn't be created." });
                }
            }
            return BadRequest(ModelState);
        }

        // GET: api/Role/ManageUserRoles
        [HttpGet("ManageUserRoles")]
        public async Task<IActionResult> GetUsersWithRoles()
        {
            var model = await _roleService.GetUsersWithRolesAsync();
            return Ok(model);
        }

        // GET: api/Role/EditUserRoles/{userId}
        [HttpGet("EditUserRoles/{userId}")]
        public async Task<IActionResult> GetUserRolesForEdit(string userId)
        {
            var model = await _roleService.GetUserRolesForEditAsync(userId);
            if (model == null)
            {
                return NotFound();
            }
            return Ok(model);
        }

        // POST: api/Role/EditUserRoles
        [HttpPost("EditUserRoles")]
        public async Task<IActionResult> EditUserRoles([FromBody] EditUserRolesRequest request)
        {
            if (string.IsNullOrEmpty(request.UserId))
            {
                return NotFound();
            }

            var model = await _roleService.GetUserRolesForEditAsync(request.UserId);
            if (model == null)
            {
                return NotFound();
            }

            foreach (var role in model.UserRoles)
            {
                role.IsSelected = (role.RoleId == request.SelectedRoleId);
            }

            var result = await _roleService.UpdateUserRolesAsync(model);
            if (result)
            {
                return Ok(new { Message = "Role updated successfully!" });
            }

            return BadRequest(new { Message = "Error updating role." });
        }

        // DELETE: api/Role/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteRole(string id)
        {
            var role = await _roleService.GetRoleByIdAsync(id);
            if (role == null)
            {
                return NotFound(new { Message = "Role not found." });
            }

            var usersInRole = await _roleService.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Any())
            {
                return BadRequest(new { Message = $"Cannot delete role '{role.Name}' because it's assigned to {usersInRole.Count} user(s). Remove all users from this role first." });
            }

            var result = await _roleService.DeleteRoleAsync(id);
            if (result)
            {
                return Ok(new { Message = "Role deleted successfully!" });
            }
            else
            {
                return BadRequest(new { Message = "Error deleting role. It might be a system role or in use." });
            }
        }

        // DELETE: api/Role/DeleteUser/{userId}
        [HttpDelete("DeleteUser/{userId}")]
        public async Task<IActionResult> DeleteUser(string userId)
        {
            var result = await _roleService.DeleteUserAsync(userId);
            if (result)
            {
                return Ok(new { Message = "User deleted successfully!" });
            }
            else
            {
                return BadRequest(new { Message = "Error deleting user." });
            }
        }
    }

    public class EditUserRolesRequest
    {
        public string UserId { get; set; }
        public string SelectedRoleId { get; set; }
    }
}