using FocusLens.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FocusLens.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services,
            IConfiguration configuration
        )
        {
            services.AddDbContext<ApplicationDBContext>(options =>
            {
                options.UseSqlServer(
                    configuration.GetConnectionString("DefaultConnection")
                );
            });

            return services;
        }
    }
}
