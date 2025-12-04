using Microsoft.Extensions.VectorData;

namespace IDSChunk.Ingestion;

public class CodeChunk
{
    [VectorStoreKey]
    public required Guid Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public required string CodeDocumentId { get; set; }

    [VectorStoreData]
    public required string Namespace { get; set; }

    [VectorStoreData]
    public required string ClassName { get; set; }

    [VectorStoreData]
    public required string MethodName { get; set; }

    [VectorStoreData]
    public required string CodeSnippet { get; set; }

    // 768 is the default vector size for the nomic-embed-text:latest
    [VectorStoreVector(768, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> CodeSnippetEmbedding { get; set; }

}
