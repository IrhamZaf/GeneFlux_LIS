using LIS.Contracts.Auth;
using LIS.Models;
using LIS.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace LIS.Controllers;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CurrentUserService _currentUserService;

    public AuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        CurrentUserService currentUserService)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _currentUserService = currentUserService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "Email and password are required." });

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not ApplicationUser appUser || !appUser.IsActive)
            return Unauthorized(new { message = "Invalid login attempt." });

        var result = await _signInManager.PasswordSignInAsync(request.Email, request.Password, request.RememberMe, lockoutOnFailure: false);
        if (!result.Succeeded)
            return Unauthorized(new { message = "Invalid login attempt." });

        var reloadedUser = await _currentUserService.GetByIdAsync(appUser.Id);
        if (reloadedUser == null)
        {
            await _signInManager.SignOutAsync();
            return Unauthorized(new { message = "Unable to load user context." });
        }

        return Ok(_currentUserService.BuildCurrentUserContext(reloadedUser));
    }
}
