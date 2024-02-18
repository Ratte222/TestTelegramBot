using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestTelegramBot
{
    internal class AudioConvertorOgg
    {

        public void ConvertOggToWav(string inputPath, string outputPath)
        {

            var decoder = new OpusDecoder(16000, 1);
            using(var fileReader = File.OpenRead(inputPath))
            {
                using(var sw = File.OpenWrite(outputPath))
                {
                    var opus = new OpusOggReadStream(decoder, fileReader);

                    while (opus.HasNextPacket)
                    {
                        short[] packet = opus.DecodeNextPacket();
                        if (packet != null)
                        {
                            for (int i = 0; i < packet.Length; i++)
                            {
                                var bytes = BitConverter.GetBytes(packet[i]);
                                sw.Write(bytes);
                            }
                        }
                    }
                    sw.Flush();
                    sw.Close();
                }    
            }
            
            
        }

        //public void ConvertOgaToWav(string inputPath, string outputPath)
        //{
        //    using (var fileStream = File.OpenRead(inputPath))
        //    {
        //        using (var reader = new Concentus.Oggfile.OpusOggReadStream())
        //        {
        //            reader.ReadSamples(fileStream, out float[] samples, out int channels, out int sampleRate);

        //            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        //            using (var waveFileWriter = new WaveFileWriter(outputPath, waveFormat))
        //            {
        //                waveFileWriter.WriteSamples(samples, 0, samples.Length);
        //            }
        //        }
        //    }
        //}
    }
}
