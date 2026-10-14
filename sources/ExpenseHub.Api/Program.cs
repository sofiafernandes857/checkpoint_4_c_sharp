using System;
using System.Threading.Tasks;
using ExpenseHub.Api.Persistence;
using ExpenseHub.Api.Security;
using ExpenseHub.Api.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ExpenseHub.Api;

internal static class Program
{
    public static async Task Main(string[] args)
    {
        WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
        builder.Services.AddOpenApi();
        builder.Services.AddControllers();
        builder.Services.AddProblemDetails();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<ExpenseService>();
        builder.Services.AddScoped<IExpenseTransitionStore, ExpenseTransitionStore>();
        builder.Services.AddAuthentication(IdentityConstants.BearerScheme).AddBearerToken(IdentityConstants.BearerScheme);
        builder.Services.AddAuthorization();
        builder.Services.AddIdentityCore<IdentityUser>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 10;
            options.Lockout.MaxFailedAccessAttempts = 5;
        })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ExpenseHubDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        builder.Services.AddDbContext<ExpenseHubDbContext>(options => options.UseSqlite(
            builder.Configuration.GetConnectionString("ExpenseHub") ?? "Data Source=expensehub.db"));

        WebApplication app = builder.Build();
        await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
        {
            ExpenseHubDbContext database = scope.ServiceProvider.GetRequiredService<ExpenseHubDbContext>();
            await database.Database.EnsureCreatedAsync();
            await using IDbContextTransaction transaction = await database.Database.BeginTransactionAsync();
            await IdentitySeed.InitializeAsync(
                scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>(),
                scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>(),
                builder.Configuration);
            await ExpenseCategorySeed.InitializeAsync(database);
            await transaction.CommitAsync();
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
            .WithName("GetHealth");

        await app.RunAsync();
    }
}
