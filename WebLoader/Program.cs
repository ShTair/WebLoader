using FluentFTP;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Run(args[0]).Wait();
        }

        private static HashSet<string> _ignorePaths;
        private static HashSet<string> _undeletableNames;
        private static StreamWriter _writer;

        private static async Task Run(string paramFile)
        {
            var now = DateTime.Now;
            Directory.CreateDirectory("logs");
            using (_writer = new StreamWriter(Path.Combine("logs", $"log_{now:yyyy-MM-dd-HH-mm-ss}.txt")))
            {
                await _writer.WriteLineAsync($"Start: {now:yyyy/MM/dd HH:mm:ss}");

                var param = await LoadParamAsync(paramFile);

                var client = new FtpClient(param.Host, param.UserName, param.Password);
                client.DownloadDataType = FtpDataType.Binary;
                client.Encoding = Encoding.GetEncoding(param.EncodingName);
                await client.ConnectAsync();

                _ignorePaths = new HashSet<string>(param.IgnorePaths);
                _undeletableNames = new HashSet<string>(param.UndeletableNames);
                Directory.CreateDirectory(param.VaultPath);
                try
                {
                    await LoadDirectoriesAsync(client, param.BasePath, param.VaultPath);
                }
                catch (Exception exp)
                {
                    Console.WriteLine(exp);
                    await _writer.WriteLineAsync(exp.ToString());
                }

                var end = DateTime.Now;
                await _writer.WriteLineAsync($"Finish: {end:yyyy/MM/dd HH:mm:ss}");
                await _writer.WriteLineAsync($"Span: {end - now}");
            }
        }

        private static async Task LoadDirectoriesAsync(FtpClient client, string path, string dstPath)
        {
            var existsFiles = Directory.EnumerateFiles(dstPath).Select(Path.GetFileName).ToList();
            var existsDirectories = Directory.EnumerateDirectories(dstPath).Select(Path.GetFileName).ToList();

            var items = await client.GetListingAsync(path, FtpListOption.AllFiles);
            foreach (var item in items)
            {
                if (_ignorePaths.Contains(item.FullName)) continue;

                switch (item.Type)
                {
                    case FtpFileSystemObjectType.File:
                        {
                            existsFiles.Remove(item.Name);
                            var file = new FileInfo(Path.Combine(dstPath, item.Name));
                            if (!file.Exists || file.LastWriteTime != item.Modified || file.Length != item.Size)
                            {
                                await _writer.WriteLineAsync($"!: {item.FullName}");
                                Console.WriteLine($"!: {item.FullName}");
                                await client.DownloadFileAsync(file.FullName, item.FullName);

                                file.Refresh();
                                if (file.Length != item.Size) throw new Exception();
                                file.LastWriteTime = item.Modified;
                            }
                            else
                            {
                                await _writer.WriteLineAsync($"x: {item.FullName}");
                                Console.WriteLine($"x: {item.FullName}");
                            }
                        }
                        break;
                    case FtpFileSystemObjectType.Directory:
                        {
                            existsDirectories.Remove(item.Name);
                            await _writer.WriteLineAsync($"D: {item.FullName}");
                            Console.WriteLine($"D: {item.FullName}");
                            var ndpath = Path.Combine(dstPath, item.Name);
                            Directory.CreateDirectory(ndpath);

                            await LoadDirectoriesAsync(client, item.FullName, ndpath);

                            Directory.SetLastWriteTime(ndpath, item.Modified);
                        }
                        break;
                    case FtpFileSystemObjectType.Link:
                        await _writer.WriteLineAsync($"L: {item.FullName}");
                        Console.WriteLine($"L: {item.FullName}");
                        break;
                    default:
                        await _writer.WriteLineAsync($"?: {item.FullName}");
                        Console.WriteLine($"?: {item.FullName}");
                        break;
                }
            }

            foreach (var item in existsFiles.Where(t => !_undeletableNames.Contains(t)))
            {
                File.Delete(Path.Combine(dstPath, item));
            }

            foreach (var item in existsDirectories.Where(t => !_undeletableNames.Contains(t)))
            {
                Directory.Delete(Path.Combine(dstPath, item), true);
            }
        }

        private static async Task<TargetParam> LoadParamAsync(string paramFile)
        {
            var json = await File.ReadAllTextAsync(paramFile);
            return JsonConvert.DeserializeObject<TargetParam>(json);
        }
    }
}
