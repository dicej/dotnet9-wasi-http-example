using System.Text;
using ProxyWorld;
using ProxyWorld.wit.imports.wasi.http.v0_2_0;
using Sha2;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

public class IncomingHandlerImpl: IIncomingHandler
{
    /// <summary>Handle the specified incoming HTTP request and send a response
    /// via `responseOut`.</summary>
    public static void Handle(ITypes.IncomingRequest request, ITypes.ResponseOutparam responseOut)
    {
        PollTaskScheduler.Factory.StartNew(async () => {
            await HandleAsync(request, responseOut);
        });
        PollTaskScheduler.Instance.Run();
    }

    /// <summary>Handle the specified incoming HTTP request and send a response
    /// via `responseOut`.</summary>
    static async Task HandleAsync(ITypes.IncomingRequest request, ITypes.ResponseOutparam responseOut)
    {
        var method = request.Method();
        var path = request.PathWithQuery().Value;
        var headers = request.Headers().Entries();

        if (method.Tag == ITypes.Method.GET && path.Equals("/hash-all")) {
            // Collect one or more "url" headers, download their contents
            // concurrently, compute their SHA-256 hashes incrementally
            // (i.e. without buffering the response bodies), and stream the
            // results back to the client as they become available.

            var urls = new List<string>();
            foreach ((var key, var value) in headers) {
                if (key.Equals("url")) {
                    urls.Add(Encoding.UTF8.GetString(value));
                }
            }
            
            var responseHeaders = new List<(string, byte[])> {
                ("content-type", Encoding.UTF8.GetBytes("text/plain"))
            };
            var response = new ITypes.OutgoingResponse(ITypes.Fields.FromList(responseHeaders).AsOk);
            var body = response.Body().AsOk;
            ITypes.ResponseOutparam.Set(responseOut, Result<ITypes.OutgoingResponse, ITypes.ErrorCode>.ok(response));

            using (var sink = new OutputStream(body.Write().AsOk)) {
                var tasks = new List<Task<(string, string)>>();
                foreach (var url in urls) {
                    tasks.Add(Sha256Async(url));
                }
                await foreach (var task in WhenEach.Iterate<Task<(string, string)>>(tasks)) {
                    (var url, var sha) = await task;
                    await sink.WriteAsync(Encoding.UTF8.GetBytes($"{url}: {sha}\n"));
                }
            }
            ITypes.OutgoingBody.Finish(body, Option<ITypes.Fields>.None);
            
        } else if (method.Tag == ITypes.Method.POST && path.Equals("/echo")) {
            // Echo the request body back to the client without arbitrary
            // buffering.

            var responseHeaders = new List<(string, byte[])>();
            foreach ((var key, var value) in headers) {
                if (key.Equals("content-type")) {
                    responseHeaders.Add((key, value));
                }
            }
            var response = new ITypes.OutgoingResponse(ITypes.Fields.FromList(responseHeaders).AsOk);
            var responseBody = response.Body().AsOk;
            ITypes.ResponseOutparam.Set(responseOut, Result<ITypes.OutgoingResponse, ITypes.ErrorCode>.ok(response));

            var requestBody = request.Consume().AsOk;
            try {
                using (var stream = new InputStream(requestBody.Stream().AsOk)) {
                    using (var sink = new OutputStream(responseBody.Write().AsOk)) {
                        var buffer = new byte[16 * 1024];
                        while (true) {
                            var count = await stream.ReadAsync(buffer);
                            if (count == 0) {
                                break;
                            } else {
                                await sink.WriteAsync(buffer, 0, count);
                            }
                        }
                    }
                }
            } finally {
                ITypes.IncomingBody.Finish(requestBody);
                ITypes.OutgoingBody.Finish(responseBody, Option<ITypes.Fields>.None);
            }
            
        } else {
            // Unsupported method and path; send 400 Bad Request.
            
            var response = new ITypes.OutgoingResponse(ITypes.Fields.FromList(new()).AsOk);
            response.SetStatusCode(400);
            var body = response.Body().AsOk;
            ITypes.ResponseOutparam.Set(responseOut, Result<ITypes.OutgoingResponse, ITypes.ErrorCode>.ok(response));
            ITypes.OutgoingBody.Finish(body, Option<ITypes.Fields>.None);
        }
    }

    /// <summary>Download the contents of the specified URL, computing the
    /// SHA-256 incrementally as the response body arrives.</summary>
    ///
    /// <remarks>This returns a tuple of the original URL and either the
    /// hex-encoded hash or an error message.</remarks>
    static async Task<(string, string)> Sha256Async(string url)
    {
        var uri = new Uri(url);

        ITypes.Scheme scheme;
        switch (uri.Scheme) {
            case "http":
                scheme = ITypes.Scheme.http();
                break;
            case "https":
                scheme = ITypes.Scheme.https();
                break;
            default:
                scheme = ITypes.Scheme.other(uri.Scheme);
                break;
        }

        var request = new ITypes.OutgoingRequest(ITypes.Fields.FromList(new()).AsOk);
        request.SetScheme(new(scheme));
        request.SetAuthority(new(uri.Authority));
        request.SetPathWithQuery(new(uri.PathAndQuery));

        var response = await SendAsync(request);
        var status = response.Status();
        if (status < 200 || status > 299) {
            return (url, $"unexpected status: {status}");
        }

        var sha = new Sha256();
        var body = response.Consume().AsOk;
        try {
            using (var stream = new InputStream(body.Stream().AsOk)) {
                var buffer = new byte[16 * 1024];
                while (true) {
                    var count = await stream.ReadAsync(buffer);
                    if (count == 0) {
                        var hash = sha.GetHash();
                        hash.CopyTo(buffer, 0);
                        var hashString = BitConverter.ToString(buffer, 0, hash.Count).Replace("-", "");
                        return (url, hashString);
                    } else {
                        sha.AddData(buffer, 0, (uint) count);
                    }
                }
            }
        } finally {
            ITypes.IncomingBody.Finish(body);
        }
    }

    /// <summary>Send the specified request and return the response.</summary>
    static async Task<ITypes.IncomingResponse> SendAsync(ITypes.OutgoingRequest request)
    {
        var future = OutgoingHandlerInterop.Handle(request, Option<ITypes.RequestOptions>.None).AsOk;

        while (true) {
            var response = future.Get();
            if (response.HasValue) {
                var value = response.Value.AsOk.AsOk;
                return value;
            } else {
                await PollTaskScheduler.Instance.Register(future.Subscribe());
            }
        }
    }
}
