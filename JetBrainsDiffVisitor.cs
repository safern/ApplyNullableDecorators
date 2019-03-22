using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Xml;

namespace ApplyNullableDecorators
{
    internal class JetBrainsDiffVisitor : CSharpSyntaxRewriter
    {
        private readonly IDictionary<string, XmlNode> _jetBrainsNodes;

        public SemanticModel SemanticModel { get; set; }
        public int TotalPublicApisVisited { get; private set; }
        public int TotalApisVisited { get; private set; }
        public int PublicApisInJetBrainsWithoutNullAttribute { get; private set; }
        public int PublicApisNotInJetBrains { get; private set; }

        public JetBrainsDiffVisitor(IDictionary<string, XmlNode> jetbrainsNodes)
        {
            _jetBrainsNodes = jetbrainsNodes;
        }

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            ISymbol symbol = SemanticModel.GetDeclaredSymbol(node);
            UpdateCounters(symbol);
            return node;
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            ISymbol symbol = SemanticModel.GetDeclaredSymbol(node);
            UpdateCounters(symbol);
            return node;
        }

        private void UpdateCounters(ISymbol symbol)
        {
            TotalApisVisited++;
            if (symbol.ContainingType.DeclaredAccessibility == Accessibility.Internal || symbol.ContainingType.DeclaredAccessibility == Accessibility.Private)
                return;

            if (symbol.DeclaredAccessibility == Accessibility.Public || symbol.DeclaredAccessibility == Accessibility.Protected || symbol.DeclaredAccessibility == Accessibility.ProtectedAndInternal)
            {
                TotalPublicApisVisited++;
                if (_jetBrainsNodes.TryGetValue(symbol.GetDocumentationCommentId(), out XmlNode xmlNode))
                {
                    if (!xmlNode.OuterXml.Contains("NullAttribute"))
                    {
                        PublicApisInJetBrainsWithoutNullAttribute++;
                    }
                }
                else
                {
                    Console.WriteLine(symbol.GetDocumentationCommentId());
                    PublicApisNotInJetBrains++;
                }
            }
        }
    }
}
