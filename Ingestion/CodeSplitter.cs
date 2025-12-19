using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.AI;
using Microsoft.ML.Tokenizers;

namespace IDSChunk.Ingestion;

public class CodeSplitter
{
    private readonly SentencePieceTokenizer _tokenizer;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private const int RecommendedMaxTokens = 256;

    public CodeSplitter(
        string vocabFilePath, 
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
    {
        _embeddingGenerator = embeddingGenerator;
        try
        {
            // for nomic-embed-text models
            //_tokenizer = WordPieceTokenizer.Create(vocabFilePath);

            using FileStream fileStream = File.OpenRead(vocabFilePath);
            _tokenizer = SentencePieceTokenizer.Create(
                modelStream: fileStream,
                addBeginningOfSentence: true,
                addEndOfSentence: false 
            );
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException($"Vocab file not found at: {vocabFilePath}");
        }
    }

    /// <summary>
    /// Counts the number of tokens a text will be encoded to by the Nomic tokenizer.
    /// </summary>
    //public int GetTokenCount(string text)
    //{
    //    return _tokenizer.CountTokens(text);
    //}

    /// <summary>
    /// Counts the number of tokens a text will be encoded to by the EmbeddingGemma tokenizer.
    /// </summary>
    public int GetTokenCount(string text)
    {
        // 🚨 CRUCIAL: Must include the model's required prompt for accurate tokenization
        string prompt = "task: search document | text: ";
        string fullText = prompt + text;

        // This is often handled internally by the library, but you must ensure 
        // that special tokens like <bos> (beginning of sentence) are included
        // in the count, as they are part of the model's input limit.
        return _tokenizer.CountTokens(fullText);
    }

    public async Task<List<CodeChunk>> GetCodeChunks(CodeDocument codeDocument, string sourceDirectory)
    {
        string filePath = Path.Combine(sourceDirectory, codeDocument.RelativePath);
        CodeAnalyzerHelper codeAnalyzerHelper = new CodeAnalyzerHelper(filePath, codeDocument);

        List<CodeChunk> chunks = new List<CodeChunk>();
        IEnumerable<ClassDeclarationSyntax> classDeclarations = codeAnalyzerHelper.GetClassDeclarations();
        chunks.AddRange(codeAnalyzerHelper.CreateClassChunks(classDeclarations));

        IEnumerable<InterfaceDeclarationSyntax> interfaceDeclarations = codeAnalyzerHelper.GetInterfaceDeclarations();
        chunks.AddRange(codeAnalyzerHelper.CreateInterfaceChunks(interfaceDeclarations));

        IEnumerable<EnumDeclarationSyntax> enumDeclarations = codeAnalyzerHelper.GetEnumDeclarations();
        chunks.AddRange(codeAnalyzerHelper.CreateEnumChunks(enumDeclarations));

        IEnumerable<StructDeclarationSyntax> structDeclarations = codeAnalyzerHelper.GetStructDeclarations();
        chunks.AddRange(codeAnalyzerHelper.CreateStructChunks(structDeclarations));

        var finalChunks = new List<CodeChunk>();

        foreach (var chunk in chunks)
        {
            var splitChunks = await SplitChunkRecursively(chunk);
            finalChunks.AddRange(splitChunks);
        }

        return finalChunks;
    }

    /// <summary>
    /// Recursively splits a chunk into smaller, token-limit-adhering chunks.
    /// Uses line-based splitting with overlap.
    /// </summary>
    private async Task<List<CodeChunk>> SplitChunkRecursively(CodeChunk codeChunk)
    {
        int tokenCount = GetTokenCount(codeChunk.CodeSnippet);

        // Base case: Chunk is within the recommended limit
        if (tokenCount <= RecommendedMaxTokens)
        {
            var embedding = await _embeddingGenerator.GenerateAsync(codeChunk.CodeSnippet);
            codeChunk.CodeSnippetEmbedding = embedding.Vector;
            codeChunk.TokenCount = tokenCount;
            return new List<CodeChunk> { codeChunk };
        }

        // Recursive Case: Chunk is too big, split by lines with overlap
        var smallerChunks = new List<CodeChunk>();
        var lines = codeChunk.CodeSnippet.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        const int overlapLines = 5; // A common overlap for code or text
        int currentLineIndex = 0;

        while (currentLineIndex < lines.Length)
        {
            List<string> currentChunkLines = new List<string>();
            int tempLineIndex = currentLineIndex;

            // Start with overlap from the previous chunk, if applicable
            for (int i = 0; i < overlapLines && (currentLineIndex - overlapLines + i) >= 0 && i < currentLineIndex; i++)
            {
                currentChunkLines.Add(lines[currentLineIndex - overlapLines + i]);
            }

            // Build the current chunk by adding lines until the token limit is approached
            while (tempLineIndex < lines.Length)
            {
                string nextLine = lines[tempLineIndex];

                // Temporarily combine the lines to check the token count
                string testSnippet = string.Join(Environment.NewLine, currentChunkLines.Concat(new[] { nextLine }));

                // if over the token count on the first line, just add the line so that we dont go into infinite loop
                if (GetTokenCount(testSnippet) <= RecommendedMaxTokens || tempLineIndex == currentLineIndex)
                {
                    currentChunkLines.Add(nextLine);
                    tempLineIndex++;
                }
                else
                {
                    // Adding the next line exceeds the limit, break and process the current chunk
                    break;
                }
            }

            // If we couldn't add any lines, we must add at least the current line and advance
            if (currentChunkLines.Count == 0 && currentLineIndex < lines.Length)
            {
                currentChunkLines.Add(lines[currentLineIndex]);
                tempLineIndex = currentLineIndex + 1; // Advance by one line
            }

            // Create a new chunk object
            if (currentChunkLines.Any())
            {
                string newSnippet = string.Join(Environment.NewLine, currentChunkLines);
                var embedding = await _embeddingGenerator.GenerateAsync(newSnippet);
                int snippetTokenCount = GetTokenCount(newSnippet);
                smallerChunks.Add(new CodeChunk
                {
                    Id = Guid.CreateVersion7(),
                    CodeDocumentId = codeChunk.CodeDocumentId,
                    CodeSnippet = newSnippet,
                    TypeName = codeChunk.TypeName,
                    Namespace = codeChunk.Namespace,
                    TokenCount = snippetTokenCount,
                    CodeSnippetEmbedding = embedding.Vector,
                });
            }

            // Move the pointer to the start of the next new, non-overlapping section
            currentLineIndex = tempLineIndex;
        }

        return smallerChunks;
    }
}