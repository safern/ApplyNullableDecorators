using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ApplyNullableDecorators
{
    internal class NullableDecoratorVisitor : CSharpSyntaxRewriter
    {

        private readonly Dictionary<SyntaxTree, SemanticModel> _semanticModels;
        private readonly Dictionary<string, DeclarationNodeContainer> _visitedNodes = new Dictionary<string, DeclarationNodeContainer>();
        private readonly Dictionary<string, MethodDeclarationSyntax> _allMethodDeclarations;

        public NullableDecoratorVisitor(Dictionary<SyntaxTree, SemanticModel> semanticModels, Dictionary<string, MethodDeclarationSyntax> allMethodDeclarations)
        {
            _semanticModels = semanticModels;
            _allMethodDeclarations = allMethodDeclarations;
        }

        public override SyntaxNode VisitInterfaceDeclaration(InterfaceDeclarationSyntax node) => node;

        public override SyntaxNode VisitMethodDeclaration(MethodDeclarationSyntax node)
        {
            return VisitMethodTree(node, 0).Node;
        }

        private DeclarationNodeContainer VisitMethodTree(MethodDeclarationSyntax node, int currentDepth, int maxDepth = 10)
        {
            if (!_semanticModels.TryGetValue(node.SyntaxTree, out SemanticModel semanticModel))
            {
                throw new Exception($"Couldn't find semantic model for node {node.ToFullString()}");
            }

            IMethodSymbol symbol = semanticModel.GetDeclaredSymbol(node);
            string methodDocumenationCommentId = symbol.GetDocumentationCommentId();

            if (_visitedNodes.TryGetValue(methodDocumenationCommentId, out DeclarationNodeContainer visitedNode))
            {
                return visitedNode;
            }

            if (symbol.IsExtern || symbol.IsAbstract)
            {
                DeclarationNodeContainer _result = new DeclarationNodeContainer(node, false);
                _visitedNodes.Add(methodDocumenationCommentId, _result);
                return _result;
            }

            List<string> notNullParameters = new List<string>();
            IEnumerable<InvocationExpressionSyntax> invocationNodes = node.DescendantNodes().OfType<InvocationExpressionSyntax>();
            if (symbol.DeclaredAccessibility == Accessibility.Internal || symbol.DeclaredAccessibility == Accessibility.Private)
            {
                notNullParameters = AnalyzeDebugAsserts(invocationNodes, semanticModel);
            }

            IEnumerable<IfStatementSyntax> ifStatementNodes = node.DescendantNodes().OfType<IfStatementSyntax>();
            bool hasArgumentValidation = false;
            foreach (IfStatementSyntax ifNode in ifStatementNodes)
            {
                SyntaxNode tmpNode = ifNode.Statement;
                if (tmpNode.IsKind(SyntaxKind.Block))
                {
                    if (tmpNode.ChildNodes().Count() != 1) continue;

                    tmpNode = tmpNode.ChildNodes().First();
                }

                if (tmpNode is ExpressionStatementSyntax expression)
                {
                    tmpNode = expression.Expression;
                }

                if (tmpNode is InvocationExpressionSyntax invocationExpression)
                {
                    var access = invocationExpression.Expression as MemberAccessExpressionSyntax;
                    if (access == null)
                    {
                        continue;
                    }

                    var methodSymbol = semanticModel.GetSymbolInfo(access).Symbol as IMethodSymbol;
                    if (methodSymbol.ContainingAssembly != symbol.ContainingAssembly ||
                        methodSymbol.ContainingType.DeclaredAccessibility == Accessibility.Public ||
                        !string.Equals(methodSymbol.ContainingType.Name, "ThrowHelper", StringComparison.Ordinal))
                    {
                        continue;
                    }

                }
                else
                {
                    if (!(tmpNode is ThrowStatementSyntax))
                    {
                        continue;
                    }
                }

                if (!hasArgumentValidation && symbol.DeclaredAccessibility == Accessibility.Private || symbol.DeclaredAccessibility == Accessibility.Internal)
                {
                    hasArgumentValidation = IfConditionHasArgumentValidation(ifNode, semanticModel);
                }

                if (ifNode.Condition is BinaryExpressionSyntax binaryExpression)
                {
                    if (binaryExpression.IsKind(SyntaxKind.EqualsExpression))
                    {
                        if (TryGetParameterIdentifierFromBinaryExpression(binaryExpression, semanticModel, out IParameterSymbol parameter))
                        {
                            notNullParameters.Add(parameter.Name);
                        }
                    }
                }
            }

            foreach (InvocationExpressionSyntax invocationNode in invocationNodes)
            {
                if (semanticModel.GetSymbolInfo(invocationNode).Symbol is IMethodSymbol symbolModel)
                {
                    // Interlocked gives a lot of false positives, so if the method is using Interlocked, let's not annotate it.
                    if (symbolModel.ContainingType.Name == "Interlocked")
                    {
                        var _result = new DeclarationNodeContainer(node, true);
                        _visitedNodes.Add(methodDocumenationCommentId, _result);
                        return _result;
                    }

                    DeclarationNodeContainer visitedInvocationNode = default;
                    IEnumerable<IdentifierNameSyntax> invocationIdentifiers = invocationNode.ArgumentList.DescendantNodes().OfType<IdentifierNameSyntax>().Where(idn => idn.Parent is ArgumentSyntax);
                    string invocationDocumentationId = symbolModel.GetDocumentationCommentId();

                    if (string.Equals(methodDocumenationCommentId,invocationDocumentationId))
                    {
                        continue;
                    }

                    if (symbolModel.ContainingType != symbol.ContainingType)
                    {
                        continue;
                    }

                    if (!symbolModel.Name.Contains(symbol.Name))
                    {
                        if (symbol.DeclaredAccessibility != Accessibility.Public || symbolModel.DeclaredAccessibility != Accessibility.Public)
                            continue;
                    }

                    if (!_visitedNodes.TryGetValue(invocationDocumentationId, out visitedInvocationNode))
                    {
                        if (currentDepth < maxDepth)
                        {
                            if (ShouldVisitInvocationMember(invocationIdentifiers, semanticModel))
                            {
                                if (_allMethodDeclarations.TryGetValue(invocationDocumentationId, out MethodDeclarationSyntax declarationToVisit))
                                {
                                    // Visit declaration method recursively
                                    visitedInvocationNode = VisitMethodTree(declarationToVisit, ++currentDepth);
                                }
                                else
                                {
                                    throw new Exception("Couldn't find method declaration in _allMethodDeclarations");
                                }
                            }
                        }
                    }

                    if (visitedInvocationNode.Node != null && visitedInvocationNode.ShouldRelyOnArgumentValidation)
                    {
                        int i = 0;
                        foreach (ArgumentSyntax argument in invocationNode.ArgumentList.Arguments)
                        {
                            if (argument.Expression is IdentifierNameSyntax identifier)
                            {
                                if (TryGetParameterIdentifier(identifier, semanticModel, out IParameterSymbol parameter))
                                {
                                    if (!visitedInvocationNode.Node.ParameterList.Parameters[i].Type.ToFullString().TrimEnd().EndsWith("?"))
                                    {
                                        notNullParameters.Add(parameter.Name);
                                    }
                                }
                            }

                            i++;
                        }
                    }
                }
            }

            SeparatedSyntaxList<ParameterSyntax> newParamList = node.ParameterList.Parameters;
            int index = 0;
            foreach (ParameterSyntax parameterSyntax in node.ParameterList.Parameters)
            {
                IParameterSymbol parameterSymbol = symbol.Parameters[index++];
                ParameterSyntax ps = newParamList.Where(p => string.Equals(p.Identifier.Value, parameterSyntax.Identifier.Value)).FirstOrDefault();
                string parameterType = ps.Type.ToFullString().TrimEnd();
                if (parameterSymbol.Type.IsReferenceType && !notNullParameters.Contains(parameterSymbol.Name))
                {
                    if (!parameterType.EndsWith("?"))
                    {
                        newParamList = newParamList.Replace(ps, ps.WithType(SyntaxFactory.ParseTypeName(parameterType + "? ")));
                    }
                }
                else
                {
                    // shouldn't contain ? so if we added it for some reason, let's remove it.
                    if (parameterType.EndsWith("?"))
                    {
                        newParamList = newParamList.Replace(ps, ps.WithType(SyntaxFactory.ParseTypeName(parameterType.Substring(0, parameterType.Length - 1) + " ")));
                    }
                }
            }

            bool shouldRelyOnArgumentValidation = symbol.DeclaredAccessibility == Accessibility.Private || symbol.DeclaredAccessibility == Accessibility.Internal ? hasArgumentValidation : true;
            DeclarationNodeContainer result = new DeclarationNodeContainer(node.WithParameterList(node.ParameterList.WithParameters(newParamList)), shouldRelyOnArgumentValidation);
            _visitedNodes.Add(methodDocumenationCommentId, result);
            return result;
        }

        private List<string> AnalyzeDebugAsserts(IEnumerable<InvocationExpressionSyntax> invocationNodes, SemanticModel semanticModel)
        {
            List<string> notNullParameters = new List<string>();
            foreach (InvocationExpressionSyntax invocationNode in invocationNodes)
            {
                if (invocationNode.Expression is MemberAccessExpressionSyntax)
                {
                    if (semanticModel.GetSymbolInfo(invocationNode).Symbol is IMethodSymbol symbolModel)
                    {
                        if (symbolModel.Name == "Assert" && symbolModel.ContainingType.Name == "Debug")
                        {
                            IEnumerable<BinaryExpressionSyntax> binaryExpressions = invocationNode.ArgumentList.DescendantNodes().OfType<BinaryExpressionSyntax>().Where(be => be.IsKind(SyntaxKind.NotEqualsExpression));
                            foreach (BinaryExpressionSyntax binaryExpression in binaryExpressions)
                            {
                                if (TryGetParameterIdentifierFromBinaryExpression(binaryExpression, semanticModel, out IParameterSymbol parameter))
                                {
                                    notNullParameters.Add(parameter.Name);
                                }
                            }
                        }
                    }
                }
            }

            return notNullParameters;
        }


        private bool IfConditionHasArgumentValidation(IfStatementSyntax ifNode, SemanticModel semanticModel)
        {
            foreach (IdentifierNameSyntax identifier in ifNode.Condition.DescendantNodes().OfType<IdentifierNameSyntax>())
            {
                ISymbol identifierSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                if (identifierSymbol.Kind == SymbolKind.Parameter)
                {
                    return true;
                }
            }

            return false;
        }

        // We only visit invocations if a parameter of the caller method is used as an argument and its type is a reference type.
        private bool ShouldVisitInvocationMember(IEnumerable<IdentifierNameSyntax> identifiers, SemanticModel semanticModel)
        {
            foreach (IdentifierNameSyntax identifier in identifiers)
            {
                if (TryGetParameterIdentifier(identifier, semanticModel, out IParameterSymbol parameter) && parameter.Type.IsReferenceType)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryGetIdentifierForNullComparison(BinaryExpressionSyntax binaryExpression, out ExpressionSyntax expressionSyntax)
        {
            expressionSyntax = null;
            if (binaryExpression.Left.IsKind(SyntaxKind.NullLiteralExpression))
            {
                expressionSyntax = binaryExpression.Right;
            }

            if (binaryExpression.Right.IsKind(SyntaxKind.NullLiteralExpression))
            {
                expressionSyntax = binaryExpression.Left;
            }

            return expressionSyntax != null;
        }

        private bool TryGetParameterIdentifierFromBinaryExpression(BinaryExpressionSyntax binaryExpression, SemanticModel semanticModel, out IParameterSymbol parameter)
        {
            parameter = null;

            if (TryGetIdentifierForNullComparison(binaryExpression, out ExpressionSyntax identifierExpression))
            {
                IdentifierNameSyntax identifier;
                if (identifierExpression is CastExpressionSyntax castExpression)
                {
                    identifier = castExpression.Expression as IdentifierNameSyntax;
                }
                else
                {
                    identifier = identifierExpression as IdentifierNameSyntax;
                }

                TryGetParameterIdentifier(identifier, semanticModel, out IParameterSymbol p);
                parameter = p;
            }

            return parameter != null;
        }

        private bool TryGetParameterIdentifier(IdentifierNameSyntax identifier, SemanticModel semanticModel, out IParameterSymbol parameter)
        {
            parameter = null;
            if (identifier != null)
            {
                ISymbol identifierSymbol = semanticModel.GetSymbolInfo(identifier).Symbol;

                if (identifierSymbol.Kind == SymbolKind.Parameter)
                {
                    parameter = identifierSymbol as IParameterSymbol;
                }
            }

            return parameter != null;
        }
    }

    internal struct DeclarationNodeContainer
    {
        public bool ShouldRelyOnArgumentValidation { get; }
        public MethodDeclarationSyntax Node { get; }

        public DeclarationNodeContainer(MethodDeclarationSyntax node, bool shouldRelyOnArgumentValidation)
        {
            Node = node;
            ShouldRelyOnArgumentValidation = shouldRelyOnArgumentValidation;
        }
    }
}
