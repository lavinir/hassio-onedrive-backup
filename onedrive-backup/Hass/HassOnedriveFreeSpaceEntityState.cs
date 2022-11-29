﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace hassio_onedrive_backup.Hass
{
    internal static class HassOnedriveFreeSpaceEntityState
    {
        private const string OneDrive_FreeSpace_Entity_ID = "sensor.onedrivefreespace";

        public static async Task UpdateOneDriveFreespaceSensorInHass(double? freeSpaceGB, IHassioClient hassioClient)
        {
            var payload = new
            {
                state = freeSpaceGB == null ? "-" : freeSpaceGB.Value.ToString("0.00"),
                attributes = new Dictionary<string, string>
                {
                    { "unit_of_measurement", "GB" }
                }
            };

            string payloadStr = JsonConvert.SerializeObject(payload);
            await hassioClient.UpdateHassEntityStateAsync(OneDrive_FreeSpace_Entity_ID, payloadStr);
        }

    }
}
