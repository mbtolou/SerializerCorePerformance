using Apex.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace SerializerCore.Serializers
{
    /// <summary>
    ///
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [SerializerType("https://github.com/dbolin/Apex.Serialization",
                    SerializerTypes.Binary)]
    public sealed class ApexSerializer<T> : TestBase<T, IBinary> where T : class
    {
        private readonly IBinary binary = Binary.Create(new Settings { UseSerializedVersionId = false }.MarkSerializable(x => true));
        private readonly IBinary binaryGraph = Binary.Create(new Settings { SerializationMode = Mode.Graph, UseSerializedVersionId = false }.MarkSerializable(x => true));
        private readonly IBinary binaryWithVersionIds = Binary.Create(new Settings { UseSerializedVersionId = true }.MarkSerializable(x => true));
        private readonly IBinary binaryWithoutFlatten = Binary.Create(new Settings { UseSerializedVersionId = false, FlattenClassHierarchy = false }.MarkSerializable(x => true));

        public ApexSerializer(Func<int, T> testData, Action<T, int, int> touchAndVerify) : base(testData, touchAndVerify)
        {
            // FormatterFactory = () => Binary.Create(new Settings { SerializationMode = Mode.Graph, UseSerializedVersionId = false }.MarkSerializable(x => true));
            FormatterFactory = () => binaryWithoutFlatten;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override void Serialize(T obj, Stream stream)
        {
            Formatter.Write(obj, stream);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        protected override T Deserialize(Stream stream)
        {
            return Formatter.Read<T>(stream);
        }
    }
}