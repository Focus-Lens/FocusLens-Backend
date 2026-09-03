using Microsoft.AspNetCore.Identity;

namespace FocusLens.Domain.Identity;

public class ApplicationRole : IdentityRole<Guid>
{
    public ApplicationRole()
    {
        Id = Guid.CreateVersion7();
    }

    public bool IsDefault { get; set; }

    public bool IsDeleted { get; set; }
}
