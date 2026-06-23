using APromisedLand.Razor.Services;
using System;
using System.Collections.Generic;
using System.Text;

namespace APromisedLand.Razor.Components;

public partial class TestingDialog
{
    private async Task OnSubmitClick()
    {
        //if (_form == null)
        //{
        //    return;
        //}
        //try
        //{
        //    await _form.Validate();

        //    if (_form.IsValid == false) return;

        //    if (_newPasswordField?.Password != _confirmPasswordField?.Password)
        //    {
        //        Snackbar.Error("确认密码与新密码不一致。", this);
        //        return;
        //    }

        //    var dto = new ChangePasswordDto
        //    {
        //        Mobile = ProjectArgs.User?.Mobile,
        //        CurrentPassword = _currentPasswordField?.Password,
        //        NewPassword = _newPasswordField?.Password
        //    };

        //    await BlazorService.ChangePassword(dto, ProjectArgs.Api?.Uri);

        //    Snackbar.Info("密码修改成功。");

        //    DialogInstance?.Close();
        //}
        //catch (Exception err)
        //{
        //    Snackbar.Error(err.Message, this);
        //}
    }
}
