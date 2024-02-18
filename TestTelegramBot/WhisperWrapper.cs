using CliWrap;
using CliWrap.Buffered;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestTelegramBot
{
    internal class WhisperWrapper
    {
        private readonly string _cliPath = "whisper"; // Path to the CLI program executable 

        public WhisperWrapper()
        {

        }
        public WhisperWrapper(string cliPath)
        {
            _cliPath = cliPath;
        }

        public async Task<string> ExecuteCliCommandAsync(WhisperArguments whisperArgs, CancellationToken cancellationToken)
        {
            try
            {
                if (!Directory.Exists(whisperArgs.OutputDirectory))
                    Directory.CreateDirectory(whisperArgs.OutputDirectory);
                var command = Cli.Wrap(_cliPath)
                    .WithArguments(args =>
                    {
                        args
                        .Add(whisperArgs.AudioFileName, true)
                        .Add("--model")
                        .Add(whisperArgs.Model, true)
                        .Add("--output_dir")
                        .Add(whisperArgs.OutputDirectory, true)
                        .Add("--output_format")
                        .Add(whisperArgs.OutputFormat, true);
                        //.Add("--verbose")
                        //.Add("VERBOSE");
                        if (!string.IsNullOrEmpty(whisperArgs.Language))
                        { 
                            args.Add("--language")
                            .Add(whisperArgs.Language);
                        }
                        if (!string.IsNullOrEmpty(whisperArgs.InitialPrompt))
                        {
                            args.Add("--initial_prompt")
                            .Add(whisperArgs.InitialPrompt, true);
                        }
                        //if (!string.IsNullOrWhiteSpace(whisperArgs.PrependPunctuation)) 
                        //{ 
                        // args.Add(" --prepend_punctuations") 
                        //.Add(whisperArgs.PrependPunctuation); 
                        //} 
                        //if (!string.IsNullOrWhiteSpace(whisperArgs.AppendPunctuation)) 
                        //{ 
                        // args.Add(" --append_punctuations") 
                        //.Add(whisperArgs.AppendPunctuation); 
                        //} 
                    });
                var result = await command
                    .ExecuteBufferedAsync(cancellationToken);

                if (result.ExitCode == 0)
                {
                    return result.StandardOutput;
                }
                else
                {
                    Console.WriteLine($"Error executing CLI command. Exit code: {result.ExitCode}");
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception occurred: {ex.Message}");
                return string.Empty;
            }
        }
    }
    public record WhisperArguments
    {
        public string Model { get; init; } = "small";
        public string OutputDirectory { get; init; } = Path.Combine(Directory.GetCurrentDirectory(), "whisper");
        public string OutputFormat { get; init; } = "txt";
        public string Language { get; init; } = "en";
        public string? InitialPrompt { get; init; } = string.Empty;
        public string AudioFileName { get; set; }
        public string? PrependPunctuation { get; init; } = "\"'“¿([{-";
        public string? AppendPunctuation { get; init; } = "\"'.。,，!！?？:：”)]}、";
    }
}
