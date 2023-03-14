using Microsoft.Graph;
using static hassio_onedrive_backup.Contracts.HassAddonsResponse;

namespace onedrive_backup.Hass
{
    public class HassContext
    {
        public string IngressUrl { get; set; }

        public string HeaderIngressPath { get; set; }

        public IEnumerable<Addon> Addons { get; set; }
    }
}
