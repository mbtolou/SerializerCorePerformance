﻿using System;
using System.Collections.Generic;
using SerializerCore.TypesToSerialize;
using SerializerCore.Serializers;
using System.Linq;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using Google.FlatBuffers;
using System.Text;

namespace SerializerCore
{
    class Program
    {
        static string Help = "SerializerCore is a serializer performance testing framework to evaluate and compare different serializers for .NET by Alois Kraus" + Environment.NewLine +
                             "SerializerCore [-Runs dd] -test [serialize, deserialize, combined, firstCall] [-reftracking] [-maxobj dd]" + Environment.NewLine +
                             " -N 1,2,10000    Set the number of objects to de/serialize which is repeated -Runs times to get stable results." + Environment.NewLine +
                             " -Runs           Default is 5. The result is averaged where the first run is excluded from the average" + Environment.NewLine +
                             " -test xx        xx can be serialize, deserialize, combined or firstcall to test a scenario for many different serializers" + Environment.NewLine +
                             " -reftracking    If set a list with many identical references is serialized." + Environment.NewLine +
                             " -serializer xxx Execute the test only for a specific serializer with the name xxx. Use , to separate multiple filters. Prefix name with # to force a full string match instead of a substring match." + Environment.NewLine +
                             " -list           List all registered serializers" + Environment.NewLine +
                             " -BookDataSize d Optional byte array payload in bytes to check how good the serializer can deal with large blob payloads (e.g. images)." + Environment.NewLine +
                             " -Verify         Verify deserialized data if all contents could be read." + Environment.NewLine +
                             "                 To execute deserialize you must first have called the serialize to generate serialized test data on disk to be read during deserialize" + Environment.NewLine +
                             "Examples" + Environment.NewLine +
                             "Compare protobuf against MessagePackSharp for serialize and deserialize performance" + Environment.NewLine +
                             " SerializerCore -Runs 1 -test combined -serializer protobuf,MessagePackSharp" + Environment.NewLine +
                             "Test how serializers perform when reference tracking is enabled. Currently that are BinaryFormatter,Protobuf_net and DataContract" + Environment.NewLine +
                             " Although Json.NET claim to have but it is not completely working." + Environment.NewLine +
                             " SerializerCore -Runs 1 -test combined -reftracking" + Environment.NewLine +
                             "Test SimdJsonSharpSerializer serializer with 3 million objects for serialize and deserialize." + Environment.NewLine +
                             " SerializerCore -test combined -N 3000000 -serializer #SimdJsonSharpSerializer" + Environment.NewLine;


        private Queue<string> Args;

        List<ISerializeDeserializeTester> SerializersToTest;
        List<ISerializeDeserializeTester> StartupSerializersToTest;
        List<ISerializeDeserializeTester> SerializersObjectReferencesToTest;

        int Runs = 5;
        public int BookDataSize = 0;
        bool IsNGenWarn = true;
        bool VerifyAndTouch = false;
        bool TestReferenceTracking = false;
        int[] NObjectsToDeSerialize = null;
        string[] SerializerFilters = new string[] { "" };

        const int StartupSerializerCount = 4;

        public Program(string[] args)
        {
            Args = new Queue<string>(args);
        }

