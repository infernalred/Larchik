using System.Security.Claims;
using Larchik.API.Controllers;
using Larchik.API.DTOs;
using Larchik.API.Services;
using Larchik.Application.Tests.TestInfrastructure;
using Larchik.Persistence.Constants;
using Larchik.Persistence.Context;
using Larchik.Persistence.Entities;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Larchik.Application.Tests.API;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task Register_TrimsInput_AssignsUserRole_AndSendsConfirmationEmail()
    {
        await using var harness = await AccountControllerTestHarness.CreateAsync();
        var controller = harness.CreateController();

        var result = await controller.Register(new RegisterDto
        {
            Email = "  USER@example.com  ",
            UserName = "  demo-user  ",
            Password = "StrongPass1"
        });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var dto = Assert.IsType<UserDto>(created.Value);
        var user = await harness.UserManager.FindByEmailAsync("user@example.com");

        Assert.NotNull(user);
        Assert.Equal("USER@example.com", user!.Email);
        Assert.Equal("demo-user", user.UserName);
        Assert.Equal("USER@EXAMPLE.COM", user.NormalizedEmail);
        Assert.Equal("DEMO-USER", user.NormalizedUserName);
        Assert.False(dto.EmailConfirmed);
        Assert.Contains(Roles.User, dto.Roles);
        Assert.Single(harness.EmailSender.Messages);
        Assert.Contains("/auth/confirm-email", harness.EmailSender.Messages[0].HtmlMessage, StringComparison.Ordinal);
        Assert.Contains("https://frontend.example", harness.EmailSender.Messages[0].HtmlMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_RejectsDuplicateEmail_IgnoringWhitespaceAndCase()
    {
        await using var harness = await AccountControllerTestHarness.CreateAsync();
        await harness.SeedUserAsync("first@example.com", "first-user", "StrongPass1", emailConfirmed: true);
        var controller = harness.CreateController();

        var result = await controller.Register(new RegisterDto
        {
            Email = "  FIRST@EXAMPLE.COM ",
            UserName = "second-user",
            Password = "StrongPass1"
        });

        var validation = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.Equal(StatusCodes.Status400BadRequest, validation.StatusCode);
        Assert.True(controller.ModelState.ContainsKey("email"));
        Assert.Equal("Email taken", controller.ModelState["email"]!.Errors[0].ErrorMessage);
    }

    [Fact]
    public async Task Login_UsesNormalizedEmail_AndSignsUserIn()
    {
        await using var harness = await AccountControllerTestHarness.CreateAsync();
        await harness.SeedUserAsync("user@example.com", "demo-user", "StrongPass1", emailConfirmed: true);
        var controller = harness.CreateController();

        var result = await controller.Login(new LoginDto
        {
            Email = "  USER@EXAMPLE.COM ",
            Password = "StrongPass1",
            RememberMe = true
        });

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var dto = Assert.IsType<UserDto>(ok.Value);

        Assert.Equal("user@example.com", dto.Email);
        Assert.True(harness.SignInManager.SignInCalled);
        Assert.True(harness.SignInManager.LastRememberMe);
    }

    [Fact]
    public async Task ForgotPassword_SendsEmail_OnlyForConfirmedUser()
    {
        await using var harness = await AccountControllerTestHarness.CreateAsync();
        await harness.SeedUserAsync("confirmed@example.com", "confirmed-user", "StrongPass1", emailConfirmed: true);
        await harness.SeedUserAsync("pending@example.com", "pending-user", "StrongPass1", emailConfirmed: false);
        var controller = harness.CreateController();

        var confirmedResult = await controller.ForgotPassword(new ForgotPasswordDto { Email = " confirmed@example.com " });
        var pendingResult = await controller.ForgotPassword(new ForgotPasswordDto { Email = "pending@example.com" });
        var missingResult = await controller.ForgotPassword(new ForgotPasswordDto { Email = "missing@example.com" });

        Assert.IsType<NoContentResult>(confirmedResult);
        Assert.IsType<NoContentResult>(pendingResult);
        Assert.IsType<NoContentResult>(missingResult);
        Assert.Single(harness.EmailSender.Messages);
        Assert.Contains("/auth/reset-password", harness.EmailSender.Messages[0].HtmlMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Me_ReturnsUnauthorized_WhenPrincipalHasNoKnownUser()
    {
        await using var harness = await AccountControllerTestHarness.CreateAsync();
        var controller = harness.CreateController(new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        ], "Test")));

        var result = await controller.Me();

        Assert.IsType<UnauthorizedResult>(result.Result);
    }

    private sealed class AccountControllerTestHarness : IAsyncDisposable
    {
        private readonly SqliteTestDatabase database;
        private readonly ServiceProvider rootProvider;
        private readonly IServiceScope scope;
        private readonly IHttpContextAccessor httpContextAccessor;

        private AccountControllerTestHarness(
            SqliteTestDatabase database,
            ServiceProvider rootProvider,
            IServiceScope scope,
            IHttpContextAccessor httpContextAccessor,
            CapturingEmailSender emailSender,
            TestSignInManager signInManager)
        {
            this.database = database;
            this.rootProvider = rootProvider;
            this.scope = scope;
            this.httpContextAccessor = httpContextAccessor;
            EmailSender = emailSender;
            SignInManager = signInManager;
        }

        public UserManager<AppUser> UserManager => scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        public RoleManager<IdentityRole<Guid>> RoleManager => scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        public IConfiguration Configuration => scope.ServiceProvider.GetRequiredService<IConfiguration>();
        public CapturingEmailSender EmailSender { get; }
        public TestSignInManager SignInManager { get; }

        public static async Task<AccountControllerTestHarness> CreateAsync()
        {
            var database = SqliteTestContextFactory.Create();
            var services = new ServiceCollection();
            var emailSender = new CapturingEmailSender();
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Frontend:BaseUrl"] = "https://frontend.example",
                    ["Cors:Origins"] = "https://frontend.example,https://other.example"
                })
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging();
            services.AddDataProtection();
            services.AddControllers();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
            services.AddScoped<IAntiforgery, StubAntiforgery>();
            services.AddScoped<IEmailSender>(_ => emailSender);
            services.AddDbContext<LarchikContext>(options => options.UseSqlite(database.Connection));
            services
                .AddIdentityCore<AppUser>(options =>
                {
                    options.Password.RequireDigit = true;
                    options.Password.RequireLowercase = true;
                    options.Password.RequireUppercase = true;
                    options.Password.RequireNonAlphanumeric = false;
                    options.Password.RequiredLength = 8;
                    options.SignIn.RequireConfirmedEmail = true;
                    options.User.RequireUniqueEmail = true;
                })
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<LarchikContext>()
                .AddSignInManager<TestSignInManager>()
                .AddDefaultTokenProviders();
            services.AddAuthentication().AddIdentityCookies();

            var rootProvider = services.BuildServiceProvider();
            var scope = rootProvider.CreateScope();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

            await EnsureRoleExistsAsync(roleManager, Roles.User);
            await EnsureRoleExistsAsync(roleManager, Roles.Admin);

            var httpContextAccessor = scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>();
            var signInManager = scope.ServiceProvider.GetRequiredService<SignInManager<AppUser>>();

            return new AccountControllerTestHarness(
                database,
                rootProvider,
                scope,
                httpContextAccessor,
                emailSender,
                (TestSignInManager)signInManager);
        }

        public async Task<AppUser> SeedUserAsync(string email, string userName, string password, bool emailConfirmed)
        {
            var user = new AppUser
            {
                Email = email,
                UserName = userName,
                EmailConfirmed = emailConfirmed
            };

            var createResult = await UserManager.CreateAsync(user, password);
            Assert.True(createResult.Succeeded, string.Join(", ", createResult.Errors.Select(x => x.Description)));

            var roleResult = await UserManager.AddToRoleAsync(user, Roles.User);
            Assert.True(roleResult.Succeeded, string.Join(", ", roleResult.Errors.Select(x => x.Description)));

            return user;
        }

        public AccountController CreateController(ClaimsPrincipal? user = null)
        {
            var antiforgery = scope.ServiceProvider.GetRequiredService<IAntiforgery>();
            var httpContext = new DefaultHttpContext
            {
                RequestServices = scope.ServiceProvider,
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
            };

            httpContext.Request.Scheme = "https";
            httpContext.Request.Host = new HostString("api.example");
            httpContext.Response.Body = new MemoryStream();
            httpContextAccessor.HttpContext = httpContext;
            SignInManager.Context = httpContext;

            return new AccountController(UserManager, SignInManager, EmailSender, Configuration, antiforgery)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = httpContext
                }
            };
        }

        public async ValueTask DisposeAsync()
        {
            scope.Dispose();
            await rootProvider.DisposeAsync();
            await database.DisposeAsync();
        }

        private static async Task EnsureRoleExistsAsync(RoleManager<IdentityRole<Guid>> roleManager, string roleName)
        {
            if (await roleManager.RoleExistsAsync(roleName))
            {
                return;
            }

            var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
            Assert.True(result.Succeeded, string.Join(", ", result.Errors.Select(x => x.Description)));
        }
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];

        public Task SendEmailAsync(string email, string subject, string htmlMessage, CancellationToken cancellationToken = default)
        {
            Messages.Add(new EmailMessage(email, subject, htmlMessage));
            return Task.CompletedTask;
        }
    }

    private sealed record EmailMessage(string Email, string Subject, string HtmlMessage);

    private sealed class StubAntiforgery : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) =>
            new("request-token", "cookie-token", "X-XSRF-TOKEN", "__Host-larchik-af");

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) =>
            new("request-token", "cookie-token", "X-XSRF-TOKEN", "__Host-larchik-af");

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(true);

        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;

        public void SetCookieTokenAndHeader(HttpContext httpContext)
        {
        }
    }

    private sealed class TestSignInManager : SignInManager<AppUser>
    {
        public TestSignInManager(
            UserManager<AppUser> userManager,
            IHttpContextAccessor contextAccessor,
            IUserClaimsPrincipalFactory<AppUser> claimsFactory,
            IOptions<IdentityOptions> optionsAccessor,
            ILogger<SignInManager<AppUser>> logger,
            IAuthenticationSchemeProvider schemes,
            IUserConfirmation<AppUser> confirmation)
            : base(userManager, contextAccessor, claimsFactory, optionsAccessor, logger, schemes, confirmation)
        {
        }

        public bool SignInCalled { get; private set; }
        public bool LastRememberMe { get; private set; }

        public override Task SignInAsync(AppUser user, bool isPersistent, string? authenticationMethod = null)
        {
            SignInCalled = true;
            LastRememberMe = isPersistent;
            return Task.CompletedTask;
        }

        public override Task SignOutAsync() => Task.CompletedTask;

        public override Task RefreshSignInAsync(AppUser user) => Task.CompletedTask;
    }
}
