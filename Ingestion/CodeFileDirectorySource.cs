using System.Security.Cryptography;
using System.Text;

namespace IDSChunk.Ingestion;

public class CSharpFileDirectorySource(string sourceDirectory) : IIngestionSource
{
    private const string SearchPattern = "*.cs";
    private static string GetDocumentHash(string filePath)
    {
        using var md5 = MD5.Create();
        using var stream = File.OpenRead(filePath);
        return Encoding.Default.GetString(md5.ComputeHash(stream));
    }

    /// <summary>
    /// Get a list of all new or modified documents from the source directory
    /// </summary>
    /// <param name="existingDocuments">list of all documents in the vector database</param>
    /// <returns>Modified or new documents</returns>
    public IEnumerable<CodeDocument> GetNewOrModifiedDocuments(IEnumerable<CodeDocument> existingDocuments)
    {
        var results = new List<CodeDocument>();
        var codeFilenames = Directory.GetFiles(sourceDirectory, SearchPattern, SearchOption.AllDirectories);
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
        var currentFilenames = Directory.GetFiles(sourceDirectory, SearchPattern, SearchOption.AllDirectories);
        var relativeFilenames = currentFilenames.Select(currentFilename => Path.GetRelativePath(sourceDirectory, currentFilename));
        var deletedDocuments = existingDocuments.Where(d => !relativeFilenames.Contains(d.RelativePath));
        return deletedDocuments;
    }

    /// <summary>
    /// Create code chunks for a given document
    /// </summary>
    /// <param name="codeDocument">Code document instance</param>
    /// <returns>Code Chunks from the document</returns>
    public IEnumerable<CodeChunk> CreateChunksForDocument(CodeDocument codeDocument)
    {
        RecursiveCodeSplitter recursiveCodeSplitter = 
            new RecursiveCodeSplitter(@"C:\Users\jwong\Desktop\tutorial\IDSChunk\NomicVocab.txt");
        return recursiveCodeSplitter.GetCodeChunks(codeDocument, sourceDirectory);
    }
}
