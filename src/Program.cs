using CommandLine;
using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ApplyNullableDecorators
{
    class Program
    {
        private static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"ERROR: {message}");
            Console.ResetColor();
            Environment.Exit(1);
        }

        static async Task Main(string[] args)
        {
            if (!Parser.TryParse(args, out CommandLineOptions options))
            {
                return;
            }

            if (options.MSBuildInstance == null)
            {
                MSBuildLocator.RegisterDefaults();
            }
            else
            {
                MSBuildLocator.RegisterMSBuildPath(options.MSBuildInstance);
            }

            MSBuildWorkspace workspace = MSBuildWorkspace.Create();
            if (!File.Exists(options.Project))
            {
                WriteError($"Couldn't find project file: {options.Project}");
            }

            Project project = await workspace.OpenProjectAsync(options.Project);
            Compilation compilation = await project.GetCompilationAsync();

            if (options.Action == CommandLineActionGroup.apply)
            {
                AddNullableAnnotations(compilation, options.Type, options.EnableNullableInFiles);
                return;
            }

            if (options.Action == CommandLineActionGroup.apistats)
            {
                GenerateApiReport(compilation, options.JetBrainsFiles.Split(CommandLineOptions.FILESEPATOR));
                return;
            }
        }

        private static void GenerateApiReport(Compilation compilation, string[] jetBrainsFiles)
        {
            Console.WriteLine("============== PUBLIC APIS NOT IN JETBRAINS ====================");
            Console.WriteLine();
            JetBrainsDiffVisitor visitor = new JetBrainsDiffVisitor(JetBrainsReader.GetAllAnnotatedApis(jetBrainsFiles));
            foreach (SyntaxTree st in compilation.SyntaxTrees)
            {
                visitor.SemanticModel = compilation.GetSemanticModel(st);
                visitor.Visit(st.GetRoot());
            }

            Console.WriteLine();
            Console.WriteLine("============== SUMMARY ==================");
            Console.WriteLine();
            Console.WriteLine($"TOTAL APIS: {visitor.TotalApisVisited}");
            Console.WriteLine($"TOTAL PUBLIC AND PROTECTED APIS: {visitor.TotalPublicApisVisited}");
            Console.WriteLine($"TOTAL PUBLIC APIS NOT IN JETBRAINS: {visitor.PublicApisNotInJetBrains}");
            Console.WriteLine($"TOTAL PUBLIC APIS IN JETBRAINS WITHOUT NULL DATA: {visitor.PublicApisInJetBrainsWithoutNullAttribute}");
        }

        private static void AddNullableAnnotations(Compilation compilation, string typeName, bool enableNullableInFiles)
        {
            Console.WriteLine($"Adding annotations to type: {typeName}");
            if (compilation != null)
            {
                INamedTypeSymbol type = compilation.Assembly.GetTypeByMetadataName(typeName);

                if (type == null)
                {
                    WriteError($"Couldn't find type \'{typeName}\' in assembly \'{compilation.AssemblyName}\'");
                }

                Dictionary<SyntaxTree, SemanticModel> semanticModels = new Dictionary<SyntaxTree, SemanticModel>();
                Dictionary<string, MethodDeclarationSyntax> allMethodDeclarations = new Dictionary<string, MethodDeclarationSyntax>();
                foreach (Location location in type.Locations)
                {
                    SemanticModel semanticModel = compilation.GetSemanticModel(location.SourceTree);
                    semanticModels.Add(location.SourceTree, semanticModel);

                    SyntaxNode root = location.SourceTree.GetRoot();
                    foreach (MethodDeclarationSyntax methodDeclaration in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
                    {
                        allMethodDeclarations.TryAdd(semanticModel.GetDeclaredSymbol(methodDeclaration).GetDocumentationCommentId(), methodDeclaration);
                    }
                }


                var visitor = new NullableDecoratorVisitor(semanticModels, allMethodDeclarations);
                foreach (Location location in type.Locations)
                {
                    SyntaxNode modifiedRoot = visitor.Visit(location.SourceTree.GetRoot());

                    if (enableNullableInFiles && !modifiedRoot.ToFullString().Contains("#nullable"))
                    {
                        modifiedRoot = modifiedRoot.WithLeadingTrivia(modifiedRoot.GetLeadingTrivia().Concat(new SyntaxTrivia[]
                        {
                            SyntaxFactory.SyntaxTrivia(SyntaxKind.DisabledTextTrivia, "#nullable enable"),
                            SyntaxFactory.SyntaxTrivia(SyntaxKind.EndOfLineTrivia, "\n")
                        }));
                    }

                    File.WriteAllText(location.SourceTree.FilePath, modifiedRoot.ToFullString());
                }
            }
        }
    }
}
