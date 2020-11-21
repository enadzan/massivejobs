using System;
using System.Collections.Generic;

namespace MassiveJobs.Core.Serialization
{
    public abstract class CustomTypeProvider: IJobTypeProvider
    {
        private readonly Dictionary<string, Type> _tagToTypeMap = new Dictionary<string, Type>();
        private readonly Dictionary<Type, string> _typeToTagMap = new Dictionary<Type, string>();

        protected CustomTypeProvider(IEnumerable<KeyValuePair<string, Type>> tagTypePairs)
        {
            _tagToTypeMap["u8"] = typeof(byte);
            _tagToTypeMap["u8?"] = typeof(byte?);
            _tagToTypeMap["i16"] = typeof(short);
            _tagToTypeMap["i16?"] = typeof(short?);
            _tagToTypeMap["u16"] = typeof(ushort);
            _tagToTypeMap["u16?"] = typeof(ushort?);
            _tagToTypeMap["i32"] = typeof(int);
            _tagToTypeMap["i32?"] = typeof(int?);
            _tagToTypeMap["u32"] = typeof(uint);
            _tagToTypeMap["u32?"] = typeof(uint?);
            _tagToTypeMap["i64"] = typeof(long);
            _tagToTypeMap["i64?"] = typeof(long?);
            _tagToTypeMap["u64"] = typeof(ulong);
            _tagToTypeMap["u64?"] = typeof(ulong?);
            _tagToTypeMap["dec"] = typeof(decimal);
            _tagToTypeMap["dec?"] = typeof(decimal?);
            _tagToTypeMap["flt"] = typeof(float);
            _tagToTypeMap["flt?"] = typeof(float?);
            _tagToTypeMap["dbl"] = typeof(double);
            _tagToTypeMap["dbl?"] = typeof(double?);
            _tagToTypeMap["dt"] = typeof(DateTime);
            _tagToTypeMap["dt?"] = typeof(DateTime?);
            _tagToTypeMap["dto"] = typeof(DateTimeOffset);
            _tagToTypeMap["dto?"] = typeof(DateTimeOffset?);
            _tagToTypeMap["s"] = typeof(string);
            _tagToTypeMap["v"] = typeof(VoidArgs);

            foreach (var tagTypePair in tagTypePairs)
            {
                _tagToTypeMap.Add(tagTypePair.Key, tagTypePair.Value);
            }

            foreach (var kvp in _tagToTypeMap)
            {
                _typeToTagMap.Add(kvp.Value, kvp.Key);
            }
        }

        public Type TagToType(string tag)
        {
            if (_tagToTypeMap.TryGetValue(tag, out var type)) return type;
            throw new Exception("unknown tag: " + tag);
        }

        public string TypeToTag(Type type)
        {
            if (_typeToTagMap.TryGetValue(type, out var tag)) return tag;
            throw new Exception("unsupported type: " + type.FullName);
        }
    }
}
