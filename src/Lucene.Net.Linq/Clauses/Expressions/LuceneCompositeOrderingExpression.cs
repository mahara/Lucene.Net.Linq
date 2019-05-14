namespace Lucene.Net.Linq.Clauses.Expressions
{
    #region Using Directives

    using System;
    using System.Collections.Generic;
    using System.Linq.Expressions;

    #endregion

    internal class LuceneCompositeOrderingExpression : Expression
    {
        private static readonly ExpressionType _nodeType = (ExpressionType) LuceneExpressionType.LuceneCompositeOrderingExpression;
        private static readonly Type _type = typeof(object);

        public LuceneCompositeOrderingExpression(IEnumerable<LuceneQueryFieldExpression> fields)
        {
            this.Fields = fields;
        }

        public override ExpressionType NodeType => _nodeType;

        public override Type Type => _type;

        public IEnumerable<LuceneQueryFieldExpression> Fields { get; }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }
    }
}