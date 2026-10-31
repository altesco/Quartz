using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShadUI;

namespace Quartz.UI.ViewModels;

public partial class InputDialogVM : ObservableObject
{
    private readonly DialogManager _dialogManager;
    private readonly Func<string, (bool IsValid, string? ErrorMessage)>? _validator;
    private readonly Action<string>? _onConfirmed;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _description;

    [ObservableProperty]
    private string _value = string.Empty;

    [ObservableProperty]
    private string _placeholder = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _primaryButtonText = "OK";

    [ObservableProperty]
    private string _cancelButtonText = "Отмена";

    public InputDialogVM(
        DialogManager dialogManager,
        string title,
        string? description,
        string initialValue,
        string placeholder,
        string primaryButtonText,
        Func<string, (bool IsValid, string? ErrorMessage)>? validator,
        Action<string>? onConfirmed)
    {
        _dialogManager = dialogManager;
        Title = title;
        Description = description;
        Value = initialValue;
        Placeholder = placeholder;
        PrimaryButtonText = primaryButtonText;
        _validator = validator;
        _onConfirmed = onConfirmed;
    }

    partial void OnValueChanged(string value)
    {
        Validate();
    }

    public bool Validate()
    {
        if (_validator != null)
        {
            var (isValid, error) = _validator(Value);
            ErrorMessage = isValid ? null : error;
            return isValid;
        }

        ErrorMessage = null;
        return true;
    }

    [RelayCommand]
    public void Confirm()
    {
        if (!Validate())
            return;

        _onConfirmed?.Invoke(Value?.Trim() ?? string.Empty);
        _dialogManager.Close(this, new CloseDialogOptions { Success = true });
    }

    [RelayCommand]
    public void Cancel()
    {
        _dialogManager.Close(this, new CloseDialogOptions { Success = false });
    }
}