        private void CreateSerializersToTest()
        {
            // used when on command line -serializer is used
            Func<ISerializeDeserializeTester, bool> filter = (s) =>
            {
                return SerializerFilters.Any(filterStr =>
                {
                    string simpleType = GetSimpleTypeName(s.GetType());

                    if (filterStr.StartsWith("#")) // Exact type match needed
                    {
                        return String.Equals(filterStr.Substring(1), simpleType, StringComparison.OrdinalIgnoreCase);
                    }
                    else
                    {
                        return simpleType.IndexOf(filterStr, StringComparison.OrdinalIgnoreCase) == 0;
                    }
                });
            };

            SerializersToTest = new List<ISerializeDeserializeTester>
            {

                new NopSerializer<BookShelf>(Data, null),
                // Apex Serializer works only on .NET Core 3.0! .NET Core 3.1 and 5.0 break with some internal FileNotFoundExeptions which breaks serialization/deserialization
                // InvalidOperationException: Type SerializerCore.TypesToSerialize.BookShelf was encountered during deserialization but was not marked as serializable. Use Binary.MarkSerializable before creating any serializers if this type is intended to be serialized.
                new ApexSerializer<BookShelf>(Data, TouchAndVerify),

                new Ceras<BookShelf>(Data, TouchAndVerify),


                new SystemTextJson<BookShelf>(Data, TouchAndVerify),
                // .NET Core 3/3.1 do not support public fields so we needed to resort back to public properties
                new SystemTextJson<NetCorePropertyBookShelf>(DataNetCore, TouchAndVerify),
                new SimdJsonSharpSerializer<BookShelf>(Data, TouchAndVerify),
                new SpanJson<BookShelf>(Data, TouchAndVerify),
                new Utf8JsonSerializer<BookShelf>(Data, TouchAndVerify),
                new MessagePackSharp<BookShelf>(Data, TouchAndVerify),
                new GroBuf<BookShelf>(Data, TouchAndVerify),
                new FlatBuffer<BookShelfFlat>(DataFlat, TouchFlat),
                // Hyperion does not work on .NET Core 3.0  https://github.com/akkadotnet/Hyperion/issues/111
                new Hyperion<BookShelf>(Data, TouchAndVerify),
                // https://github.com/rogeralsing/Wire/issues/146
                new Wire<BookShelf>(Data, TouchAndVerify),
                new Bois<BookShelf>(Data, TouchAndVerify),
                new Bois_LZ4<BookShelf>(Data, TouchAndVerify),
                new Jil<BookShelf>(Data, TouchAndVerify),
                new Protobuf_net<BookShelf>(Data, TouchAndVerify),
                new SlimSerializer<BookShelf>(Data, TouchAndVerify),


                // new ServiceStack<BookShelf>(Data, TouchAndVerify),
                new FastJson<BookShelf>(Data, TouchAndVerify),
                //new DataContractIndented<BookShelf>(Data, TouchBookShelf),
                new DataContractBinaryXml<BookShelf>(Data, TouchAndVerify),
                new DataContract<BookShelf>(Data, TouchAndVerify),
                new XmlSerializer<BookShelf>(Data, TouchAndVerify),
                new JsonNet<BookShelf>(Data, TouchAndVerify),
                // new MsgPack_Cli<BookShelf>(Data, TouchAndVerify),
                new BinaryFormatter<BookShelf>(Data, TouchAndVerify),
            };

            // if on command line a filter was specified filter the serializers to test according to filter by type name 
            SerializersToTest = SerializersToTest.Where(filter).ToList();


            StartupSerializersToTest = new List<ISerializeDeserializeTester>
            {
                new Ceras<BookShelf>(Data, null),
                new Ceras<BookShelf1>(Data1, null),
                new Ceras<BookShelf2>(Data2, null),
                new Ceras<LargeBookShelf>(DataLarge, null),

                new ApexSerializer<BookShelf>(Data, null),
                new ApexSerializer<BookShelf1>(Data1, null),
                new ApexSerializer<BookShelf2>(Data2, null),
                new ApexSerializer<LargeBookShelf>(DataLarge, null),


                new Bois<BookShelf>(Data, null),
                new Bois<BookShelf1>(Data1, null),
                new Bois<BookShelf2>(Data2, null),
                new Bois<LargeBookShelf>(DataLarge, null),

                new Bois_LZ4<BookShelf>(Data, null),
                new Bois_LZ4<BookShelf1>(Data1, null),
                new Bois_LZ4<BookShelf2>(Data2, null),
                new Bois_LZ4<LargeBookShelf>(DataLarge, null),

                new GroBuf<BookShelf>(Data, null),
                new GroBuf<BookShelf1>(Data1, null),
                new GroBuf<BookShelf2>(Data2, null),
                new GroBuf<LargeBookShelf>(DataLarge, null),


                // Hyperion does not work on .NET Core 3.0  https://github.com/akkadotnet/Hyperion/issues/111
                new Hyperion<BookShelf>(Data, null),
                new Hyperion<BookShelf1>(Data1, null),
                new Hyperion<BookShelf2>(Data2, null),
                new Hyperion<LargeBookShelf>(DataLarge, null),

                // Wire crashes on Deserializaiton on .NET Core 3.0 https://github.com/rogeralsing/Wire/issues/146
                new Wire<BookShelf>(Data, null),
                new Wire<BookShelf1>(Data1, null),
                new Wire<BookShelf2>(Data2, null),
                new Wire<LargeBookShelf>(DataLarge, null),
                new SlimSerializer<BookShelf>(Data, null),
                new SlimSerializer<BookShelf1>(Data1, null),
                new SlimSerializer<BookShelf2>(Data2, null),
                new SlimSerializer<LargeBookShelf>(DataLarge, null),

                new BinaryFormatter<BookShelf>(Data, null),
                new BinaryFormatter<BookShelf1>(Data1, null),
                new BinaryFormatter<BookShelf2>(Data2, null),
                new BinaryFormatter<LargeBookShelf>(DataLarge, null),

                new FastJson<BookShelf>(Data, null),
                new FastJson<BookShelf1>(Data1, null),
                new FastJson<BookShelf2>(Data2, null),
                new FastJson<LargeBookShelf>(DataLarge, null),

                new Jil<BookShelf>(Data, null),
                new Jil<BookShelf1>(Data1, null),
                new Jil<BookShelf2>(Data2, null),
                new Jil<LargeBookShelf>(DataLarge, null),

                new DataContract<BookShelf>(Data, null),
                new DataContract<BookShelf1>(Data1, null),
                new DataContract<BookShelf2>(Data2, null),
                new DataContract<LargeBookShelf>(DataLarge, null),

                new XmlSerializer<BookShelf>(Data, null),
                new XmlSerializer<BookShelf1>(Data1, null),
                new XmlSerializer<BookShelf2>(Data2, null),
                new XmlSerializer<LargeBookShelf>(DataLarge, null),

                new JsonNet<BookShelf>(Data, null),
                new JsonNet<BookShelf1>(Data1, null),
                new JsonNet<BookShelf2>(Data2, null),
                new JsonNet<LargeBookShelf>(DataLarge, null),

                new Protobuf_net<BookShelf>(Data, null),
                new Protobuf_net<BookShelf1>(Data1, null),
                new Protobuf_net<BookShelf2>(Data2, null),
                new Protobuf_net<LargeBookShelf>(DataLarge, null),

                new MessagePackSharp<BookShelf>(Data, null),
                new MessagePackSharp<BookShelf1>(Data1, null),
                new MessagePackSharp<BookShelf2>(Data2, null),
                new MessagePackSharp<LargeBookShelf>(DataLarge, null),

                // new MsgPack_Cli<BookShelf>(Data, null),
                // new MsgPack_Cli<BookShelf1>(Data1, null),
                // new MsgPack_Cli<BookShelf2>(Data2, null),
                // new MsgPack_Cli<LargeBookShelf>(DataLarge, null),

	            new Utf8JsonSerializer<BookShelf>(Data, null),
                new Utf8JsonSerializer<BookShelf1>(Data1, null),
                new Utf8JsonSerializer<BookShelf2>(Data2, null),
                new Utf8JsonSerializer<LargeBookShelf>(DataLarge, null),
            };

            StartupSerializersToTest = StartupSerializersToTest.Where(filter).ToList();

            SerializersObjectReferencesToTest = new List<ISerializeDeserializeTester>
            {
                 // Apex Serializer works only on .NET Core 3.0 3.1 and 5.0 break with some internal FileNotFoundExeptions which apparently break serialization/deserialization
                // InvalidOperationException: Type SerializerCore.TypesToSerialize.BookShelf was encountered during deserialization but was not marked as serializable. Use Binary.MarkSerializable before creating any serializers if this type is intended to be serialized.
                new ApexSerializer<ReferenceBookShelf>(DataReferenceBookShelf, null),
                // FlatBuffer does not support object references
                new MessagePackSharp<ReferenceBookShelf>(DataReferenceBookShelf, null),
                new GroBuf<ReferenceBookShelf>(DataReferenceBookShelf, null),

                // Hyperion does not work on .NET Core 3.0  https://github.com/akkadotnet/Hyperion/issues/111
                new Hyperion<ReferenceBookShelf>(DataReferenceBookShelf, null, refTracking: TestReferenceTracking),
                // Wire crashes on Deserialization on .NET Core 3.0 https://github.com/rogeralsing/Wire/issues/146
                new Wire<ReferenceBookShelf>(DataReferenceBookShelf, null, refTracking: TestReferenceTracking),
                new Bois<ReferenceBookShelf>(DataReferenceBookShelf, null),
                new Bois_LZ4<ReferenceBookShelf>(DataReferenceBookShelf, null),
                //new Jil<ReferenceBookShelf>(DataReferenceBookShelf, null),  // Jil does not support a dictionary with DateTime as key
                new Protobuf_net<ReferenceBookShelf>(DataReferenceBookShelf, null),  // Reference tracking in protobuf can be enabled via attributes in the types!
                new SlimSerializer<ReferenceBookShelf>(DataReferenceBookShelf, null),

                new FastJson<ReferenceBookShelf>(DataReferenceBookShelf, null), // DateTime strings are not round trip capable because FastJSON keeps the time only until ms but the rest is not serialized!
                new DataContractIndented<ReferenceBookShelf>(DataReferenceBookShelf, null, refTracking:TestReferenceTracking),
                new DataContractBinaryXml<ReferenceBookShelf>(DataReferenceBookShelf, null, refTracking:TestReferenceTracking),
                new DataContract<ReferenceBookShelf>(DataReferenceBookShelf, null, refTracking:TestReferenceTracking),
                new XmlSerializer<ReferenceBookShelf>(DataReferenceBookShelf, null),  // XmlSerializer does not support Dictionaries https://stackoverflow.com/questions/2911514/why-doesnt-xmlserializer-support-dictionary
                new JsonNet<ReferenceBookShelf>(DataReferenceBookShelf, null, refTracking:TestReferenceTracking),
                new BinaryFormatter<ReferenceBookShelf>(DataReferenceBookShelf, null),
                new Utf8JsonSerializer<ReferenceBookShelf>(DataReferenceBookShelf, null)
            };

            SerializersObjectReferencesToTest = SerializersObjectReferencesToTest.Where(filter).ToList();
        }

