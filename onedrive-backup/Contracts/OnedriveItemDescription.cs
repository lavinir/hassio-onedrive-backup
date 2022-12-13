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
    }
}
