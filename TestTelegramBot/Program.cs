using CliWrap;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Formatting.Json;
using System;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Extensions.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TestTelegramBot.Configurations;

namespace TestTelegramBot
{
    public class Program
    {
        public static string appSettingsFileName = "appsettings.json";
        public static string jannaPhrasesFileName = "JannaPhrases.json";
        private static IConfiguration _config;
        private static TelegramBotClient botClient;
        private const string jannaFolderName = "JannaMessages";

        private static SemaphoreSlim semaphore = new SemaphoreSlim(1, 3);
        private static WhisperWrapper whisperWrapper = new WhisperWrapper();
        private static WhisperArguments whisperArguments = new WhisperArguments()
        {
            Language = string.Empty
        };
        private static CancellationTokenSource cancellationTokenSource =  new CancellationTokenSource();
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
            botClient = new TelegramBotClient(telegramConfig.Token);

            var me = await botClient.GetMeAsync(cancellationTokenSource.Token);
            Console.Title = me.Username;

            var receiverOptions = new ReceiverOptions
            {
                AllowedUpdates = { } // receive all update types
            };
            botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions,
                                cancellationTokenSource.Token);
            while (true)
            {
                if (Console.ReadLine() == "stop")
                    break;
                else
                    Thread.Sleep(100);
            }
            cancellationTokenSource.Cancel();
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
            if (message.Type == MessageType.Text)
            {
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
            else if(message.Type == MessageType.Voice)
            {
                await SpeechRecognitionVoiceMessage(message);
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
            return await botClient.SendTextMessageAsync(chatId: message.Chat.Id,
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

        //public static async Task<Message> SendImageMessage(Message message, string fileName, Stream stream)
        //{

        //    InputOnlineFile inputOnlineFile = new InputOnlineFile(stream, fileName);
        //    return await botClient.SendPhotoAsync(chatId: message.Chat.Id,
        //                                    photo: inputOnlineFile);

        //}

        private static async Task SpeechRecognitionVoiceMessage(Message message)
        {
            if (message.Voice != null)
            {
                var fileId = message.Voice.FileId;
                var file = await botClient.GetFileAsync(fileId);
                var audioDirectory = Path.Combine(Directory.GetCurrentDirectory(), "audio");
                if(!Directory.Exists(audioDirectory))
                    Directory.CreateDirectory(audioDirectory);
                var filePath = Path.Combine(audioDirectory, $"{file.FileId}.oga");
                var filePathWav = Path.Combine(audioDirectory, $"{file.FileId}.wav");
                var whisperResponsePath = Path.Combine(whisperArguments.OutputDirectory, $"{file.FileId}.json");
                //using (var saveImageStream = System.IO.File.Open(filePath, FileMode.Create,))
                //{
                //    await botClient.DownloadFileAsync(file.FilePath, saveImageStream, cancellationTokenSource.Token);
                //    await saveImageStream.FlushAsync();
                //    saveImageStream.Close();
                //}
                using (var saveImageStream = new System.IO.StreamWriter(filePath, false, Encoding.UTF8))
                {
                    await botClient.DownloadFileAsync(file.FilePath, saveImageStream.BaseStream, cancellationTokenSource.Token);
                    await saveImageStream.FlushAsync();
                    saveImageStream.Close();
                }
                //var audioConvertor = new AudioConvertorVorbis();
                //using (var fileStream = System.IO.File.OpenRead(filePath))
                //{
                //    audioConvertor.ConvertOgaToWav(fileStream, filePathWav);
                //}

                //var ac = new AudioConvertorOgg();
                //ac.ConvertOggToWav(filePath, filePathWav);

                //await Cli.Wrap("ffmpeg")
                //    .WithArguments(args=>
                //        args.Add("-i")
                //        .Add(filePath)
                //        .Add(filePathWav))
                //    .ExecuteAsync(cancellationTokenSource.Token);

                //await Task.Delay(1000);
                await semaphore.WaitAsync();
                try
                {
                    whisperArguments.AudioFileName =  filePath;
                    
                    var result = await whisperWrapper.ExecuteCliCommandAsync(whisperArguments, cancellationTokenSource.Token);
                    var processedResponse = ProcessWhisperAnswer(result);
                    if(!string.IsNullOrEmpty(processedResponse))
                    {
                        await botClient.SendTextMessageAsync(message.Chat.Id, processedResponse, replyToMessageId: message.MessageId);
                    }
                    else
                    {
                        string errorMessage = $"Problem with recognition message. result {result}";
                        Log.Logger.Warning(errorMessage);
                        Console.WriteLine(errorMessage);
                        await botClient.SendTextMessageAsync(message.Chat.Id, "Unfortunately, I can't recognize speech in this voice message", replyToMessageId: message.MessageId);
                    }
                }
                catch (Exception ex)
                {
                    Log.Logger.Error($"Some exception occur while program was working with whisper. {Environment.NewLine}Message: {ex.Message}{Environment.NewLine}StackTrace:{ex.StackTrace}");
                }
                finally
                {
                    if (System.IO.File.Exists(filePath))
                    { System.IO.File.Delete(filePath); }
                    if (System.IO.File.Exists(filePathWav))
                    { System.IO.File.Delete(filePathWav); }
                    if (System.IO.File.Exists(whisperResponsePath))
                    { System.IO.File.Delete(whisperResponsePath); }
                    semaphore.Release();
                }
            }
        }
        static string ProcessWhisperAnswer(string input)
        {
            if (input.Contains("[00"))
            {
                string pattern = @"\[.*?\]";
                string replacement = "";
                string result = Regex.Replace(input, pattern, replacement);
                result = DeleteLineContainingSubstring(result, "Detecting");
                result = DeleteLineContainingSubstring(result, "Detected");
                return result;
            }
            return null;
        }
        static string DeleteLineContainingSubstring(string text, string substring)
        {
            var lines = text.Split('\n');
            var filteredLines = lines.Where(line => !line.Contains(substring));
            return string.Join("\n", filteredLines);
        }
    }
}