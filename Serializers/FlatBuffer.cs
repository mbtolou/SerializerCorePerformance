﻿using Google.FlatBuffers;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SerializerCore.Serializers
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [SerializerType("https://google.github.io/flatbuffers/",
                    SerializerTypes.Binary | SerializerTypes.SupportsVersioning)]
    class FlatBuffer<T> : TestBase<BookShelfFlat, ByteBuffer>
    {
        public FlatBuffer(Func<int, BookShelfFlat> testData, Action<BookShelfFlat, int, int> touchAndVerify) : base(testData, touchAndVerify)
        {
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override void Serialize(BookShelfFlat obj, Stream stream)
        {
            stream.Write(obj.ByteBuffer.ToFullArray(), obj.ByteBuffer.Position, obj.ByteBuffer.Length - obj.ByteBuffer.Position);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override BookShelfFlat Deserialize(Stream stream)
        {
            MemoryStream mem = new MemoryStream();
            // Since flatbuffers do not support memory streams we have to copy here
            stream.CopyTo(mem);
            byte[] data = mem.ToArray();
            var bookShelf = BookShelfFlat.GetRootAsBookShelfFlat(new ByteBuffer(data));
            return bookShelf;
        }
    }
}
