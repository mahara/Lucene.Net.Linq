namespace Lucene.Net.Linq.Mapping
{
    #region Using Directives

    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;

    using Lucene.Net.Analysis;
    using Lucene.Net.Documents;
    using Lucene.Net.Linq.Analysis;
    using Lucene.Net.QueryParsers;
    using Lucene.Net.Search;

    using Version = Lucene.Net.Util.Version;

    #endregion

    public abstract class DocumentMapperBase<T> : IDocumentMapper<T>, IDocumentKeyConverter, IDocumentModificationDetector<T>
    {
        protected readonly Analyzer ExternalAnalyzerProperty;
        protected readonly IDictionary<string, IFieldMapper<T>> FieldMapProperty = new Dictionary<string, IFieldMapper<T>>(StringComparer.Ordinal);
        protected readonly List<IFieldMapper<T>> KeyFieldsProperty = new List<IFieldMapper<T>>();
        protected readonly Version VersionProperty;
        protected PerFieldAnalyzer AnalyzerProperty;

        /// <summary>
        ///     Constructs an instance that will create an <see cref="Analyzer" />
        ///     using metadata on public properties on the type <typeparamref name="T" />.
        /// </summary>
        /// <param name="version">Version compatibility for analyzers and indexers.</param>
        protected DocumentMapperBase(Version version)
            : this(version, null)
        {
        }

        /// <summary>
        ///     Constructs an instance with an externall supplied analyzer
        ///     and the compatibility version of the index.
        /// </summary>
        /// <param name="version">Version compatibility for analyzers and indexers.</param>
        /// <param name="externalAnalyzer"></param>
        protected DocumentMapperBase(Version version, Analyzer externalAnalyzer)
        {
            this.VersionProperty = version;
            this.ExternalAnalyzerProperty = externalAnalyzer;
            this.AnalyzerProperty = new PerFieldAnalyzer(new KeywordAnalyzer());
        }

        protected virtual bool EnableScoreTracking
        {
            get { return this.FieldMapProperty.Values.Any(m => m is ReflectionScoreMapper<T>); }
        }

        public virtual IDocumentKey ToKey(Document document)
        {
            var keyValues = this.KeyFieldsProperty.ToDictionary(f => (IFieldMappingInfo) f, f => this.GetFieldValue(f, document));

            this.ValidateKey(keyValues);

            return new DocumentKey(keyValues);
        }

        public virtual PerFieldAnalyzer Analyzer => this.AnalyzerProperty;

        public virtual IEnumerable<string> AllProperties
        {
            get { return this.FieldMapProperty.Values.Select(m => m.PropertyName); }
        }

        public IEnumerable<string> IndexedProperties
        {
            get { return this.FieldMapProperty.Values.Where(m => m.IndexMode != IndexMode.NotIndexed).Select(m => m.PropertyName); }
        }

        public virtual IEnumerable<string> KeyProperties
        {
            get { return this.KeyFieldsProperty.Select(k => k.PropertyName); }
        }

        public virtual IFieldMappingInfo GetMappingInfo(string propertyName)
        {
            return this.FieldMapProperty[propertyName];
        }

        public virtual void ToObject(Document source, IQueryExecutionContext context, T target)
        {
            foreach (var mapping in this.FieldMapProperty)
            {
                mapping.Value.CopyFromDocument(source, context, target);
            }
        }

        public virtual void ToDocument(T source, Document target)
        {
            foreach (var mapping in this.FieldMapProperty)
            {
                mapping.Value.CopyToDocument(source, target);
            }
        }

        public virtual IDocumentKey ToKey(T source)
        {
            var keyValues = this.KeyFieldsProperty.ToDictionary(f => (IFieldMappingInfo) f, f => f.GetPropertyValue(source));

            this.ValidateKey(keyValues);

            return new DocumentKey(keyValues);
        }

        public virtual void PrepareSearchSettings(IQueryExecutionContext context)
        {
            if (this.EnableScoreTracking)
            {
                context.Searcher.SetDefaultFieldSortScoring(true, false);
            }
        }

        public Query CreateMultiFieldQuery(string pattern)
        {
            // TODO: pattern should be analyzed/converted on per-field basis.
            var parser = new MultiFieldQueryParser(this.VersionProperty, this.FieldMapProperty.Keys.ToArray(), this.ExternalAnalyzerProperty);
            return parser.Parse(pattern);
        }

        public virtual bool Equals(T item1, T item2)
        {
            foreach (var field in this.FieldMapProperty.Values)
            {
                var val1 = field.GetPropertyValue(item1);
                var val2 = field.GetPropertyValue(item2);

                if (!this.ValuesEqual(val1, val2))
                {
                    return false;
                }
            }

            return true;
        }

        public virtual bool IsModified(T item, Document document)
        {
            foreach (var field in this.FieldMapProperty.Values)
            {
                // IFieldMapper should tell us if the field is transient/non-comparable
                if (field is ReflectionScoreMapper<T>)
                {
                    continue;
                }

                var val1 = field.GetPropertyValue(item);
                var val2 = this.GetFieldValue(field, document);

                if (!this.ValuesEqual(val1, val2))
                {
                    return true;
                }
            }

            return false;
        }

        private object GetFieldValue(IFieldMappingInfo fieldMapper, Document document)
        {
            var fieldConverter = fieldMapper as IDocumentFieldConverter;

            if (fieldConverter == null)
            {
                throw new NotSupportedException(
                    string.Format("The field mapping of type {0} for field {1} must implement {2}.",
                                  fieldMapper.GetType(), fieldMapper.FieldName, typeof(IDocumentFieldConverter)));
            }

            return fieldConverter.GetFieldValue(document);
        }

        protected virtual void ValidateKey(Dictionary<IFieldMappingInfo, object> keyValues)
        {
            var nulls = keyValues.Where(kv => kv.Value == null).ToArray();

            if (!nulls.Any())
            {
                return;
            }

            var message = string.Format("Cannot create key for document of type '{0}' with null value(s) for properties {1} which are marked as Key=true.",
                                        typeof(T),
                                        string.Join(", ", nulls.Select(n => n.Key.PropertyName)));

            throw new InvalidOperationException(message);
        }

        protected internal virtual bool ValuesEqual(object val1, object val2)
        {
            if (val1 is IEnumerable && val2 is IEnumerable)
            {
                return ((IEnumerable) val1).Cast<object>().SequenceEqual(((IEnumerable) val2).Cast<object>());
            }

            return Equals(val1, val2);
        }

        public void AddField(IFieldMapper<T> fieldMapper)
        {
            this.FieldMapProperty.Add(fieldMapper.PropertyName, fieldMapper);
            if (!string.IsNullOrWhiteSpace(fieldMapper.FieldName) && fieldMapper.Analyzer != null)
            {
                this.Analyzer.AddAnalyzer(fieldMapper.FieldName, fieldMapper.Analyzer);
            }
        }

        public void AddKeyField(IFieldMapper<T> fieldMapper)
        {
            this.AddField(fieldMapper);
            this.KeyFieldsProperty.Add(fieldMapper);
        }
    }
}