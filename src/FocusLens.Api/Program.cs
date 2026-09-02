using FocusLens.Application;
using FocusLens.Infrastructure;
using FocusLens.Infrastructure.Data;
using FocusLens.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Serilog;

namespace FocusLens.API
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddAuthorization();
            builder.Services.AddOpenApi();

            // Identity
            builder
                .Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.Password.RequiredLength = 4;
                    options.Password.RequireDigit = false;
                    options.Password.RequireUppercase = false;
                    options.Password.RequireLowercase = false;
                    options.Password.RequireNonAlphanumeric = false;
                })
                .AddEntityFrameworkStores<ApplicationDBContext>()
                .AddDefaultTokenProviders();

            // Layers
            builder.Services
                .AddApplication()
                .AddInfrastructure(builder.Configuration);

            // Serilog
            builder.Host.UseSerilog(
                (context, configuration) =>
                    configuration.ReadFrom.Configuration(context.Configuration)
            );

            // CORS
            builder.Services.AddCors(options =>
            {
                options.AddPolicy(
                    "AllowAll",
                    policy =>
                    {
                        policy.AllowAnyOrigin()
                            .AllowAnyMethod()
                            .AllowAnyHeader();
                    }
                );
            });

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();
            }

            // Middleware
            app.UseHttpsRedirection();
            app.UseSerilogRequestLogging();
            app.UseCors("AllowAll");

            app.UseAuthorization();

            app.Run();
        }
    }
}
