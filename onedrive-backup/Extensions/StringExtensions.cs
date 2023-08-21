namespace onedrive_backup.Extensions
{
	public static class StringExtensions
	{
		public static string StripLeadingSlash(this string str)
		{
			if (str.StartsWith("/") && str.Length >= 1)
			{
				return str.Substring(1);
			}

			return str;
		}
	}
}
