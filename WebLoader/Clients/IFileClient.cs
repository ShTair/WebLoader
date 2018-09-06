using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WebLoader.Clients
{
    interface IFileClient
    {
        Task<IEnumerable<RemoteItemInfo>> GetItemsAsync(string path);

        Task DownloadFileAsync(string target, string path);
    }

    class RemoteItemInfo
    {
        public ItemType Type { get; set; }

        public string Name { get; set; }

        public string FullName { get; set; }

        public long Size { get; set; }

        public DateTime Modified { get; set; }
    }

    enum ItemType
    {
        Unknown,
        File,
        Directory,
        Others,
    }
}
