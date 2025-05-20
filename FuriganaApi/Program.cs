using System.Text.RegularExpressions;
using MeCab;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowLocalhost",
        policy => policy
            .WithOrigins("http://localhost:56595", "http://127.0.0.1:56595") // Your frontend origin(s)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

var app = builder.Build();

app.UseCors("AllowLocalhost");

app.MapPost("/api/furigana", (TextRequest request) =>
{
    var param = new MeCabParam
    {
        DicDir = @"C:\Users\SL-135\Desktop\IPA" // Update this to your UniDic path
    };

    var tokens = new List<Token>();
    using var tagger = MeCabTagger.Create(param);
    var nodes = tagger.ParseToNodes(request.Text);

    foreach (var node in nodes)
    {
        if (node.Stat != MeCabNodeStat.Bos && node.Stat != MeCabNodeStat.Eos)
        {
            string surface = node.Surface;
            string feature = node.Feature;
            var features = feature.Split(',');

            string readingKatakana = features.Length > 6 ? features[6] : "";
            string readingHiragana = Helpers.KatakanaToHiragana(readingKatakana);

            var token = new Token
            {
                Word = surface,
                Furigana = (Helpers.ContainsKanji(surface) && !string.IsNullOrWhiteSpace(readingHiragana)) ? readingHiragana : ""
            };

            tokens.Add(token);
        }
    }

    return Results.Ok(new { data = tokens});
});

app.MapPost("/api/furigana/single", (TextRequest request) =>
{
    var param = new MeCabParam
    {
        DicDir = @"C:\Users\SL-135\Desktop\IPA" // Update to your UniDic path
    };

    var charTokens = new List<Token>();
    using var tagger = MeCabTagger.Create(param);
    var nodes = tagger.ParseToNodes(request.Text);

    foreach (var node in nodes)
    {
        if (node.Stat != MeCabNodeStat.Bos && node.Stat != MeCabNodeStat.Eos)
        {
            string surface = node.Surface;
            string feature = node.Feature;
            var features = feature.Split(',');

            string readingKatakana = features.Length > 6 ? features[6] : "";
            string readingHiragana = Helpers.KatakanaToHiragana(readingKatakana);

            // Distribute reading to each character (naively)
            var chars = surface.ToCharArray();
            var readingChars = readingHiragana.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                var c = chars[i];
                var token = new Token
                {
                    Word = c.ToString(),
                    Furigana = (Helpers.ContainsKanji(c.ToString()) && i < readingChars.Length)
                        ? readingChars[i].ToString()
                        : ""
                };
                charTokens.Add(token);
            }
        }
    }

    return Results.Ok(new { data = charTokens });
});


app.Run();

record TextRequest(string Text);

record Token
{
    public string Word { get; set; } = "";
    public string Furigana { get; set; } = "";
}

static class Helpers
{
    public static string KatakanaToHiragana(string katakana) =>
        new string(katakana.Select(c =>
            (c >= 'ァ' && c <= 'ン') ? (char)(c - 0x60) : c
        ).ToArray());

    public static bool ContainsKanji(string text) =>
        Regex.IsMatch(text, @"\p{IsCJKUnifiedIdeographs}");
}
