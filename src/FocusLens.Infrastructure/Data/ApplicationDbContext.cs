using FocusLens.Domain;
using FocusLens.Domain.Access;
using FocusLens.Domain.ChildSetup;
using FocusLens.Domain.Identity;
using FocusLens.Domain.Students;
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

    public DbSet<StudySessionImage> StudySessionImages => Set<StudySessionImage>();

    public DbSet<StudyMaterialSection> StudyMaterialSections => Set<StudyMaterialSection>();

    public DbSet<StudySessionCompletedSection> StudySessionCompletedSections =>
        Set<StudySessionCompletedSection>();

    public DbSet<StudySessionQuestion> StudySessionQuestions =>
        Set<StudySessionQuestion>();

    public DbSet<StudySessionQuestionOption> StudySessionQuestionOptions =>
        Set<StudySessionQuestionOption>();

    public DbSet<StudySessionQuestionAnswer> StudySessionQuestionAnswers =>
        Set<StudySessionQuestionAnswer>();

    public DbSet<StudySessionBehaviorEvent> StudySessionBehaviorEvents =>
        Set<StudySessionBehaviorEvent>();

    public DbSet<StudySessionBehaviorWindow> StudySessionBehaviorWindows =>
        Set<StudySessionBehaviorWindow>();
    public DbSet<StudySessionPauseInterval> StudySessionPauseIntervals => Set<StudySessionPauseInterval>();

    public DbSet<StudySessionBehaviorAnalysisJob> StudySessionBehaviorAnalysisJobs => Set<StudySessionBehaviorAnalysisJob>();


    public DbSet<ParentStudentRelationship> ParentStudentRelationships =>
        Set<ParentStudentRelationship>();

    public DbSet<LegalDocument> LegalDocuments => Set<LegalDocument>();

    public DbSet<UserTermsAcceptance> UserTermsAcceptances =>
        Set<UserTermsAcceptance>();

    public DbSet<ChildSetupDraft> ChildSetupDrafts => Set<ChildSetupDraft>();

    public DbSet<ChildSetupInvitation> ChildSetupInvitations => Set<ChildSetupInvitation>();

    public DbSet<StudyGoalProposal> StudyGoalProposals => Set<StudyGoalProposal>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}
