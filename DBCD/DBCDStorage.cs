using DBCD.Helpers;

using DBFileReaderLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.IO;
using System.Linq;

namespace DBCD
{
    public class DBCDRow : DynamicObject
    {
        private int ID;

        private readonly dynamic raw;
        private FieldAccessor fieldAccessor;

        internal DBCDRow(int ID, dynamic raw, FieldAccessor fieldAccessor)
        {
            this.raw = raw;
            this.fieldAccessor = fieldAccessor;
            this.ID = ID;
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            return fieldAccessor.TryGetMember(this.raw, binder.Name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            return fieldAccessor.TrySetMember(this.raw, binder.Name, value);
        }

        public object this[string fieldName]
        {
            get => fieldAccessor[this.raw, fieldName];
            set => fieldAccessor[this.raw, fieldName] = value;
        }

        public object this[string fieldName, int index]
        {
            get => ((Array)this[fieldName]).GetValue(index);
            set => ((Array)this[fieldName]).SetValue(value, index);
        }

        public T Field<T>(string fieldName)
        {
            return (T)fieldAccessor[this.raw, fieldName];
        }

        public T FieldAs<T>(string fieldName)
        {
            return fieldAccessor.GetMemberAs<T>(this.raw, fieldName);
        }

        public override IEnumerable<string> GetDynamicMemberNames()
        {
            return fieldAccessor.FieldNames;
        }

        public T AsType<T>() => (T)raw;
    }

    public class DynamicKeyValuePair<T>
    {
        public T Key;
        public dynamic Value;

        internal DynamicKeyValuePair(T key, dynamic value)
        {
            this.Key = key;
            this.Value = value;
        }
    }

    public class RowConstructor
    {
        private readonly IDBCDStorage storage;
        public RowConstructor(IDBCDStorage storage)
        {
            this.storage = storage;
        }

        public bool Create(int index, Action<dynamic> f)
        {
            var constructedRow = storage.ConstructRow(index);
            if (storage.ContainsKey(index))
                return false;
            else
            {
                f(constructedRow);
                storage.Add(index, constructedRow);
            }

            return true;
        }
    }

    public interface IDBCDStorage : IEnumerable<DynamicKeyValuePair<int>>, IDictionary<int, DBCDRow>
    {
        string[] AvailableColumns { get; }

        DBCDRow ConstructRow(int index);

        void ApplyingHotfixes(HotfixReader hotfixReader);
        void ApplyingHotfixes(HotfixReader hotfixReader, HotfixReader.RowProcessor processor);

        Dictionary<ulong, int> GetEncryptedSections();
        void Save(string filename);
    }

    public class DBCDStorage<T> : Dictionary<int, DBCDRow>, IDBCDStorage where T : class, new()
    {
        private readonly FieldAccessor fieldAccessor;
        private readonly Storage<T> storage;
        private readonly DBCDInfo info;
        private readonly DBParser parser;

        string[] IDBCDStorage.AvailableColumns => this.info.availableColumns;
        public override string ToString() => $"{this.info.tableName}";

        public DBCDStorage(Stream stream, DBCDInfo info) : this(new DBParser(stream), info) { }

        public DBCDStorage(DBParser dbParser, DBCDInfo info) : this(dbParser, dbParser.GetRecords<T>(), info) { }

        public DBCDStorage(DBParser parser, Storage<T> storage, DBCDInfo info) : base(new Dictionary<int, DBCDRow>())
        {
            this.info = info;
            this.fieldAccessor = new FieldAccessor(typeof(T), info.availableColumns);
            this.parser = parser;
            this.storage = storage;

            foreach (var record in storage)
                base.Add(record.Key, new DBCDRow(record.Key, record.Value, fieldAccessor));

            storage.Clear();
        }

        IEnumerator<DynamicKeyValuePair<int>> IEnumerable<DynamicKeyValuePair<int>>.GetEnumerator()
        {
            var enumerator = GetEnumerator();
            while (enumerator.MoveNext())
                yield return new DynamicKeyValuePair<int>(enumerator.Current.Key, enumerator.Current.Value);
        }
        
        public Dictionary<ulong, int> GetEncryptedSections() => this.parser.GetEncryptedSections();


        public void ApplyingHotfixes(HotfixReader hotfixReader)
        {
            this.ApplyingHotfixes(hotfixReader, null);
        }

        public void ApplyingHotfixes(HotfixReader hotfixReader, HotfixReader.RowProcessor processor)
        {
            var mutableStorage = this.storage.ToDictionary(k => k.Key, v => v.Value);

            hotfixReader.ApplyHotfixes(mutableStorage, this.parser, processor);

            foreach (var (id, row) in mutableStorage)
                base[id] = new DBCDRow(id, row, fieldAccessor);
        }


        public void Save(string filename)
        {
            foreach (var (id, record) in this)
                storage.Add(id, record.AsType<T>());

            storage?.Save(filename);
        }

        public DBCDRow ConstructRow(int index) => new DBCDRow(index, new T(), fieldAccessor);
    }
}
