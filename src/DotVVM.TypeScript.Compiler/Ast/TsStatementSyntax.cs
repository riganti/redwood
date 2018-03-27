﻿using System.Collections.Generic;
using System.Linq;
using DotVVM.Framework.Utils;

namespace DotVVM.TypeScript.Compiler.Ast
{
    public abstract class TsStatementSyntax : TsSyntaxNode
    {
        public TsStatementSyntax(TsSyntaxNode parent) : base(parent)
        {
        }
    }


    public class TsVariableDeclaratorSyntax : TsSyntaxNode
    {
        public TsExpressionSyntax Expression { get; }
        public TsIdentifierSyntax Identifier { get; }
        
        public TsVariableDeclaratorSyntax(TsSyntaxNode parent, TsExpressionSyntax expression, TsIdentifierSyntax identifier) : base(parent)
        {
            Expression = expression;
            Identifier = identifier;
        }

        public override string ToDisplayString()
        {
            if (Expression != null)
            {
                return $"{Identifier.ToDisplayString()} = {Expression.ToDisplayString()}";
            }
            else
            {
                return Identifier.ToDisplayString();
            }
        }

        public override IEnumerable<TsSyntaxNode> DescendantNodes()
        {
            return Enumerable.Empty<TsSyntaxNode>();
        }
    }

    public class TsLocalVariableDeclarationSyntax : TsStatementSyntax
    {
        public IList<TsVariableDeclaratorSyntax> Declarators { get; }

        public TsLocalVariableDeclarationSyntax(TsSyntaxNode parent, IList<TsVariableDeclaratorSyntax> declarators) : base(parent)
        {
            Declarators = declarators;
        }

        public override string ToDisplayString()
        {
            return $"let {Declarators.Select(d => d.ToDisplayString()).StringJoin(",")}";
        }

        public override IEnumerable<TsSyntaxNode> DescendantNodes()
        {
            return Declarators;
        }
    }

}
