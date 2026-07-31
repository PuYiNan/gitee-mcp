namespace GiteeManager.Tests;

/// <summary>测试用 HttpMessageHandler：捕获请求并返回预设响应，全程不发起真实网络请求。</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static HttpClient CreateClient(FakeHttpMessageHandler handler, string baseAddress)
    {
        var client = new HttpClient(handler);
        client.BaseAddress = new Uri(baseAddress);
        return client;
    }

    public static HttpResponseMessage JsonResponse(int statusCode, string json)
    {
        var response = new HttpResponseMessage((System.Net.HttpStatusCode)statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
        return response;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_responder(request));
    }
}
