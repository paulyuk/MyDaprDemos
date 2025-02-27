using System.Text.Json.Serialization;

var app = WebApplication.Create(args);

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

string API_TOKEN = app.Configuration.GetValue("AI_TOKEN", "");
string ENDPOINT = app.Configuration.GetValue("AI_ENDPOINT", "");
app.Logger.LogInformation("Env vars: " + API_TOKEN + ", " + ENDPOINT);

// The full URL to the sentiment service
var apiURL = $"{ENDPOINT}text/analytics/v2.1/sentiment";

app.MapPost("/score", async (Tweet t) =>
{
    //if (t == null) throw new ArgumentNullException(nameof(t));
    _ = t ?? throw new ArgumentNullException(nameof(t));
    app.Logger.LogInformation(t.ToString());
    app.Logger.LogInformation($"processing tweet: {t.Author.Name}, {t.Language}, {t.Text}");

    // this allows the demo to run locally with no cloud resources provisioned.
    if(string.IsNullOrEmpty(ENDPOINT))
    {
       app.Logger.LogInformation("ENDPOINT is empty, returning 0.0");
       return new AnalyzedTweet(t, 0.0f);
    }

    var request = new HttpRequestMessage(HttpMethod.Post, new Uri(apiURL));
    request.Headers.Add("Ocp-Apim-Subscription-Key", API_TOKEN);
    request.Content = new StringContent($"{{documents: [{{id: \"1\", language: \"{t.Language}\", text: \"{t.Text}\"}},],}}");
    request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
    
    var result = await new HttpClient().SendAsync(request);
    var scores = await result.Content.ReadFromJsonAsync<ScoreResult>();

    if(scores == null)
    {
        app.Logger.LogTrace($"scores is null");
        return null;
    }

    if(scores.Documents == null)
    {
        app.Logger.LogTrace($"scores.Documents is null");
        return null;
    }

    if(scores.Documents.Length == 0)
    {
        app.Logger.LogTrace($"scores.Documents is empty");
        return null;
    }

    app.Logger.LogInformation($"tweet score: {scores.Documents[0].score}");

    // Score it
    return new AnalyzedTweet(t, scores.Documents[0].score);
});

await app.RunAsync();

app.Run();

public record TwitterUser([property: JsonPropertyName("screen_name")] string ScreenName,
                          [property: JsonPropertyName("profile_image_url_https")] string Picture,
                          string Name);

public record Tweet([property: JsonPropertyName("id_str")] string Id,
                    [property: JsonPropertyName("lang")] string Language,
                    [property: JsonPropertyName("user")] TwitterUser Author,
                    [property: JsonPropertyName("full_text")] string FullText,
                    [property: JsonPropertyName("text")] string Text);

public record AnalyzedTweet([property: JsonPropertyName("tweet")] Tweet Tweet,
                            float score);

public record ScoreResult(Document[] Documents);
public record Document(int id, float score);