using System.Security.Cryptography;

using Microsoft.Extensions.AI;

namespace IDSChunk.Ingestion;

public class CSharpFileDirectorySource(
    string sourceDirectory,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator) : IIngestionSource
{
    private const string SearchPattern = "*.cs";

    private readonly List<string> _exclusionPatterns =
    [
        @".g.cs",
        @"\obj\",
        @"\AssemblyInfo.cs",
        @"\packages\"
    ];

    private static string GetDocumentHash(string filePath)
    {
        using var sha256 = SHA256.Create();
        using var stream = File.OpenRead(filePath);
        var hashBytes = sha256.ComputeHash(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Get a list of all new or modified documents from the source directory
    /// </summary>
    /// <param name="existingDocuments">list of all documents in the vector database</param>
    /// <returns>Modified or new documents</returns>
    public IEnumerable<CodeDocument> GetNewOrModifiedDocuments(IEnumerable<CodeDocument> existingDocuments)
    {
        var results = new List<CodeDocument>();
        var allCsFilenames = Directory.GetFiles(sourceDirectory, SearchPattern, SearchOption.AllDirectories);
        var codeFilenames = allCsFilenames
            .Where(csFilename => !_exclusionPatterns.Any(pattern => csFilename.Contains(pattern, StringComparison.Ordinal)))
            .ToArray();

        var existingDocumentHashmap = existingDocuments.ToDictionary(d => d.RelativePath);

        foreach (var codeFilename in codeFilenames)
        {
            var relativePath = Path.GetRelativePath(sourceDirectory, codeFilename);
            var fileHashsum = GetDocumentHash(codeFilename);
            var existingDocumentHashsum = existingDocumentHashmap.TryGetValue(relativePath, out var existingDocument) ? existingDocument.DocumentHash : null;
            if (existingDocumentHashsum != fileHashsum)
            {
                var codeDocument = new CodeDocument()
                {
                    Id = Guid.CreateVersion7(),
                    RelativePath = relativePath,
                    DocumentHash = fileHashsum,
                };
                results.Add(codeDocument);
            }
        }

        return results;
    }

    /// <summary>
    /// Get the documents that is deleted from the source directory
    /// </summary>
    /// <param name="existingDocuments">list of all documents in the vector database</param>
    /// <returns>Deleted documents</returns>
    public IEnumerable<CodeDocument> GetDeletedDocuments(IEnumerable<CodeDocument> existingDocuments)
    {
        var allCsFilenames = Directory.GetFiles(sourceDirectory, SearchPattern, SearchOption.AllDirectories);
        var currentFilenames = allCsFilenames
            .Where(csFilename => !_exclusionPatterns.Any(pattern => csFilename.Contains(pattern, StringComparison.Ordinal)))
            .ToArray();

        var relativeFilenames = currentFilenames.Select(currentFilename => Path.GetRelativePath(sourceDirectory, currentFilename));
        var deletedDocuments = existingDocuments.Where(d => !relativeFilenames.Contains(d.RelativePath));
        return deletedDocuments;
    }

    /// <summary>
    /// Create code chunks for a given document
    /// </summary>
    /// <param name="codeDocument">Code document instance</param>
    /// <returns>Code Chunks from the document</returns>
    public async Task<IEnumerable<CodeChunk>> CreateChunksForDocument(CodeDocument codeDocument)
    {
        CodeSplitter recursiveCodeSplitter = 
            new CodeSplitter(
                @"C:\Users\jwong\Desktop\tutorial\IDSChunk\NomicVocab.txt", 
                embeddingGenerator);
        return await recursiveCodeSplitter.GetCodeChunks(codeDocument, sourceDirectory);
    }
}
