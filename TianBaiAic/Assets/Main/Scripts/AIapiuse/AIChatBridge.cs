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
        return TrySend(userText, null, null, null);
    }

    public static bool TrySend(
        string userText,
        Action<string> onStreamUpdate,
        Action<string> onComplete,
        Action<string> onError)
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

        _ = SendInternalAsync(userText, onStreamUpdate, onComplete, onError);
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

    private static async Task SendInternalAsync(
        string userText,
        Action<string> onStreamUpdate,
        Action<string> onComplete,
        Action<string> onError)
    {
        try
        {
            // Default UI behavior when caller doesn't provide callbacks.
            if (onStreamUpdate == null)
            {
                WebDialog.Dialog("Thinking...");
            }

            await _session.SendMessageAsync(
                userText,
                onStreamUpdate: text =>
                {
                    if (onStreamUpdate != null)
                    {
                        onStreamUpdate(text);
                    }
                    else
                    {
                        WebDialog.Dialog(text);
                    }
                },
                onComplete: text =>
                {
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        if (onComplete != null)
                        {
                            onComplete(string.Empty);
                        }
                        else
                        {
                            WebDialog.Dialog("Received an empty response.");
                        }
                        return;
                    }
                    if (onComplete != null)
                    {
                        onComplete(text);
                    }
                    else
                    {
                        WebDialog.Dialog(text);
                    }
                },
                onError: error =>
                {
                    StatusBox.Error(error);
                    if (onError != null)
                    {
                        onError(error);
                    }
                    else
                    {
                        WebDialog.Dialog("Request failed. Check launcher AI config.");
                    }
                });
        }
        catch (Exception e)
        {
            StatusBox.Error(e.Message);
            if (onError != null)
            {
                onError(e.Message);
            }
            else
            {
                WebDialog.Dialog("Request failed. Please try again.");
            }
        }
    }
}
