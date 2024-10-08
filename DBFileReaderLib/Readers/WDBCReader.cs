﻿using DBFileReaderLib.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace DBFileReaderLib.Readers
{
    class WDBCRow : IDBRow
    {
        private BaseReader m_reader;
        private readonly int m_recordIndex;

        public int Id { get; set; }
        public BitReader Data { get; set; }

        public WDBCRow(BaseReader reader, BitReader data, int recordIndex)
        {
            m_reader = reader;
            Data = data;
            m_recordIndex = recordIndex + 1;

            Id = m_recordIndex = recordIndex + 1;
        }

        private static Dictionary<Type, Func<BitReader, Dictionary<long, string>, BaseReader, object>> simpleReaders = new Dictionary<Type, Func<BitReader, Dictionary<long, string>, BaseReader, object>>
        {
            [typeof(long)] = (data, stringTable, header) => GetFieldValue<long>(data),
            [typeof(float)] = (data, stringTable, header) => GetFieldValue<float>(data),
            [typeof(int)] = (data, stringTable, header) => GetFieldValue<int>(data),
            [typeof(uint)] = (data, stringTable, header) => GetFieldValue<uint>(data),
            [typeof(short)] = (data, stringTable, header) => GetFieldValue<short>(data),
            [typeof(ushort)] = (data, stringTable, header) => GetFieldValue<ushort>(data),
            [typeof(sbyte)] = (data, stringTable, header) => GetFieldValue<sbyte>(data),
            [typeof(byte)] = (data, stringTable, header) => GetFieldValue<byte>(data),
            [typeof(string)] = (data, stringTable, header) => stringTable[GetFieldValue<int>(data)],
        };

        private static Dictionary<Type, Func<BitReader, Dictionary<long, string>, int, object>> arrayReaders = new Dictionary<Type, Func<BitReader, Dictionary<long, string>, int, object>>
        {
            [typeof(ulong[])] = (data, stringTable, cardinality) => GetFieldValueArray<ulong>(data, cardinality),
            [typeof(long[])] = (data, stringTable, cardinality) => GetFieldValueArray<long>(data, cardinality),
            [typeof(float[])] = (data, stringTable, cardinality) => GetFieldValueArray<float>(data, cardinality),
            [typeof(int[])] = (data, stringTable, cardinality) => GetFieldValueArray<int>(data, cardinality),
            [typeof(uint[])] = (data, stringTable, cardinality) => GetFieldValueArray<uint>(data, cardinality),
            [typeof(ulong[])] = (data, stringTable, cardinality) => GetFieldValueArray<ulong>(data, cardinality),
            [typeof(ushort[])] = (data, stringTable, cardinality) => GetFieldValueArray<ushort>(data, cardinality),
            [typeof(short[])] = (data, stringTable, cardinality) => GetFieldValueArray<short>(data, cardinality),
            [typeof(byte[])] = (data, stringTable, cardinality) => GetFieldValueArray<byte>(data, cardinality),
            [typeof(sbyte[])] = (data, stringTable, cardinality) => GetFieldValueArray<sbyte>(data, cardinality),
            [typeof(string[])] = (data, stringTable, cardinality) => GetFieldValueArray<int>(data, cardinality).Select(i => stringTable[i]).ToArray(),
        };

        public void GetFields<T>(FieldCache<T>[] fields, T entry)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                FieldCache<T> info = fields[i];
                if (info.IndexMapField)
                {
                    Id = GetFieldValue<int>(Data);
                    info.Setter(entry, Convert.ChangeType(Id, info.FieldType));
                    continue;
                }

                object value = null;

                if (info.IsArray)
                {
                    if (arrayReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(Data, m_reader.StringTable, info.Cardinality);
                    else
                        throw new Exception("Unhandled array type: " + typeof(T).Name);
                }
                else if (info.IsLocalisedString)
                {
                    Data.Position += 32 * info.LocaleInfo.Locale;
                    value = simpleReaders[typeof(string)](Data, m_reader.StringTable, m_reader);
                    Data.Position += 32 * (info.LocaleInfo.LocaleCount - info.LocaleInfo.Locale);
                }
                else
                {
                    if (simpleReaders.TryGetValue(info.FieldType, out var reader))
                        value = reader(Data, m_reader.StringTable, m_reader);
                    else
                        throw new Exception("Unhandled field type: " + typeof(T).Name);
                }

                info.Setter(entry, value);
            }
        }

        private static T GetFieldValue<T>(BitReader r) where T : struct
        {
            return r.ReadValue64(Unsafe.SizeOf<T>() * 8).GetValue<T>();
        }

        private static T[] GetFieldValueArray<T>(BitReader r, int cardinality) where T : struct
        {
            T[] array = new T[cardinality];
            for (int i = 0; i < array.Length; i++)
                array[i] = r.ReadValue64(Unsafe.SizeOf<T>() * 8).GetValue<T>();

            return array;
        }

        public IDBRow Clone()
        {
            return (IDBRow)MemberwiseClone();
        }
    }

    class WDBCReader : BaseReader
    {
        private const int HeaderSize = 20;
        private const uint WDBCFmtSig = 0x43424457; // WDBC

        public WDBCReader(string dbcFile) : this(new FileStream(dbcFile, FileMode.Open)) { }

        public WDBCReader(Stream stream)
        {
            using (var reader = new BinaryReader(stream, Encoding.UTF8))
            {
                if (reader.BaseStream.Length < HeaderSize)
                    throw new InvalidDataException("WDBC file is corrupted!");

                uint magic = reader.ReadUInt32();

                if (magic != WDBCFmtSig)
                    throw new InvalidDataException("WDBC file is corrupted!");

                RecordsCount = reader.ReadInt32();
                FieldsCount = reader.ReadInt32();
                RecordSize = reader.ReadInt32();
                StringTableSize = reader.ReadInt32();

                if (RecordsCount == 0)
                    return;

                RecordsData = reader.ReadBytes(RecordsCount * RecordSize);
                Array.Resize(ref RecordsData, RecordsData.Length + 8); // pad with extra zeros so we don't crash when reading

                for (int i = 0; i < RecordsCount; i++)
                {
                    BitReader bitReader = new BitReader(RecordsData) { Position = i * RecordSize * 8 };
                    IDBRow rec = new WDBCRow(this, bitReader, i);
                    _Records.Add(i, rec);
                }

                StringTable = new Dictionary<long, string>(StringTableSize / 0x20);
                for (int i = 0; i < StringTableSize;)
                {
                    long oldPos = reader.BaseStream.Position;
                    StringTable[i] = reader.ReadCString();
                    i += (int)(reader.BaseStream.Position - oldPos);
                }
            }
        }
    }
}
