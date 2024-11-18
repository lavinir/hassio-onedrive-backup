using Newtonsoft.Json;
using onedrive_backup.Contracts;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Hass
{
    //public class HassOnedriveFreeSpaceEntityState : INotifyPropertyChanged
    //{
    //    private const string OneDrive_FreeSpace_Entity_ID = "sensor.onedrivefreespace";
    //    private IHassioClient? _hassioClient;

    //    public HassOnedriveFreeSpaceEntityState(IHassioClient hassioClient)
    //    {
    //        _hassioClient = hassioClient;
    //    }

    //    public double? FreeSpaceGB { get; private set; }
    //    public double? TotalSpaceGB { get; private set; }

    //    public event PropertyChangedEventHandler? PropertyChanged;

    //    public async Task UpdateOneDriveFreespaceSensorInHass(OneDriveFreeSpaceData freeSpaceData)
    //    {
    //        FreeSpaceGB = freeSpaceData.FreeSpace;
    //        TotalSpaceGB = freeSpaceData.TotalSpace;

    //        var payload = new
    //        {
    //            state = FreeSpaceGB == null ? "-" : FreeSpaceGB.Value.ToString("0.00"),
    //            attributes = new Dictionary<string, string>
    //            {
    //                { "unit_of_measurement", "GB" }
    //            }
    //        };

    //        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    //        string payloadStr = JsonConvert.SerializeObject(payload);
    //        await _hassioClient.UpdateHassEntityStateAsync(OneDrive_FreeSpace_Entity_ID, payloadStr);
    //    }

    //}
}
