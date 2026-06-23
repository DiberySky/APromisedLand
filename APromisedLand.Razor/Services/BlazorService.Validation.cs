using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Razor.Services;

public partial class BlazorService
{
    public string? PasswordValidation(string pw, int length = 6)
    {
        if (string.IsNullOrWhiteSpace(pw))
        {
            return "请设置密码！";
        }
        if (pw.Length < length)
            return $"密码长度不能小于{length}位。";

        return default;
    }


}
