using Amazon;
using Amazon.Translate;
using Amazon.Translate.Model;
using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

class Program
{
    static Dictionary<long, string> UserMessages = [];
    static List<Language> Languages = [];

    static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        string botToken = config["TelegramBotToken"];

        using var client = new AmazonTranslateClient(config["AWSAccessKey"], config["AWSSecretKey"], RegionEndpoint.GetBySystemName(config["AWSRegion"]));

        var response = await client.ListLanguagesAsync(new ListLanguagesRequest());
        Languages = response.Languages;

        var botClient = new TelegramBotClient(botToken);
        using var cts = new CancellationTokenSource();

        var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
        botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, cts.Token);

        Console.WriteLine("Bot launched! Waiting for a message...");
        await Task.Delay(-1);
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message?.Text != null)
        {
            long chatId = update.Message.Chat.Id;
            string text = update.Message.Text;

            if (!UserMessages.ContainsKey(chatId))
            {
                UserMessages[chatId] = text;

                await botClient.SendTextMessageAsync(chatId, "Choose a language for translation:", replyMarkup: GetLanguageButtons());
            }
        }
        else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery?.Data != null)
        {
            long chatId = update.CallbackQuery.Message.Chat.Id;
            string selectedLang = update.CallbackQuery.Data;

            if (UserMessages.TryGetValue(chatId, out string textToTranslate))
            {
                string translatedText = await TranslateText(textToTranslate, selectedLang);

                await botClient.SendTextMessageAsync(chatId, $"{selectedLang}:\n{translatedText}");
                UserMessages.Remove(chatId);
            }
        }
    }

    static async Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
    }

    static async Task<string> TranslateText(string text, string targetLang)
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        using var client = new AmazonTranslateClient(config["AWSAccessKey"], config["AWSSecretKey"], RegionEndpoint.GetBySystemName(config["AWSRegion"]));

        var request = new TranslateTextRequest
        {
            Text = text,
            SourceLanguageCode = "auto",
            TargetLanguageCode = targetLang
        };

        var response = await client.TranslateTextAsync(request);
        return response.TranslatedText;
    }

    static InlineKeyboardMarkup GetLanguageButtons()
    {
        var buttons = Languages
            .Where(x => x.LanguageCode != "ru" && x.LanguageCode != "auto")
            .Select(lang => InlineKeyboardButton.WithCallbackData(lang.LanguageName, lang.LanguageCode))
            .Chunk(3) 
            .Select(chunk => chunk.ToArray())
            .ToArray();

        return new InlineKeyboardMarkup(buttons);
    }
}
