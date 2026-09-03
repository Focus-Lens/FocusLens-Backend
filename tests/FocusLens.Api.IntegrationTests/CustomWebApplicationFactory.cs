using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FocusLens.Api.IntegrationTests;

public sealed class CustomWebApplicationFactory
    : WebApplicationFactory<FocusLens.API.Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }
}
