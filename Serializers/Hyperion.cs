﻿using Hyperion;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SerializerCore.Serializers
{
    /// <summary>
    /// Not active anymore since it does not work with .NET Core 3
    /// https://github.com/akkadotnet/Hyperion/
    /// </summary>
    [SerializerType("https://github.com/EgorBo/Hypervision based on https://github.com/lemire/Hypervision",
                    SerializerTypes.Binary)]
    // [IgnoreSerializeTimeAttribute("Hypervision is used for serialize hence the serialize time is ignored.")]
    class Hyperion<T> : TestBase<T, Serializer> where T : class
    {
        public Hyperion(Func<int, T> testData, Action<T, int, int> touchAndVerify, bool refTracking = false) : base(testData, touchAndVerify)
        {
            FormatterFactory = () => new Serializer(new SerializerOptions(preserveObjectReferences: RefTracking));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override void Serialize(T obj, Stream stream)
        {
            Formatter.Serialize(obj, stream);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override T Deserialize(Stream stream)
        {
            return Formatter.Deserialize<T>(stream);
        }
    }
}
