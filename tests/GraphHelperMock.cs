using Azure.Identity;
using hassio_onedrive_backup.Graph;
using Microsoft.Graph;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace tests
{
    public class GraphHelperMock : GraphHelper
    {
        public GraphHelperMock(IEnumerable<string> scopes, string clientId, Func<DeviceCodeInfo, CancellationToken, Task> deviceCodePrompt, string persistentDataPath = "") : 
            base(scopes, clientId, deviceCodePrompt, persistentDataPath)
        {
        }

        protected override Task InitializeGraphForUserAuthAsync()
        {
            //_userClient
            var graphServiceClietnMock = new Mock<GraphServiceClient>();
            _userClient = graphServiceClietnMock.Object;
            return Task.CompletedTask;
        }
    }
}