        static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            try
            {
                new Program(args).Run();
            }
            catch (Exception ex)
            {
                PrintHelp(ex);
            }
        }

        static void PrintHelp(Exception ex = null)
        {
            Console.WriteLine(Help);
            if (ex != null)
            {
                Console.WriteLine($"{ex.GetType().Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Return only the non generic type name
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        static string GetSimpleTypeName(Type type)
        {
            return type.Name.TrimEnd('1').TrimEnd('`');
        }

        private void Run()
        {
            string testCase = null;

            while (Args.Count > 0)
            {
                string curArg = Args.Dequeue();
                string lowerArg = curArg.ToLower();

                switch (lowerArg)
                {
                    case "-runs":
                        string n = NextLower();
                        Runs = int.Parse(n);
                        break;
                    case "-reftracking":
                        TestReferenceTracking = true;
                        break;
                    case "-serializer":
                        string serializers = NextLower() ?? "";
                        SerializerFilters = serializers.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        break;
                    case "-list":
                        CreateSerializersToTest();
                        Console.WriteLine("Registered Serializers");
                        foreach (var test in SerializersToTest)
                        {
                            Console.WriteLine($"{GetSimpleTypeName(test.GetType()) }");
                        }
                        return;
                    case "-verify":
                        VerifyAndTouch = true;
                        break;
                    case "-n":
                        NObjectsToDeSerialize = NextLower()?.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)?.Select(int.Parse).ToArray();
                        if (NObjectsToDeSerialize == null)
                        {
                            throw new NotSupportedException("Missing object count after -N option");
                        }
                        break;
                    case "-test":
                        testCase = NextLower();
                        break;
                    case "-bookdatasize":
                        BookDataSize = int.Parse(NextLower());
                        break;
                    case "-nongenwarn":
                        IsNGenWarn = false;
                        break;
                    default:
                        throw new NotSupportedException($"Argument {curArg} is not valid");
                }
            }

            PreChecks();

            CreateSerializersToTest();

            // Set optional payload size to be able to generate data files with the payload size in the file name
            foreach (var x in SerializersToTest.Concat(StartupSerializersToTest).Concat(SerializersObjectReferencesToTest))
            {
                x.OptionalBytePayloadSize = BookDataSize;
            }

            if (testCase?.Equals("serialize") == true)
            {
                Serialize();
            }
            else if (testCase?.Equals("deserialize") == true)
            {
                Deserialize();
            }
            else if (testCase?.Equals("firstcall") == true)
            {
                FirstCall();
            }
            else if (testCase?.Equals("combined") == true)
            {
                Combined();
            }
            else
            {
                throw new NotSupportedException($"Error: Arg {testCase} is not a valid option!");
            }
        }

        private void PreChecks()
        {
            // Since XmlSerializer tries to load a pregenerated serialization assembly which will on first access read the GAC contents from the registry and cache them
            // we do this before to measure not the overhead of an failed assembly load try but only the overhead of the code gen itself.
            try
            {
                // Assembly.Load("notExistingToTriggerGACPrefetch, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            }
            catch (FileNotFoundException)
            {
            }

            if (IsNGenWarn && !IsNGenned())
            {
                Console.WriteLine("Warning: Not NGenned! Results may not be accurate in your target deployment.");
                Console.WriteLine(@"Please execute Ngen.cmd install to Ngen all dlls.");
                Console.WriteLine(@"To uninstall call Ngen.cmd uninstall");
                Console.WriteLine(@"The script will take care that the assemblies are really uninstalled.");
            }

            WarnIfDebug();
        }


        /// <summary>
        /// Return right set of serializers depending on requested test
        /// </summary>
        private List<ISerializeDeserializeTester> TestSerializers
        {
            get { return TestReferenceTracking ? SerializersObjectReferencesToTest : SerializersToTest; }
        }


        private void Deserialize()
        {
            var tester = new Test_O_N_Behavior(TestSerializers);
            tester.TestDeserialize(NObjectsToDeSerialize, nRuns: Runs);
        }

        private void Serialize()
        {
            var tester = new Test_O_N_Behavior(TestSerializers);
            tester.TestSerialize(NObjectsToDeSerialize, nRuns: Runs);
        }

        private void Combined()
        {
            var tester = new Test_O_N_Behavior(TestSerializers);
            tester.TestCombined(NObjectsToDeSerialize, nRuns: Runs);
        }


        /// <summary>
        /// To measure things accurately we spawn a new process for every serializer and then create for 4 different types a serializer where some data is serialized
        /// </summary>
        private void FirstCall()
        {
            if (StartupSerializersToTest.Count == StartupSerializerCount) // we always create 4 serializer with different types for startup tests
            {
                var tester = new Test_O_N_Behavior(StartupSerializersToTest);
                tester.TestSerialize(new int[] { 1 }, nRuns: 1);
            }
            else
            {
                for (int i = 0; i < StartupSerializersToTest.Count; i += StartupSerializerCount)
                {
                    var serializer = StartupSerializersToTest[i];
                    // Spawn new process for each serializer to measure each serializer overhead in isolation 
                    var startArgs = new ProcessStartInfo(Assembly.GetEntryAssembly().Location.Replace(".dll", ".exe"), String.Join(" ", Environment.GetCommandLineArgs().Skip(1)) + $" -serializer #{GetSimpleTypeName(serializer.GetType())}")
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                    };
                    Process proc = Process.Start(startArgs);
                    // trim newline of newly started process
                    string output = proc.StandardOutput.ReadToEnd().Trim(Environment.NewLine.ToCharArray());
                    if (i > 0) // trim header since we need it only once
                    {
                        output = output.Substring(output.IndexOf('\n') + 1);
                    }
                    Console.WriteLine(output);
                    proc.WaitForExit();
                }
            }
        }

        string NextLower()
        {
            if (Args.Count > 0)
            {
                return Args.Dequeue().ToLower();
            }

            return null;
        }

        private bool IsNGenned()
        {
            bool lret = false;
            foreach (ProcessModule module in Process.GetCurrentProcess().Modules)
            {
                string file = module.ModuleName;
                if (file == "SerializerCore.ni.exe" || file == "coreclr.dll")
                {
                    lret = true;
                }
            }

            return lret;
        }

        [Conditional("DEBUG")]
        void WarnIfDebug()
        {
            Console.WriteLine();
            Console.WriteLine("DEBUG build detected. Please recompile in Release mode before publishing your data.");
        }


        BookShelf Data(int nToCreate)
        {
            var lret = new BookShelf("private member value")
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new Book
                {
                    Id = i,
                    Title = $"Book {i}",
                    BookData = CreateAndFillByteBuffer(),
                }
                ).ToList()
            };
            return lret;
        }

