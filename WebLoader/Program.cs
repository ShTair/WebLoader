using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using WebLoader.Clients;

namespace WebLoader
{
    class Program
    {
        static void Main(string[] args)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            Run(args[0]).Wait();
        }

        private static Regex[] _ignoreRegices;
        private static HashSet<string> _undeletableNames;
        private static StreamWriter _writer;

        private static async Task Run(string paramFile)
        {
            var now = DateTime.Now;

            var logsPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "logs");
            Directory.CreateDirectory(logsPath);
            var logName = Path.Combine(logsPath, $"log_{now:yyyy-MM-dd_HH-mm-ss}.txt");
            Console.WriteLine($"Log: {logName}");
            using (_writer = new StreamWriter(logName))
            {
                await _writer.WriteLineAsync($"Start: {now:yyyy/MM/dd HH:mm:ss}");

                var param = await LoadParamAsync(paramFile);

                IFileClient client;
                if (param.Protocol.Equals("sftp", StringComparison.CurrentCultureIgnoreCase))
                {
                    client = CreateSftpClient(param);
                }
                else
                {
                    client = await CreateFtpClient(param);
                }

                _ignoreRegices = param.IgnorePaths.Select(t => new Regex(t)).ToArray();

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

        private static async Task<IFileClient> CreateFtpClient(TargetParam param)
        {
            var client = new FtpFileClient(param.Host, param.UserName, param.Password, param.EncodingName);
            await client.ConnectAsync();
            return client;
        }

        private static IFileClient CreateSftpClient(TargetParam param)
        {
            SftpFileClient client;
            if (string.IsNullOrWhiteSpace(param.Password))
            {
                client = SftpFileClient.CreateWithKeyFile(param.Host, param.UserName, param.KeyPath);
            }
            else
            {
                client = SftpFileClient.CreateWithPassword(param.Host, param.UserName, param.Password);
            }
            client.Connect();
            return client;
        }

        private static async Task LoadDirectoriesAsync(IFileClient client, string path, string dstPath)
        {
            var existsFiles = Directory.EnumerateFiles(dstPath).Select(Path.GetFileName).ToList();
            var existsDirectories = Directory.EnumerateDirectories(dstPath).Select(Path.GetFileName).ToList();

            var items = await client.GetItemsAsync(path);
            foreach (var item in items)
            {
                try
                {
                    if (_ignoreRegices.Any(t => t.IsMatch(item.FullName))) continue;

                    switch (item.Type)
                    {
                        case ItemType.File:
                            {
                                existsFiles.Remove(item.Name);
                                var file = new FileInfo(Path.Combine(dstPath, item.Name));
                                if (!file.Exists || file.LastWriteTime != item.Modified || file.Length != item.Size)
                                {
                                    await _writer.WriteLineAsync($"!: {item.FullName}");
                                    Console.WriteLine($"!: {item.FullName}");
                                    await client.DownloadFileAsync(item.FullName, file.FullName);

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
                        case ItemType.Directory:
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
                        case ItemType.Others:
                            await _writer.WriteLineAsync($"O: {item.FullName}");
                            Console.WriteLine($"O: {item.FullName}");
                            break;
                        default:
                            await _writer.WriteLineAsync($"?: {item.FullName}");
                            Console.WriteLine($"?: {item.FullName}");
                            break;
                    }
                }
                catch (Exception exp)
                {
                    await _writer.WriteLineAsync("ERROR");
                    await _writer.WriteLineAsync(exp.ToString());
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
