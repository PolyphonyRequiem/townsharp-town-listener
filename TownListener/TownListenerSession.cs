using Townsharp.Infrastructure.Consoles;

using System.Speech.Recognition;
using System.Globalization;
using Townsharp.Infrastructure.WebApi;
using Townsharp.Infrastructure.Configuration;
using System.Speech.Recognition.SrgsGrammar;

namespace TownListener;

public class TownListenerSession
{
    static int count;

    SpeechRecognitionEngine recognizer;
    private readonly ConsoleClientFactory consoleClientFactory;
    private readonly WebApiUserClient webApiClient;
    Dictionary<string, Func<string, string>> aliases = new Dictionary<string, Func<string, string>>();

    CancellationTokenSource cancellation = new CancellationTokenSource();
    private IConsoleClient? consoleClient;

    public TownListenerSession(UserCredential userCredential)
    {
        aliases.Add("me", _ => userCredential.Username);
        aliases.Add("everyone", _ => "*");

        if (!string.IsNullOrEmpty(Config.Current.AliasFilePath) && File.Exists(Config.Current.AliasFilePath))
        {
            LoadAliasFile();
        }

        recognizer = new SpeechRecognitionEngine(new CultureInfo(Config.Current.Language));
        this.consoleClientFactory = new ConsoleClientFactory();
        this.webApiClient = new WebApiUserClient(userCredential);
    }

    void LoadAliasFile()
    {
        foreach (var line in File.ReadAllLines(Config.Current.AliasFilePath))
        {
            var splitLine = line.Split(',');

            aliases.Add(splitLine[0], _ => splitLine[1]);

            Console.WriteLine("Loaded Alias: {0} => {1}", splitLine[0], splitLine[1]);
        }
    }

    public async Task ConnectAndListen(int serverIdentifier)
    {
        await ConnectToServer(serverIdentifier);

        SetupVoiceRecognizer();

        StartVoiceRecognition();

        Console.WriteLine("Start Speaking, say quit to stop the application");

        await Task.Delay(-1, cancellation.Token);
    }

    async Task ConnectToServer(int serverIdentifier)
    {
        var accessRequest = await this.webApiClient.RequestConsoleAccessAsync(serverIdentifier);

        if (!accessRequest.IsSuccess)
        {
            throw new InvalidOperationException($"Failed while requesting console access for server {serverIdentifier}. Error provided is {accessRequest.ErrorMessage}");
        }

        Console.WriteLine("Got Connection details, Allowed: {0}", accessRequest.Content.allowed);

        if (!accessRequest.Content.allowed)
        {
            throw new Exception("Access not allowed.");
        }

        this.consoleClient = this.consoleClientFactory.CreateClient(accessRequest.Content.BuildConsoleUri(), accessRequest.Content.token!, _ => { });
    }

    void StartVoiceRecognition()
    {
        if (!Config.Current.ConsoleMode)
        {
            recognizer.SetInputToDefaultAudioDevice();
            recognizer.RecognizeAsync(RecognizeMode.Multiple);
        }
        else
        {
            Console.WriteLine("Starting in console mode, enter phrases in the console:");

            Task.Run(() =>
            {
                while (true)
                {
                    var result = recognizer.EmulateRecognize(Console.ReadLine());

                    if (result != null)
                    {
                        Console.WriteLine(result.Text);
                    }
                }
            });
        }
    }

    void SetupVoiceRecognizer()
    {
        recognizer = new SpeechRecognitionEngine(new CultureInfo(Config.Current.Language));

        // Create and load a dictation grammar.  
        //recognizer.LoadGrammar(new DictationGrammar());

        string filePath = Config.Current.GrammarFilePath;

        SrgsDocument doc = new SrgsDocument(filePath);

        Grammar grammar = new Grammar(doc);

        Console.WriteLine("Loaded Grammar: {0}", grammar.Name);

        recognizer.LoadGrammar(grammar);

        recognizer.SpeechRecognized += RecognizedSpeech;

        if (Config.Current.OverrideConfidence.HasValue)
        {
            recognizer.SpeechRecognitionRejected += RejectedSpeech;
        }
    }

    void RecognizedSpeech(object? sender, SpeechRecognizedEventArgs e)
    {
        string text = e.Result.Text;

        HandleRecognisedVoice(text);
    }

    void RejectedSpeech(object? sender, SpeechRecognitionRejectedEventArgs e)
    {
        string text = e.Result.Text;

        if (e.Result.Confidence > (Config.Current.OverrideConfidence ?? 0.0))
        {
            HandleRecognisedVoice(text);
        }
        else
        {
            Console.WriteLine("Failed recognizing phrase: {0}, confidence: {1}", text, e.Result.Confidence);
        }
    }

    void HandleRecognisedVoice(string text)
    {
        string lowered = text.ToLowerInvariant();

        if (lowered == "quit")
        {
            Stop();

            return;
        }

        string processed = PreProcessVoice(lowered);

        Console.WriteLine("Raw Speech: {0} converted: {1}", text, processed);

        string message = "{{\"id\":{0},\"content\":\"{1}\"}}";

        string data = string.Format(message, count++, processed);

        _ = this.consoleClient?.RunCommandAsync(data);
    }

    string PreProcessVoice(string text)
    {
        bool wasModified = false;

        string[] words = text.Split(' ');

        for (int i = 0; i < words.Length; i++)
        {
            if (aliases.TryGetValue(words[i], out Func<string, string>? convert))
            {
                words[i] = convert(words[i]);

                wasModified = true;
            }
        }

        if (!wasModified)
        {
            return text;
        }

        return string.Join(" ", words);
    }

    public void Stop()
    {
        Console.WriteLine("Stopping...");

        StopRecordingVoice();

        cancellation.Cancel();
    }

    void StopRecordingVoice()
    {
        recognizer.SpeechRecognitionRejected -= RejectedSpeech;
        recognizer.SpeechRecognized -= RecognizedSpeech;

        recognizer.Dispose();
    }
}