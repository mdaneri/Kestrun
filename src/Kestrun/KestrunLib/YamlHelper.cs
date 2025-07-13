using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Management.Automation;
using System.Text.Json;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
namespace KestrumLib
{
    public static class YamlHelper
    {
        private static readonly ISerializer _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance).DisableAliases()
            .Build();

        private static readonly IDeserializer _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        // Serialize any PowerShell object to YAML
        public static string ToYaml(object? input)
        {
            var normalized = NormalizePSObject(input);
            return _serializer.Serialize(normalized);
        }

        // Deserialize YAML into Hashtable
        public static Hashtable FromYamlToHashtable(string yaml)
        {
            var obj = _deserializer.Deserialize<object>(yaml);
            return (Hashtable)ConvertToPSCompatible(obj);
        }

        // Deserialize YAML into PSCustomObject
        public static PSObject FromYamlToPSCustomObject(string yaml)
        {
            var obj = _deserializer.Deserialize<object>(yaml);
            var hash = (Hashtable)ConvertToPSCompatible(obj);
            return ConvertToPSCustomObject(hash);
        }

        // --- Helper: Normalize PowerShell object into plain .NET structure
        private static object? NormalizePSObject(object? obj)
        {
            if (obj is PSObject psObj)
                return NormalizePSObject(psObj.BaseObject);

            if (obj is IDictionary dict)
            {
                var d = new Dictionary<object, object?>();
                foreach (var key in dict.Keys)
                {
                    var value = dict[key];
                    d[key] = value is not null ? NormalizePSObject(value) : null;
                }
                return d;
            }

            if (obj is IEnumerable enumerable && obj is not string)
                return enumerable.Cast<object>().Select(NormalizePSObject).ToList();

            if (obj is null || obj.GetType().IsPrimitive || obj is string)
                return obj;

            var props = obj.GetType().GetProperties();
            var result = new Dictionary<string, object?>();
            foreach (var prop in props)
            {
                try
                {
                    var propValue = prop.GetValue(obj);
                    result[prop.Name] = propValue is not null ? NormalizePSObject(propValue) : null;
                }
                catch { result[prop.Name] = null; }
            }

            return result;
        }

        // --- Helper: Convert raw YAML object to Hashtable recursively
        private static object ConvertToPSCompatible(object obj)
        {
            switch (obj)
            {
                case IDictionary dict:
                    var ht = new Hashtable();
                    foreach (DictionaryEntry entry in dict)
                        ht[entry.Key] = entry.Value is not null ? ConvertToPSCompatible(entry.Value) : null;
                    return ht;

                case IList list:
                    var array = new ArrayList();
                    foreach (var item in list)
                        array.Add(ConvertToPSCompatible(item));
                    return array;

                default:
                    return obj ?? string.Empty;
            }
        }

        // --- Helper: Convert Hashtable to PSCustomObject recursively
        private static PSObject ConvertToPSCustomObject(object obj)
        {
            if (obj is Hashtable ht)
            {
                var result = new PSObject();
                foreach (DictionaryEntry entry in ht)
                {
                    var key = entry.Key.ToString();
                    var value = entry.Value;

                    if (value is Hashtable || value is ArrayList)
                        value = ConvertToPSCustomObject(value);
                    if (key != null)
                    {
                        AddMember(result, key, value ?? string.Empty);
                    }
                }
                return result;
            }
            else if (obj is ArrayList list)
            {
                var resultList = new List<object>();
                foreach (var item in list)
                    resultList.Add(ConvertToPSCustomObject(item));
                return new PSObject(resultList.ToArray());
            }

            return PSObject.AsPSObject(obj);
        }

        private static void AddMember(PSObject obj, string name, object value)
        {
            obj.Properties.Add(new PSNoteProperty(name, value));
        }
    }
}