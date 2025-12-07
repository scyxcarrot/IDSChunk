using IDSChunk.Ingestion;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.SemanticKernel.Connectors.Qdrant;

using OllamaSharp;

using Qdrant.Client;

using Serilog;

const string chatModelId = "gemma3:12b";
const string embeddingModelId = "nomic-embed-text:latest";

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
await DataIngestor.IngestDataAsync(
    host.Services,
    new CSharpFileDirectorySource(@"C:\Users\jwong\Desktop\IDS_GIT\IDS", embeddingGenerator));
