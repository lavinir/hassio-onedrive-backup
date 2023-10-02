using Newtonsoft.Json.Converters;
using System.Text.Json.Serialization;

namespace onedrive_backup.Contracts
{
	public interface IBackup
	{
		public DateTime BackupDate { get; }

		public string Slug { get; set; }

		public string Type { get; set; }

		public float Size { get; set; }

		[JsonConverter(typeof(StringEnumConverter))]
		public BackupTypeDisplayName TypeDisplayName { get; }
    }

	public enum BackupTypeDisplayName
	{
		Online,
		Local,
	}
}
