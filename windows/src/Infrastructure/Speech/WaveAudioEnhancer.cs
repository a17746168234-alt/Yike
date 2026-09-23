using System;
using System.IO;
using System.Text;

namespace WindowsTranslator {
internal static class WaveAudioEnhancer
{
	internal static bool TryEnhance (string inputPath, out string outputPath)
	{
		outputPath = null;
		try {
			byte[] wave = File.ReadAllBytes (inputPath);
			if (wave.Length < 44 || Encoding.ASCII.GetString (wave, 0, 4) != "RIFF" || Encoding.ASCII.GetString (wave, 8, 4) != "WAVE") return false;
			int channels = 0;
			int bits = 0;
			int format = 0;
			int dataOffset = -1;
			int dataLength = 0;
			int offset = 12;
			while (offset + 8 <= wave.Length) {
				int size = BitConverter.ToInt32 (wave, offset + 4);
				if (size < 0 || offset + 8L + size > wave.Length) return false;
				string id = Encoding.ASCII.GetString (wave, offset, 4);
				if (id == "fmt " && size >= 16) {
					format = BitConverter.ToUInt16 (wave, offset + 8);
					channels = BitConverter.ToUInt16 (wave, offset + 10);
					bits = BitConverter.ToUInt16 (wave, offset + 22);
				} else if (id == "data") {
					dataOffset = offset + 8;
					dataLength = Math.Min (size, wave.Length - dataOffset);
					break;
				}
				offset += 8 + size + (size & 1);
			}
			if (format != 1 || bits != 16 || channels < 1 || channels > 2 || dataOffset < 0 || dataLength < channels * 2) return false;

			int samples = dataLength / 2;
			short[] filtered = new short[samples];
			double[] previousInput = new double[channels];
			double[] previousOutput = new double[channels];
			double energy = 0.0;
			double peak = 1.0;
			for (int i = 0; i < samples; i++) {
				int channel = i % channels;
				double input = BitConverter.ToInt16 (wave, dataOffset + i * 2);
				double highPassed = input - previousInput[channel] + 0.975 * previousOutput[channel];
				previousInput[channel] = input;
				previousOutput[channel] = highPassed;
				short value = (short)Math.Max (short.MinValue, Math.Min (short.MaxValue, Math.Round (highPassed)));
				filtered[i] = value;
				double absolute = Math.Abs ((double)value);
				if (absolute > 180.0) energy += absolute * absolute;
				if (absolute > peak) peak = absolute;
			}
			double rms = Math.Sqrt (energy / Math.Max (1, samples));
			double gain = rms < 1.0 ? 1.0 : Math.Min (6.0, 4200.0 / rms);
			gain = Math.Min (gain, 30000.0 / peak);
			if (gain < 1.0) gain = 1.0;
			for (int i = 0; i < samples; i++) {
				short value = (short)Math.Max (short.MinValue, Math.Min (short.MaxValue, Math.Round (filtered[i] * gain)));
				byte[] encoded = BitConverter.GetBytes (value);
				wave[dataOffset + i * 2] = encoded[0];
				wave[dataOffset + i * 2 + 1] = encoded[1];
			}
			outputPath = Path.Combine (Path.GetDirectoryName (inputPath), Path.GetFileNameWithoutExtension (inputPath) + ".enhanced.wav");
			File.WriteAllBytes (outputPath, wave);
			return true;
		} catch (IOException) {
			return false;
		} catch (UnauthorizedAccessException) {
			return false;
		} catch (ArgumentException) {
			return false;
		}
	}
}
}
