namespace GiteeManager.Tests;

/// <summary>测试用 HttpMessageHandler：捕获请求并返回预设响应，全程不发起真实网络请求。</summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public List<HttpRequestMessage> Requests { get; } = [];

    /// <summary>请求体快照（发送时读取，避免请求被 dispose 后无法读取）。与 Requests 同序。</summary>
    public List<string?> RequestBodies { get; } = [];

    /// <summary>取唯一请求体并断言非空（消除 nullable 警告）。</summary>
    public string RequireSingleBody()
    {
        var body = Assert.Single(RequestBodies);
        return body ?? throw new InvalidOperationException("预期存在请求体，实际为空");
    }

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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string? body = null;
        if (request.Content is not null)
        {
            body = await request.Content.ReadAsStringAsync(cancellationToken);
        }
        Requests.Add(request);
        RequestBodies.Add(body);
        return _responder(request);
    }
}
