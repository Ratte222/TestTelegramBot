using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Json;
using System.Reflection;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using Telegram.Bot.Types.ReplyMarkups;
using TestTelegramBot.Configurations;

namespace TestTelegramBot
{
    public class Program
    {
        public static string appSettingsFileName = "appsettings.json";
        public static string jannaPhrasesFileName = "JannaPhrases.json";
        private static IConfiguration _config;
        private static TelegramBotClient Bot;
        private const string jannaFolderName = "JannaMessages";
        public static async Task Main(string[] args)
        {
            using var log = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Console()
                .WriteTo.File(
                new JsonFormatter(),
                        "./SLogs/SLog-.log",
                        encoding: System.Text.Encoding.UTF8,
                        buffered: true,
                        shared: false,
                        rollingInterval: RollingInterval.Day,
                        flushToDiskInterval: TimeSpan.FromSeconds(600),
                        fileSizeLimitBytes: 500000,
                        rollOnFileSizeLimit: true,
                        retainedFileCountLimit: 50)
                .CreateLogger();
            Log.Logger = log;
            try
            {
                BuildConfiguration();
                await StartBot();
                log.Information("The program finished");
            }
            catch (Exception ex)
            {
                log.Fatal(ex, "The program has dropped");
            }
            finally
            {
                log?.Dispose();
            }            
        }

        public static void BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile(appSettingsFileName)
                .AddJsonFile(jannaPhrasesFileName);
            _config = builder.Build();            
        }

        public static async Task StartBot()
        {
            TelegramConfig telegramConfig = _config.GetSection(nameof(TelegramConfig)).Get<TelegramConfig>();
            Bot = new TelegramBotClient(telegramConfig.Token);

            var me = await Bot.GetMeAsync();
            Console.Title = me.Username;

            var cts = new CancellationTokenSource();

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };
            Bot.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions,
                                cts.Token);
            while (true)
            {
                if (Console.ReadLine() == "stop")
                    break;
                else
                    Thread.Sleep(100);
            }
            cts.Cancel();
        }

        public static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            var handler = update.Type switch
            {
                //UpdateType.Unknown:
                // UpdateType.ChannelPost:
                // UpdateType.EditedChannelPost:
                // UpdateType.ShippingQuery:
                // UpdateType.PreCheckoutQuery:
                // UpdateType.Poll:
                UpdateType.Message => BotOnMessageReceived(update.Message),
                //UpdateType.EditedMessage => BotOnMessageReceived(update.EditedMessage),
                //UpdateType.CallbackQuery => BotOnCallbackQueryReceived(update.CallbackQuery),
                //UpdateType.InlineQuery => BotOnInlineQueryReceived(update.InlineQuery),
                //UpdateType.ChosenInlineResult => BotOnChosenInlineResultReceived(update.ChosenInlineResult),
                _ => UnknownUpdateHandlerAsync(update)
            };

            try
            {
                await handler;
            }
            catch (Exception exception)
            {
                await HandleErrorAsync(botClient, exception, cancellationToken);
            }
        }

        private static async Task BotOnMessageReceived(Message message)
        {
            Console.WriteLine($"Receive message type: {message.Type}");
            if (message.Type != MessageType.Text)
                return;
            var action = (message.Text.ToLower()) switch
            {
                "/start" => SendMessage(message, "Hello!"),
                "/help" => SendMessage(message, "Known commands: \r\nJanna\r\nЖанночка"),
                "janna" => JannaPhrase(message),
                "жанночка" => JannaPhrase(message),

                //_ => SendMessage(message, "unknown command")
            };
            if (action is null)
            {
                var sentMessage = await action;
                Console.WriteLine($"The message was sent with id: {sentMessage.MessageId}");
            }
        }

        private static Task UnknownUpdateHandlerAsync(Update update)
        {
            Console.WriteLine($"Unknown update type: {update.Type}");
            return Task.CompletedTask;
        }

        public static Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };

            Console.WriteLine(ErrorMessage);
            return Task.CompletedTask;
        }

        public static async Task<Message> SendMessage(Message message, string text)
        {
            return await Bot.SendTextMessageAsync(chatId: message.Chat.Id,
                                                  text: text,
                                                  replyMarkup: new ReplyKeyboardRemove());
        }

        public static async Task<Message> JannaPhrase(Message message)
        {
            //string pathToFolder = Path.Combine(
            //    Path.GetDirectoryName(Assembly.GetEntryAssembly().Location),
            //    jannaFolderName);
            //string[] messages = Directory.GetFiles(pathToFolder);
            Random random = new Random();
            //FileInfo fileInfo = new FileInfo(messages[random.Next(messages.Length)]);
            //return await SendImageMessage(message, fileInfo.Name, fileInfo.OpenRead());
            var phrases = _config.GetSection("JannaPhrases").Get<List<string>>();
            return await SendMessage(message, phrases[random.Next(phrases.Count - 1)]);
        }

        public static async Task<Message> SendImageMessage(Message message, string fileName, Stream stream)
        {

            InputOnlineFile inputOnlineFile = new InputOnlineFile(stream, fileName);
            return await Bot.SendPhotoAsync(chatId: message.Chat.Id,
                                            photo: inputOnlineFile);

        }
    }
}