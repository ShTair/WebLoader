using FluentFTP;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WebLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            Run(args[0]).Wait();
        }

        private static async Task Run(string paramFile)
        {
            var param = await LoadParamAsync(paramFile);

            var client = new FtpClient(param.Host, param.UserName, param.Password);
            client.DownloadDataType = FtpDataType.Binary;
            client.Encoding = Encoding.Default;
            await client.ConnectAsync();

            Directory.CreateDirectory(param.VaultPath);
            try
            {
                await LoadDirectoriesAsync(client, param.BasePath, param.VaultPath);
            }
            catch (Exception exp)
            {
                Console.WriteLine(exp);
            }
        }

        private static async Task LoadDirectoriesAsync(FtpClient client, string path, string dstPath)
        {
            var items = await client.GetListingAsync(path, FtpListOption.AllFiles);
            foreach (var item in items)
            {
                switch (item.Type)
                {
                    case FtpFileSystemObjectType.File:
                        {
                            Console.Write($"F: {item.FullName} ");
                            var filePath = Path.Combine(dstPath, item.Name);
                            if (!File.Exists(filePath) | (File.GetLastWriteTime(filePath) != item.Modified))
                            {
                                Console.WriteLine("DL");
                                await client.DownloadFileAsync(filePath, item.FullName);
                                File.SetLastWriteTime(filePath, item.Modified);
                            }
                            else
                            {
                                Console.WriteLine("NMOD");
                            }
                        }
                        break;
                    case FtpFileSystemObjectType.Directory:
                        {
                            Console.WriteLine($"D: {item.FullName}");
                            var ndpath = Path.Combine(dstPath, item.Name);
                            Directory.CreateDirectory(ndpath);

                            await LoadDirectoriesAsync(client, item.FullName, ndpath);

                            Directory.SetLastWriteTime(ndpath, item.Modified);
                        }
                        break;
                    case FtpFileSystemObjectType.Link:
                        break;
                    default:
                        break;
                }
            }
        }

        private static async Task<TargetParam> LoadParamAsync(string paramFile)
        {
            var json = await File.ReadAllTextAsync(paramFile);
            return JsonConvert.DeserializeObject<TargetParam>(json);
        }
    }
}
