using FluentFTP;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebLoader.Clients
{
    class FtpFileClient : IFileClient
    {
        private FtpClient _client;

        public FtpFileClient(string host, string userName, string password, string encodingName)
        {
            _client = new FtpClient(host, userName, password);
            _client.DownloadDataType = FtpDataType.Binary;
            _client.Encoding = Encoding.GetEncoding(encodingName);
        }

        public Task ConnectAsync()
        {
            return _client.ConnectAsync();
        }

        public async Task<IEnumerable<RemoteItemInfo>> GetItemsAsync(string path)
        {
            var fis = await _client.GetListingAsync(path, FtpListOption.AllFiles);
            return fis.Select(t => new RemoteItemInfo
            {
                Type = ConvertType(t.Type),
                Name = t.Name,
                FullName = t.FullName,
                Modified = t.Modified,
                Size = t.Size,
            });
        }

        public Task DownloadFileAsync(string target, string path)
        {
            return _client.DownloadFileAsync(path, target);
        }

        private ItemType ConvertType(FtpFileSystemObjectType type)
        {
            switch (type)
            {
                case FtpFileSystemObjectType.File: return ItemType.File;
                case FtpFileSystemObjectType.Directory: return ItemType.Directory;
                case FtpFileSystemObjectType.Link: return ItemType.Others;
                default: return ItemType.Unknown;
            }
        }
    }
}
