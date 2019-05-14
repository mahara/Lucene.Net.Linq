using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Lucene.Net.Linq.Clauses.Expressions
{
    internal class LuceneCompositeOrderingExpression : Expression
    {
        private readonly IEnumerable<LuceneQueryFieldExpression> fields;
        private static ExpressionType _nodeType = (ExpressionType)LuceneExpressionType.LuceneCompositeOrderingExpression;
        private static Type _type = typeof(object);

        public LuceneCompositeOrderingExpression(IEnumerable<LuceneQueryFieldExpression> fields)
        {
            this.fields = fields;
        }

        public override ExpressionType NodeType => _nodeType;
        public override Type Type => _type;

        public IEnumerable<LuceneQueryFieldExpression> Fields
        {
            get { return fields; }
        }

        protected override Expression VisitChildren(ExpressionVisitor visitor)
        {
            return this;
        }
    }
}
