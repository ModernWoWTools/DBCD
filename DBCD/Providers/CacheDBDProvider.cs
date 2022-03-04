using System;
using System.IO;
using System.Net.Http;

namespace DBCD.Providers
{
    public class CacheDBDProvider : IDBDProvider
    {
        private static Uri BaseURI { get; } = new Uri("https://raw.githubusercontent.com/wowdev/WoWDBDefs/master/definitions/");
        private static string CachePath { get; } =  "Cache/";
        private HttpClient HttpClient { get; } = new HttpClient();

        public CacheDBDProvider()
        {
            if (!Directory.Exists(CachePath))
                Directory.CreateDirectory(CachePath);

            HttpClient.BaseAddress = BaseURI;
        }

        public Stream StreamForTableName(string tableName, string? build = null)
        {
            string dbdName = Path.GetFileName(tableName) + ".dbd";
            if (!File.Exists($"{CachePath}/{dbdName}") || (DateTime.Now - File.GetLastWriteTime($"{CachePath}/{dbdName}")).TotalHours > 24)
            {
                var bytes = HttpClient.GetByteArrayAsync(dbdName).Result;
                File.WriteAllBytes($"{CachePath}/{dbdName}", bytes);
                return new MemoryStream(bytes);
            }
            else
                return new MemoryStream(File.ReadAllBytes($"{CachePath}/{dbdName}"));
        }
    }
}
