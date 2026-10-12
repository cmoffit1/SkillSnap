using System.Security.Cryptography;
using System.Text;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SkillSnap.Api.Auth;
using SkillSnap.Api.Contracts.Auth;
using SkillSnap.Api.Data;

namespace SkillSnap.Api.Controllers;

[ApiController]
[Route("auth")]
[EnableRateLimiting("auth")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    SkillSnapContext dbContext,
    JwtTokenService tokenService,
    ILogger<AuthController> logger) : ControllerBase
{
    private static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
    private const string RefreshCookieName = "__Secure-SkillSnapRefresh";
    private const string RefreshRequestHeader = "X-SkillSnap-Refresh";

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Register(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new
            {
                errors = createResult.Errors.Select(error => error.Description)
            });
        }

        var roleResult = await userManager.AddToRoleAsync(user, Roles.Staff);
        if (!roleResult.Succeeded)
        {
            var cleanupResult = await userManager.DeleteAsync(user);
            if (!cleanupResult.Succeeded)
            {
                logger.LogError(
                    "Failed to remove newly registered user {UserId} after role assignment failed: {Errors}",
                    user.Id,
                    string.Join("; ", cleanupResult.Errors.Select(error => error.Description)));
            }

            logger.LogError(
                "Failed to assign the Staff role to newly registered user {UserId}: {Errors}",
                user.Id,
                string.Join("; ", roleResult.Errors.Select(error => error.Description)));
            return Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Unauthorized();
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(
            user,
            request.Password,
            lockoutOnFailure: true);
        if (!signInResult.Succeeded)
        {
            return Unauthorized();
        }

        return await CreateSessionAsync(user, cancellationToken);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Refresh(
        [FromHeader(Name = RefreshRequestHeader)] string? requestMarker,
        CancellationToken cancellationToken)
    {
        if (requestMarker != "1" ||
            !Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        var tokenHash = HashRefreshToken(refreshToken);
        var storedToken = await dbContext.RefreshTokens
            .Include(token => token.User)
            .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        if (storedToken.RevokedUtc is not null)
        {
            await RevokeAllRefreshTokensAsync(storedToken.UserId, cancellationToken);
            DeleteRefreshCookie();
            logger.LogWarning("Detected reuse of a revoked refresh token for user {UserId}.", storedToken.UserId);
            return Unauthorized();
        }

        if (storedToken.ExpiresUtc <= DateTime.UtcNow ||
            await userManager.IsLockedOutAsync(storedToken.User))
        {
            DeleteRefreshCookie();
            return Unauthorized();
        }

        var replacementValue = CreateRefreshTokenValue();
        var replacementHash = HashRefreshToken(replacementValue);
        var now = DateTime.UtcNow;
        storedToken.RevokedUtc = now;
        storedToken.ReplacedByTokenHash = replacementHash;
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = storedToken.UserId,
            TokenHash = replacementHash,
            CreatedUtc = now,
            ExpiresUtc = now.Add(RefreshTokenLifetime)
        });

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await RevokeAllRefreshTokensAsync(storedToken.UserId, cancellationToken);
            DeleteRefreshCookie();
            logger.LogWarning(
                "Detected concurrent refresh-token use for user {UserId}; revoked active refresh tokens.",
                storedToken.UserId);
            return Unauthorized();
        }

        SetRefreshCookie(replacementValue);
        return await CreateAuthResponseAsync(storedToken.User, cancellationToken);
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(
        [FromHeader(Name = RefreshRequestHeader)] string? requestMarker,
        CancellationToken cancellationToken)
    {
        if (requestMarker != "1")
        {
            return BadRequest();
        }

        if (Request.Cookies.TryGetValue(RefreshCookieName, out var refreshToken) &&
            !string.IsNullOrWhiteSpace(refreshToken))
        {
            var tokenHash = HashRefreshToken(refreshToken);
            var storedToken = await dbContext.RefreshTokens
                .SingleOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);
            if (storedToken is { RevokedUtc: null })
            {
                storedToken.RevokedUtc = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        DeleteRefreshCookie();
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public ActionResult<CurrentUserResponse> GetCurrentUser()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ??
                     User.FindFirstValue("sub");
        var email = User.FindFirstValue(ClaimTypes.Email) ??
                    User.FindFirstValue("email");
        if (userId is null || email is null)
        {
            return Unauthorized();
        }

        var roles = User.FindAll(ClaimTypes.Role).Select(claim => claim.Value).ToArray();
        return Ok(new CurrentUserResponse(userId, email, roles));
    }

    private async Task<ActionResult<AuthResponse>> CreateSessionAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var refreshToken = CreateRefreshTokenValue();
        var now = DateTime.UtcNow;
        dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedUtc = now,
            ExpiresUtc = now.Add(RefreshTokenLifetime)
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        SetRefreshCookie(refreshToken);
        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    private async Task<ActionResult<AuthResponse>> CreateAuthResponseAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var token = await tokenService.CreateAsync(user, cancellationToken);
        var roles = await userManager.GetRolesAsync(user);
        return Ok(new AuthResponse(
            token.Token,
            token.ExpiresUtc,
            user.Id,
            user.Email ?? string.Empty,
            roles.ToArray()));
    }

    private async Task RevokeAllRefreshTokensAsync(string userId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        await dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedUtc == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(token => token.RevokedUtc, now),
                cancellationToken);
    }

    private void SetRefreshCookie(string token)
    {
        Response.Cookies.Append(RefreshCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/auth",
            MaxAge = RefreshTokenLifetime,
            IsEssential = true
        });
    }

    private void DeleteRefreshCookie()
    {
        Response.Cookies.Delete(RefreshCookieName, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/auth"
        });
    }

    private static string CreateRefreshTokenValue() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    private static string HashRefreshToken(string token) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
