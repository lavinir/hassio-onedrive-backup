using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Contracts
{
    internal class OnedriveBackup
    {
        public OnedriveBackup(string slug, string fileName)
        {
            Slug = slug;
            FileName = fileName;
        }

        public string Slug { get; }

        public string FileName { get; }
    }
}
