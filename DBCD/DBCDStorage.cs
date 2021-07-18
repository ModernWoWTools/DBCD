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
        private readonly FieldAccessor fieldAccessor;

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

        public object this[string fieldName]
        {
            get => fieldAccessor[this.raw, fieldName];
        }

        public object this[string fieldName, int index]
        {
            get => ((Array)this[fieldName]).GetValue(index);
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

    public interface IDBCDStorage : IEnumerable<DynamicKeyValuePair<int>>, IDictionary<int, DBCDRow>
    {
        string[] AvailableColumns { get; }

        Dictionary<ulong, int> GetEncryptedSections();
        void Save(string filename);
    }

    public class DBCDStorage<T> : Dictionary<int, DBCDRow>, IDBCDStorage where T : class, new()
    {
        private readonly FieldAccessor fieldAccessor;
        private readonly Storage<T> storage;
        private readonly DBCDInfo info;
        private readonly DBParser reader;

        string[] IDBCDStorage.AvailableColumns => this.info.availableColumns;
        public override string ToString() => $"{this.info.tableName}";

        public DBCDStorage(Stream stream, DBCDInfo info) : this(new DBParser(stream), info) { }

        public DBCDStorage(DBParser dbReader, DBCDInfo info) : this(dbReader, dbReader.GetRecords<T>(), info) { }

        public DBCDStorage(DBParser reader, Storage<T> storage, DBCDInfo info) : base(new Dictionary<int, DBCDRow>())
        {
            this.info = info;
            this.fieldAccessor = new FieldAccessor(typeof(T), info.availableColumns);
            this.reader = reader;
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
        
        public Dictionary<ulong, int> GetEncryptedSections() => this.reader.GetEncryptedSections();

        public void Save(string filename)
        {
            foreach (var (id, record) in this)
                storage.Add(id, record.AsType<T>());

            storage?.Save(filename);
        }
    }
}
