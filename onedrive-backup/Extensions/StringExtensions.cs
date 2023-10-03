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

        public static string SanitizeString(this string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return input; 
            }

            char[] illegalChars = { '*', ':', '<', '>', '?', '/', '\\', '|' };

            string sanitizedString = new string(input.Select(c => illegalChars.Contains(c) ? '_' : c).ToArray());

            return sanitizedString;
        }
    }
}
