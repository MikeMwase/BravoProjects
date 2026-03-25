using BravoProjects.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BravoProjects
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            // Update this line in your Program.cs
            builder.Services.AddControllersWithViews(options =>
            {
                var policy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();
              //  options.Filters.Add(new AuthorizeFilter(policy));
            });

            // Register DbContext
            builder.Services.AddDbContext<BravoProjectsDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("BravoConnection")));

            // Add Identity Services
            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = false)
                .AddRoles<IdentityRole>()
                .AddEntityFrameworkStores<BravoProjectsDbContext>();

            var app = builder.Build();

            // Seed the data - UPDATED TO INCLUDE ROLEMANAGER
            // Seed the data
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    // 1. Get all three required services
                    var context = services.GetRequiredService<BravoProjectsDbContext>();
                    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
                    var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

                    // 2. Pass all three into the Initialize method
                    // Using .GetAwaiter().GetResult() because Main is not 'async Task'
                    DbInitializer.Initialize(context, roleManager, userManager).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    // This will print the error to your Output window if something goes wrong
                    var logger = services.GetRequiredService<ILogger<Program>>();
                    logger.LogError(ex, "An error occurred while seeding the database.");
                }
            }

            // Configure the HTTP request pipeline.
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            // Enable Razor Pages for Identity UI (Required for Login/Register)
            app.MapRazorPages();

            app.Run();
        }
    }
}