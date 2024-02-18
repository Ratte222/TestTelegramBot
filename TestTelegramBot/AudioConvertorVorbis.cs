using NAudio.Vorbis;
using NAudio.Wave;
using NVorbis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestTelegramBot
{
    internal class AudioConvertorVorbis
    {
        public void ConvertOgaToWav_(Stream oggStream, string outputPath)
        {
            using (var reader = new VorbisWaveReader(oggStream))
            {
                WaveFileWriter.CreateWaveFile(outputPath, reader);
            }
        }

        public void ConvertOgaToWav(string inputPath, string outputPath)
        {
            using (var vorbisReader = new VorbisReader(inputPath))
            {
                using (var waveStream = WaveFormatConversionStream.CreatePcmStream(new VorbisWaveReader(inputPath)))
                {
                    using (var waveFileWriter = new WaveFileWriter(outputPath, waveStream.WaveFormat))
                    {
                        byte[] bytes = new byte[waveStream.Length];
                        waveStream.Read(bytes, 0, (int)waveStream.Length);
                        waveFileWriter.Write(bytes, 0, bytes.Length);
                    }
                }
            }
        }
    }
}
