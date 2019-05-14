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

namespace Lucene.Net.Linq.Mapping
{
    public class ReflectionFieldMapper<T> : IFieldMapper<T>, IDocumentFieldConverter
    {
        protected static ConcurrentDictionary<string, object> InternalCacheProperty = new ConcurrentDictionary<string, object> (StringComparer.Ordinal);
        protected readonly PropertyInfo PropertyInfoProperty;
        protected readonly Func<T, object> PropertyGetterProperty;
        protected readonly Action<T, object> PropertySetterProperty;
        protected readonly StoreMode StoreProperty;
        protected readonly IndexMode IndexProperty;
        protected readonly TermVectorMode TermVectorProperty;
        protected readonly TypeConverter ConverterProperty;
        protected readonly string FieldNameProperty;
        protected readonly QueryParser.Operator DefaultParserOperatorProperty;
        protected readonly bool CaseSensitiveProperty;
        protected readonly Analyzer AnalyzerProperty;
        protected readonly float BoostProperty;
        protected readonly bool NativeSortProperty;

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
                this.PropertySetterProperty = CreatePropertySetter(propertyInfo);
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

        public virtual Analyzer Analyzer
        {
            get
            {
                return this.AnalyzerProperty;
            }
        }

        public virtual PropertyInfo PropertyInfo
        {
            get
            {
                return this.PropertyInfoProperty;
            }
        }

        public virtual StoreMode Store
        {
            get
            {
                return this.StoreProperty;
            }
        }

        public virtual IndexMode IndexMode
        {
            get
            {
                return this.IndexProperty;
            }
        }

        public virtual TermVectorMode TermVector
        {
            get
            {
                return this.TermVectorProperty;
            }
        }

        public virtual TypeConverter Converter
        {
            get
            {
                return this.ConverterProperty;
            }
        }

        public virtual string FieldName
        {
            get
            {
                return this.FieldNameProperty;
            }
        }

        public virtual bool CaseSensitive
        {
            get
            {
                return this.CaseSensitiveProperty;
            }
        }

        public virtual float Boost
        {
            get
            {
                return this.BoostProperty;
            }
        }

        public virtual string PropertyName
        {
            get
            {
                return this.PropertyInfoProperty.Name;
            }
        }

        public virtual QueryParser.Operator DefaultParseOperator
        {
            get
            {
                return this.DefaultParserOperatorProperty;
            }
        }

        public virtual bool NativeSort
        {
            get { return this.NativeSortProperty; }
        }

        public virtual object GetPropertyValue(T source)
        {
            return this.PropertyGetterProperty(source);
        }

        public virtual void CopyFromDocument(Document source, IQueryExecutionContext context, T target)
        {
            if (!this.PropertyInfoProperty.CanWrite) return;

            var fieldValue = GetFieldValue(source);

            if (fieldValue != null)
                this.PropertySetterProperty(target, fieldValue);
        }

        public object GetFieldValue(Document document)
        {
            var field = document.GetFieldable(this.FieldNameProperty);

            if (field == null)
                return null;

            if (!this.PropertyInfoProperty.CanWrite)
                return null;

            return ConvertFieldValue(field);
        }

        public virtual void CopyToDocument(T source, Document target)
        {
            var value = this.PropertyGetterProperty(source);

            target.RemoveFields(this.FieldNameProperty);

            AddField(target, value);
        }

        public virtual string ConvertToQueryExpression(object value)
        {
            if (this.ConverterProperty != null)
            {
                return (string)this.ConverterProperty.ConvertTo(value, typeof(string));
            }

            return (string)value;
        }

        public virtual string EscapeSpecialCharacters(string value)
        {
            return QueryParser.Escape(value ?? string.Empty);
        }

        public virtual Query CreateQuery(string pattern)
        {
            Query query;

            if (TryParseKeywordContainingWhitespace(pattern, out query))
            {
                return query;
            }

            var queryParser = new QueryParser(Version.LUCENE_30, FieldName, this.AnalyzerProperty)
            {
                AllowLeadingWildcard = true,
                LowercaseExpandedTerms = !CaseSensitive,
                DefaultOperator = this.DefaultParserOperatorProperty
            };

            return queryParser.Parse(pattern);
        }

