using Azure.Storage.Blobs.Models;
using hassio_onedrive_backup.Contracts;
using hassio_onedrive_backup.Hass;
using onedrive_backup.Graph;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test.onedrive_backup.Mocks
{
	public class BackupManagerMock : BackupManager
	{		
		public BackupManagerMock(
			IServiceProvider serviceProvider, 
			TransferSpeedHelper? transferSpeedHelper) : base(serviceProvider, transferSpeedHelper)
		{
			
		}

		public void SetIsExecuting(bool isExecuting)
		{
			_isExecuting = isExecuting;
		}

		public new bool IsMonitoredBackup(hassio_onedrive_backup.Contracts.HassBackupsResponse.Backup backup)
		{
			return base.IsMonitoredBackup(backup);
		}
	}
}
