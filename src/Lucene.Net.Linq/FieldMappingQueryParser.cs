namespace Lucene.Net.Linq
{
    #region Using Directives

    using System;
    using System.Collections.Generic;

    using Lucene.Net.Linq.Mapping;
    using Lucene.Net.Linq.Search;
    using Lucene.Net.QueryParsers;
    using Lucene.Net.Search;

    using Version = Lucene.Net.Util.Version;

    #endregion

    public class FieldMappingQueryParser<T> : QueryParser
    {
        private readonly string _defaultSearchField;

        public FieldMappingQueryParser(Version matchVersion, string defaultSearchField, IDocumentMapper<T> mapper)
            : base(matchVersion, defaultSearchField, mapper.Analyzer)
        {
            this._defaultSearchField = defaultSearchField;
            this.MatchVersion = matchVersion;
            this.DocumentMapper = mapper;

            this.DefaultSearchProperty = defaultSearchField;
        }

        /// <summary>
        ///     Sets the default property for queries that don't specify which field to search.
        ///     For an example query like <c>Lucene OR NuGet</c>, if this property is set to <c>SearchText</c>,
        ///     it will produce a query like <c>SearchText:Lucene OR SearchText:NuGet</c>.
        /// </summary>
        public string DefaultSearchProperty { get; set; }

        public Version MatchVersion { get; }

        public IDocumentMapper<T> DocumentMapper { get; }

        public override string Field => this.DefaultSearchProperty;

        protected override Query GetFieldQuery(string field, string queryText)
        {
            var mapping = this.GetMapping(field);

            try
            {
                var codedQueryText = mapping.ConvertToQueryExpression(queryText);
                return mapping.CreateQuery(codedQueryText);
            }
            catch (Exception ex)
            {
                throw new ParseException(ex.Message, ex);
            }
        }

        protected override Query GetRangeQuery(string field, string part1, string part2, bool inclusive)
        {
            var rangeType = inclusive ? RangeType.Inclusive : RangeType.Exclusive;
            var mapping = this.GetMapping(field);
            try
            {
                return mapping.CreateRangeQuery(part1, part2, rangeType, rangeType);
            }
            catch (Exception ex)
            {
                throw new ParseException(ex.Message, ex);
            }
        }

        protected override Query GetFieldQuery(string field, string queryText, int slop)
        {
            return base.GetFieldQuery(this.OverrideField(field), queryText, slop);
        }

        protected override Query GetWildcardQuery(string field, string termStr)
        {
            return base.GetWildcardQuery(this.OverrideField(field), termStr);
        }

        protected override Query GetPrefixQuery(string field, string termStr)
        {
            return base.GetPrefixQuery(this.OverrideField(field), termStr);
        }

        protected override Query GetFuzzyQuery(string field, string termStr, float minSimilarity)
        {
            return base.GetFuzzyQuery(this.OverrideField(field), termStr, minSimilarity);
        }

        private string OverrideField(string field)
        {
            if (field == this._defaultSearchField)
            {
                field = this.DefaultSearchProperty;
            }

            return field;
        }

        protected virtual IFieldMappingInfo GetMapping(string field)
        {
            field = this.OverrideField(field);

            try
            {
                return this.DocumentMapper.GetMappingInfo(field);
            }
            catch (KeyNotFoundException)
            {
                throw new ParseException("Unrecognized field: '" + field + "'");
            }
        }
    }
}