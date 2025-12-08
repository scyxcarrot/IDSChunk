using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.VectorData;

namespace IDSChunk.Ingestion;

public class DataIngestor(
    ILogger<DataIngestor> logger,
    VectorStoreCollection<Guid, CodeChunk> chunksCollection,
    VectorStoreCollection<Guid, CodeDocument> documentsCollection)
{
    /// <summary>
    /// For calling it through dependency injection
    /// </summary>
    /// <param name="services">service provider instance from host app</param>
    /// <param name="source">Instance of Ingestion source to vectorize different data</param>
    /// <returns></returns>
    public static async Task IngestDataAsync(IServiceProvider services, IIngestionSource source)
    {
        using var scope = services.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<DataIngestor>();
        await ingestor.IngestDataAsync(source);
    }

    /// <summary>
    /// Ingest data into the vector collections
    /// </summary>
    /// <param name="source">instance of DataIngestor</param>
    /// <returns></returns>
    public async Task IngestDataAsync(IIngestionSource source)
    {
        await chunksCollection.EnsureCollectionExistsAsync();
        await documentsCollection.EnsureCollectionExistsAsync();

        var documentsFromSource = await documentsCollection
            .GetAsync(doc => true, top: int.MaxValue)
            .ToListAsync();

        var deletedDocuments = source.GetDeletedDocuments(documentsFromSource);
        foreach (var deletedDocument in deletedDocuments)
        {
            logger.LogInformation("Removing ingested data for {documentId}", deletedDocument.RelativePath);
            await DeleteChunksForDocumentAsync(deletedDocument);
            await documentsCollection.DeleteAsync(deletedDocument.Id);
        }

        var modifiedDocuments = source.GetNewOrModifiedDocuments(documentsFromSource);
        int modifiedDocumentCount = modifiedDocuments.Count();

        int counter = 0;
        int errorCount = 0;
        foreach (var modifiedDocument in modifiedDocuments)
        {
            logger.LogInformation("Processing {documentId}", modifiedDocument.RelativePath);
            await DeleteChunksForDocumentAsync(modifiedDocument);
            await documentsCollection.UpsertAsync(modifiedDocument);

            try
            {
                IEnumerable<CodeChunk> newCodeChunks = await source.CreateChunksForDocument(modifiedDocument);
                await chunksCollection.UpsertAsync(newCodeChunks);

                counter++;
            }
            catch
            {
                errorCount++;
                logger.LogError("Creating chunks failed");
                await DeleteChunksForDocumentAsync(modifiedDocument);
                await documentsCollection.DeleteAsync(modifiedDocument.Id);
            }

            logger.LogInformation(
                "Progress {counter}/{modifiedDocumentCount}. ErrorCount {errorCount}", 
                counter, 
                modifiedDocumentCount, 
                errorCount);

        }

        logger.LogInformation("Ingestion is complete");
    }

    /// <summary>
    /// For calling it through dependency injection
    /// </summary>
    /// <param name="services">service provider instance from host app</param>
    /// <param name="source">Instance of Ingestion source to vectorize different data</param>
    /// <returns></returns>
    public static async Task DeleteDocumentAndChunks(IServiceProvider services, string relativePath)
    {
        using var scope = services.CreateScope();
        var ingestor = scope.ServiceProvider.GetRequiredService<DataIngestor>();
        await ingestor.DeleteDocumentAndChunks(relativePath);
    }

    public async Task DeleteDocumentAndChunks(string relativePath)
    {
        await chunksCollection.EnsureCollectionExistsAsync();
        await documentsCollection.EnsureCollectionExistsAsync();

        List<CodeDocument> documentToDelete = await documentsCollection
            .GetAsync(codeDocument => codeDocument.RelativePath == relativePath, int.MaxValue)
            .ToListAsync();

        foreach (var document in documentToDelete)
        {
            await DeleteChunksForDocumentAsync(document);
            await documentsCollection.DeleteAsync(document.Id);
        }
    }

    /// <summary>
    /// Delete the code document and chunks from the vector collections
    /// </summary>
    /// <param name="codeDocument">Code Document instance to delete</param>
    /// <returns></returns>
    private async Task DeleteChunksForDocumentAsync(CodeDocument codeDocument)
    {
        string documentIdString = codeDocument.Id.ToString();
        List<CodeChunk> chunksToDelete = await chunksCollection
            .GetAsync(codeChunk => codeChunk.CodeDocumentId == documentIdString, int.MaxValue)
            .ToListAsync();
        if (chunksToDelete.Any())
        {
            await chunksCollection.DeleteAsync(chunksToDelete.Select(r => r.Id));
        }
    }
}
