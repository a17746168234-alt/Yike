using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace WindowsTranslator {
internal static class Utf8PipeReader
{
    // StreamReader.ReadAsync tries to fill the requested character buffer. A pipe
    // must instead deliver each available byte block, including partial UTF-8.
    internal static async Task Read (Stream stream, Action<string> receive)
    {
        byte[] bytes = new byte[1024];
        char[] chars = new char[1024];
        Decoder decoder = Encoding.UTF8.GetDecoder ();
        int count;
        while ((count = await stream.ReadAsync (bytes, 0, bytes.Length).ConfigureAwait (false)) > 0) {
            int decoded = decoder.GetChars (bytes, 0, count, chars, 0, false);
            if (decoded > 0) receive (new string (chars, 0, decoded));
        }
        int tail = decoder.GetChars (bytes, 0, 0, chars, 0, true);
        if (tail > 0) receive (new string (chars, 0, tail));
    }
}
}
