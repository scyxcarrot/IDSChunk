using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.ML.Tokenizers;

namespace IDSChunk.Ingestion;

public class RecursiveCodeSplitter
{
    private readonly WordPieceTokenizer _tokenizer;
    private const int RecommendedMaxTokens = 512;

    public RecursiveCodeSplitter(string vocabFilePath)
    {
        try
        {
            _tokenizer = WordPieceTokenizer.Create(vocabFilePath);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException($"Nomic vocab file not found at: {vocabFilePath}");
        }
    }

    /// <summary>
    /// Counts the number of tokens a text will be encoded to by the Nomic tokenizer.
    /// </summary>
    public int GetTokenCount(string text)
    {
        return _tokenizer.CountTokens(text);
    }

    public List<CodeChunk> GetCodeChunks(CodeDocument codeDocument, string sourceDirectory)
    {
        string filePath = Path.Combine(sourceDirectory, codeDocument.RelativePath);
        string fullCodeString = File.ReadAllText(filePath);

        // 1. Get the syntax tree and namespace
        SyntaxTree tree = CSharpSyntaxTree.ParseText(fullCodeString);
        string namespaceName = GetNamespace(tree.GetCompilationUnitRoot());

        // 2. Split the file into logical units (methods, classes)
        var splitCodeSnippets = SplitTextIntoCodeSnippets(tree, codeDocument.Id, namespaceName);

        var finalChunks = new List<CodeChunk>();

        // 3. Recursively split any large units and collect the final chunks
        foreach (var chunk in splitCodeSnippets)
        {
            finalChunks.AddRange(SplitChunkRecursively(chunk));
        }

        return finalChunks;
    }

    /// <summary>
    /// Extracts the first declared namespace using Roslyn.
    /// </summary>
    private string GetNamespace(CompilationUnitSyntax root)
    {
        var namespaceDeclaration = root.DescendantNodes()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .FirstOrDefault();
        return namespaceDeclaration?.Name.ToString();
    }

    /// <summary>
    /// Uses Roslyn to split the file into initial CodeChunk objects based on C# structure.
    /// </summary>
    private List<CodeChunk> SplitTextIntoCodeSnippets(SyntaxTree tree, Guid codeDocumentId, string namespaceName)
    {
        var chunks = new List<CodeChunk>();
        var root = tree.GetCompilationUnitRoot();

        // 1. Find all classes
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

        foreach (var classDeclaration in classDeclarations)
        {
            string className = classDeclaration.Identifier.Text;

            // 2. Find all methods within the class
            var methodDeclarations = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>();

            foreach (var method in methodDeclarations)
            {
                // ToFullString() includes trivia (whitespace, comments) which is better for context
                string methodCode = method.ToFullString();
                string methodName = method.Identifier.Text;

                // Create the initial chunk based on the method
                CodeChunk chunk = new CodeChunk()
                {
                    Id = Guid.CreateVersion7(),
                    CodeDocumentId = codeDocumentId.ToString(),
                    CodeSnippet = methodCode,
                    ClassName = className,
                    MethodName = methodName,
                    Namespace = namespaceName,
                };
                chunks.Add(chunk);
            }
        }

        // Add other top-level statements (e.g., global usings, static methods, records, structs) here if needed.

        return chunks;
    }

    // --- Recursive Splitting Method ---

    /// <summary>
    /// Recursively splits a chunk into smaller, token-limit-adhering chunks.
    /// Uses line-based splitting with overlap.
    /// </summary>
    private List<CodeChunk> SplitChunkRecursively(CodeChunk codeChunk)
    {
        int tokenCount = GetTokenCount(codeChunk.CodeSnippet);

        // Base case: Chunk is within the recommended limit
        if (tokenCount <= RecommendedMaxTokens)
        {
            return new List<CodeChunk> { codeChunk };
        }

        // Recursive Case: Chunk is too big, split by lines with overlap
        var smallerChunks = new List<CodeChunk>();
        var lines = codeChunk.CodeSnippet.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        const int OverlapLines = 5; // A common overlap for code or text
        int currentLineIndex = 0;

        while (currentLineIndex < lines.Length)
        {
            List<string> currentChunkLines = new List<string>();
            int tempLineIndex = currentLineIndex;

            // Start with overlap from the previous chunk, if applicable
            for (int i = 0; i < OverlapLines && (currentLineIndex - OverlapLines + i) >= 0 && i < currentLineIndex; i++)
            {
                currentChunkLines.Add(lines[currentLineIndex - OverlapLines + i]);
            }

            // Build the current chunk by adding lines until the token limit is approached
            while (tempLineIndex < lines.Length)
            {
                string nextLine = lines[tempLineIndex];

                // Temporarily combine the lines to check the token count
                string testSnippet = string.Join(Environment.NewLine, currentChunkLines.Concat(new[] { nextLine }));

                if (GetTokenCount(testSnippet) <= RecommendedMaxTokens)
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
                smallerChunks.Add(new CodeChunk
                {
                    Id = Guid.CreateVersion7(),
                    CodeDocumentId = codeChunk.CodeDocumentId,
                    CodeSnippet = newSnippet,
                    ClassName = codeChunk.ClassName,
                    MethodName = codeChunk.MethodName, // Preserve context from the original large chunk
                    Namespace = codeChunk.Namespace
                });
            }

            // Move the pointer to the start of the next new, non-overlapping section
            currentLineIndex = tempLineIndex;
        }

        return smallerChunks;
    }
}