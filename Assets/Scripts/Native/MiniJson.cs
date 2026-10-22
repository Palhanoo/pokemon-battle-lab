using System.Collections.Generic;
using System.Globalization;
using System.Text;

// Minimal JSON -> Dictionary<string,object> / List<object> / double / string / bool / null.
public static class MiniJson
{
    public static object Parse(string s) { int i = 0; return Value(s, ref i); }

    static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

    static object Value(string s, ref int i)
    {
        Ws(s, ref i);
        char c = s[i];
        if (c == '{')
        {
            var d = new Dictionary<string, object>(); i++; Ws(s, ref i);
            if (s[i] == '}') { i++; return d; }
            while (true) { Ws(s, ref i); var k = Str(s, ref i); Ws(s, ref i); i++; d[k] = Value(s, ref i); Ws(s, ref i); if (s[i++] == '}') return d; }
        }
        if (c == '[')
        {
            var l = new List<object>(); i++; Ws(s, ref i);
            if (s[i] == ']') { i++; return l; }
            while (true) { l.Add(Value(s, ref i)); Ws(s, ref i); if (s[i++] == ']') return l; }
        }
        if (c == '"') return Str(s, ref i);
        if (string.CompareOrdinal(s, i, "true", 0, 4) == 0) { i += 4; return true; }
        if (string.CompareOrdinal(s, i, "false", 0, 5) == 0) { i += 5; return false; }
        if (string.CompareOrdinal(s, i, "null", 0, 4) == 0) { i += 4; return null; }
        int st = i;
        while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
        return double.Parse(s.Substring(st, i - st), CultureInfo.InvariantCulture);
    }

    static string Str(string s, ref int i)
    {
        var sb = new StringBuilder(); i++;
        while (s[i] != '"')
        {
            if (s[i] == '\\')
            {
                i++; char e = s[i];
                if (e == 'u') { sb.Append((char)int.Parse(s.Substring(i + 1, 4), NumberStyles.HexNumber)); i += 4; }
                else sb.Append(e == 'n' ? '\n' : e == 't' ? '\t' : e);
            }
            else sb.Append(s[i]);
            i++;
        }
        i++;
        return sb.ToString();
    }
}
