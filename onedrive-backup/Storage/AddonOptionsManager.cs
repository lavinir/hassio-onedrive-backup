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
        public static AddonOptions ReadOptions(ConsoleLogger logger, string path = "settings.json")
        {
            if (File.Exists(path) == false)
            {
                logger.LogInfo($"No new settings file. Attempting to restore from legacy configuration");
                path = "options.json";

				if (File.Exists(path) == false)
				{
					logger.LogWarning($"No legacy configuration found. Loading default settings");
					return new AddonOptions();
				}
			}


			string optionContents = File.ReadAllText(path);
            var options = JsonConvert.DeserializeObject<AddonOptions>(optionContents);
            return options!;
        }

        public static async Task WriteOptions(AddonOptions options, string path = "settings.json")
        {
            await File.WriteAllTextAsync(path, JsonConvert.SerializeObject(options));
        }
    }
}
