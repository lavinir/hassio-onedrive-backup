using hassio_onedrive_backup.Contracts;
using Newtonsoft.Json;
using onedrive_backup.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace test.onedrive_backup
{
	[TestClass]
	public class GeneralTests
	{
		[TestMethod]
		public void ValidateVersionMatch()
		{
			string config = File.ReadAllText("./config.yaml");
			var deserializer = new DeserializerBuilder()
				.Build();
			var configData = deserializer.Deserialize<Dictionary<string, object>>(config);
			Assert.IsTrue(configData["version"].ToString().Equals(AddonOptions.AddonVersion));
		}

		[TestMethod]
		public void VerifyChangeLogEntryForVersion()
		{
			string changelogContents = File.ReadAllText("./CHANGELOG.md");
			Assert.IsTrue(changelogContents.Contains($"## v{AddonOptions.AddonVersion}"));
		}

		[TestMethod]
		public void ConfigOptionsInDescriptions()
		{
			string config = File.ReadAllText("translations/en.yaml");
			var properties = GetAddonOptionsProperties();
			var deserializer = new DeserializerBuilder()
				.Build();
			var configData = deserializer.Deserialize<Dictionary<string, object>>(config);
			var configProperties = (Dictionary<object, object>)configData["configuration"];

			// Verify all props in en.yaml
			foreach (var property in properties)
			{
				Assert.IsTrue(configProperties.ContainsKey(property), $"{property} not found in en.yaml");
			}
		}

		[TestMethod]
		public void ConfigOptionsInReadme()
		{
			string config = File.ReadAllText("translations/en.yaml");
			string readme = File.ReadAllText("../../../../README.md");
			var deserializer = new DeserializerBuilder()
				.Build();
			var configData = deserializer.Deserialize<Dictionary<string, object>>(config);
			var configProperties = (Dictionary<object, object>)configData["configuration"];

			// Verify all props in en.yaml
			foreach (var property in configProperties)
			{
				var propDetails = (Dictionary<object, object>)property.Value;
				var propName = propDetails["name"].ToString();
				Assert.IsTrue(readme.Contains($"### {propName}") || readme.Contains($"### **{propName}"), $"{propName} not found in Readme");
			}
		}

		[TestMethod]
		public void SanitizedStringTests()
		{
			var dataSet = new Dictionary<string, string>
			{
                { "hass:backup:123.tar", "hass_backup_123.tar" },
                { "hass:backup*123.tar", "hass_backup_123.tar" },
                { "hass<backup*123.tar", "hass_backup_123.tar" },
                { "hass>backup??123.tar", "hass_backup__123.tar" },
                { "hass<backup*123//.tar", "hass_backup_123__.tar" },
                { "hass<backup*123||.tar", "hass_backup_123__.tar" },
                { "hass<backup*123\\.tar", "hass_backup_123_.tar" }
			};

			foreach (var ds in dataSet)
			{
				Assert.IsTrue(ds.Value.Equals(ds.Key.SanitizeString()), $"Original: {ds.Key}. Sanitized: {ds.Key.SanitizeString()}. Expected: {ds.Value}");
			}
		}

		private IEnumerable<string> GetAddonOptionsProperties()
		{
			PropertyInfo[] properties = typeof(AddonOptions).GetProperties(BindingFlags.Public | BindingFlags.Instance);

			foreach (PropertyInfo property in properties)
			{
				JsonPropertyAttribute attribute = property.GetCustomAttribute<JsonPropertyAttribute>();

				if (attribute != null)
				{
					yield return attribute.PropertyName;
				}
			}
		}
	}
}
