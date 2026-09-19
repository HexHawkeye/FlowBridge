using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FlowBridge.Web.Pages;

public sealed class LoginModel(IConfiguration configuration) : PageModel
{
    [BindProperty] public string Username { get; set; } = "";
    [BindProperty] public string Password { get; set; } = "";
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var expectedUser = configuration["Admin:Username"] ?? "admin";
        var expectedHash = configuration["Admin:PasswordSha256"] ?? "";
        var actualHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Password))).ToLowerInvariant();
        if (!string.Equals(Username, expectedUser, StringComparison.Ordinal) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(actualHash), Encoding.ASCII.GetBytes(expectedHash)))
        {
            ModelState.AddModelError("", "Invalid username or password.");
            return Page();
        }
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, Username)], CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
        return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) ? "/" : ReturnUrl);
    }
}
