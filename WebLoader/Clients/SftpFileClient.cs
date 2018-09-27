using Renci.SshNet;
using Renci.SshNet.Sftp;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace WebLoader.Clients
{
    class SftpFileClient : IFileClient
    {
        private SftpClient _client;

        private SftpFileClient(SftpClient client)
        {
            _client = client;
        }

        public static SftpFileClient CreateWithPassword(string host, string userName, string password)
        {
            return new SftpFileClient(new SftpClient(host, userName, password));
        }

        public static SftpFileClient CreateWithKeyFile(string host, string userName, string keyPath)
        {
            var key = new PrivateKeyFile(keyPath);
            return new SftpFileClient(new SftpClient(host, userName, key));
        }

        public void Connect()
        {
            _client.Connect();
        }

        public Task DownloadFileAsync(string target, string path)
        {
            using (var stream = File.Create(path))
            {
                _client.DownloadFile(target, stream);
            }

            return Task.FromResult(true);
        }

        public Task<IEnumerable<RemoteItemInfo>> GetItemsAsync(string path)
        {
            var items = _client.ListDirectory(path);
            return Task.FromResult(items.Where(t => CheckName(t.Name)).Select(t => new RemoteItemInfo
            {
                Type = ConvertType(t),
                Name = t.Name,
                FullName = t.FullName,
                Modified = t.LastWriteTime,
                Size = t.Length,
            }));
        }

        private bool CheckName(string name)
        {
            switch (name)
            {
                case ".":
                case "..": return false;
                default: return true;
            }
        }

        private ItemType ConvertType(SftpFile item)
        {
            if (item.IsRegularFile) return ItemType.File;
            if (item.IsDirectory) return ItemType.Directory;

            return ItemType.Others;
        }
    }
}
