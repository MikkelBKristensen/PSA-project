using System.Globalization;

namespace Fuzzers.Grammar;

public class JSON
{
    public class KeyValue(string k, JsonValue v)
    {
        public override string ToString()
        {
            return $"\"{k}\":{v}";
        }
    }

    public class Object(Members? members)
    {
        public override string ToString()
        {
            return members is null ? "{}" : "{" + members + "}";
        }
    }

    public class Members(KeyValue[] keyValues)
    {
        public override string ToString()
        {
            if (keyValues.Length == 0) return "";
            string s = string.Empty;
            for (int i = 0; i < keyValues.Length-1; i++)
            {
                s += keyValues[i] + ",";
            }
            s += keyValues[^1];
            return s;
        }
    }

    public union JsonValue(string, int, float, bool, Array, Object, Null)
    {
        public override string ToString()
        {
            return Value switch
            {
                string s => $"\"{s}\"",
                int i => i.ToString(),
                float f => f.ToString(CultureInfo.InvariantCulture),
                bool b => b ? "true" : "false",
                Array a => a.ToString(),
                Object o => o.ToString(),
                Null n => n.ToString(),
                _ => "null"
            };
        }
    }

    public class Elements(JsonValue[] values)
    {
        public override string ToString()
        {
            if (values.Length == 0) return "";
            string s = string.Empty;
            for (int i = 0; i < values.Length-1; i++)
            {
                s += values[i] + ",";
            }
            s += values[^1];
            return s;
        }
    }

    public class Array(Elements elements)
    {
        public override string ToString()
        {
            return "[" + elements + "]";
        }
    }


    public class Null()
    {
        public override string ToString()
        {
            return "null";
        }
    }
}