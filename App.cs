using System.Text;
using ProxyWorld;
using ProxyWorld.wit.imports.wasi.http.v0_2_0;
using Sha2;

namespace ProxyWorld.wit.exports.wasi.http.v0_2_0;

public class IncomingHandlerImpl: IIncomingHandler
{
    public static void Handle(ITypes.IncomingRequest request, ITypes.ResponseOutparam responseOut)
    {
            try {
        PollTaskScheduler.Factory.StartNew(async () => {
            await HandleAsync(request, responseOut);
        });
        PollTaskScheduler.Instance.Run();
            } catch (Exception e) {
                Console.WriteLine("caught {e}");
                throw e;
            }
    }

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

            try {
            using (var sink = new OutputStream(body.Write().AsOk)) {
                var tasks = new List<Task<(string, string)>>();
                foreach (var url in urls) {
                    tasks.Add(Sha256(url));
                }
                    Console.WriteLine("enter await foreach");                
                await foreach (var task in Task.WhenEach(tasks)) {
                    Console.WriteLine("task ready");                    
                    (var url, var sha) = await task;
                    Console.WriteLine($"url is {url}; sha is {sha}");
                    await sink.WriteAsync(Encoding.UTF8.GetBytes($"{url}: {sha}\n"));
                }
            }
            } catch (Exception e) {
                Console.WriteLine("caught {e}");
                throw e;
            }
        } else {
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
    static async Task<(string, string)> Sha256(string url)
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

        var response = await Send(request);
        var status = response.Status();
        if (status < 200 || status > 299) {
            return (url, $"unexpected status: {status}");
        }

            try {
        Console.WriteLine("dicej sha create");
        var sha = new Sha256();
        Console.WriteLine("dicej sha create complete");        
        var body = response.Consume().AsOk;
        try {
            using (var stream = new InputStream(body.Stream().AsOk)) {
                var buffer = new byte[16 * 1024];
                while (true) {
                    var count = await stream.ReadAsync(buffer);
                    Console.WriteLine($"got {count}");
                    if (count == 0) {
                        var hash = sha.GetHash();
                        hash.CopyTo(buffer, 0);
                        var hashString = BitConverter.ToString(buffer, hash.Count).Replace("-", "");
                        Console.WriteLine("returning");
                        return (url, hashString);
                    } else {
                        sha.AddData(buffer, 0, (uint) count);
                    }
                }
            }
        } finally {
            Console.WriteLine("finish");
            ITypes.IncomingBody.Finish(body);
            Console.WriteLine("finished");
        }
            } catch (Exception e) {
                Console.WriteLine("caught {e}");
                throw e;
            }
    }

    static async Task<ITypes.IncomingResponse> Send(ITypes.OutgoingRequest request)
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
