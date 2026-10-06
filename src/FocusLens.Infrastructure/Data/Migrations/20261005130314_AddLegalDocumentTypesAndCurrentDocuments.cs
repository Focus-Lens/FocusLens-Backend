using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FocusLens.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddLegalDocumentTypesAndCurrentDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LegalDocuments_Audience_IsPublished_PublishedOnUtc",
                table: "LegalDocuments");

            migrationBuilder.DropIndex(
                name: "IX_LegalDocuments_Audience_Version",
                table: "LegalDocuments");

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "LegalDocuments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Pre-migration records combined terms and privacy. Retain every
            // such row for acceptance history, but do not present one as current.
            migrationBuilder.Sql(
                "UPDATE [LegalDocuments] " +
                "SET [Type] = N'Terms', [IsPublished] = 0 " +
                "WHERE [Type] = N'';");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Audience_Type_IsPublished_PublishedOnUtc",
                table: "LegalDocuments",
                columns: new[] { "Audience", "Type", "IsPublished", "PublishedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Audience_Type_Version",
                table: "LegalDocuments",
                columns: new[] { "Audience", "Type", "Version" },
                unique: true);

            DateTimeOffset publishedOnUtc = new(new DateTime(2026, 10, 5, 0, 0, 0, DateTimeKind.Unspecified), TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "LegalDocuments",
                columns: new[] { "Id", "Audience", "Type", "Version", "Content", "IsPublished", "PublishedOnUtc", "CreatedAtUtc", "CreatedBy", "LastModifiedUtc", "LastModifiedBy" },
                values: new object[,]
                {
                    {
                        new Guid("33333333-3333-7333-8333-333333333333"), "Student", "Privacy", "2026-10-05",
                        """
                        Information we collect

                        Student account, profile, and learning preference information used to provide the student experience. [Legal review: confirm the final categories and required disclosures.]

                        How we use information

                        Information is used to support secure account access and approved learning workflows. [Legal review: confirm the final purpose language.]

                        Parent and student relationships

                        Connected account visibility follows the approved relationship between the student and parent. [Legal review: confirm relationship controls and disclosures.]

                        How information is shared

                        [Legal review required: describe when information may be shared, with whom, and what safeguards or choices apply.]

                        Data security and retention

                        [Legal review required: describe security practices and approved retention language without stating unverified guarantees or time periods.]

                        Privacy rights and choices

                        [Legal review required: describe available privacy choices, request methods, eligibility, and any applicable limitations.]

                        Contact and policy updates

                        [Legal review required: add approved contact details and explain how policy updates will be communicated.]
                        """,
                        true, publishedOnUtc, publishedOnUtc, null, publishedOnUtc, null
                    },
                    {
                        new Guid("44444444-4444-7444-8444-444444444444"), "Parent", "Privacy", "2026-10-05",
                        """
                        Information we collect

                        Parent account information and relationship information used to provide the parent experience. [Legal review: confirm the final categories and required disclosures.]

                        How we use information

                        Information is used to support account access, parent application features, and approved parent/student relationships. [Legal review: confirm the final purpose language.]

                        Parent and student relationships

                        Connected account visibility follows the approved relationship between the parent and student. [Legal review: confirm relationship controls and disclosures.]

                        How information is shared

                        [Legal review required: describe when information may be shared, with whom, and what safeguards or choices apply.]

                        Data security and retention

                        [Legal review required: describe security practices and approved retention language without stating unverified guarantees or time periods.]

                        Privacy rights and choices

                        [Legal review required: describe available privacy choices, request methods, eligibility, and any applicable limitations.]

                        Contact and policy updates

                        [Legal review required: add approved contact details and explain how policy updates will be communicated.]
                        """,
                        true, publishedOnUtc, publishedOnUtc, null, publishedOnUtc, null
                    },
                    {
                        new Guid("55555555-5555-7555-8555-555555555555"), "Student", "Terms", "2026-10-05",
                        """
                        Acceptance of terms

                        By using FocusLens, users acknowledge these terms. [Legal review: replace with approved acceptance language.]

                        Account responsibilities

                        Parents and students are responsible for account access and accurate account information. [Legal review: confirm obligations and limitations.]

                        Parent and student connections

                        Approved connections control relationship visibility. Connecting accounts does not provide unrestricted access to student information.

                        Acceptable use

                        [Legal review required: add approved permitted and prohibited use language.]

                        Privacy and personal information

                        Use of personal information is described in the Privacy Policy. [Legal review: confirm the final cross-reference language.]

                        Service availability

                        [Legal review required: describe availability, maintenance, and service-change language.]

                        Account suspension or termination

                        [Legal review required: describe grounds, process, and effects of account suspension or termination.]

                        Changes to the terms

                        [Legal review required: explain how changes become effective and how users will be notified.]

                        Contact and support

                        [Legal review required: add approved support and legal contact details.]
                        """,
                        true, publishedOnUtc, publishedOnUtc, null, publishedOnUtc, null
                    },
                    {
                        new Guid("66666666-6666-7666-8666-666666666666"), "Parent", "Terms", "2026-10-05",
                        """
                        Acceptance of terms

                        By using FocusLens, users acknowledge these terms. [Legal review: replace with approved acceptance language.]

                        Account responsibilities

                        Parents and students are responsible for account access and accurate account information. [Legal review: confirm obligations and limitations.]

                        Parent and student connections

                        Approved connections control relationship visibility. Connecting accounts does not provide unrestricted access to student information.

                        Acceptable use

                        [Legal review required: add approved permitted and prohibited use language.]

                        Privacy and personal information

                        Use of personal information is described in the Privacy Policy. [Legal review: confirm the final cross-reference language.]

                        Service availability

                        [Legal review required: describe availability, maintenance, and service-change language.]

                        Account suspension or termination

                        [Legal review required: describe grounds, process, and effects of account suspension or termination.]

                        Changes to the terms

                        [Legal review required: explain how changes become effective and how users will be notified.]

                        Contact and support

                        [Legal review required: add approved support and legal contact details.]
                        """,
                        true, publishedOnUtc, publishedOnUtc, null, publishedOnUtc, null
                    }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "LegalDocuments",
                keyColumn: "Id",
                keyValues: new object[]
                {
                    new Guid("33333333-3333-7333-8333-333333333333"),
                    new Guid("44444444-4444-7444-8444-444444444444"),
                    new Guid("55555555-5555-7555-8555-555555555555"),
                    new Guid("66666666-6666-7666-8666-666666666666")
                });

            migrationBuilder.DropIndex(
                name: "IX_LegalDocuments_Audience_Type_IsPublished_PublishedOnUtc",
                table: "LegalDocuments");

            migrationBuilder.DropIndex(
                name: "IX_LegalDocuments_Audience_Type_Version",
                table: "LegalDocuments");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "LegalDocuments");

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Audience_IsPublished_PublishedOnUtc",
                table: "LegalDocuments",
                columns: new[] { "Audience", "IsPublished", "PublishedOnUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_LegalDocuments_Audience_Version",
                table: "LegalDocuments",
                columns: new[] { "Audience", "Version" },
                unique: true);
        }
    }
}
