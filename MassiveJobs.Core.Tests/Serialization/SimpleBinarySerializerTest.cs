using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

using MassiveJobs.Core.Serialization;

namespace MassiveJobs.Core.Tests.Serialization
{
    [TestClass]
    public class SimpleBinarySerializerTest
    {
        [TestMethod]
        public void Deserialize_should_correctly_deserialize_non_nullable_properties()
        {
            var jobInfo = new JobInfo
            {
                Args = new MyArg
                {
                    ByteProp = 123,
                    ShortProp = -31234,
                    UShortProp = 65432,
                    IntProp = int.MinValue,
                    UIntProp = uint.MaxValue,
                    LongProp = long.MinValue + 1,
                    ULongProp = ulong.MaxValue - 1,
                    FloatProp = 1.23f,
                    DoubleProp = 2.34,
                    DecimalProp = decimal.MaxValue - 2,
                    DateTimeProp = new DateTime(1976, 10, 23, 0, 0, 0, DateTimeKind.Utc),
                    DateTimeOffsetProp = new DateTimeOffset(1976, 10, 23, 0, 0, 0, TimeSpan.FromHours(3)),
                    StringProp = "Hello world"
                },
                ArgsType = typeof(MyArg),
                JobType = typeof(SimpleBinarySerializer)
            };

            var serializer = new SimpleBinarySerializer();

            var typeProvider = new DefaultTypeProvider();

            var serialized = serializer.Serialize(jobInfo, typeProvider);
            var deserialized = serializer.Deserialize(serialized, typeProvider.TypeToTag(typeof(MyArg)), typeProvider);

            Assert.IsNotNull(deserialized.JobType);
            Assert.AreEqual(typeof(SimpleBinarySerializer), deserialized.JobType);

            Assert.IsNotNull(deserialized.Args);
            Assert.AreEqual(typeof(MyArg), deserialized.Args.GetType());

            var args = (MyArg) deserialized.Args;

            Assert.AreEqual(123, args.ByteProp);
            Assert.AreEqual(-31234, args.ShortProp);
            Assert.AreEqual(65432, args.UShortProp);
            Assert.AreEqual(int.MinValue, args.IntProp);
            Assert.AreEqual(uint.MaxValue, args.UIntProp);
            Assert.AreEqual(long.MinValue + 1, args.LongProp);
            Assert.AreEqual(ulong.MaxValue - 1, args.ULongProp);
            Assert.AreEqual(1.23f, args.FloatProp);
            Assert.AreEqual(2.34, args.DoubleProp);
            Assert.AreEqual(decimal.MaxValue - 2, args.DecimalProp);
            Assert.AreEqual(new DateTime(1976, 10, 23, 0, 0, 0, DateTimeKind.Utc), args.DateTimeProp);
            Assert.AreEqual(DateTimeKind.Utc, args.DateTimeProp.Kind);
            Assert.AreEqual(new DateTimeOffset(1976, 10, 23, 0, 0, 0, TimeSpan.FromHours(3)), args.DateTimeOffsetProp);
            Assert.AreEqual("Hello world", args.StringProp);
        }

        [TestMethod]
        public void Deserialize_should_correctly_deserialize_nullable_properties_A()
        {
            var jobInfo = new JobInfo
            {
                Args = new MyArg2
                {
                    ByteProp = 123,
                    //ShortProp = -31234,
                    UShortProp = 65432,
                    //IntProp = int.MinValue,
                    UIntProp = uint.MaxValue,
                    //LongProp = long.MinValue + 1,
                    ULongProp = ulong.MaxValue - 1,
                    //FloatProp = 1.23f,
                    DoubleProp = 2.34,
                    //DecimalProp = decimal.MaxValue - 2,
                    DateTimeProp = new DateTime(1976, 10, 23, 0, 0, 0, DateTimeKind.Utc),
                    //DateTimeOffsetProp = new DateTimeOffset(1976, 10, 23, 0, 0, 0, TimeSpan.FromHours(3)),
                    StringProp = "Hello world"
                },
                ArgsType = typeof(MyArg2),
                JobType = typeof(SimpleBinarySerializer)
            };

            var serializer = new SimpleBinarySerializer();

            var typeProvider = new DefaultTypeProvider();

            var serialized = serializer.Serialize(jobInfo, typeProvider);
            var deserialized = serializer.Deserialize(serialized, typeProvider.TypeToTag(typeof(MyArg2)), typeProvider);

            Assert.IsNotNull(deserialized.JobType);
            Assert.AreEqual(typeof(SimpleBinarySerializer), deserialized.JobType);

            Assert.IsNotNull(deserialized.Args);
            Assert.AreEqual(typeof(MyArg2), deserialized.Args.GetType());

            var args = (MyArg2) deserialized.Args;

            Assert.AreEqual((byte?)123, args.ByteProp);
            Assert.IsNull(args.ShortProp);
            Assert.AreEqual((ushort?)65432, args.UShortProp);
            Assert.IsNull(args.IntProp);
            Assert.AreEqual(uint.MaxValue, args.UIntProp);
            Assert.IsNull(args.LongProp);
            Assert.AreEqual(ulong.MaxValue - 1, args.ULongProp);
            Assert.IsNull(args.FloatProp);
            Assert.AreEqual(2.34, args.DoubleProp);
            Assert.IsNull(args.DecimalProp);
            Assert.AreEqual(new DateTime(1976, 10, 23, 0, 0, 0, DateTimeKind.Utc), args.DateTimeProp);
            Assert.AreEqual(DateTimeKind.Utc, args.DateTimeProp?.Kind);
            Assert.IsNull(args.DateTimeOffsetProp);
            Assert.AreEqual("Hello world", args.StringProp);
        }

