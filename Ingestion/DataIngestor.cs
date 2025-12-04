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
        foreach (var modifiedDocument in modifiedDocuments)
        {
            logger.LogInformation("Processing {documentId}", modifiedDocument.RelativePath);
            await DeleteChunksForDocumentAsync(modifiedDocument);
            await documentsCollection.UpsertAsync(modifiedDocument);

            IEnumerable<CodeChunk> newCodeChunks = source.CreateChunksForDocument(modifiedDocument);
            await chunksCollection.UpsertAsync(newCodeChunks);
        }

        logger.LogInformation("Ingestion is complete");
    }

    /// <summary>
    /// Delete the code document and chunks from the vector collections
    /// </summary>
    /// <param name="codeDocument">Code Document instance to delete</param>
    /// <returns></returns>
    private async Task DeleteChunksForDocumentAsync(CodeDocument codeDocument)
    {
        var chunksToDelete = await chunksCollection
            .GetAsync(codeChunk => codeChunk.CodeDocumentId == codeDocument.Id.ToString(), int.MaxValue)
            .ToListAsync();
        if (chunksToDelete.Any())
        {
            await chunksCollection.DeleteAsync(chunksToDelete.Select(r => r.Id));
        }
    }
}
