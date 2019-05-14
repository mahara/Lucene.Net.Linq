namespace Lucene.Net.Linq.Clauses.Expressions
{
    internal enum LuceneExpressionType
    {
        LuceneQueryFieldExpression = 150001,
        LuceneQueryPredicateExpression,
        LuceneRangeQueryExpression,
        LuceneCompositeOrderingExpression,
        LuceneOrderByRelevanceExpression,
        LuceneQueryAnyFieldExpression,
        BoostBinaryExpression,
        LuceneQueryExpression,
        AllowSpecialCharactersExpression,
    }
}