        NetCorePropertyBookShelf DataNetCore(int nToCreate)
        {
            var lret = new NetCorePropertyBookShelf("private member value")
            {
                Books = Enumerable.Range(1, nToCreate).Select(i =>
                new NetCoreBook
                {
                    Id = i,
                    Title = $"Book {i}",
                    BookData = CreateAndFillByteBuffer(),
                }).ToList()
            };
            return lret;
        }

        BookShelfFlat DataFlat(int nToCreate)
        {
            var builder = new FlatBufferBuilder(1024);

            Offset<BookFlat>[] books = new Offset<BookFlat>[nToCreate];

            for (int i = 1; i <= nToCreate; i++)
            {
                var title = builder.CreateString($"Book {i}");
                builder.StartVector(1, BookDataSize, 0);
                byte[] bytes = CreateAndFillByteBuffer();
                if (bytes.Length > 0)
                {
                    builder.Put(bytes);
                }
                VectorOffset bookbyteArrayOffset = builder.EndVector();
                var bookOffset = BookFlat.CreateBookFlat(builder, title, i, bookbyteArrayOffset);
                books[i - 1] = bookOffset;
            }

            var secretOffset = builder.CreateString("private member value");
            VectorOffset booksVector = builder.CreateVectorOfTables<BookFlat>(books);
            var lret = BookShelfFlat.CreateBookShelfFlat(builder, booksVector, secretOffset);
            builder.Finish(lret.Value);
            var bookshelf = BookShelfFlat.GetRootAsBookShelfFlat(builder.DataBuffer);
            return bookshelf;
        }

