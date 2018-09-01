using FluentFTP;
using Newtonsoft.Json;
using System.IO;
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
            await client.ConnectAsync();

            var items = await client.GetListingAsync("/", FtpListOption.AllFiles);
        }

        private static async Task<TargetParam> LoadParamAsync(string paramFile)
        {
            var json = await File.ReadAllTextAsync(paramFile);
            return JsonConvert.DeserializeObject<TargetParam>(json);
        }
    }
}
