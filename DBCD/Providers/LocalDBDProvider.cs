using System.IO;

namespace DBCD.Providers
{
    public class LocalDBCProvider : IDBCProvider
    {
        private readonly string Directory;

        public LocalDBCProvider(string directory) => Directory = directory;

        public Stream StreamForTableName(string tableName, string build) => File.OpenRead(Path.Combine(Directory, tableName + ".db2"));
    }
}
