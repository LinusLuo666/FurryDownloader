using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Xunit;

namespace FurryDownloader.Tests
{
    public class DownloadWorkerTests
    {
        [Fact]
        public void StopBeforeRequestDoesNotCreateAFile()
        {
            bool stopped = false;
            var task = new FurryDownloader.Task
            {
                Url = "http://127.0.0.1:1/image", FilePath = Path.GetTempPath(), FileName = Guid.NewGuid().ToString(),
                TaskStart = (id, item) => { }, TaskStop = (id, item) => stopped = true,
                TaskFinish = (id, item) => throw new Exception("Unexpected finish"), TaskFail = (id, item) => throw new Exception("Unexpected failure")
            };
            new TaskWorker(1, task).RunSynchronously(() => true, false);
            Assert.True(stopped);
            Assert.False(File.Exists(Path.Combine(task.FilePath, task.FileName)));
        }

        [Fact]
        public async System.Threading.Tasks.Task SuccessfulDownloadWritesCompleteFile()
        {
            var probe = new TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            int port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();
            string directory = Path.Combine(Path.GetTempPath(), "furry-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            using (var listener = new HttpListener())
            {
                listener.Prefixes.Add("http://127.0.0.1:" + port + "/");
                listener.Start();
                var server = System.Threading.Tasks.Task.Run(async () => {
                    var context = await listener.GetContextAsync();
                    byte[] data = { 1, 2, 3, 4 };
                    context.Response.ContentLength64 = data.Length;
                    await context.Response.OutputStream.WriteAsync(data, 0, data.Length);
                    context.Response.Close();
                });
                bool finished = false;
                var task = new FurryDownloader.Task {
                    Url = "http://127.0.0.1:" + port + "/image", FilePath = directory + Path.DirectorySeparatorChar, FileName = "image.bin",
                    TaskStart = (id, item) => { }, TaskStop = (id, item) => { },
                    TaskFinish = (id, item) => finished = true, TaskFail = (id, item) => { }
                };
                try {
                    await System.Threading.Tasks.Task.Run(() => new TaskWorker(1, task).RunSynchronously(() => false, false));
                    await server;
                    Assert.True(finished);
                    Assert.Equal(new byte[] { 1, 2, 3, 4 }, File.ReadAllBytes(Path.Combine(directory, "image.bin")));
                    Assert.Empty(Directory.GetFiles(directory, "*.part"));
                } finally { listener.Stop(); Directory.Delete(directory, true); }
            }
        }
    }
}
