using FocusLens.Domain.StudySessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public sealed class StudySessionQuestionOptionConfiguration
    : IEntityTypeConfiguration<StudySessionQuestionOption>
{
    public void Configure(EntityTypeBuilder<StudySessionQuestionOption> builder)
    {
        builder.ToTable("StudySessionQuestionOptions");
        builder.HasKey(option => option.Id);

        builder.Property(option => option.StudySessionQuestionId).IsRequired();
        builder.Property(option => option.Text).HasMaxLength(1000).IsRequired();
        builder.Property(option => option.Order).IsRequired();

        builder.HasIndex(option => new { option.StudySessionQuestionId, option.Order })
            .IsUnique();

        builder.HasOne<StudySessionQuestion>()
            .WithMany(question => question.Options)
            .HasForeignKey(option => option.StudySessionQuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}