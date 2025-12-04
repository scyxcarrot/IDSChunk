using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

public class CSharpParser
{
    public static SyntaxTree ParseCode(string codeText)
    {
        // 1. Parse the string into a SyntaxTree
        // Encoding.UTF8 is the default
        SyntaxTree tree = CSharpSyntaxTree.ParseText(codeText);

        // 2. Get the root node of the tree (a CompilationUnit)
        CompilationUnitSyntax root = tree.GetCompilationUnitRoot();

        // 3. You can now traverse the tree to find specific elements:

        // Find the first namespace declaration
        var namespaceDeclaration = root.DescendantNodes()
                                     .OfType<BaseNamespaceDeclarationSyntax>()
                                     .FirstOrDefault();

        if (namespaceDeclaration != null)
        {
            // For block-scoped: "namespace IDSChunk.Ingestion { ... }"
            // For file-scoped: "namespace IDSChunk.Ingestion;"
            Console.WriteLine($"Namespace: {namespaceDeclaration.Name}");
        }

        // Find all class declarations
        var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();
        foreach (var classDeclaration in classDeclarations)
        {
            Console.WriteLine($"Class Name: {classDeclaration.Identifier.Text}");

            // Find all method declarations within this class
            var methodDeclarations = classDeclaration.DescendantNodes().OfType<MethodDeclarationSyntax>();
            foreach (var method in methodDeclarations)
            {
                Console.WriteLine($"  Method Name: {method.Identifier.Text}");

                // Get the *full text* of the method body for chunking
                // NOTE: Using ToFullString() includes trivia (whitespace, comments)
                string methodCode = method.ToFullString();
            }
        }

        return tree;
    }
}