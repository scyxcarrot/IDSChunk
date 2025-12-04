using Microsoft.Extensions.VectorData;

namespace IDSChunk.Ingestion;

public class SemanticSearch(
    VectorStoreCollection<Guid, CodeDocument> documentCollection,
    VectorStoreCollection<Guid, CodeChunk> chunkCollection)
{
    /// <summary>
    /// For an agentic LLM to search the RAG using Model Context Protocol
    /// </summary>
    /// <param name="searchText">search text</param>
    /// <param name="documentNameFilter">document name to filter</param>
    /// <param name="maxResults"></param>
    /// <returns>strings of the relevant search text</returns>
    public async Task<IEnumerable<string>> SearchAsync(string searchText, string? documentNameFilter, int maxResults)
    {
        VectorSearchOptions<CodeChunk> searchOptions = new VectorSearchOptions<CodeChunk>();
        if (documentNameFilter != null && documentNameFilter.Length > 0)
        {
            List<CodeDocument> relevantDocuments = await documentCollection
                .GetAsync(document => document.RelativePath.Contains(documentNameFilter),top: int.MaxValue)
                .ToListAsync();

            List<string> relevantDocumentIds = relevantDocuments
                .Select(codeDocument => codeDocument.Id.ToString())
                .ToList();
            searchOptions.Filter = record => relevantDocumentIds.Contains(record.CodeDocumentId);
        }

        IAsyncEnumerable<VectorSearchResult<CodeChunk>> searchResults =
            chunkCollection.SearchAsync(searchText, maxResults, searchOptions);
        return await searchResults.Select(result => result.Record.CodeSnippet).ToListAsync();
    }

    /// <summary>
    /// To find relevant context based on user prompt with similarity scores
    /// </summary>
    /// <param name="searchText">prompt from the user</param>
    /// <param name="maxResults">maximum results</param>
    /// <returns>All the relevant code chunks with similarity scores</returns>
    public async Task<Dictionary<CodeChunk, double>> SearchWithSimilarityScoreAsync(string searchText, int maxResults)
    {
        IAsyncEnumerable<VectorSearchResult<CodeChunk>> searchResults =
            chunkCollection.SearchAsync(searchText, maxResults);

        var codeChunkAndScoreMap = new Dictionary<CodeChunk, double>();
        await foreach (var searchResult in searchResults)
        {
            if (searchResult.Score == null)
            {
                continue;
            }
            codeChunkAndScoreMap[searchResult.Record] = searchResult.Score.Value;
        }

        return codeChunkAndScoreMap;
    }
}