        byte[] CreateAndFillByteBuffer()
        {
            byte[] optionalPayload = new byte[BookDataSize];

            for (int j = 0; j < optionalPayload.Length; j++)
            {
                optionalPayload[j] = (byte)(j % 26 + 'a');
            }

            return optionalPayload;
        }

        void TouchAndVerify(BookShelf data, int nExpectedBooks, int optionalPayloadDataSize)
        {
            if (!VerifyAndTouch)
            {
                return;
            }

            string tmpTitle = null;
            int tmpId = 0;

            if (nExpectedBooks != data.Books.Count)
            {
                throw new InvalidOperationException($"Number of deserialized Books was {data.Books.Count} but expected {nExpectedBooks}. This Serializer seem to have lost data.");
            }

            for (int i = 0; i < data.Books.Count; i++)
            {
                tmpTitle = data.Books[i].Title;
                tmpId = data.Books[i].Id;
                if (data.Books[i].Id != i + 1)
                {
                    throw new InvalidOperationException($"Book Id was {data.Books[i].Id} but exepcted {i + 1}");
                }
                if (optionalPayloadDataSize > 0 && data.Books[i].BookData.Length != optionalPayloadDataSize)
                {
                    throw new InvalidOperationException($"BookData length was {data.Books[i].BookData.Length} but expected {optionalPayloadDataSize}");
                }
            }
        }

