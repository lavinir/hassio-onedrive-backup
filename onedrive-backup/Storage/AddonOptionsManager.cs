using hassio_onedrive_backup.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Storage
{
    internal class AddonOptionsManager
    {
        public static AddonOptions ReadOptions(string path = "options.json")
        {
            if (File.Exists(path) == false)
            {
                throw new NotSupportedException($"{path} not found");
            }

            string optionContents = File.ReadAllText(path);
            var options = JsonConvert.DeserializeObject<AddonOptions>(optionContents);
            return options!;
        }

        public static async Task WriteOptions(AddonOptions options, string path = "options.json")
        {
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(options));
        }
    }
}
