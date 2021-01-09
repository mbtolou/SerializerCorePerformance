using Wire;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace SerializerCore.Serializers
{
    /// <summary>
    /// Not active anymore since it does not work with .NET Core 3
    /// https://github.com/akkadotnet/Hyperion/
    /// </summary>
    [SerializerType("",SerializerTypes.Binary)]
    // [IgnoreSerializeTimeAttribute("Wire is used for serialize hence the serialize time is ignored.")]
    class Wire<T> : TestBase<T, Serializer> where T : class
    {
        public Wire(Func<int, T> testData, Action<T, int, int> touchAndVerify, bool refTracking = false) : base(testData, touchAndVerify)
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
