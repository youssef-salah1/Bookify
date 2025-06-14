using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.Web.CodeGenerators.Mvc.Templates.BlazorIdentity.Pages.Manage;
using System.Data;
using System.Security.Claims;
namespace Bookify.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IMapper _mapper;

        public UsersController(UserManager<ApplicationUser> userManager, IMapper mapper, RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _mapper = mapper;
            _roleManager = roleManager;

        }

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var model = _mapper.Map<IEnumerable<UserViewModel>>(users);
            return View(model);
        }
        
        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> Create()
        {
            var model = new UserFormViewModel
            {
                Roles = await _roleManager.Roles.
                Select(r => new SelectListItem { Text = r.Name, Value = r.Name }).ToListAsync()
            };
            return PartialView("_Form", model);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserFormViewModel model)
        {

            if (!ModelState.IsValid)
                return BadRequest();

            ApplicationUser user = new()
            {
                UserName = model.UserName,
                FullName = model.FullName,
                Email = model.Email,
                CreatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value
            };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
                return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));

            await _userManager.AddToRolesAsync(user, model.SelectedRoles);
            var viewModel = _mapper.Map<UserViewModel>(user);
            return PartialView("_UserRow", viewModel);
        }
        [HttpGet]
        [AjaxOnly]
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if(user is null) return NotFound();
            var model = _mapper.Map<UserFormViewModel>(user);

            model.Roles = await _roleManager.Roles.
            Select(r => new SelectListItem { Text = r.Name, Value = r.Name }).ToListAsync();
            
            model.SelectedRoles = await _userManager.GetRolesAsync(user);
            return PartialView("_Form", model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserFormViewModel model)
        {

            if (!ModelState.IsValid)
                return BadRequest();
            var user = await _userManager.FindByIdAsync(model.Id);
            if( user is null )
                return NotFound();
            user = _mapper.Map(model, user);

            user.LastUpdatedOn = DateTime.Now;
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var prevRoles = await _userManager.GetRolesAsync(user);
                if (!prevRoles.SequenceEqual(model.SelectedRoles))
                {
                    await _userManager.RemoveFromRolesAsync(user, prevRoles);
                    await _userManager.AddToRolesAsync(user, model.SelectedRoles);
                }
                var viewModel = _mapper.Map<UserViewModel>(user);
                return PartialView("_UserRow", viewModel);
            }
            return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
        }

        [HttpGet]
        [AjaxOnly]
        public IActionResult ResetPassword(string id)
        {
            UserPassWordFormViewModel viewModel = new()
            {
                Id = id
            };
            return PartialView("_ResetPassWord", viewModel);
        }
        
        [HttpPost]
        [ValidateAntiForgeryToken]
        
        public async Task<IActionResult> ResetPassword(UserPassWordFormViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user is null)
                return NotFound();
            var PasswordHash = user.PasswordHash;
            user.LastUpdatedOn = DateTime.Now;
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            await _userManager.RemovePasswordAsync(user);
            var result = await _userManager.AddPasswordAsync(user, model.Password);
            if (!result.Succeeded)
            {
                user.PasswordHash = PasswordHash;
                await _userManager.UpdateAsync(user);
                return BadRequest(string.Join(',', result.Errors.Select(e => e.Description)));
            }
            await _userManager.UpdateAsync(user);
            var viewModel = _mapper.Map<UserViewModel>(user);
            return PartialView("_UserRow", viewModel);
        }
        
        public async Task<IActionResult> ToggleStatus(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user is null)
                return NotFound();
            user.IsDeleted = !user.IsDeleted;
            user.LastUpdatedOn = DateTime.Now;
            user.LastUpdatedById = User.FindFirst(ClaimTypes.NameIdentifier)!.Value;
            await _userManager.UpdateAsync(user);
            return Ok(user.LastUpdatedOn.ToString());
        }
        
        public async Task<IActionResult> AllowItemByName(UserFormViewModel model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            var isAllowed = user is null || user.Id.Equals(model.Id);
            return Json(isAllowed);
        }
        
        public async Task<IActionResult> AllowItemByEmail(UserFormViewModel model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            var isAllowed = user is null || user.Id.Equals(model.Id);
            return Json(isAllowed);
        }
    }
}
