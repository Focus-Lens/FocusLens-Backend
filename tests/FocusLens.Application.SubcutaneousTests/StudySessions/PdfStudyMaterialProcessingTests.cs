using FocusLens.Domain.StudySessions;
using FocusLens.Infrastructure.StudySessions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PdfSharp.Pdf;

namespace FocusLens.Application.SubcutaneousTests.StudySessions;

public class PdfStudyMaterialProcessingTests
{
    [Fact]
    public async Task ExtractPages_WhenSelectionChanges_KeepsOriginalAndUsesNewDerivedReference()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"focuslens-study-materials-{Guid.NewGuid():N}");
        try
        {
            var fileStore = new LocalStudyMaterialFileStore(
                Options.Create(new StudyMaterialStorageOptions { RootPath = rootPath }),
                new TestHostEnvironment());
            var processor = new PdfSharpStudyMaterialPdfProcessor();
            byte[] originalPdf = CreatePdf(pageCount: 5);
            Guid studentId = Guid.NewGuid();

            await using var originalContent = new MemoryStream(originalPdf);
            string originalReference = await fileStore.SaveOriginalAsync(
                studentId,
                "book.pdf",
                originalContent,
                CancellationToken.None);

            Assert.False(Path.IsPathRooted(originalReference));
            Assert.Contains("/original/", originalReference);

            StudyMaterial material = StudyMaterial.Create(
                studentId,
                "book.pdf",
                originalPdf.Length,
                5,
                originalReference,
                StudyMaterialSource.Upload).Value;
            StudySession session = StudySession.Create(studentId, StudySessionMode.Digital).Value;
            session.SetStudyMaterial(material);
            StudySessionSelection firstSelection = StudySessionSelection.Create(session, material, 2, 4).Value;
            string firstDerivedReference = await ExtractAndSaveDerivedAsync(
                fileStore,
                processor,
                studentId,
                originalReference,
                StudySessionPageRange.Create(2, 4).Value);
            firstSelection.SetDerivedStorageReference(firstDerivedReference);

            StudySessionSelection secondSelection = StudySessionSelection.Create(session, material, 4, 5).Value;
            string secondDerivedReference = await ExtractAndSaveDerivedAsync(
                fileStore,
                processor,
                studentId,
                originalReference,
                StudySessionPageRange.Create(4, 5).Value);
            secondSelection.SetDerivedStorageReference(secondDerivedReference);

            Assert.False(Path.IsPathRooted(secondDerivedReference));
            Assert.Contains("/derived/", secondDerivedReference);

            await using Stream originalAgain = await fileStore.OpenReadAsync(originalReference, CancellationToken.None);
            await using Stream derived = await fileStore.OpenReadAsync(secondSelection.DerivedStorageReference!, CancellationToken.None);

            Assert.Equal(5, await processor.GetPageCountAsync(originalAgain, CancellationToken.None));
            Assert.Equal(2, await processor.GetPageCountAsync(derived, CancellationToken.None));
            Assert.NotEqual(firstDerivedReference, secondSelection.DerivedStorageReference);
            Assert.Equal(secondDerivedReference, secondSelection.DerivedStorageReference);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    private static async Task<string> ExtractAndSaveDerivedAsync(
        LocalStudyMaterialFileStore fileStore,
        PdfSharpStudyMaterialPdfProcessor processor,
        Guid studentId,
        string originalReference,
        StudySessionPageRange pageRange)
    {
        await using Stream original = await fileStore.OpenReadAsync(originalReference, CancellationToken.None);
        await using Stream derived = await processor.ExtractPagesAsync(original, pageRange, CancellationToken.None);
        return await fileStore.SaveDerivedAsync(studentId, "book.pdf", derived, CancellationToken.None);
    }

    private static byte[] CreatePdf(int pageCount)
    {
        using PdfDocument document = new();
        for (int index = 0; index < pageCount; index++)
        {
            document.AddPage();
        }

        using var output = new MemoryStream();
        document.Save(output, closeStream: false);
        return output.ToArray();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "FocusLens.Tests";

        public string ContentRootPath { get; set; } = Path.GetTempPath();

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
