using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using ObjectPrinting.Tests;

namespace ObjectPrinting
{
    public class PrintingConfig<TOwner>
    {
        private static readonly Type[] FinalTypes =
        [
            typeof(int), typeof(double), typeof(float), typeof(string),
            typeof(DateTime), typeof(TimeSpan)
        ];
        private readonly List<Type> exceptsTypes = [];
        private readonly List<PropertyInfo> exceptsProperties = [];
        private readonly Dictionary<Type, Delegate> serializeTypeMethods = [];
        private readonly Dictionary<PropertyInfo, Delegate> serializePropertyMethods = [];
        
        public PrintingConfig<TOwner> ExcludeType<T>()
        {
            exceptsTypes.Add(typeof(T));
            return this;
        }
        
        public PrintingConfig<TOwner> ExcludeProperty<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector)
        {
            var propertyInfo = ((MemberExpression)memberSelector.Body).Member as PropertyInfo;
            exceptsProperties.Add(propertyInfo);
            return this;
        }
        
        public PrintingConfig<TOwner> SerializeTypeBy<T>(Func<T, string> serializationMethod)
        {
            serializeTypeMethods[typeof(T)] = serializationMethod;
            return this;
        }
        
        public PrintingConfig<TOwner> SerializePropertyBy<TPropType>(Expression<Func<TOwner, TPropType>> memberSelector, 
            Func<TPropType, string> serializationMethod)
        {
            var propertyInfo = ((MemberExpression)memberSelector.Body).Member as PropertyInfo;
            serializePropertyMethods[propertyInfo] = serializationMethod;
            return this;
        }
        
        public PrintingConfig<TOwner> SetCulture<TNum>(CultureInfo cultureInfo) where TNum : IFormattable
        {
            if (!IsNumericType<TNum>())
                throw new NotSupportedException("Only numeric types are supported");
            
            serializeTypeMethods[typeof(TNum)] = (TNum x) => x.ToString("", cultureInfo);
            return this;
        }

        public PrintingConfig<TOwner> TrimmedByLength(Expression<Func<TOwner, string>> memberSelector, int length)
        {
            var propertyInfo = ((MemberExpression)memberSelector.Body).Member as PropertyInfo;
            serializePropertyMethods[propertyInfo] = (string s) => s[..length];
            return this;
        }
        
        public string PrintToString(TOwner obj)
        {
            return PrintToString(obj, 0);
        }

        private string PrintToString(object obj, int nestingLevel)
        {
            if (obj == null)
                return "null" + Environment.NewLine;
            
            if (FinalTypes.Contains(obj.GetType()))
                return obj + Environment.NewLine;

            var indentation = new string('\t', nestingLevel + 1);
            var sb = new StringBuilder();
            var type = obj.GetType();
            sb.AppendLine(type.Name);
            foreach (var propertyInfo in type.GetProperties())
            {
                if (exceptsTypes.Contains(propertyInfo.PropertyType))
                    continue;
                if (exceptsProperties.Contains(propertyInfo))
                    continue;
                var value = propertyInfo.GetValue(obj);
                var serializeStr = TryUseCustomSerializer(value, propertyInfo, out var str)
                    ? str
                    : PrintToString(value, nestingLevel + 1);
                sb.Append('\n' + indentation + propertyInfo.Name + " = " + serializeStr);
            }
            return sb.ToString();
        }
        
        private bool TryUseCustomSerializer(object value, PropertyInfo propertyInfo, out string str)
        {
            str = null;
            if (serializePropertyMethods.TryGetValue(propertyInfo, out var lambda))
            {
                str = (string)lambda.DynamicInvoke(value);
                return true;
            }
            if (serializeTypeMethods.TryGetValue(propertyInfo.PropertyType, out lambda)) 
            {
                str = (string)lambda.DynamicInvoke(value);
                return true;
            }
            return false;
        }
        
        private static bool IsNumericType<T>()
        {
            var type = typeof(T);
            return type == typeof(int) ||
                   type == typeof(double) ||
                   type == typeof(float) ||
                   type == typeof(decimal) ||
                   type == typeof(long) ||
                   type == typeof(short) ||
                   type == typeof(byte) ||
                   type == typeof(uint) ||
                   type == typeof(ulong) ||
                   type == typeof(ushort) ||
                   type == typeof(sbyte);
        }
    }
}