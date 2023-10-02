using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;

namespace onedrive_backup.Sync
{
	public static class FileOperationHelper
	{
		public static string CalculateFileHash(string path)
		{
			using (var hasher = SHA256.Create())
			{
				using (var fileStream = File.OpenRead(path))
				{
					byte[] hash = hasher!.ComputeHash(fileStream);
					return BitConverter.ToString(hash).Replace("-", "");
				}
			}
		}
	}
}