        void TouchAndVerify(NetCorePropertyBookShelf data, int nExpectedBooks, int optionalPayloadDataSize)
        {
            if (!VerifyAndTouch)
            {
                return;
            }

            string tmpTitle = null;
            int tmpId = 0;

            if (nExpectedBooks != data.Books.Count)
            {
                throw new InvalidOperationException($"Number of deserialized Books was {data.Books.Count} but expected {nExpectedBooks}. This Serializer seem to have lost data.");
            }

            for (int i = 0; i < data.Books.Count; i++)
            {
                var book = data.Books[i];
                tmpTitle = book.Title;
                tmpId = book.Id;
                if (book.Id != i + 1)
                {
                    throw new InvalidOperationException($"Book Id was {book.Id} but exepcted {i + 1}");
                }
                if (optionalPayloadDataSize > 0 && book.BookData.Length != optionalPayloadDataSize)
                {
                    throw new InvalidOperationException($"BookData length was {book.BookData.Length} but expected {optionalPayloadDataSize}");
                }
            }
        }

        /// <summary>
        /// Call all setters once to get a feeling for the deserialization overhead
        /// </summary>
        /// <param name="data"></param>
        void TouchFlat(BookShelfFlat data, int nExpectedBooks, int optionalPayloadDataSize)
        {
            if (!VerifyAndTouch)
            {
                return;
            }

            string tmpTitle = null;
            int tmpId = 0;
            if (nExpectedBooks != data.BooksLength)
            {
                throw new InvalidOperationException($"Number of deserialized Books was {data.BooksLength} but expected {nExpectedBooks}. This Serializer seem to have lost data.");
            }

            for (int i = 0; i < data.BooksLength; i++)
            {
                var book = data.Books(i);
                tmpTitle = book.Value.Title;
                tmpId = book.Value.Id;
                if (tmpId != i + 1)
                {
                    throw new InvalidOperationException($"Book Id was {tmpId} but exepcted {i + 1}");
                }
                if (optionalPayloadDataSize > 0 && book.Value.BookDataLength != optionalPayloadDataSize)
                {
                    throw new InvalidOperationException($"BookData length was {book.Value.BookDataLength} but expected {optionalPayloadDataSize}");
                }
            }
        }

