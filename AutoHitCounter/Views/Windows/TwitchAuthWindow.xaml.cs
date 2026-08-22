//

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using AutoHitCounter.Models.Twitch;
using AutoHitCounter.Utilities;

namespace AutoHitCounter.Views.Windows;

/// <summary>
/// Shows the device code and waits for the user to approve it on twitch.tv. The polling itself
/// lives in the auth service; this window only drives it and reports the outcome.
/// </summary>
public partial class TwitchAuthWindow : Window
{
    private readonly CancellationTokenSource _cancellation = new CancellationTokenSource();
    private readonly Func<CancellationToken, Task<bool>> _awaitAuthorization;
    private readonly string _verificationUri;
    private readonly string _userCode;

    public TwitchAuthWindow(DeviceCodeResponse device, Func<CancellationToken, Task<bool>> awaitAuthorization)
    {
        InitializeComponent();

        _awaitAuthorization = awaitAuthorization;
        _userCode = device.UserCode ?? string.Empty;
        _verificationUri = string.IsNullOrWhiteSpace(device.VerificationUri)
            ? "https://www.twitch.tv/activate"
            : device.VerificationUri;

        CodeText.Text = _userCode;
        UriText.Text = _verificationUri;

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        User32.SetTopmost(hwnd);

        // Save the user a copy/paste round trip, then get out of the way.
        OpenTwitch();

        bool authorized;
        try
        {
            authorized = await _awaitAuthorization(_cancellation.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Waiting for the Twitch device authorization failed");
            authorized = false;
        }

        if (_cancellation.IsCancellationRequested) return;

        StatusText.Text = authorized
            ? "Connected."
            : "Authorization was not completed. Close this and try again.";

        DialogResult = authorized;
    }

    private void OpenTwitch()
    {
        try
        {
            Clipboard.SetDataObject(_userCode, false);
        }
        catch (Exception ex)
        {
            // A locked clipboard is not worth failing the whole flow over.
            Logger.Error(ex, "Could not copy the Twitch device code to the clipboard");
        }

        try
        {
            Process.Start(new ProcessStartInfo(_verificationUri) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not open the Twitch authorization page");
            StatusText.Text = "Open " + _verificationUri + " manually and enter the code.";
        }
    }

    private void OpenButton_Click(object sender, RoutedEventArgs e) => OpenTwitch();

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cancellation.Cancel();
        DialogResult = false;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => DragMove();

    protected override void OnClosed(EventArgs e)
    {
        _cancellation.Cancel();
        _cancellation.Dispose();
        base.OnClosed(e);
    }
}
