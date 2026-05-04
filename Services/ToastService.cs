using LIS.Models;

namespace LIS.Services;

public class ToastService
{
    public event Action? OnChange;
    private readonly List<ToastMessage> _toastMessages = new();

    public IReadOnlyList<ToastMessage> ToastMessages => _toastMessages.AsReadOnly();

    public void ShowToast(string message, ToastLevel level = ToastLevel.Info)
    {
        var toast = new ToastMessage { Message = message, Level = level };
        _toastMessages.Add(toast);
        OnChange?.Invoke();

        // Auto remove after 4 seconds
        _ = RemoveToastAsync(toast.Id);
    }

    public void RemoveToast(Guid id)
    {
        var toast = _toastMessages.FirstOrDefault(t => t.Id == id);
        if (toast != null)
        {
            _toastMessages.Remove(toast);
            OnChange?.Invoke();
        }
    }

    private async Task RemoveToastAsync(Guid id)
    {
        await Task.Delay(4000);
        RemoveToast(id);
    }
}
