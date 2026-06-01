using System;
using System.Threading.Tasks;
using UnityEngine;

public static class AIChatBridge
{
    private static AISession _session;
    private static bool _isInitializing;

    public static void ReloadConfig()
    {
        _session = null;
    }

    public static bool TrySend(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            return true;
        }

        if (!EnsureSession(out string reason))
        {
            Debug.LogWarning($"AIChatBridge disabled: {reason}");
            return false;
        }

        _ = SendInternalAsync(userText);
        return true;
    }

    private static bool EnsureSession(out string reason)
    {
        reason = null;
        if (_session != null)
        {
            return true;
        }

        if (_isInitializing)
        {
            reason = "AI session is initializing";
            return false;
        }

        _isInitializing = true;
        try
        {
            if (!AIConfigLoader.TryLoad(out AIConfig config, out reason))
            {
                return false;
            }

            _session = new AISession(config, config.BuildDefaultSessionSettings());
            return true;
        }
        finally
        {
            _isInitializing = false;
        }
    }

    private static async Task SendInternalAsync(string userText)
    {
        try
        {
            WebDialog.Dialog("Thinking...");

            await _session.SendMessageAsync(
                userText,
                onStreamUpdate: text => WebDialog.Dialog(text),
                onComplete: text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        WebDialog.Dialog("Received an empty response.");
                        return;
                    }
                    WebDialog.Dialog(text);
                },
                onError: error =>
                {
                    StatusBox.Error(error);
                    WebDialog.Dialog("Request failed. Check launcher AI config.");
                });
        }
        catch (Exception e)
        {
            StatusBox.Error(e.Message);
            WebDialog.Dialog("Request failed. Please try again.");
        }
    }
}
