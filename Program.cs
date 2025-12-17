using IDSChunk.Ingestion;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.Qdrant;

using OllamaSharp;

using Qdrant.Client;

using Serilog;

const string chatModelId = "gemma3:1b";
const string embeddingModelId = "embeddinggemma:latest";
//const string embeddingModelId = "nomic-embed-text:latest";

var builder = Host.CreateDefaultBuilder()
    .UseSerilog((context, configuration) =>
    {
        configuration.WriteTo.Console();
    })
    .ConfigureServices((hostContext, services) =>
    {
        // register LLM models
        Uri ollamaEndpoint = new Uri("http://localhost:11434");
        IChatClient chatClient = new OllamaApiClient(ollamaEndpoint, chatModelId);
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator = new OllamaApiClient(ollamaEndpoint, embeddingModelId);
        services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
        services.AddEmbeddingGenerator(embeddingGenerator);

        // register qdrant
        Uri qdrantUri = new Uri("http://localhost:6334");
        services.AddSingleton(new QdrantClient(qdrantUri));

        services.AddQdrantVectorStore(
            host: qdrantUri.Host,
            port: qdrantUri.Port,
            https: false,
            apiKey: null,
            options: new QdrantVectorStoreOptions
            {
                // Define your options here, including the EmbeddingGenerator
                EmbeddingGenerator = embeddingGenerator
            }
        );
        services.AddQdrantCollection<Guid, CodeChunk>("CodeExplainer-IDS-CodeChunk");
        services.AddQdrantCollection<Guid, CodeDocument>("CodeExplainer-IDS-CodeDocument");

        services.AddScoped<DataIngestor>();
        services.AddSingleton<SemanticSearch>();

    });


var host = builder.Build();

// Change the path to the directory you want to ingest
using var scope = host.Services.CreateScope();
var embeddingGenerator = scope.ServiceProvider.GetService<IEmbeddingGenerator<string, Embedding<float>>>();

// delete the previous failed embedding to make sure its properly trained
// await DataIngestor.DeleteDocumentAndChunks(host.Services, @"IDSPICMF\IDSPICMFPlugIn.cs");
//await DataIngestor.IngestDataAsync(
//    host.Services,
//    new CSharpFileDirectorySource(@"C:\Users\jwong\Desktop\IDS", embeddingGenerator));

var systemMessage = new ChatMessage(ChatRole.System, "You are a helpful assistant specialized in retrieving code base knowledge using RAG");
string userQuery = "Tell me about ImplantDataModel";
var userMessage = new ChatMessage(ChatRole.User, userQuery);
var queryEmbedding = await embeddingGenerator.GenerateVectorAsync(userQuery);

VectorStoreCollection<Guid, CodeChunk> codeChunkCollection =
    scope.ServiceProvider.GetService<VectorStoreCollection<Guid, CodeChunk>>();
var results = codeChunkCollection.SearchAsync(queryEmbedding, 10, new VectorSearchOptions<CodeChunk>()
{
    VectorProperty = codeChunk => codeChunk.CodeSnippetEmbedding
});

await foreach (var result in results)
{
    var score = result.Score ?? 0;
    var percent = (score * 100).ToString("F2");
    string codeSnippet = result.Record.CodeSnippet;

    Console.WriteLine($"Score = {score}");
    Console.WriteLine($"=== CodeSnippet ===");
    Console.WriteLine(codeSnippet);
}

Console.ReadLine();
