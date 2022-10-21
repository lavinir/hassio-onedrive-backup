using hassio_onedrive_backup.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Storage
{
    internal class AddonOptionsReader
    {
        public static AddonOptions ReadOptions(string dataPath)
        {
            if (File.Exists(dataPath) == false)
            {
                throw new NotSupportedException($"{dataPath} not found");
            }

            string optionContents = File.ReadAllText(dataPath);
            var options = JsonConvert.DeserializeObject<AddonOptions>(optionContents);
            return options!;
        }
    }
}
