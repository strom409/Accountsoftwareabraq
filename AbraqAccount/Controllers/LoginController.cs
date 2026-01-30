using AbraqAccount.Models.Common;
using AbraqAccount.Extensions;
using Microsoft.AspNetCore.Mvc;
using AbraqAccount.Services.Interfaces;
using System.Text.Json;

namespace AbraqAccount.Controllers;

[Route("account")]
public class LoginController : Controller
{
    private readonly IAccountService _accountService;

    public LoginController(IAccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost("login")]
    // [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] string username, [FromForm] string password)
    {
        var user = await _accountService.AuthenticateUserAsync(username, password);

        if (user == null)
        {
            return Redirect("/login?error=Invalid credentials");
        }

        var sessionData = new UserSession 
        { 
            UserId = user.Id, 
            Username = user.Username 
        };
        
        HttpContext.Session.SetObject(SessionKeys.UserSession, sessionData);

        return Redirect("/dashboard");
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return Redirect("/login");
    }
}
