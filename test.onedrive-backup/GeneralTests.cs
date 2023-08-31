using hassio_onedrive_backup.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
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
	}
}