        ZeroFormatterBookShelf DataZeroFormatter(int nToCreate)
        {
            var shelf = new ZeroFormatterBookShelf
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new ZeroFormatterBook { Id = i, Title = $"Book {i}" }).ToList()
            };
            return shelf;
        }

        ZeroFormatterBookShelf1 DataZeroFormatter1(int nToCreate)
        {
            var shelf = new ZeroFormatterBookShelf1
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new ZeroFormatterBook1 { Id = i, Title = $"Book {i}" }).ToList()
            };
            return shelf;
        }

        ZeroFormatterBookShelf2 DataZeroFormatter2(int nToCreate)
        {
            var shelf = new ZeroFormatterBookShelf2
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new ZeroFormatterBook2 { Id = i, Title = $"Book {i}" }).ToList()
            };
            return shelf;
        }

        ZeroFormatterLargeBookShelf DataZeroFormatterLarge(int nToCreate)
        {
            var lret = new ZeroFormatterLargeBookShelf("private member value2")
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new ZeroFormatterLargeBook { Id = i, Title = $"Book {i}" }).ToList()
            };
            return lret;
        }

        void Touch(ZeroFormatterBookShelf data)
        {
            if (!VerifyAndTouch) return;

            string tmpTitle = null;
            int tmpId = 0;
            for (int i = 0; i < data.Books.Count; i++)
            {
                tmpTitle = data.Books[i].Title;
                tmpId = data.Books[i].Id;
            }
        }




        BookShelf1 Data1(int nToCreate)
        {
            var lret = new BookShelf1("private member value1")
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new Book1 { Id = i, Title = $"Book {i}" }).ToList()
            };
            return lret;
        }

        BookShelf2 Data2(int nToCreate)
        {
            var lret = new BookShelf2("private member value2")
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new Book2 { Id = i, Title = $"Book {i}" }).ToList()
            };
            return lret;
        }

        LargeBookShelf DataLarge(int nToCreate)
        {
            var lret = new LargeBookShelf("private member value2")
            {
                Books = Enumerable.Range(1, nToCreate).Select(i => new LargeBook { Id = i, Title = $"Book {i}" }).ToList()
            };
            return lret;
        }

        ReferenceBookShelf DataReferenceBookShelf(int nToCreate)
        {
            var lret = new ReferenceBookShelf();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 10; i++)
            {
                sb.Append("This is a really long string");
            }
            string largeStrSameReference = sb.ToString();

            for (int i = 1; i <= nToCreate; i++)
            {
                var book = new ReferenceBook()
                {
                    Container = null,
                    Name = largeStrSameReference,
                    Price = i
                };
                lret.Books.Add(new DateTime(DateTime.MinValue.Ticks + i, DateTimeKind.Utc), book);
            }
            return lret;
        }
    }
}
