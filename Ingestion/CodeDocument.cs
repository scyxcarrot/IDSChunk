using Microsoft.Extensions.VectorData;

namespace IDSChunk.Ingestion;

public class CodeDocument
{
    [VectorStoreKey]
    public required Guid Id { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public required string RelativePath { get; set; }

    [VectorStoreData(IsIndexed = true)]
    public required string DocumentHash { get; set; }

    // dummy vector for abstractions only
    [VectorStoreVector(Dimensions: 768, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float>? DummyVector { get; set; }
}
