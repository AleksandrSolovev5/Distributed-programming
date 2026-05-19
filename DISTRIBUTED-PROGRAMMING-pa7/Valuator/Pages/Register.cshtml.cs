using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Valuator.Services;

namespace Valuator.Pages;

public class RegisterModel : PageModel
{
    private readonly UserService _userService;

    public RegisterModel( UserService userService )
    {
        _userService = userService;
    }

    [BindProperty]
    public string Login { get; set; } = "";

    [BindProperty]
    public string Password { get; set; } = "";

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        RegisterResult result = await _userService.RegisterAsync( Login, Password );

        if ( !result.Success || result.User is null )
        {
            ModelState.AddModelError( string.Empty, result.ErrorMessage );
            return Page();
        }

        await SignInUserAsync( result.User );

        return RedirectToPage( "/Index" );
    }

    private async Task SignInUserAsync( UserAccount user )
    {
        var claims = new List<Claim>
        {
            new Claim( ClaimTypes.NameIdentifier, user.Id ),
            new Claim( ClaimTypes.Name, user.Login )
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme );

        var principal = new ClaimsPrincipal( identity );

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal );
    }
}