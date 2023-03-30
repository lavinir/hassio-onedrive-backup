namespace onedrive_backup.Shared
{
	public static class ViewHelpers
	{
		public static string GetSpeedDisplayText(int? speedKbSec)
		{
			if (speedKbSec == null)
			{
				return "0 KB/s";
			}

			if (speedKbSec < 1024)
			{
				return $"{speedKbSec} KB/s";
			}
			else if (speedKbSec < 1024 * 1024)
			{
				return $"{speedKbSec / 1024} MB/s";
			}
			else
			{
				return $"{speedKbSec / (1024 * 1024)} GB/s";
			}
		}
	}
}
