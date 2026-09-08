using FocusLens.Domain.StudySessions;
using FocusLens.Infrastructure.StudySessions;
using Microsoft.Extensions.Configuration;
using PdfSharp.Pdf;

namespace FocusLens.Application.SubcutaneousTests.StudySessions;

public class PdfStudyMaterialProcessingTests
{
    [Fact]
    public async Task ExtractPages_WhenPageRangeChanges_KeepsOriginalAndReplacesDerivedReference()
    {
        string rootPath = Path.Combine(Path.GetTempPath(), $"focuslens-study-materials-{Guid.NewGuid():N}");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["StudyMaterialStorage:RootPath"] = rootPath
                })
                .Build();
            var fileStore = new LocalStudyMaterialFileStore(configuration);
            var processor = new PdfSharpStudyMaterialPdfProcessor();
            byte[] originalPdf = CreatePdf(pageCount: 5);
            Guid studentId = Guid.NewGuid();

            await using var originalContent = new MemoryStream(originalPdf);
            string originalReference = await fileStore.SaveOriginalAsync(
                studentId,
                "book.pdf",
                originalContent,
                CancellationToken.None);

            StudyMaterial material = StudyMaterial.Create(
                studentId,
                "book.pdf",
                originalPdf.Length,
                5,
                originalReference,
                StudyMaterialSource.Upload).Value;
            string firstDerivedReference = await ExtractAndSaveDerivedAsync(
                fileStore,
                processor,
                studentId,
                originalReference,
                StudySessionPageRange.Create(2, 4).Value);
            material.SetDerivedStorageReference(firstDerivedReference);

            string secondDerivedReference = await ExtractAndSaveDerivedAsync(
                fileStore,
                processor,
                studentId,
                originalReference,
                StudySessionPageRange.Create(4, 5).Value);
            material.SetDerivedStorageReference(secondDerivedReference);

            await using Stream originalAgain = await fileStore.OpenReadAsync(originalReference, CancellationToken.None);
            await using Stream derived = await fileStore.OpenReadAsync(material.DerivedStorageReference!, CancellationToken.None);

            Assert.Equal(5, await processor.GetPageCountAsync(originalAgain, CancellationToken.None));
            Assert.Equal(2, await processor.GetPageCountAsync(derived, CancellationToken.None));
            Assert.NotEqual(firstDerivedReference, material.DerivedStorageReference);
            Assert.Equal(secondDerivedReference, material.DerivedStorageReference);
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
}
