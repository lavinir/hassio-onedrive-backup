using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Contracts
{
    public class OnedriveItemDescription
    {
        public string Slug { get; set; }

        public DateTime BackupDate { get; set; }

        public string? InstanceName { get; set; }

        public bool IsProtected { get; set; }

        public float Size { get; set; }

        public string BackupType { get; set; }

        public IEnumerable<string> Addons { get; set; }

        public IEnumerable<string> Folders { get; set; }
    }
}
