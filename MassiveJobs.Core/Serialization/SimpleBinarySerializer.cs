using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace MassiveJobs.Core.Serialization
{
    using PropertyDesc = Tuple<int, string, Type, Func<object, object>, Action<object, object>>;

    public class SimpleBinarySerializer : BaseSerializer
    {
        private static readonly ConcurrentDictionary<Type, PropertyDesc[]> PropertiesCache
            = new ConcurrentDictionary<Type, PropertyDesc[]>();

        private static readonly ConcurrentDictionary<Type, Func<object>> ConstructorsCache
            = new ConcurrentDictionary<Type, Func<object>>();

        protected override object DeserializeEnvelope(ReadOnlySpan<byte> data, Type envType)
        {
            var env = GetDefaultConstructor(envType)();

            var nextIndex = 4; // skip header size field

            SetStringProperty(envType, env, nameof(SerializedEnvelope<object>.J), data, ref nextIndex);
            SetDateTimeProperty(envType, env, nameof(SerializedEnvelope<object>.At), data, ref nextIndex);
            SetIntProperty(envType, env, nameof(SerializedEnvelope<object>.R), data, ref nextIndex);
            SetIntProperty(envType, env, nameof(SerializedEnvelope<object>.T), data, ref nextIndex);
            SetStringProperty(envType, env, nameof(SerializedEnvelope<object>.G), data, ref nextIndex);

            var indexBeforePeriodicRunInfo = nextIndex;

            var periodicRunInfo = new PeriodicRunInfo();

            SetIntProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.RepeatSeconds), data,
                ref nextIndex);
            SetDateTimeProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.NextRunTime), data,
                ref nextIndex);
            SetDateTimeProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.LastRunTimeUtc), data,
                ref nextIndex);
            SetDateTimeProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.EndAtUtc), data,
                ref nextIndex);
            SetStringProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.CronExp), data,
                ref nextIndex);
            SetStringProperty(typeof(PeriodicRunInfo), periodicRunInfo, nameof(PeriodicRunInfo.TzId), data,
                ref nextIndex);

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

        protected override byte[] SerializeEnvelope(Type argsType, SerializedEnvelope<object> envelope)
        {
            var objectProps = new List<byte[]>();

            var size = 0;

            size += Serialize(envelope.J, objectProps);
            size += Serialize(envelope.At, objectProps);
            size += Serialize(envelope.R, objectProps);
            size += Serialize(envelope.T, objectProps);
            size += Serialize(envelope.G, objectProps);

            size += Serialize(envelope.P?.RepeatSeconds, objectProps);
            size += Serialize(envelope.P?.NextRunTime, objectProps);
            size += Serialize(envelope.P?.LastRunTimeUtc, objectProps);
            size += Serialize(envelope.P?.EndAtUtc, objectProps);
            size += Serialize(envelope.P?.CronExp, objectProps);
            size += Serialize(envelope.P?.TzId, objectProps);

            var headerSize = size + 4;

            size += SerializeObject(argsType, envelope.A, objectProps);

            var result = new byte[size + 4];

            BitConverter.GetBytes(headerSize).CopyTo(result, 0);

            var nextIndex = 4;
            foreach (var prop in objectProps)
            {
                prop.CopyTo(result, nextIndex);
                nextIndex += prop.Length;
            }

            return result;
        }

        private static int SerializeObject(Type type, object obj, ICollection<byte[]> objectProps)
        {
            var wrappedType = type.GetWrapperType();

            object wrappedObj;
            if (type.ShouldWrap())
            {
                wrappedObj = GetDefaultConstructor(wrappedType)();
                GetPublicProperty(wrappedType, nameof(PrimitiveWrapper<object>.Value)).SetValue(wrappedObj, obj);
            }
            else
            {
                wrappedObj = obj;
            }

            if (wrappedObj == null) return 0; // possible for VoidArgs

            var properties = GetPublicProperties(wrappedType);

            var hasOrderedProperties = false;

            var size = 0;

            foreach (var property in properties)
            {
                if (property.Item1 == int.MaxValue) break; // no more properties with PropertyOrder attribute

                var propertyType = property.Item3;

                hasOrderedProperties = true;

                if (propertyType == typeof(string))
                {
                    size += Serialize((string) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(byte) || propertyType == typeof(byte?))
                {
                    size += Serialize((byte?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(short) || propertyType == typeof(short?))
                {
                    size += Serialize((short?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(int) || propertyType == typeof(int?))
                {
                    size += Serialize((int?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(long) || propertyType == typeof(long?))
                {
                    size += Serialize((long?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(ushort) || propertyType == typeof(ushort?))
                {
                    size += Serialize((ushort?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(uint) || propertyType == typeof(uint?))
                {
                    size += Serialize((uint?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(ulong) || propertyType == typeof(ulong?))
                {
                    size += Serialize((ulong?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(double) || propertyType == typeof(double?))
                {
                    size += Serialize((double?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(float) || propertyType == typeof(float?))
                {
                    size += Serialize((float?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
                {
                    size += Serialize((DateTime?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(DateTimeOffset) || propertyType == typeof(DateTimeOffset?))
                {
                    size += Serialize((DateTimeOffset?) property.GetValue(wrappedObj), objectProps);
                }
                else if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
                {
                    size += Serialize((decimal?) property.GetValue(wrappedObj), objectProps);
                }
            }

            if (!hasOrderedProperties)
            {
                throw new Exception(
                    $"Cannot serialize object without PropertyOrder attributes ({wrappedType.FullName})");
            }

            return size;
        }

        private object DeserializeObject(Type type, in ReadOnlySpan<byte> data, ref int nextIndex)
        {
            if (type == typeof(VoidArgs)) return null;

            var wrapperType = type.GetWrapperType();
            var obj = GetDefaultConstructor(wrapperType)();

            var properties = GetPublicProperties(wrapperType);

            foreach (var tuple in properties)
            {
                if (tuple.Item1 == int.MaxValue) continue; // skip properties without PropertyOrder attribute
                var propertyName = tuple.Item2;
                var propertyType = tuple.Item3;

                if (propertyType == typeof(string))
                {
                    SetStringProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(byte) || propertyType == typeof(byte?))
                {
                    SetByteProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(short) || propertyType == typeof(short?))
                {
                    SetShortProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(int) || propertyType == typeof(int?))
                {
                    SetIntProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(long) || propertyType == typeof(long?))
                {
                    SetLongProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(ushort) || propertyType == typeof(ushort?))
                {
                    SetUShortProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(uint) || propertyType == typeof(uint?))
                {
                    SetUIntProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(ulong) || propertyType == typeof(ulong?))
                {
                    SetULongProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(double) || propertyType == typeof(double?))
                {
                    SetDoubleProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(float) || propertyType == typeof(float?))
                {
                    SetFloatProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
                {
                    SetDateTimeProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(DateTimeOffset) ||
                         propertyType == typeof(DateTimeOffset?))
                {
                    SetDateTimeOffsetProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
                else if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
                {
                    SetDecimalProperty(wrapperType, obj, propertyName, data, ref nextIndex);
                }
            }

            return type.ShouldWrap()
                ? GetPublicProperty(wrapperType, nameof(PrimitiveWrapper<object>.Value)).GetValue(obj)
                : obj;
        }

        private static int Serialize(double? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 10 : 2];
            bytes[0] = (byte) SupportedTypes.Double;

            if (value.HasValue)
            {
                bytes[1] = 8;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetDoubleProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(float? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 6 : 2];
            bytes[0] = (byte) SupportedTypes.Float;

            if (value.HasValue)
            {
                bytes[1] = 4;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetFloatProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(ulong? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 10 : 2];
            bytes[0] = (byte) SupportedTypes.ULong;

            if (value.HasValue)
            {
                bytes[1] = 8;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetULongProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(long? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 10 : 2];
            bytes[0] = (byte) SupportedTypes.Long;

            if (value.HasValue)
            {
                bytes[1] = 8;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetLongProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(uint? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 6 : 2];
            bytes[0] = (byte) SupportedTypes.UInt;

            if (value.HasValue)
            {
                bytes[1] = 4;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetUIntProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(int? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 6 : 2];
            bytes[0] = (byte) SupportedTypes.Int;

            if (value.HasValue)
            {
                bytes[1] = 4;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetIntProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(ushort? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 4 : 2];
            bytes[0] = (byte) SupportedTypes.UShort;

            if (value.HasValue)
            {
                bytes[1] = 2;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetUShortProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(short? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 4 : 2];
            bytes[0] = (byte) SupportedTypes.Short;

            if (value.HasValue)
            {
                bytes[1] = 2;
                BitConverter.GetBytes(value.Value).CopyTo(bytes, 2);
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetShortProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(byte? value, ICollection<byte[]> objectProps)
        {
            var bytes = new byte[value.HasValue ? 3 : 2];
            bytes[0] = (byte) SupportedTypes.Byte;

            if (value.HasValue)
            {
                bytes[1] = 1;
                bytes[2] = value.Value;
            }

            objectProps.Add(bytes);
            return bytes.Length;
        }

        private void SetByteProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(string value, ICollection<byte[]> objectProps)
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
            return bytes.Length;
        }

        private void SetStringProperty(Type type, object obj, string propName, ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(decimal? value, ICollection<byte[]> objectProps)
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
            return bytes.Length;
        }

        private void SetDecimalProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
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

        private static int Serialize(DateTimeOffset? value, ICollection<byte[]> objectProps)
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
            return bytes.Length;
        }

        private void SetDateTimeOffsetProperty(Type type, object obj, string propName, in ReadOnlySpan<byte> data,
            ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.DateTimeOffset)
                throw new Exception(
                    $"Expected DateTimeOffset ({(byte) SupportedTypes.DateTimeOffset}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var ticks = BitConverter.ToInt64(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var offset = TimeSpan.FromMinutes(BitConverter.ToInt16(data.Slice(nextIndex, 2).ToArray(), 0));
            nextIndex += 2;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, new DateTimeOffset(ticks, offset));
        }

        private static int Serialize(DateTime? value, ICollection<byte[]> objectProps)
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
            return bytes.Length;
        }

        private void SetDateTimeProperty(Type type, object obj, string propName, ReadOnlySpan<byte> data,
            ref int nextIndex)
        {
            if (data[nextIndex] != (byte) SupportedTypes.DateTime)
                throw new Exception(
                    $"Expected DateTime ({(byte) SupportedTypes.DateTime}) but found {data[nextIndex]}");

            nextIndex++;
            if (data[nextIndex++] == 0) return;

            var ticks = BitConverter.ToInt64(data.Slice(nextIndex, 8).ToArray(), 0);
            nextIndex += 8;

            var kind = (DateTimeKind) data[nextIndex];
            nextIndex++;

            var prop = GetPublicProperty(type, propName);
            prop.SetValue(obj, new DateTime(ticks, kind));
        }

        private static Func<object> GetDefaultConstructor(Type type)
        {
            if (ConstructorsCache.TryGetValue(type, out var ctor)) return ctor;

            var genericMethod = typeof(SimpleBinarySerializer)
                                    .GetMethod(nameof(CreateDefaultConstructor),
                                        BindingFlags.Static | BindingFlags.NonPublic)?
                                    .MakeGenericMethod(type)
                                ?? throw new Exception($"{nameof(CreateDefaultConstructor)} not found");

            ctor = (Func<object>) genericMethod.Invoke(null, null);

            ConstructorsCache.TryAdd(type, ctor);

            return ctor;
        }

        private static Func<object> CreateDefaultConstructor<T>()
        {
            var strongD = Expression.Lambda<Func<T>>(
                Expression.New(typeof(T)),
                Array.Empty<ParameterExpression>())
            .Compile();

            return () => strongD();
        }

        private static PropertyDesc GetPublicProperty(Type type, string propertyName)
        {
            var properties = GetPublicProperties(type);
            foreach (var tuple in properties)
            {
                if (tuple.Item2 == propertyName) return tuple;
            }

            throw new Exception($"Property {propertyName} not found on {type.Name}");
        }

        private static IEnumerable<PropertyDesc> GetPublicProperties(Type type)
        {
            if (!PropertiesCache.TryGetValue(type, out var properties))
            {
                properties = type
                    .GetProperties(BindingFlags.Public | BindingFlags.DeclaredOnly | BindingFlags.Instance)
                    .Select(p => new PropertyDesc(p.GetPropertyOrder(), p.Name, p.PropertyType, p.GetGetDelegate(), p.GetSetDelegate()))
                    .OrderBy(t => t.Item1)
                    .ToArray();

                var duplicates = properties
                    .Where(t => t.Item1 != int.MaxValue)
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

    internal class PrimitiveWrapper<T>
    {
        [PropertyOrder(0)]
        public T Value { get; set; }
    }

    internal static class PropertyInfoExtensions
    {
        public static void SetValue(this PropertyDesc propertyDesc, object obj, object value)
        {
            propertyDesc.Item5(obj, value);
        }

        public static object GetValue(this PropertyDesc propertyDesc, object obj)
        {
            return propertyDesc.Item4(obj);
        }

        /// <summary>
        /// Based on: https://blogs.msmvps.com/jonskeet/2008/08/09/making-reflection-fly-and-exploring-delegates/
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        public static Func<object, object> GetGetDelegate(this PropertyInfo propertyInfo)
        {
            var type = propertyInfo.DeclaringType;
            var method = propertyInfo.GetMethod;

            // First fetch the generic form
            MethodInfo genericHelper = typeof(PropertyInfoExtensions).GetMethod(nameof(GetMethodHelper),
                BindingFlags.Static | BindingFlags.NonPublic) ?? throw new Exception($"{nameof(GetMethodHelper)} not found");

            // Now supply the type arguments
            MethodInfo constructedHelper = genericHelper.MakeGenericMethod(type, method.ReturnType);

            // Now call it. The null argument is because it’s a static method.
            return (Func<object, object>) constructedHelper.Invoke(null, new object[] {method});
        }

        /// <summary>
        /// Based on: https://blogs.msmvps.com/jonskeet/2008/08/09/making-reflection-fly-and-exploring-delegates/
        /// </summary>
        /// <param name="propertyInfo"></param>
        /// <returns></returns>
        public static Action<object, object> GetSetDelegate(this PropertyInfo propertyInfo)
        {
            var type = propertyInfo.DeclaringType;
            var method = propertyInfo.SetMethod;

            // First fetch the generic form
            MethodInfo genericHelper = typeof(PropertyInfoExtensions).GetMethod(nameof(SetMethodHelper),
                BindingFlags.Static | BindingFlags.NonPublic) ?? throw new Exception($"{nameof(SetMethodHelper)} not found");

            // Now supply the type arguments
            MethodInfo constructedHelper = genericHelper.MakeGenericMethod(type, method.GetParameters()[0].ParameterType);

            // Now call it. The null argument is because it’s a static method.
            return (Action<object, object>) constructedHelper.Invoke(null, new object[] {method});
        }

        public static Type GetWrapperType(this Type type)
        {
            return WrapperTypes.TryGetValue(type, out var wrapperType) ? wrapperType : type;
        }

        public static bool ShouldWrap(this Type type)
        {
            return WrapperTypes.ContainsKey(type);
        }

        public static int GetPropertyOrder(this PropertyInfo propInfo)
        {
            return propInfo.GetCustomAttribute<PropertyOrderAttribute>()?.Order ?? int.MaxValue;
        }

        public static bool HasPropertyOrder(this PropertyInfo propInfo)
        {
            return propInfo.GetCustomAttribute<PropertyOrderAttribute>() != null;
        }

        private static Func<object, object> GetMethodHelper<TTarget, TReturn>(MethodInfo method)
            where TTarget : class
        {
            // Convert the slow MethodInfo into a fast, strongly typed, open delegate
            var func = (Func<TTarget, TReturn>) Delegate.CreateDelegate(typeof(Func<TTarget, TReturn>), method);

            // Now create a more weakly typed delegate which will call the strongly typed one
            return target => func((TTarget)target);
        }

        private static Action<object, object> SetMethodHelper<TTarget, TParam>(MethodInfo method)
            where TTarget : class
        {
            // Convert the slow MethodInfo into a fast, strongly typed, open delegate
            var action = (Action<TTarget, TParam>) Delegate.CreateDelegate(typeof(Action<TTarget, TParam>), method);

            // Now create a more weakly typed delegate which will call the strongly typed one
            return (target, param) => action((TTarget) target, (TParam) param);
        }

        private static readonly Dictionary<Type, Type> WrapperTypes = new Dictionary<Type, Type>
        {
            {typeof(byte), typeof(PrimitiveWrapper<byte>)},
            {typeof(byte?), typeof(PrimitiveWrapper<byte?>)},
            {typeof(short), typeof(PrimitiveWrapper<short>)},
            {typeof(short?), typeof(PrimitiveWrapper<short?>)},
            {typeof(ushort), typeof(PrimitiveWrapper<ushort>)},
            {typeof(ushort?), typeof(PrimitiveWrapper<ushort?>)},
            {typeof(int), typeof(PrimitiveWrapper<int>)},
            {typeof(int?), typeof(PrimitiveWrapper<int?>)},
            {typeof(uint), typeof(PrimitiveWrapper<uint>)},
            {typeof(uint?), typeof(PrimitiveWrapper<uint?>)},
            {typeof(long), typeof(PrimitiveWrapper<long>)},
            {typeof(long?), typeof(PrimitiveWrapper<long?>)},
            {typeof(ulong), typeof(PrimitiveWrapper<ulong>)},
            {typeof(ulong?), typeof(PrimitiveWrapper<ulong?>)},
            {typeof(decimal), typeof(PrimitiveWrapper<decimal>)},
            {typeof(decimal?), typeof(PrimitiveWrapper<decimal?>)},
            {typeof(float), typeof(PrimitiveWrapper<float>)},
            {typeof(float?), typeof(PrimitiveWrapper<float?>)},
            {typeof(double), typeof(PrimitiveWrapper<double>)},
            {typeof(double?), typeof(PrimitiveWrapper<double?>)},
            {typeof(DateTime), typeof(PrimitiveWrapper<DateTime>)},
            {typeof(DateTime?), typeof(PrimitiveWrapper<DateTime?>)},
            {typeof(DateTimeOffset), typeof(PrimitiveWrapper<DateTimeOffset>)},
            {typeof(DateTimeOffset?), typeof(PrimitiveWrapper<DateTimeOffset?>)},
            {typeof(string), typeof(PrimitiveWrapper<string>)},
        };
    }
}
