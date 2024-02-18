using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestTelegramBot
{
    internal class WhisperWeb
    {
        private readonly HttpClient client = new HttpClient();
        private readonly string _url = "http://localhost:9000/asr?encode=true&task=transcribe&word_timestamps=false&output=txt";

        public async Task<string> SendPostRequestAsync(string filePath)
        {
            var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(File.ReadAllBytes(filePath));
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data");

            content.Add(fileContent, "audio_file", Path.GetFileName(filePath));

            var response = await client.PostAsync(_url, content);

            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
                
            }
            else
            {
                return $"Failed to POST data. Status code: {response.StatusCode}";
            }
        }
    }
}
