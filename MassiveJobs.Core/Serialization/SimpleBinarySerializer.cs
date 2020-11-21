using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MassiveJobs.Core.Serialization
{
    public class SimpleBinarySerializer: BaseSerializer
    {
        private static readonly ConcurrentDictionary<Type, Tuple<int, PropertyInfo>[]> PropertiesCache
            = new ConcurrentDictionary<Type, Tuple<int, PropertyInfo>[]>();

        private static readonly ConcurrentDictionary<Type, ConstructorInfo> ConstructorsCache
            = new ConcurrentDictionary<Type, ConstructorInfo>();

        protected override object DeserializeEnvelope(ReadOnlySpan<byte> data, Type envType)
        {
            var env = GetDefaultConstructor(envType).Invoke(null);

            var nextIndex = 4; // skip header size field

            SetStringProperty(envType, env, nameof(SerializedEnvelope<object>.J), data, ref nextIndex);
            SetDateTimeProperty(envType, env, nameof(SerializedEnvelope<object>.At), data, ref nextIndex);
            SetIntProperty(envType, env, nameof(SerializedEnvelope<object>.R), data, ref nextIndex);
            SetIntProperty(envType, env, nameof(SerializedEnvelope<object>.T), data, ref nextIndex);
            SetStringProperty(envType, env, nameof(SerializedEnvelope<object>.G), data, ref nextIndex);

            var indexBeforePeriodicRunInfo = nextIndex;

            var periodicRunInfo = new PeriodicRunInfo();

            SetIntProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.RepeatSeconds), data, ref nextIndex);
            SetDateTimeProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.NextRunTime), data, ref nextIndex);
            SetDateTimeProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.LastRunTimeUtc), data, ref nextIndex);
            SetDateTimeProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.EndAtUtc), data, ref nextIndex);
            SetStringProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.CronExp), data, ref nextIndex);
            SetStringProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.TzId), data, ref nextIndex);

            if (nextIndex > indexBeforePeriodicRunInfo + 18) // headers 2 + 2 + 2 + 2 + 5 + 5
            {
                var pProp = GetPublicProperty(envType, nameof(SerializedEnvelope<object>.P));
                pProp.SetValue(env, periodicRunInfo);
            }

            var headerSize = BitConverter.ToInt32(data.Slice(0, 4).ToArray(), 0);

            // sanity check
            if (headerSize < nextIndex) throw new Exception("Invalid header size");

            // skip header part that we don't understand (future proofing)
            nextIndex = headerSize;

            var arg = DeserializeObject(envType.GenericTypeArguments[0], data, ref nextIndex);

            var argProp = GetPublicProperty(envType, nameof(SerializedEnvelope<object>.A));
            argProp.SetValue(env, arg);

            return env;
        }

        protected override byte[] SerializeEnvelope(SerializedEnvelope<object> envelope)
        {
            var objectProps = new List<byte[]>();

            Serialize(envelope.J, objectProps);
            Serialize(envelope.At, objectProps);
            Serialize(envelope.R, objectProps);
            Serialize(envelope.T, objectProps);
            Serialize(envelope.G, objectProps);

            Serialize(envelope.P?.RepeatSeconds, objectProps);
            Serialize(envelope.P?.NextRunTime, objectProps);
            Serialize(envelope.P?.LastRunTimeUtc, objectProps);
            Serialize(envelope.P?.EndAtUtc, objectProps);
            Serialize(envelope.P?.CronExp, objectProps);
            Serialize(envelope.P?.TzId, objectProps);

            var headerSize = objectProps.Sum(o => o.Length) + 4;

            SerializeObject(envelope.A, objectProps);

            var result = new byte[objectProps.Sum(p => p.Length) + 4];

            BitConverter.GetBytes(headerSize).CopyTo(result, 0);

            var nextIndex = 4;
            foreach (var prop in objectProps)
            {
                prop.CopyTo(result, nextIndex);
                nextIndex += prop.Length;
            }

            return result;
        }

        private static void SerializeObject(object obj, ICollection<byte[]> objectProps)
        {
            if (obj == null) return; // possible for VoidArgs

            var properties = GetPublicProperties(obj.GetType());

            var hasOrderedProperties = false;

            foreach (var tuple in properties)
            {
                if (tuple.Item1 == int.MaxValue) continue; // skip properties without PropertyOrder attribute
                var property = tuple.Item2;

                hasOrderedProperties = true;

                if (property.PropertyType == typeof(string))
                {
                    Serialize((string) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(byte) || property.PropertyType == typeof(byte?))
                {
                    Serialize((byte?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(short) || property.PropertyType == typeof(short?))
                {
                    Serialize((short?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                {
                    Serialize((int?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(long) || property.PropertyType == typeof(long?))
                {
                    Serialize((long?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(ushort) || property.PropertyType == typeof(ushort?))
                {
                    Serialize((ushort?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(uint) || property.PropertyType == typeof(uint?))
                {
                    Serialize((uint?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(ulong) || property.PropertyType == typeof(ulong?))
                {
                    Serialize((ulong?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(double?))
                {
                    Serialize((double?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(float) || property.PropertyType == typeof(float?))
                {
                    Serialize((float?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    Serialize((DateTime?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(DateTimeOffset) || property.PropertyType == typeof(DateTimeOffset?))
                {
                    Serialize((DateTimeOffset?) property.GetValue(obj), objectProps);
                }
                else if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
                {
                    Serialize((decimal?) property.GetValue(obj), objectProps);
                }
            }

            if (!hasOrderedProperties)
            {
                throw new Exception(
                    $"Cannot serialize object without PropertyOrder attributes ({obj.GetType().FullName})");
            }
        }

        private object DeserializeObject(Type type, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (type == typeof(VoidArgs)) return null;

            var obj = GetDefaultConstructor(type).Invoke(null);

            var properties = GetPublicProperties(type);

            foreach (var tuple in properties)
            {
                if (tuple.Item1 == int.MaxValue) continue; // skip properties without PropertyOrder attribute
                var property = tuple.Item2;

                if (property.PropertyType == typeof(string))
                {
                    SetStringProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(byte) || property.PropertyType == typeof(byte?))
                {
                    SetByteProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(short) || property.PropertyType == typeof(short?))
                {
                    SetShortProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(int) || property.PropertyType == typeof(int?))
                {
                    SetIntProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(long) || property.PropertyType == typeof(long?))
                {
                    SetLongProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(ushort) || property.PropertyType == typeof(ushort?))
                {
                    SetUShortProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(uint) || property.PropertyType == typeof(uint?))
                {
                    SetUIntProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(ulong) || property.PropertyType == typeof(ulong?))
                {
                    SetULongProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(double) || property.PropertyType == typeof(double?))
                {
                    SetDoubleProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(float) || property.PropertyType == typeof(float?))
                {
                    SetFloatProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    SetDateTimeProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(DateTimeOffset) || property.PropertyType == typeof(DateTimeOffset?))
                {
                    SetDateTimeOffsetProperty(type, obj, property.Name, data, ref nextIndex);
                }
                else if (property.PropertyType == typeof(decimal) || property.PropertyType == typeof(decimal?))
                {
                    SetDecimalProperty(type, obj, property.Name, data, ref nextIndex);
                }
            }

            return obj;
        }

        private static void Serialize(double? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 10 : 2];
            bytes[0] = (byte) SupportedTypes.Double;

            if (value.HasValue)
            {
                bytes[1] = 8;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetDoubleProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Double)
                throw new Exception($"Expected Double ({(byte) SupportedTypes.Double}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToDouble(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(float? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 6 : 2];
            bytes[0] = (byte) SupportedTypes.Float;

            if (value.HasValue)
            {
                bytes[1] = 4;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetFloatProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Float)
                throw new Exception($"Expected Float ({(byte) SupportedTypes.Float}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToSingle(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(ulong? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 10 : 2];
            bytes[0] = (byte) SupportedTypes.ULong;

            if (value.HasValue)
            {
                bytes[1] = 8;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetULongProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.ULong)
                throw new Exception($"Expected ULong ({(byte) SupportedTypes.ULong}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToUInt64(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(long? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 10 : 2];
            bytes[0] = (byte) SupportedTypes.Long;

            if (value.HasValue)
            {
                bytes[1] = 8;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetLongProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Long)
                throw new Exception($"Expected Long ({(byte) SupportedTypes.Long}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToInt64(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(uint? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 6 : 2];
            bytes[0] = (byte) SupportedTypes.UInt;

            if (value.HasValue)
            {
                bytes[1] = 4;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetUIntProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.UInt)
                throw new Exception($"Expected UInt ({(byte) SupportedTypes.UInt}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToUInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(int? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 6 : 2];
            bytes[0] = (byte) SupportedTypes.Int;

            if (value.HasValue)
            {
                bytes[1] = 4;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetIntProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Int)
                throw new Exception($"Expected Int ({(byte) SupportedTypes.Int}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(ushort? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 4 : 2];
            bytes[0] = (byte) SupportedTypes.UShort;

            if (value.HasValue)
            {
                bytes[1] = 2;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetUShortProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.UShort)
                throw new Exception($"Expected UShort ({(byte) SupportedTypes.UShort}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToUInt16(data.Slice(nextIndex, 2).ToArray(), 0);
            nextIndex += 2;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(short? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 4 : 2];
            bytes[0] = (byte) SupportedTypes.Short;

            if (value.HasValue)
            {
                bytes[1] = 2;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
        }

        private void SetShortProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Short)
                throw new Exception($"Expected Short ({(byte) SupportedTypes.Short}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = BitConverter.ToInt16(data.Slice(nextIndex, 2).ToArray(), 0);
            nextIndex += 2;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(byte? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 3 : 2];
            bytes[0] = (byte) SupportedTypes.Byte;

            if (value.HasValue)
            {
                bytes[1] = 1;
                bytes[2] = value.Value;
            }

            objectProps.Add(bytes);
        }

        private void SetByteProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Byte)
                throw new Exception($"Expected Byte ({(byte) SupportedTypes.Byte}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var value = data[nextIndex];
            nextIndex++;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(string value, ICollection<byte[]> objectProps)
        {
            var valueBytes = value != null ? Encoding.UTF8.GetBytes(value) : null;
            var length = valueBytes?.Length ?? 0;

            var bytes = new byte[length + 5];
            bytes[0] = (byte) SupportedTypes.String;

            if (valueBytes != null)
            {
                BitConverter.GetBytes(length).CopyTo(bytes, 1);
                valueBytes.CopyTo(bytes, 5);
            }
            else
            {
                BitConverter.GetBytes(-1).CopyTo(bytes, 1);
            }

            objectProps.Add(bytes);
        }

        private void SetStringProperty(Type type, object obj, string propName, ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.String)
                throw new Exception($"Expected String ({(byte) SupportedTypes.String}) but found {data[nextIndex]}");

            nextIndex++;

            var size = BitConverter.ToInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            if (size == -1) return;

            var value = size > 0 ? Encoding.UTF8.GetString(data.Slice(nextIndex, size).ToArray()) : "";
            nextIndex += size;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, value);
        }

        private static void Serialize(decimal? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 18 : 2];
            bytes[0] = (byte) SupportedTypes.Decimal;

            if (value.HasValue)
            {
                bytes[1] = 16;
                var parts = decimal.GetBits(value.Value);

                BitConverter.GetBytes(parts[0]).CopyTo(bytes, 2);
                BitConverter.GetBytes(parts[1]).CopyTo(bytes, 6);
                BitConverter.GetBytes(parts[2]).CopyTo(bytes, 10);
                BitConverter.GetBytes(parts[3]).CopyTo(bytes, 14);
            }

            objectProps.Add(bytes);
        }

        private void SetDecimalProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.Decimal)
                throw new Exception($"Expected Decimal ({(byte) SupportedTypes.Decimal}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var part0 = BitConverter.ToInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var part1 = BitConverter.ToInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var part2 = BitConverter.ToInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var part3 = BitConverter.ToInt32(data.Slice(nextIndex, 4).ToArray(), 0);
            nextIndex += 4;

            var sign = (part3 & 0x80000000) != 0;
            var scale = (byte) ((part3 >> 16) & 0x7F);

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, new decimal(part0, part1, part2, sign, scale));
        }

        private static void Serialize(DateTimeOffset? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 12 : 2];
            bytes[0] = (byte) SupportedTypes.DateTimeOffset;

            if (value.HasValue)
            {
                bytes[1] = 10;
                BitConverter.GetBytes(value.Value.Ticks).CopyTo(bytes, 2);
                BitConverter.GetBytes((short) value.Value.Offset.TotalMinutes).CopyTo(bytes, 10);
            }
                
            objectProps.Add(bytes);
        }

        private void SetDateTimeOffsetProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.DateTimeOffset)
                throw new Exception($"Expected DateTimeOffset ({(byte) SupportedTypes.DateTimeOffset}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var ticks = BitConverter.ToInt64(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var offset = TimeSpan.FromMinutes(BitConverter.ToInt16(data.Slice(nextIndex, 2).ToArray(), 0));
            nextIndex += 2;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, new DateTimeOffset(ticks, offset));
        }

        private static void Serialize(DateTime? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 11 : 2];
            bytes[0] = (byte) SupportedTypes.DateTime;

            if (value.HasValue)
            {
                bytes[1] = 9;
                BitConverter.GetBytes(value.Value.Ticks).CopyTo(bytes, 2);
                bytes[10] = (byte) value.Value.Kind;
            }

            objectProps.Add(bytes);
        }

        private void SetDateTimeProperty(Type type, object obj, string propName, ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.DateTime)
                throw new Exception($"Expected DateTime ({(byte) SupportedTypes.DateTime}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var ticks = BitConverter.ToInt64(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var kind = (DateTimeKind) data[nextIndex];
            nextIndex++;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, new DateTime(ticks, kind));
        }

        private static ConstructorInfo GetDefaultConstructor(Type type)
        {
            if (ConstructorsCache.TryGetValue(type, out var ctor)) return ctor;

            ctor = type.GetConstructor(Type.EmptyTypes);
            ConstructorsCache.TryAdd(type, ctor);

            return ctor;
        }

        private static PropertyInfo GetPublicProperty(Type type, string propertyName)
        {
            var properties = GetPublicProperties(type);
            foreach (var tuple in properties)
            {
                if (tuple.Item2.Name == propertyName) return tuple.Item2;
            }

            throw new Exception($"Property {propertyName} not found on {type.Name}");
        }

        private static IEnumerable<Tuple<int, PropertyInfo>> GetPublicProperties(Type type)
        {
            if (!PropertiesCache.TryGetValue(type, out var properties))
            {
                properties = type.GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                    .Select(p => new Tuple<int, PropertyInfo>(p.GetPropertyOrder(), p))
                    .OrderBy(t => t.Item1)
                    .ToArray();

                var duplicates = properties
                    .Where(t => t.Item2.HasPropertyOrder())
                    .GroupBy(t => t.Item1)
                    .Select(grp => new Tuple<int, int>(grp.Key, grp.Count()))
                    .Where(t => t.Item2 > 1)
                    .Aggregate("", (acc, p) => acc == "" ? acc + p.Item1 : acc + ", " + p.Item1);

                if (duplicates != "")
                {
                    throw new Exception($"Duplicate PropertyOrder values in {type.FullName}: {duplicates}");
                }

                PropertiesCache.TryAdd(type, properties);
            }

            return properties;
        }

        private enum SupportedTypes: byte
        {
            Byte = 1,
            Short = 2,
            UShort = 3,
            Int = 4,
            UInt = 5,
            Long = 6,
            ULong = 7,
            Float = 8,
            Double = 9,
            Decimal = 10,
            DateTime = 11,
            DateTimeOffset = 12,
            String = 13,
        }
    }

    [AttributeUsage(AttributeTargets.Property)]
    public class PropertyOrderAttribute : Attribute
    {
        public int Order { get; }

        public PropertyOrderAttribute(int order)
        {
            Order = order;
        }
    }

    internal static class PropertyInfoExtensions
    {
        public static int GetPropertyOrder(this PropertyInfo propInfo)
        {
            return propInfo.GetCustomAttribute<PropertyOrderAttribute>()?.Order ?? int.MaxValue;
        }

        public static bool HasPropertyOrder(this PropertyInfo propInfo)
        {
            return propInfo.GetCustomAttribute<PropertyOrderAttribute>() != null;
        }
    }
}