        [TestMethod]
        public void Deserialize_should_correctly_deserialize_nullable_properties_B()
        {
            var jobInfo = new JobInfo
            {
                Args = new MyArg2
                {
                    //ByteProp = 123,
                    ShortProp = -31234,
                    //UShortProp = 65432,
                    IntProp = int.MinValue,
                    //UIntProp = uint.MaxValue,
                    LongProp = long.MinValue + 1,
                    //ULongProp = ulong.MaxValue - 1,
                    FloatProp = 1.23f,
                    //DoubleProp = 2.34,
                    DecimalProp = decimal.MaxValue - 2,
                    //DateTimeProp = new DateTime(1976, 10, 23, 0, 0, 0, DateTimeKind.Utc),
                    DateTimeOffsetProp = new DateTimeOffset(1976, 10, 23, 0, 0, 0, TimeSpan.FromHours(3)),
                    //StringProp = "Hello world"
                },
                ArgsType = typeof(MyArg2),
                JobType = typeof(SimpleBinarySerializer)
            };

            var serializer = new SimpleBinarySerializer();

            var typeProvider = new DefaultTypeProvider();

            var serialized = serializer.Serialize(jobInfo, typeProvider);
            var deserialized = serializer.Deserialize(serialized, typeProvider.TypeToTag(typeof(MyArg2)), typeProvider);

            Assert.IsNotNull(deserialized.JobType);
            Assert.AreEqual(typeof(SimpleBinarySerializer), deserialized.JobType);

            Assert.IsNotNull(deserialized.Args);
            Assert.AreEqual(typeof(MyArg2), deserialized.Args.GetType());

            var args = (MyArg2) deserialized.Args;

            Assert.IsNull(args.ByteProp);
            Assert.AreEqual((short?)-31234, args.ShortProp);
            Assert.IsNull(args.UShortProp);
            Assert.AreEqual(int.MinValue, args.IntProp);
            Assert.IsNull(args.UIntProp);
            Assert.AreEqual(long.MinValue + 1, args.LongProp);
            Assert.IsNull(args.ULongProp);
            Assert.AreEqual(1.23f, args.FloatProp);
            Assert.IsNull(args.DoubleProp);
            Assert.AreEqual(decimal.MaxValue - 2, args.DecimalProp);
            Assert.IsNull(args.DateTimeProp);
            Assert.AreEqual(new DateTimeOffset(1976, 10, 23, 0, 0, 0, TimeSpan.FromHours(3)), args.DateTimeOffsetProp);
            Assert.IsNull(args.StringProp);
        }

        private class MyArg
        {
            [PropertyOrder(0)]
            public byte ByteProp { get; set; }

            [PropertyOrder(1)]
            public short ShortProp { get; set; }

            [PropertyOrder(2)]
            public ushort UShortProp { get; set; }

            [PropertyOrder(3)]
            public int IntProp { get; set; }

            [PropertyOrder(4)]
            public uint UIntProp { get; set; }

            [PropertyOrder(5)]
            public long LongProp { get; set; }

            [PropertyOrder(6)]
            public ulong ULongProp { get; set; }

            [PropertyOrder(7)]
            public float FloatProp { get; set; }

            [PropertyOrder(8)]
            public decimal DecimalProp { get; set; }

            [PropertyOrder(9)]
            public double DoubleProp { get; set; }

            [PropertyOrder(10)]
            public DateTime DateTimeProp { get; set; }

            [PropertyOrder(11)]
            public DateTimeOffset DateTimeOffsetProp { get; set; }

            [PropertyOrder(12)]
            public string StringProp { get; set; }
        }

        private class MyArg2
        {
            [PropertyOrder(0)]
            public byte? ByteProp { get; set; }

            [PropertyOrder(1)]
            public short? ShortProp { get; set; }

            [PropertyOrder(2)]
            public ushort? UShortProp { get; set; }

            [PropertyOrder(3)]
            public int? IntProp { get; set; }

            [PropertyOrder(4)]
            public uint? UIntProp { get; set; }

            [PropertyOrder(5)]
            public long? LongProp { get; set; }

            [PropertyOrder(6)]
            public ulong? ULongProp { get; set; }

            [PropertyOrder(7)]
            public float? FloatProp { get; set; }

            [PropertyOrder(8)]
            public decimal? DecimalProp { get; set; }

            [PropertyOrder(9)]
            public double? DoubleProp { get; set; }

            [PropertyOrder(10)]
            public DateTime? DateTimeProp { get; set; }

            [PropertyOrder(11)]
            public DateTimeOffset? DateTimeOffsetProp { get; set; }

            [PropertyOrder(12)]
            public string StringProp { get; set; }
        }
    }
}
