using System.Collections;
using System.Reflection;
using System.Xml.Linq;

namespace Kestrun.Utilities;

/// <summary>
/// Helpers for converting arbitrary objects into <see cref="XElement"/> instances.
/// </summary>
public static class XmlUtil
{
    private static readonly XNamespace xsi = "http://www.w3.org/2001/XMLSchema-instance";

    /// <summary>
    /// Converts an object to an <see cref="XElement"/> with the specified name, handling nulls, primitives, dictionaries, enumerables, and complex types.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="value">The object to convert to XML.</param>
    /// <returns>An <see cref="XElement"/> representing the object.</returns>
    public static XElement ToXml(string name, object? value)
    {
        // 1️⃣ null  → <name xsi:nil="true"/>
        if (value is null)
        {
            return new XElement(name, new XAttribute(xsi + "nil", true));
        }

        // 2️⃣ Primitive or string → <name>42</name>
        if (IsSimple(value))
        {
            return new XElement(name, value);
        }

        // 3️⃣ IDictionary (generic or non-generic)
        if (value is IDictionary dict)
        {
            return DictionaryToXml(name, dict);
        }

        // 4️⃣ IEnumerable (lists, arrays, StringValues, etc.)
        if (value is IEnumerable enumerable)
        {
            return EnumerableToXml(name, enumerable);
        }

        // 5️⃣ Fallback: reflect public instance properties (skip indexers)
        return ObjectToXml(name, value);
    }

    /// <summary>
    /// Determines whether the specified object is a simple type (primitive, string, DateTime, Guid, or decimal).
    /// </summary>
    /// <param name="value">The object to check.</param>
    /// <returns><c>true</c> if the object is a simple type; otherwise, <c>false</c>.</returns>
    private static bool IsSimple(object value)
    {
        var type = value.GetType();
        return type.IsPrimitive || value is string || value is DateTime || value is Guid || value is decimal;
    }

    /// <summary>
    /// Converts a dictionary to an XML element.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="dict">The dictionary to convert.</param>
    /// <returns>An <see cref="XElement"/> representing the dictionary.</returns>
    private static XElement DictionaryToXml(string name, IDictionary dict)
    {
        var elem = new XElement(name);
        foreach (DictionaryEntry entry in dict)
        {
            var key = entry.Key?.ToString() ?? "Key";
            elem.Add(ToXml(key, entry.Value));
        }
        return elem;
    }
    /// <summary>
    /// Converts an enumerable collection to an XML element.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="enumerable">The enumerable collection to convert.</param>
    /// <returns>An <see cref="XElement"/> representing the collection.</returns>
    private static XElement EnumerableToXml(string name, IEnumerable enumerable)
    {
        var elem = new XElement(name);
        foreach (var item in enumerable)
        {
            elem.Add(ToXml("Item", item));
        }
        return elem;
    }

    /// <summary>
    /// Converts an object to an XML element.
    /// </summary>
    /// <param name="name">The name of the XML element.</param>
    /// <param name="value">The object to convert.</param>
    /// <returns>An <see cref="XElement"/> representing the object.</returns>
    private static XElement ObjectToXml(string name, object value)
    {
        var objElem = new XElement(name);
        var type = value.GetType();
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.GetIndexParameters().Length > 0)   // <<—— SKIP INDEXERS
            {
                continue;
            }

            object? propVal;
            try
            {
                propVal = prop.GetValue(value);
            }
            catch
            {
                continue; // skip unreadable props
            }

            objElem.Add(ToXml(prop.Name, propVal));
        }

        return objElem;
    }
}