        /// <summary>
        /// Attempt to determine if a given query pattern contains whitespace and
        /// the analyzer does not tokenize on whitespace. This is a work-around
        /// for cases when QueryParser would split a keyword that contains whitespace
        /// into multiple tokens.
        /// </summary>
        protected virtual bool TryParseKeywordContainingWhitespace(string pattern, out Query query)
        {
            query = null;

            if (pattern.IndexOfAny(new[] { ' ', '\t', '\r', '\n' }) < 0) return false;

            var terms = Analyzer.GetTerms(FieldName, pattern).ToList();

            if (terms.Count > 1) return false;
            
            var termValue = Unescape(terms.Single());
            var term = new Term(FieldName, termValue);

            if (IsWildcardPattern(termValue))
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
        /// Determine if a (potentially escaped) pattern contains
        /// any non-escaped wildcard characters such as <c>*</c> or <c>?</c>.
        /// </summary>
        protected virtual bool IsWildcardPattern(string pattern)
        {
            var unescaped = pattern.Replace(@"\\", "");
            return unescaped.Replace(@"\*", "").Contains("*")
                || unescaped.Replace(@"\?", "").Contains("?");
        }

        /// <summary>
        /// Remove escape characters from a pattern. This method
        /// is called when a <see cref="Query"/> is being created without using
        /// <see cref="QueryParser.Parse"/>.
        /// </summary>
        protected virtual string Unescape(string pattern)
        {
            return pattern.Replace(@"\", "");
        }

        /// <summary>
        /// Creates a property getter method with Lambda Expressions.
        /// </summary>
        /// <param name="propertyInfo">The property info.</param>
        private static Func<T, object> CreatePropertyGetter(System.Reflection.PropertyInfo propertyInfo)
        {
            // check cache to avoid creating another method
            string cacheKey = "getter." + propertyInfo.GetHashCode ();
            object cache;
            if (InternalCacheProperty.TryGetValue (cacheKey, out cache))
                return (Func<T, object>)cache;
            
            // create method
            var name = propertyInfo.Name;
            var source = Expression.Parameter(typeof(T));
            var method = Expression.Lambda<Func<T, object>>(Expression.Convert(Expression.Property(source, name), typeof (object)), source).Compile();

            // add to cache and return
            InternalCacheProperty.TryAdd (cacheKey, method);
            return method;
        }

        /// <summary>
        /// Creates a property setter method with Lambda Expressions.
        /// </summary>
        /// <param name="propertyInfo">The property info.</param>
        private static Action<T, object> CreatePropertySetter(System.Reflection.PropertyInfo propertyInfo)
        {
            // check cache to avoid creating another method
            string cacheKey = "setter." + propertyInfo.GetHashCode ();
            object cache;
            if (InternalCacheProperty.TryGetValue (cacheKey, out cache))
                return (Action<T, object>)cache;
            
            // create method
            var name = propertyInfo.Name;
            var propType = propertyInfo.PropertyType;

            var sourceType = Expression.Parameter(typeof(T));
            var argument = Expression.Parameter(typeof(object), name);
            var propExp = Expression.Property(sourceType, name);

            var castToObject = Expression.Convert(argument, propType);

            var method = Expression.Lambda<Action<T, object>> (Expression.Assign (propExp, castToObject), sourceType, argument).Compile ();

            // add to cache and return
            InternalCacheProperty.TryAdd (cacheKey, method);
            return method;
        }

        public virtual Query CreateRangeQuery(object lowerBound, object upperBound, RangeType lowerRange, RangeType upperRange)
        {
            var minInclusive = lowerRange == RangeType.Inclusive;
            var maxInclusive = upperRange == RangeType.Inclusive;

            var lowerBoundStr = lowerBound == null ? null : EvaluateExpressionToStringAndAnalyze(lowerBound);
            var upperBoundStr = upperBound == null ? null : EvaluateExpressionToStringAndAnalyze(upperBound);
            return new TermRangeQuery(FieldName, lowerBoundStr, upperBoundStr, minInclusive, maxInclusive);
        }

        public virtual SortField CreateSortField(bool reverse)
        {
            if (Converter == null || NativeSort)
                return new SortField(FieldName, SortField.STRING, reverse);

            var propertyType = this.PropertyInfoProperty.PropertyType;

            FieldComparatorSource source;

            if (typeof(IComparable).IsAssignableFrom(propertyType))
            {
                source = new NonGenericConvertableFieldComparatorSource(propertyType, Converter);
            }
            else if (typeof(IComparable<>).MakeGenericType(propertyType).IsAssignableFrom(propertyType))
            {
                source = new GenericConvertableFieldComparatorSource(propertyType, Converter);
            }
            else
            {
                throw new NotSupportedException(string.Format("The type {0} does not implement IComparable or IComparable<T>. To use alphanumeric sorting, specify NativeSort=true on the mapping.",
                    propertyType));
            }

            return new SortField(FieldName, source, reverse);
        }

        private string EvaluateExpressionToStringAndAnalyze(object value)
        {
            return this.AnalyzerProperty.Analyze(FieldName, ConvertToQueryExpression(value));
        }

        protected internal virtual object ConvertFieldValue(IFieldable field)
        {
            var fieldValue = (object)field.StringValue;

            if (this.ConverterProperty != null)
            {
                fieldValue = this.ConverterProperty.ConvertFrom(fieldValue);
            }
            return fieldValue;
        }

        protected internal void AddField(Document target, object value)
        {
            if (value == null)
                return;

            var fieldValue = (string)null;

            if (this.ConverterProperty != null)
            {
                fieldValue = (string)this.ConverterProperty.ConvertTo(value, typeof(string));
            }
            else if (value is string)
            {
                fieldValue = (string)value;
            }

            if (fieldValue != null)
            {
                var field = new Field(this.FieldNameProperty, fieldValue, FieldStore, (Field.Index)this.IndexProperty, (Field.TermVector)TermVector);
                field.Boost = Boost;
                target.Add(field);
            }
        }

        protected Field.Store FieldStore
        {
            get
            {
                return (Field.Store)this.StoreProperty;
            }
        }
    }
}