using System;
using System.Collections.Generic;

namespace WindowsTranslator {
internal static class SpeechChunks
{
    internal static string[] Split (string text)
    {
        if (string.IsNullOrWhiteSpace (text)) return new string[0];
        bool wide = false;
        foreach (char c in text) if (c >= '\u3000') { wide = true; break; }
        List<string> chunks = new List<string> ();
        int start = 0;
        while (start < text.Length) {
            int limit = wide ? (chunks.Count == 0 ? 40 : 180) : (chunks.Count == 0 ? 96 : 400);
            int end = Math.Min (text.Length, start + limit);
            if (end < text.Length) {
                int sentence = -1, space = -1;
                for (int i = start; i < end; i++) {
                    char c = text[i];
                    if (char.IsWhiteSpace (c)) space = i + 1;
                    if ("。！？!?;；\n".IndexOf (c) >= 0 || (c == '.' && i + 1 < text.Length && char.IsWhiteSpace (text[i + 1]))) sentence = i + 1;
                }
                if (sentence > start) end = sentence;
                else if (space > start) end = space;
                if (char.IsHighSurrogate (text[end - 1])) end--;
            }
            chunks.Add (text.Substring (start, end - start));
            start = end;
        }
        for (int i = chunks.Count - 1; i >= 0; i--) {
            if (!string.IsNullOrWhiteSpace (chunks[i])) continue;
            if (i > 0) chunks[i - 1] += chunks[i];
            else chunks[i + 1] = chunks[i] + chunks[i + 1];
            chunks.RemoveAt (i);
        }
        return chunks.ToArray ();
    }
}
}
