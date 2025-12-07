using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IDSChunk.Ingestion
{
    public class CodeAnalyzerHelper
    {
        private CompilationUnitSyntax CompilationUnitSyntax { get; set; }
        private CodeDocument Document { get; set; }

        public CodeAnalyzerHelper(string filePath, CodeDocument codeDocument)
        {
            CompilationUnitSyntax = GetCompilationUnitSyntax(filePath);
            Document = codeDocument;
        }

        /// <summary>
        /// Read the filepath to get the Compilation Unit Syntax
        /// </summary>
        /// <param name="filePath">File path to read</param>
        /// <returns>Compilation Unit Syntax for using statements, attributes, methods</returns>
        private CompilationUnitSyntax GetCompilationUnitSyntax(string filePath)
        {
            // Parse the text into a SyntaxTree
            string codeText = File.ReadAllText(filePath);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(codeText);
            CompilationUnitSyntax compilationUnitSyntax = tree.GetCompilationUnitRoot();

            return compilationUnitSyntax;
        }

        /// <summary>
        /// Get the using statements in string form
        /// </summary>
        /// <returns>All using statements in string form</returns>
        public string GetUsingStatements()
        {
            SyntaxList<UsingDirectiveSyntax> usingDirectives = CompilationUnitSyntax.Usings;
            StringBuilder stringBuilder = new StringBuilder();
            foreach (UsingDirectiveSyntax usingDirective in usingDirectives)
            {
                string directiveText = usingDirective.ToString();
                stringBuilder.AppendLine(directiveText);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Get namespace
        /// </summary>
        /// <returns></returns>
        public string GetNamespace()
        {
            var namespaceDeclaration = CompilationUnitSyntax.DescendantNodes()
                .OfType<BaseNamespaceDeclarationSyntax>()
                .FirstOrDefault();
            return namespaceDeclaration?.Name.ToString();
        }

        /// <summary>
        /// Get all the classes
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ClassDeclarationSyntax> GetClassDeclarations()
        {
            var classDeclarations = CompilationUnitSyntax
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();

            return classDeclarations;
        }

        /// <summary>
        /// Get all the interfaces
        /// </summary>
        /// <returns></returns>
        public IEnumerable<InterfaceDeclarationSyntax> GetInterfaceDeclarations()
        {
            var interfaceDeclarations = CompilationUnitSyntax
                .DescendantNodes()
                .OfType<InterfaceDeclarationSyntax>();

            return interfaceDeclarations;
        }

        /// <summary>
        /// Get all the enum
        /// </summary>
        /// <returns></returns>
        public IEnumerable<EnumDeclarationSyntax> GetEnumDeclarations()
        {
            var enumDeclarations = CompilationUnitSyntax
                .DescendantNodes()
                .OfType<EnumDeclarationSyntax>();

            return enumDeclarations;
        }

        /// <summary>
        /// Get all the enum
        /// </summary>
        /// <returns></returns>
        public IEnumerable<StructDeclarationSyntax> GetStructDeclarations()
        {
            var structDeclarations = CompilationUnitSyntax
                .DescendantNodes()
                .OfType<StructDeclarationSyntax>();

            return structDeclarations;
        }

        /// <summary>
        /// Return the class name
        /// </summary>
        /// <param name="classDeclarationSyntax"></param>
        /// <returns></returns>
        private string GetClassName(ClassDeclarationSyntax classDeclarationSyntax)
        {
            return classDeclarationSyntax.Identifier.ValueText;
        }

        /// <summary>
        /// Get the text representation of access and non-access modifiers 
        /// for any C# member or type declaration.
        /// </summary>
        /// <param name="memberDeclarationSyntax">
        /// The Roslyn syntax node for a class, method, property, field, etc.
        /// </param>
        /// <returns>A space-separated string of the declaration's modifiers (e.g., "public sealed").</returns>
        private string GetDeclarationModifiers(MemberDeclarationSyntax memberDeclarationSyntax)
        {
            var modifiers = memberDeclarationSyntax.Modifiers;
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.AppendJoin(" ", modifiers.Select(token => token.Text));
            return stringBuilder.ToString();
        }


        /// <summary>
        /// Get the classes that it is inheriting from
        /// </summary>
        /// <param name="classDeclarationSyntax"></param>
        /// <returns></returns>
        private string GetBaseList(ClassDeclarationSyntax classDeclarationSyntax)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (classDeclarationSyntax.BaseList == null)
            {
                return stringBuilder.ToString();
            }

            foreach (var baseType in classDeclarationSyntax.BaseList.Types)
            {
                string parentName = baseType.Type.ToString();
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }
                stringBuilder.Append(parentName);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Get the classes that it is inheriting from
        /// </summary>
        /// <param name="interfaceDeclarationSyntax"></param>
        /// <returns></returns>
        private string GetBaseList(InterfaceDeclarationSyntax interfaceDeclarationSyntax)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (interfaceDeclarationSyntax.BaseList == null)
            {
                return stringBuilder.ToString();
            }

            foreach (var baseType in interfaceDeclarationSyntax.BaseList.Types)
            {
                string parentName = baseType.Type.ToString();
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }
                stringBuilder.Append(parentName);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Get the classes that it is inheriting from
        /// </summary>
        /// <param name="enumDeclarationSyntax"></param>
        /// <returns></returns>
        public string GetBaseList(EnumDeclarationSyntax enumDeclarationSyntax)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (enumDeclarationSyntax.BaseList == null)
            {
                return stringBuilder.ToString();
            }

            foreach (var baseType in enumDeclarationSyntax.BaseList.Types)
            {
                string parentName = baseType.Type.ToString();
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }
                stringBuilder.Append(parentName);
            }

            return stringBuilder.ToString();
        }

        /// <summary>
        /// Get the classes that it is inheriting from
        /// </summary>
        /// <param name="structDeclarationSyntax"></param>
        /// <returns></returns>
        public string GetBaseList(StructDeclarationSyntax structDeclarationSyntax)
        {
            StringBuilder stringBuilder = new StringBuilder();

            if (structDeclarationSyntax.BaseList == null)
            {
                return stringBuilder.ToString();
            }

            foreach (var baseType in structDeclarationSyntax.BaseList.Types)
            {
                string parentName = baseType.Type.ToString();
                if (stringBuilder.Length > 0)
                {
                    stringBuilder.Append(", ");
                }
                stringBuilder.Append(parentName);
            }

            return stringBuilder.ToString();
        }

        public IEnumerable<string> CreateMemberChunks(IEnumerable<MemberDeclarationSyntax> memberDeclarations)
        {
            List<string> chunks = new List<string>();
            foreach (var memberDeclaration in memberDeclarations)
            {
                string indentedEnum = IndentText(
                    memberDeclaration.GetText().ToString().TrimStart());

                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.AppendLine(GetUsingStatements());
                stringBuilder.AppendLine();
                stringBuilder.AppendLine($"namespace {GetNamespace()}");
                stringBuilder.AppendLine("{");
                stringBuilder.AppendLine($"\t{indentedEnum}");
                stringBuilder.AppendLine("}");

                string chunk = stringBuilder.ToString();
                chunks.Add(chunk);
            }

            return chunks;
        }

        public IEnumerable<CodeChunk> CreateStructChunks(IEnumerable<StructDeclarationSyntax> structDeclarations)
        {
            List<CodeChunk> chunks = new List<CodeChunk>();
            foreach (var structDeclaration in structDeclarations)
            {
                string structName = structDeclaration.Identifier.ValueText;
                string accessModifier = GetDeclarationModifiers(structDeclaration);
                string baseList = GetBaseList(structDeclaration);

                string structString = $"{accessModifier} struct {structName} : {baseList}".Trim();
                if (string.IsNullOrEmpty(baseList))
                {
                    structString = $"{accessModifier} struct {structName}".Trim();
                }

                SyntaxList<MemberDeclarationSyntax> structMembers = structDeclaration.Members;
                foreach (MemberDeclarationSyntax structMember in structMembers)
                {
                    string indentedText = IndentText(structMember.GetText().ToString().Trim());

                    string namespaceName = GetNamespace();
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(GetUsingStatements());
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"namespace {namespaceName}");
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($"\t{structString}");
                    stringBuilder.AppendLine("\t{");
                    stringBuilder.AppendLine($"\t{indentedText}");
                    stringBuilder.AppendLine("\t}");
                    stringBuilder.AppendLine("}");

                    CodeChunk chunk = new CodeChunk()
                    {
                        Id = Guid.CreateVersion7(),
                        CodeDocumentId = Document.Id.ToString(),
                        CodeSnippet = stringBuilder.ToString(),
                        TypeName = structName,
                        Namespace = namespaceName,
                    };
                    chunks.Add(chunk);
                }
            }

            return chunks;
        }

        public IEnumerable<CodeChunk> CreateEnumChunks(IEnumerable<EnumDeclarationSyntax> enumDeclarations)
        {
            List<CodeChunk> chunks = new List<CodeChunk>();
            foreach (var enumDeclaration in enumDeclarations)
            {
                string enumName = enumDeclaration.Identifier.ValueText;
                string accessModifier = GetDeclarationModifiers(enumDeclaration);
                string baseList = GetBaseList(enumDeclaration);

                string enumString = $"{accessModifier} enum {enumName} : {baseList}".Trim();
                if (string.IsNullOrEmpty(baseList))
                {
                    enumString = $"{accessModifier} enum {enumName}".Trim();

                }

                SeparatedSyntaxList<EnumMemberDeclarationSyntax> enumMembers = enumDeclaration.Members;
                foreach (EnumMemberDeclarationSyntax enumMember in enumMembers)
                {
                    string indentedText = IndentText(enumMember.Identifier.ValueText.Trim());

                    string namespaceName = GetNamespace();
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(GetUsingStatements());
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"namespace {namespaceName}");
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($"\t{enumString}");
                    stringBuilder.AppendLine("\t{");
                    stringBuilder.AppendLine($"\t{indentedText}");
                    stringBuilder.AppendLine("\t}");
                    stringBuilder.AppendLine("}");

                    CodeChunk chunk = new CodeChunk()
                    {
                        Id = Guid.CreateVersion7(),
                        CodeDocumentId = Document.Id.ToString(),
                        CodeSnippet = stringBuilder.ToString(),
                        TypeName = enumName,
                        Namespace = namespaceName,
                    };
                    chunks.Add(chunk);
                }
            }

            return chunks;
        }

        public IEnumerable<CodeChunk> CreateInterfaceChunks(IEnumerable<InterfaceDeclarationSyntax> interfaceDeclarations)
        {
            List<CodeChunk> chunks = new List<CodeChunk>();
            foreach (var interfaceDeclaration in interfaceDeclarations)
            {
                string interfaceName = interfaceDeclaration.Identifier.ValueText;
                string accessModifier = GetDeclarationModifiers(interfaceDeclaration);
                string baseList = GetBaseList(interfaceDeclaration);

                string interfaceString = $"{accessModifier} interface {interfaceName} : {baseList}".Trim();
                if (string.IsNullOrEmpty(baseList))
                {
                    interfaceString = $"{accessModifier} interface {interfaceName}".Trim();

                }

                SyntaxList<MemberDeclarationSyntax> interfaceMembers = interfaceDeclaration.Members;
                foreach (MemberDeclarationSyntax interfaceMember in interfaceMembers)
                {
                    string indentedText = IndentText(interfaceMember.GetText().ToString().Trim());

                    var namespaceName = GetNamespace();
                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(GetUsingStatements());
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"namespace {namespaceName}");
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($"\t{interfaceString}");
                    stringBuilder.AppendLine("\t{");
                    stringBuilder.AppendLine($"\t{indentedText}");
                    stringBuilder.AppendLine("\t}");
                    stringBuilder.AppendLine("}");

                    CodeChunk chunk = new CodeChunk()
                    {
                        Id = Guid.CreateVersion7(),
                        CodeDocumentId = Document.Id.ToString(),
                        CodeSnippet = stringBuilder.ToString(),
                        TypeName = interfaceName,
                        Namespace = namespaceName,
                    };

                    chunks.Add(chunk);
                }
            }

            return chunks;
        }

        public IEnumerable<CodeChunk> CreateClassChunks(IEnumerable<ClassDeclarationSyntax> classDeclarations)
        {
            List<CodeChunk> chunks = new List<CodeChunk>();
            foreach (var classDeclaration in classDeclarations)
            {
                string className = GetClassName(classDeclaration);
                string accessModifier = GetDeclarationModifiers(classDeclaration);
                string baseList = GetBaseList(classDeclaration);

                string classString = $"{accessModifier} class {className} : {baseList}".Trim();
                if (string.IsNullOrEmpty(baseList))
                {
                    classString = $"{accessModifier} class {className}".Trim();

                }

                SyntaxList<MemberDeclarationSyntax> classMembers = classDeclaration.Members;

                // if class is empty
                if (!classMembers.Any())
                {
                    string namespaceName = GetNamespace();

                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(GetUsingStatements());
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"namespace {namespaceName}");
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($"\t{classString}");
                    stringBuilder.AppendLine("\t{");
                    stringBuilder.AppendLine("\t}");
                    stringBuilder.AppendLine("}");

                    CodeChunk chunk = new CodeChunk()
                    {
                        Id = Guid.CreateVersion7(),
                        CodeDocumentId = Document.Id.ToString(),
                        CodeSnippet = stringBuilder.ToString(),
                        TypeName = className,
                        Namespace = namespaceName,
                    };
                    chunks.Add(chunk);
                }

                foreach (var classMember in classMembers)
                {
                    string classMemberString = classMember.GetText().ToString().TrimStart();
                    string indentedMemberString = IndentText(classMemberString);

                    StringBuilder stringBuilder = new StringBuilder();
                    stringBuilder.AppendLine(GetUsingStatements());
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"namespace {GetNamespace()}");
                    stringBuilder.AppendLine("{");
                    stringBuilder.AppendLine($"\t{classString}");
                    stringBuilder.AppendLine("\t{");
                    stringBuilder.AppendLine($"\t{indentedMemberString}");
                    stringBuilder.AppendLine("\t}");
                    stringBuilder.AppendLine("}");

                    string namespaceName = GetNamespace();
                    CodeChunk chunk = new CodeChunk()
                    {
                        Id = Guid.CreateVersion7(),
                        CodeDocumentId = Document.Id.ToString(),
                        CodeSnippet = stringBuilder.ToString(),
                        TypeName = className,
                        Namespace = namespaceName,
                    };

                    chunks.Add(chunk);
                }
            }

            return chunks;
        }

        public static string IndentText(string text)
        {
            string indentedText = string.Join(
                Environment.NewLine,
                text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => $"\t{line}")
            );

            return indentedText;
        }
    }
}
