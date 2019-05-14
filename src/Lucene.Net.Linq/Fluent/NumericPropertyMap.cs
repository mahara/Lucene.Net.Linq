using System.Reflection;
using Lucene.Net.Documents;
using Lucene.Net.Linq.Mapping;
using Lucene.Net.Util;

namespace Lucene.Net.Linq.Fluent
{
    /// <summary>
    /// Extends <see cref="PropertyMap{T}"/> to allow a property
    /// to be indexed as a <see cref="NumericField"/> with a
    /// given precision step. See <see cref="PropertyMap{T}.AsNumericField"/>
    /// </summary>
    public class NumericPropertyMap<T> : PropertyMap<T>
    {
        private int _precisionStep = NumericUtils.PRECISION_STEP_DEFAULT;

        internal NumericPropertyMap(ClassMap<T> classMap, PropertyInfo propertyInfo, PropertyMap<T> copy) : base(classMap, propertyInfo, copy)
        {
        }

        protected internal override ReflectionFieldMapper<T> ToFieldMapperInternal()
        {
            var attrib = new NumericFieldAttribute(this.FieldNameProperty)
                {
                    Boost = BoostProperty,
                    ConverterInstance = ConverterProperty,
                    PrecisionStep = this._precisionStep,
                    Store = StoreProperty
            };

            return NumericFieldMappingInfoBuilder.BuildNumeric<T>(this.PropertyInfoProperty, PropertyType, attrib);
        }

        /// <summary>
        /// Sets the precision step for the field. Defaults to <see cref="NumericUtils.PRECISION_STEP_DEFAULT"/>.
        /// </summary>
        public NumericPropertyMap<T> WithPrecisionStep(int precisionStep)
        {
            this._precisionStep = precisionStep;
            return this;
        }
    }
}
