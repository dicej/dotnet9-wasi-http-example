using System.Net.Http;
using System.Text;
using ProxyWorld;
using ProxyWorld.wit.imports.wasi.http.v0_2_0;
using ProxyWorld.wit.imports.wasi.io.v0_2_0;
using Sha2;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

public class IncomingHandlerImpl : IIncomingHandler
{
    /// <summary>Handle the specified incoming HTTP request and send a response
    /// via `responseOut`.</summary>
    public static void Handle(ITypes.IncomingRequest request, ITypes.ResponseOutparam responseOut)
    {
        var task = HandleAsync(request, responseOut);
        while (!task.IsCompleted)
        {
            WasiEventLoop.Dispatch();
        }
        var exception = task.Exception;
        if (exception is not null)
        {
            throw exception;
        }
    }

    /// <summary>Handle the specified incoming HTTP request and send a response
    /// via `responseOut`.</summary>
    static async Task HandleAsync(
        ITypes.IncomingRequest request,
        ITypes.ResponseOutparam responseOut
    )
    {
        var method = request.Method();
        var path = request.PathWithQuery();
        var headers = request.Headers().Entries();

        if (method.Tag == ITypes.Method.GET && path.Equals("/hash-all"))
        {
            // Collect one or more "url" headers, download their contents
            // concurrently, compute their SHA-256 hashes incrementally
            // (i.e. without buffering the response bodies), and stream the
            // results back to the client as they become available.

            var urls = new List<string>();
            foreach ((var key, var value) in headers)
            {
                if (key.Equals("url"))
                {
                    urls.Add(Encoding.UTF8.GetString(value));
                }
            }

            var responseHeaders = new List<(string, byte[])>
            {
                ("content-type", Encoding.UTF8.GetBytes("text/plain"))
            };
            var response = new ITypes.OutgoingResponse(ITypes.Fields.FromList(responseHeaders));
            var body = response.Body();
            ITypes.ResponseOutparam.Set(
                responseOut,
                Result<ITypes.OutgoingResponse, ITypes.ErrorCode>.ok(response)
            );

            using (var sink = new OutputStream(body.Write()))
            {
                var tasks = new List<Task<(string, string)>>();
                foreach (var url in urls)
                {
                    tasks.Add(Sha256Async(url));
                }
                await foreach (var task in Task.WhenEach(tasks))
                {
                    (var url, var sha) = await task;
                    await sink.WriteAsync(Encoding.UTF8.GetBytes($"{url}: {sha}\n"));
                }
            }
            ITypes.OutgoingBody.Finish(body, null);
        }
        else if (method.Tag == ITypes.Method.POST && path.Equals("/echo"))
        {
            // Echo the request body back to the client without arbitrary
            // buffering.

            var responseHeaders = new List<(string, byte[])>();
            foreach ((var key, var value) in headers)
            {
                if (key.Equals("content-type"))
                {
                    responseHeaders.Add((key, value));
                }
            }
            var response = new ITypes.OutgoingResponse(ITypes.Fields.FromList(responseHeaders));
            var responseBody = response.Body();
            ITypes.ResponseOutparam.Set(
                responseOut,
                Result<ITypes.OutgoingResponse, ITypes.ErrorCode>.ok(response)
            );

            var requestBody = request.Consume();
            try
            {
                using (var stream = new InputStream(requestBody.Stream()))
                {
                    using (var sink = new OutputStream(responseBody.Write()))
                    {
                        var buffer = new byte[16 * 1024];
                        while (true)
                        {
                            var count = await stream.ReadAsync(buffer);
                            if (count == 0)
                            {
                                break;
                            }
                            else
                            {
                                await sink.WriteAsync(buffer, 0, count);
                            }
                        }
                    }
                }
            }
            finally
            {
                ITypes.IncomingBody.Finish(requestBody);
                ITypes.OutgoingBody.Finish(responseBody, null);
            }
        }
        else
        {
            // Unsupported method and path; send 400 Bad Request.

            var response = new ITypes.OutgoingResponse(ITypes.Fields.FromList(new()));
            response.SetStatusCode(400);
            var body = response.Body();
            ITypes.ResponseOutparam.Set(
                responseOut,
                Result<ITypes.OutgoingResponse, ITypes.ErrorCode>.ok(response)
            );
            ITypes.OutgoingBody.Finish(body, null);
        }
    }

    /// <summary>Download the contents of the specified URL, computing the
    /// SHA-256 incrementally as the response body arrives.</summary>
    ///
    /// <remarks>This returns a tuple of the original URL and either the
    /// hex-encoded hash or an error message.</remarks>
    static async Task<(string, string)> Sha256Async(string url)
    {
        var sha = new Sha256();
        try
        {
            using (var client = new HttpClient())
            {
                client.Timeout = Timeout.InfiniteTimeSpan;
                using (var stream = await client.GetStreamAsync(url))
                {
                    var buffer = new byte[16 * 1024];
                    while (true)
                    {
                        var count = await stream.ReadAsync(buffer);
                        if (count == 0)
                        {
                            var hash = sha.GetHash();
                            hash.CopyTo(buffer, 0);
                            var hashString = BitConverter
                                .ToString(buffer, 0, hash.Count)
                                .Replace("-", "")
                                .ToLower();
                            return (url, hashString);
                        }
                        else
                        {
                            sha.AddData(buffer, 0, (uint)count);
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            return (url, e.ToString());
        }
    }
}
