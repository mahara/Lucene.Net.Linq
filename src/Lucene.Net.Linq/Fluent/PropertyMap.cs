using System;
using System.ComponentModel;
using System.Reflection;
using Lucene.Net.Analysis;
using Lucene.Net.Documents;
using Lucene.Net.Linq.Analysis;
using Lucene.Net.Linq.Mapping;
using Lucene.Net.QueryParsers;

namespace Lucene.Net.Linq.Fluent
{
    /// <summary>
    /// A fluent interface for specifying additional options
    /// for how a property is analyzed, indexed and stored.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class PropertyMap<T>
    {
        protected readonly ClassMap<T> ClassMapProperty;
        protected readonly PropertyInfo PropertyInfoProperty;
        protected string FieldNameProperty;
        protected bool IsKeyProperty;
        protected TypeConverter ConverterProperty;
        protected Analyzer AnalyzerProperty;
        protected IndexMode IndexModeProperty = Mapping.IndexMode.Analyzed;
        protected StoreMode StoreProperty = StoreMode.Yes;
        protected float BoostProperty = 1.0f;
        protected bool CaseSensitiveProperty;
        protected QueryParser.Operator DefaultParseOperatorProperty = QueryParser.OR_OPERATOR;
        protected bool NativeSortProperty;

        internal PropertyMap(ClassMap<T> classMap, PropertyInfo propertyInfo, bool isKey = false)
            : this(classMap, propertyInfo, null)
        {
            this.IsKeyProperty = isKey;
        }

        protected internal PropertyMap(ClassMap<T> classMap, PropertyInfo propertyInfo, PropertyMap<T> copy)
        {
            this.ClassMapProperty = classMap;
            this.PropertyInfoProperty = propertyInfo;
            SetDefaults(propertyInfo, copy);
        }

        /// <summary>
        /// Set the field name. Defaults to same as property name being mapped.
        /// </summary>
        public virtual PropertyMap<T> ToField(string fieldName)
        {
            this.FieldNameProperty = fieldName;
            return this;
        }

        /// <summary>
        /// Configure values to be stored using <see cref="NumericField"/> instead
        /// of default <see cref="Field"/>.
        /// </summary>
        public NumericPropertyMap<T> AsNumericField()
        {
            if (this is NumericPropertyMap<T>) return (NumericPropertyMap<T>)this;
            var numericPart = new NumericPropertyMap<T>(this.ClassMapProperty, this.PropertyInfoProperty, this);
            this.ClassMapProperty.AddProperty(numericPart);
            return numericPart;
        }

        /// <summary>
        /// Specify a custom TypeConverter to convert the given type to a <see cref="string"/>
        /// and back to the other <see cref="Type"/>.
        /// </summary>
        public PropertyMap<T> ConvertWith(TypeConverter converter)
        {
            this.ConverterProperty = converter;
            return this;
        }

        /// <summary>
        /// Specify an <see cref="Analyzer"/> to use when indexing this property.
        /// </summary>
        public PropertyMap<T> AnalyzeWith(Analyzer analyzer)
        {
            this.AnalyzerProperty = analyzer;
            return this;
        }

        #region IndexMode settings

        /// <summary>
        /// Specify IndexMode slightly less fluently.
        /// </summary>
        public PropertyMap<T> IndexMode(IndexMode mode)
        {
            this.IndexModeProperty = mode;
            return this;
        }

        /// <summary>
        /// Specify IndexMode.
        /// </summary>
        public PropertyMap<T> Analyzed()
        {
            return IndexMode(Mapping.IndexMode.Analyzed);
        }

        /// <summary>
        /// Specify IndexMode.
        /// </summary>
        public PropertyMap<T> AnalyzedNoNorms()
        {
            return IndexMode(Mapping.IndexMode.AnalyzedNoNorms);
        }

        /// <summary>
        /// Specify IndexMode.
        /// </summary>
        public PropertyMap<T> NotAnalyzed()
        {
            return IndexMode(Mapping.IndexMode.NotAnalyzed);
        }

        /// <summary>
        /// Specify IndexMode.
        /// </summary>
        public PropertyMap<T> NotAnalyzedNoNorms()
        {
            return IndexMode(Mapping.IndexMode.NotAnalyzedNoNorms);
        }

        /// <summary>
        /// Specify IndexMode.
        /// </summary>
        public PropertyMap<T> NotIndexed()
        {
            return IndexMode(Mapping.IndexMode.NotIndexed);
        }

        #endregion

        /// <summary>
        /// Specify that the field is stored for later retrieval (the default behavior).
        /// </summary>
        public PropertyMap<T> Stored()
        {
            this.StoreProperty = StoreMode.Yes;
            return this;
        }

        /// <summary>
        /// Specify that the field is NOT stored for later retrieval.
        /// </summary>
        public PropertyMap<T> NotStored()
        {
            this.StoreProperty = StoreMode.No;
            return this;
        }

        /// <summary>
        /// Specify a constant boost to apply to this field at indexing time.
        /// </summary>
        public PropertyMap<T> BoostBy(float amount)
        {
            this.BoostProperty = amount;
            return this;
        }

        /// <summary>
        /// Specify that values for this field are case sensitive as
        /// opposed to the default behavior which assumes that the
        /// analyzer will convert tokens to lower case at indexing time.
        /// This controls <see cref="QueryParser.LowercaseExpandedTerms"/>
        /// when building queries.
        /// </summary>
        public PropertyMap<T> CaseSensitive()
        {
            this.CaseSensitiveProperty = true;
            return this;
        }

        /// <summary>
        /// Controls whether term vectors are stored for later retrieval.
        /// See <see cref="Field.TermVector"/> for more info.
        /// </summary>
        public TermVectorPart<T> WithTermVector
        {
            get
            {
                return new TermVectorPart<T>(this);
            }
        }

        /// <summary>
        /// Set the <see cref="QueryParser.DefaultOperator"/> to
        /// use <see cref="QueryParser.AND_OPERATOR"/> by default
        /// when parsing queries that contain multiple terms.
        /// </summary>
        public PropertyMap<T> ParseWithAndOperatorByDefault()
        {
            this.DefaultParseOperatorProperty = QueryParser.Operator.AND;
            return this;
        }

        /// <summary>
        /// Set the <see cref="QueryParser.DefaultOperator"/> to
        /// use <see cref="QueryParser.OR_OPERATOR"/> by default
        /// when parsing queries that contain multiple terms. This
        /// is the default behavior.
        /// </summary>
        public PropertyMap<T> ParseWithOrOperatorByDefault()
        {
            this.DefaultParseOperatorProperty = QueryParser.Operator.OR;
            return this;
        }

        public PropertyMap<T> NativeSort()
        {
            this.NativeSortProperty = true;
            return this;
        }
        protected internal string PropertyName
        {
            get { return this.PropertyInfoProperty.Name; }
        }

        protected internal bool IsKey
        {
            get { return this.IsKeyProperty; }
        }

        protected internal TermVectorMode TermVectorMode { get; set; }

        protected virtual Type PropertyType
        {
            get
            {
                Type type;

                FieldMappingInfoBuilder.IsCollection(this.PropertyInfoProperty.PropertyType, out type);

                return type;
            }
        }

        protected internal virtual IFieldMapper<T> ToFieldMapper()
        {
            var mapper = ToFieldMapperInternal();

            Type type;

            if (FieldMappingInfoBuilder.IsCollection(this.PropertyInfoProperty.PropertyType, out type))
            {
                return new CollectionReflectionFieldMapper<T>(mapper, type);
            }

            return mapper;
        }

        protected internal virtual ReflectionFieldMapper<T> ToFieldMapperInternal()
        {
            return new ReflectionFieldMapper<T>(this.PropertyInfoProperty, this.StoreProperty, this.IndexModeProperty, TermVectorMode,
                                                ResolveConverter(), this.FieldNameProperty, this.DefaultParseOperatorProperty,
                                                this.CaseSensitiveProperty, ResolveAnalyzer(), this.BoostProperty, this.NativeSortProperty);
        }

        private TypeConverter ResolveConverter()
        {
            if (this.ConverterProperty != null) return this.ConverterProperty;
            
            var fakeAttr = new FieldAttribute(this.IndexModeProperty) { CaseSensitive = this.CaseSensitiveProperty };

            return FieldMappingInfoBuilder.GetConverter(this.PropertyInfoProperty, PropertyType, fakeAttr);
        }

        private Analyzer ResolveAnalyzer()
        {
            if (this.AnalyzerProperty != null) return this.AnalyzerProperty;

            var fakeAttr = new FieldAttribute(this.IndexModeProperty) { CaseSensitive = this.CaseSensitiveProperty };

            var flag = FieldMappingInfoBuilder.GetCaseSensitivity(fakeAttr, this.ConverterProperty);

            return flag ? new KeywordAnalyzer() : new CaseInsensitiveKeywordAnalyzer();
        }

        private void SetDefaults(PropertyInfo propInfo, PropertyMap<T> copy)
        {
            if (copy != null)
            {
                this.IsKeyProperty = copy.IsKeyProperty;
                this.FieldNameProperty = copy.FieldNameProperty ?? propInfo.Name;
                return;
            }

            this.FieldNameProperty = propInfo.Name;
        }

    }
}