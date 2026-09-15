using FocusLens.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FocusLens.Infrastructure.Data.Configurations;

public class ParentConfiguration : IEntityTypeConfiguration<Parent>
{
    public void Configure(EntityTypeBuilder<Parent> builder)
    {
        builder.ToTable("Parents");

        builder.HasKey(parent => parent.Id);

        builder.Property(parent => parent.UserId)
            .IsRequired();

        builder.Property(parent => parent.WeekStartsOn);

        builder.HasIndex(parent => parent.UserId)
            .IsUnique();

        builder.HasOne(parent => parent.User)
            .WithOne()
            .HasForeignKey<Parent>(parent => parent.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
