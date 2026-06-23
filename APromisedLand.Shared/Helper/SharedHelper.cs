using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Shared.Helper;

public static partial class SharedHelper
{
    public static string Ellipsis(this string text, int length = 10)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        return text.Length < length ? text : $"{text[..length]}...";
    }
}
