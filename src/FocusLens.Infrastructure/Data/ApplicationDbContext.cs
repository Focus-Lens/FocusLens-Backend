using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.Identity;
using FocusLens.Infrastructure.Identity.Verification;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FocusLens.Infrastructure.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
    {
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

        public DbSet<EmailVerificationCode> EmailVerificationCodes =>
            Set<EmailVerificationCode>();

        public DbSet<Parent> Parents => Set<Parent>();

        public DbSet<Student> Students => Set<Student>();

        public DbSet<ParentStudentRelationship> ParentStudentRelationships =>
            Set<ParentStudentRelationship>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
        }
    }
}