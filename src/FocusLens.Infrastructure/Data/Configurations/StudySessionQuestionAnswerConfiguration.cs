using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionQuestionAnswerConfiguration
    : IEntityTypeConfiguration<StudySessionQuestionAnswer>
{
    public void Configure(EntityTypeBuilder<StudySessionQuestionAnswer> builder)
    {
        builder.ToTable("StudySessionQuestionAnswers");
        builder.HasKey(answer => answer.Id);

        builder.Property(answer => answer.StudySessionQuestionId).IsRequired();
        builder.Property(answer => answer.SelectedAnswer).HasMaxLength(1000).IsRequired();
        builder.Property(answer => answer.IsCorrect).IsRequired();
        builder.Property(answer => answer.LearningSignal).HasMaxLength(30);
        builder.Property(answer => answer.AttemptNumber).IsRequired();
        builder.Property(answer => answer.AnsweredAtUtc).IsRequired();

        builder.HasIndex(answer => new { answer.StudySessionQuestionId, answer.AttemptNumber })
            .IsUnique();

        builder.HasOne<StudySessionQuestion>()
            .WithMany()
            .HasForeignKey(answer => answer.StudySessionQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}