using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionQuestionConfiguration
    : IEntityTypeConfiguration<StudySessionQuestion>
{
    public void Configure(EntityTypeBuilder<StudySessionQuestion> builder)
    {
        builder.ToTable("StudySessionQuestions");
        builder.HasKey(question => question.Id);

        builder.Property(question => question.StudySessionId).IsRequired();
        builder.Property(question => question.StudyMaterialSectionId);
        builder.Property(question => question.AiSectionId).HasMaxLength(100).IsRequired();
        builder.Property(question => question.AiSectionTitle).HasMaxLength(200).IsRequired();
        builder.Property(question => question.AiConceptId).HasMaxLength(100).IsRequired();
        builder.Property(question => question.AiConceptName).HasMaxLength(300).IsRequired();
        builder.Property(question => question.Question).HasMaxLength(2000).IsRequired();
        builder.Property(question => question.CorrectAnswer).HasMaxLength(1000).IsRequired();
        builder.Property(question => question.Difficulty).HasMaxLength(20).IsRequired();
        builder.Property(question => question.Explanation).HasMaxLength(4000).IsRequired();
        builder.Property(question => question.ExplanationShownAtUtc);
        builder.Property(question => question.EstimatedTimeMinutes).IsRequired();
        builder.Property(question => question.Order).IsRequired();

        builder.HasIndex(question => new { question.StudySessionId, question.Order })
            .IsUnique();

        builder.HasOne<StudySession>()
            .WithMany()
            .HasForeignKey(question => question.StudySessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<StudyMaterialSection>()
            .WithMany()
            .HasForeignKey(question => question.StudyMaterialSectionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(question => question.Options)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}