using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Identity;
using FocusLens.Domain.StudySessions;
using FocusLens.Infrastructure.Identity.Verification;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FocusLens.Infrastructure.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>(options)
{
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<EmailVerificationCode> EmailVerificationCodes =>
        Set<EmailVerificationCode>();

    public DbSet<Parent> Parents => Set<Parent>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<StudySession> StudySessions => Set<StudySession>();

    public DbSet<StudyMaterial> StudyMaterials => Set<StudyMaterial>();

    public DbSet<StudySessionSelection> StudySessionSelections => Set<StudySessionSelection>();

    public DbSet<StudyMaterialSection> StudyMaterialSections => Set<StudyMaterialSection>();

    public DbSet<StudySessionCompletedSection> StudySessionCompletedSections =>
        Set<StudySessionCompletedSection>();

    public DbSet<ParentStudentRelationship> ParentStudentRelationships =>
        Set<ParentStudentRelationship>();

    public DbSet<StudentParentInvitation> StudentParentInvitations =>
        Set<StudentParentInvitation>();

    public DbSet<ChildSetupDraft> ChildSetupDrafts =>
        Set<ChildSetupDraft>();

    public DbSet<ChildSetupInvitation> ChildSetupInvitations =>
        Set<ChildSetupInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}