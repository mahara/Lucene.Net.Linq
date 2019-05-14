namespace Lucene.Net.Linq.Mapping
{
    #region Using Directives

    using System;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;

    using Lucene.Net.Analysis;
    using Lucene.Net.Documents;
    using Lucene.Net.Index;
    using Lucene.Net.Linq.Search;
    using Lucene.Net.Linq.Util;
    using Lucene.Net.QueryParsers;
    using Lucene.Net.Search;

    using Version = Lucene.Net.Util.Version;

    using System.Linq.Expressions;
    using System.Collections.Concurrent;

    #endregion

    public class ReflectionFieldMapper<T> : IFieldMapper<T>, IDocumentFieldConverter
    {
        protected static ConcurrentDictionary<string, object> InternalCacheProperty = new ConcurrentDictionary<string, object>(StringComparer.Ordinal);
        protected readonly Analyzer AnalyzerProperty;
        protected readonly float BoostProperty;
        protected readonly bool CaseSensitiveProperty;
        protected readonly TypeConverter ConverterProperty;
        protected readonly QueryParser.Operator DefaultParserOperatorProperty;
        protected readonly string FieldNameProperty;
        protected readonly IndexMode IndexProperty;
        protected readonly bool NativeSortProperty;
        protected readonly Func<T, object> PropertyGetterProperty;
        protected readonly PropertyInfo PropertyInfoProperty;
        protected readonly Action<T, object> PropertySetterProperty;
        protected readonly StoreMode StoreProperty;
        protected readonly TermVectorMode TermVectorProperty;

        public ReflectionFieldMapper(PropertyInfo propertyInfo, StoreMode store, IndexMode index, TermVectorMode termVector,
                                     TypeConverter converter, string fieldName, bool caseSensitive, Analyzer analyzer)
            : this(propertyInfo, store, index, termVector, converter, fieldName, caseSensitive, analyzer, 1f)
        {
        }

        public ReflectionFieldMapper(PropertyInfo propertyInfo, StoreMode store, IndexMode index, TermVectorMode termVector, TypeConverter converter, string fieldName, bool caseSensitive, Analyzer analyzer, float boost)
            : this(propertyInfo, store, index, termVector, converter, fieldName, QueryParser.Operator.OR, caseSensitive, analyzer, boost)
        {
        }

        public ReflectionFieldMapper(PropertyInfo propertyInfo, StoreMode store, IndexMode index, TermVectorMode termVector, TypeConverter converter, string fieldName, QueryParser.Operator defaultParserOperator, bool caseSensitive, Analyzer analyzer, float boost, bool nativeSort = false)
        {
            this.PropertyInfoProperty = propertyInfo;
            this.PropertyGetterProperty = CreatePropertyGetter(propertyInfo);
            if (propertyInfo.CanWrite)
            {
                this.PropertySetterProperty = CreatePropertySetter(propertyInfo);
            }

            this.StoreProperty = store;
            this.IndexProperty = index;
            this.TermVectorProperty = termVector;
            this.ConverterProperty = converter;
            this.FieldNameProperty = fieldName;
            this.DefaultParserOperatorProperty = defaultParserOperator;
            this.CaseSensitiveProperty = caseSensitive;
            this.AnalyzerProperty = analyzer;
            this.BoostProperty = boost;
            this.NativeSortProperty = nativeSort;
        }

        public virtual PropertyInfo PropertyInfo => this.PropertyInfoProperty;

        public virtual StoreMode Store => this.StoreProperty;

        public virtual TermVectorMode TermVector => this.TermVectorProperty;

        public virtual TypeConverter Converter => this.ConverterProperty;

        public virtual bool CaseSensitive => this.CaseSensitiveProperty;

        public virtual float Boost => this.BoostProperty;

        public virtual QueryParser.Operator DefaultParseOperator => this.DefaultParserOperatorProperty;

        public virtual bool NativeSort => this.NativeSortProperty;

        protected Field.Store FieldStore => (Field.Store) this.StoreProperty;

        public object GetFieldValue(Document document)
        {
            var field = document.GetFieldable(this.FieldNameProperty);

            if (field == null)
            {
                return null;
            }

            if (!this.PropertyInfoProperty.CanWrite)
            {
                return null;
            }

            return this.ConvertFieldValue(field);
        }

        public virtual Analyzer Analyzer => this.AnalyzerProperty;

        public virtual IndexMode IndexMode => this.IndexProperty;

        public virtual string FieldName => this.FieldNameProperty;

        public virtual string PropertyName => this.PropertyInfoProperty.Name;

        public virtual object GetPropertyValue(T source)
        {
            return this.PropertyGetterProperty(source);
        }

        public virtual void CopyFromDocument(Document source, IQueryExecutionContext context, T target)
        {
            if (!this.PropertyInfoProperty.CanWrite)
            {
                return;
            }

            var fieldValue = this.GetFieldValue(source);

            if (fieldValue != null)
            {
                this.PropertySetterProperty(target, fieldValue);
            }
        }

        public virtual void CopyToDocument(T source, Document target)
        {
            var value = this.PropertyGetterProperty(source);

            target.RemoveFields(this.FieldNameProperty);

            this.AddField(target, value);
        }

        public virtual string ConvertToQueryExpression(object value)
        {
            if (this.ConverterProperty != null)
            {
                return (string) this.ConverterProperty.ConvertTo(value, typeof(string));
            }

            return (string) value;
        }

        public virtual string EscapeSpecialCharacters(string value)
        {
            return QueryParser.Escape(value ?? string.Empty);
        }

        public virtual Query CreateQuery(string pattern)
        {
            Query query;

            if (this.TryParseKeywordContainingWhitespace(pattern, out query))
            {
                return query;
            }

            var queryParser = new QueryParser(Version.LUCENE_30, this.FieldName, this.AnalyzerProperty)
            {
                AllowLeadingWildcard = true,
                LowercaseExpandedTerms = !this.CaseSensitive,
                DefaultOperator = this.DefaultParserOperatorProperty
            };

            return queryParser.Parse(pattern);
        }

        public virtual Query CreateRangeQuery(object lowerBound, object upperBound, RangeType lowerRange, RangeType upperRange)
        {
            var minInclusive = lowerRange == RangeType.Inclusive;
            var maxInclusive = upperRange == RangeType.Inclusive;

            var lowerBoundStr = lowerBound == null ? null : this.EvaluateExpressionToStringAndAnalyze(lowerBound);
            var upperBoundStr = upperBound == null ? null : this.EvaluateExpressionToStringAndAnalyze(upperBound);
            return new TermRangeQuery(this.FieldName, lowerBoundStr, upperBoundStr, minInclusive, maxInclusive);
        }

        public virtual SortField CreateSortField(bool reverse)
        {
            if (this.Converter == null || this.NativeSort)
            {
                return new SortField(this.FieldName, SortField.STRING, reverse);
            }

            var propertyType = this.PropertyInfoProperty.PropertyType;

            FieldComparatorSource source;

            if (typeof(IComparable).IsAssignableFrom(propertyType))
            {
                source = new NonGenericConvertableFieldComparatorSource(propertyType, this.Converter);
            }
            else if (typeof(IComparable<>).MakeGenericType(propertyType).IsAssignableFrom(propertyType))
            {
                source = new GenericConvertableFieldComparatorSource(propertyType, this.Converter);
            }
            else
            {
                throw new NotSupportedException(string.Format("The type {0} does not implement IComparable or IComparable<T>. To use alphanumeric sorting, specify NativeSort=true on the mapping.",
                                                              propertyType));
            }

            return new SortField(this.FieldName, source, reverse);
        }

        /// <summary>
        ///     Attempt to determine if a given query pattern contains whitespace and
        ///     the analyzer does not tokenize on whitespace. This is a work-around
        ///     for cases when QueryParser would split a keyword that contains whitespace
        ///     into multiple tokens.
        /// </summary>
        protected virtual bool TryParseKeywordContainingWhitespace(string pattern, out Query query)
        {
            query = null;

            if (pattern.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0)
            {
                return false;
            }

            var terms = this.Analyzer.GetTerms(this.FieldName, pattern).ToList();

            if (terms.Count > 1)
            {
                return false;
            }

            var termValue = this.Unescape(terms.Single());
            var term = new Term(this.FieldName, termValue);

            if (this.IsWildcardPattern(termValue))
            {
                query = new WildcardQuery(term);
            }
            else
            {
                query = new TermQuery(term);
            }

            return true;
        }

        /// <summary>
        ///     Determine if a (potentially escaped) pattern contains
        ///     any non-escaped wildcard characters such as <c>*</c> or <c>?</c>.
        /// </summary>
        protected virtual bool IsWildcardPattern(string pattern)
        {
            var unescaped = pattern.Replace(@"\\", "");
            return unescaped.Replace(@"\*", "").Contains("*")
                   || unescaped.Replace(@"\?", "").Contains("?");
        }

        /// <summary>
        ///     Remove escape characters from a pattern. This method
        ///     is called when a <see cref="Query" /> is being created without using
        ///     <see cref="QueryParser.Parse" />.
        /// </summary>
        protected virtual string Unescape(string pattern)
        {
            return pattern.Replace(@"\", "");
        }

        /// <summary>
        ///     Creates a property getter method with Lambda Expressions.
        /// </summary>
        /// <param name="propertyInfo">The property info.</param>
        private static Func<T, object> CreatePropertyGetter(PropertyInfo propertyInfo)
        {
            // check cache to avoid creating another method
            var cacheKey = "getter." + propertyInfo.GetHashCode();
            object cache;
            if (InternalCacheProperty.TryGetValue(cacheKey, out cache))
            {
                return (Func<T, object>) cache;
            }

            // create method
            var name = propertyInfo.Name;
            var source = Expression.Parameter(typeof(T));
            var method = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(source, name), typeof(object)), source).Compile();

            // add to cache and return
            InternalCacheProperty.TryAdd(cacheKey, method);
            return method;
        }

        /// <summary>
        ///     Creates a property setter method with Lambda Expressions.
        /// </summary>
        /// <param name="propertyInfo">The property info.</param>
        private static Action<T, object> CreatePropertySetter(PropertyInfo propertyInfo)
        {
            // check cache to avoid creating another method
            var cacheKey = "setter." + propertyInfo.GetHashCode();
            object cache;
            if (InternalCacheProperty.TryGetValue(cacheKey, out cache))
            {
                return (Action<T, object>) cache;
            }

            // create method
            var name = propertyInfo.Name;
            var propType = propertyInfo.PropertyType;

            var sourceType = Expression.Parameter(typeof(T));
            var argument = Expression.Parameter(typeof(object), name);
            var propExp = Expression.Property(sourceType, name);

            var castToObject = Expression.Convert(argument, propType);

            var method = Expression.Lambda<Action<T, object>>(Expression.Assign(propExp, castToObject), sourceType, argument).Compile();

            // add to cache and return
            InternalCacheProperty.TryAdd(cacheKey, method);
            return method;
        }

        private string EvaluateExpressionToStringAndAnalyze(object value)
        {
            return this.AnalyzerProperty.Analyze(this.FieldName, this.ConvertToQueryExpression(value));
        }

        protected internal virtual object ConvertFieldValue(IFieldable field)
        {
            var fieldValue = (object) field.StringValue;

            if (this.ConverterProperty != null)
            {
                fieldValue = this.ConverterProperty.ConvertFrom(fieldValue);
            }

            return fieldValue;
        }

        protected internal void AddField(Document target, object value)
        {
            if (value == null)
            {
                return;
            }

            var fieldValue = (string) null;

            if (this.ConverterProperty != null)
            {
                fieldValue = (string) this.ConverterProperty.ConvertTo(value, typeof(string));
            }
            else if (value is string)
            {
                fieldValue = (string) value;
            }

            if (fieldValue != null)
            {
                var field = new Field(this.FieldNameProperty, fieldValue, this.FieldStore, (Field.Index) this.IndexProperty, (Field.TermVector) this.TermVector);
                field.Boost = this.Boost;
                target.Add(field);
            }
        }
    }
}