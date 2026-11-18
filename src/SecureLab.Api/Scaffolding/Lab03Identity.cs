using Microsoft.AspNetCore.Identity;
using SecureLab.Api.Data;
using SecureLab.Api.Data.Entities;

namespace SecureLab.Api.Scaffolding;

public static class Lab03Identity
{
    public static void AddLab03Identity(this WebApplicationBuilder builder)
    {
        builder.Services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.User.AllowedUserNameCharacters =
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";

            options.Password.RequiredLength = 10;
            options.Password.RequireDigit = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireUppercase = true;
            options.Password.RequireNonAlphanumeric = true;
        }).AddEntityFrameworkStores<SecureLabDbContext>().AddDefaultTokenProviders();
        builder.Services.AddAuthorization();
        builder.Services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = builder.Environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            };
        });
    }

    public static void MapLab03Identity(this WebApplication app)
    {
        
app.MapPost("/api/auth/register", async (RegistrationInput input, UserManager<ApplicationUser> users) =>
{
    var userName = input.UserName?.Trim();
    var email = input.Email?.Trim();
    var displayName = input.DisplayName?.Trim();
    var password = input.Password;

    if (string.IsNullOrWhiteSpace(userName) || userName.Length < 3 || userName.Length > 30)
        return Results.BadRequest(new { message = "Invalid registration data" });

    if (string.IsNullOrWhiteSpace(email) || email.Length > 254
        || !System.Net.Mail.MailAddress.TryCreate(email, out var address)
        || !string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { message = "Invalid registration data" });

    if (string.IsNullOrWhiteSpace(displayName) || displayName.Length < 2 || displayName.Length > 100)
        return Results.BadRequest(new { message = "Invalid registration data" });

    if (string.IsNullOrEmpty(password) || password.Length > 128)
        return Results.BadRequest(new { message = "Invalid registration data" });

    var user = new ApplicationUser
    {
        Id = Guid.NewGuid(),
        UserName = userName,
        Email = email,
        DisplayName = displayName
    };

    var result = await users.CreateAsync(user, password);

    if (!result.Succeeded)
        return Results.BadRequest(new { message = "Registration failed" });

    var role = await users.AddToRoleAsync(user, DbSeeder.ReporterRole);

    if (!role.Succeeded)
        throw new InvalidOperationException("Default role assignment failed.");

    return Results.Created("/api/me", new
    {
        user.Id,
        user.UserName,
        user.DisplayName
    });
});

        app.MapPost("/api/auth/login", async (LoginInput input, SignInManager<ApplicationUser> signIn) =>
        {
            if (string.IsNullOrWhiteSpace(input.UserName) || string.IsNullOrEmpty(input.Password))
                return Results.Unauthorized();
            var result = await signIn.PasswordSignInAsync(input.UserName, input.Password, false, false);
            return result.Succeeded ? Results.NoContent() : Results.Unauthorized();
        });
        app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signIn) =>
{
    await signIn.SignOutAsync();
    return Results.NoContent();
}).RequireAuthorization();
        // Навмисно хибна навчальна довіра: лабораторна №3 замінює заявлений ID на перевірену особу.
        app.MapGet("/api/me", async (HttpContext context, UserManager<ApplicationUser> users) =>
{
    var user = await users.GetUserAsync(context.User);

    if (user is null)
        return Results.Unauthorized();

    return Results.Ok(new
    {
        user.Id,
        user.UserName,
        user.DisplayName
    });
}).RequireAuthorization();
        // Допоміжна перевірка сесії: повертає успішну відповідь лише авторизованому користувачу.
        app.MapGet("/api/auth/session-check", () => Results.NoContent()).RequireAuthorization();
    }
}

public sealed record RegistrationInput(string? UserName, string? Email, string? DisplayName, string? Password);
public sealed record LoginInput(string? UserName, string? Password);
