using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Helix.Models.Users.GetUsers;
using TwitchLib.EventSub.Core.Models.Chat;
using TwitchLib.EventSub.Core.SubscriptionTypes.Channel;
using TwitchLib.EventSub.Websockets;
using TwitchLib.EventSub.Websockets.Core.EventArgs;
using TwitchLib.EventSub.Websockets.Core.EventArgs.Channel;
using TransparentTwitchChatWPF.Utils;
using TransparentTwitchChatWPF.Helpers;

namespace TransparentTwitchChatWPF.Twitch;

public class TwitchService : IHostedService, IDisposable
{
    private readonly ILogger<TwitchService> _logger;
    private readonly TwitchAPI _api;
    private readonly EventSubWebsocketClient _eventSubWebsocketClient;
    private string _oauthUserId = string.Empty;
    private string _broadcasterUserId = string.Empty;

    public event EventHandler<AccessTokenValidatedEventArgs> AccessTokenValidated;
    public event EventHandler<TwitchUserDataEventArgs> UserDataFetched;
    public event EventHandler<ChannelPointsCustomRewardRedemptionArgs> ChannelPointsRewardRedeemed;
    public event EventHandler<TwitchChatMessageEventArgs> ChatMessageReceived;
    public event EventHandler<TwitchChatMessageDeleteEventArgs> ChatMessageDeleted;
    public event EventHandler ChatCleared;
    public event EventHandler<TwitchChatClearUserEventArgs> ChatUserCleared;

    public string AuthTokenExpiration { get; private set; } = "...";
    public string TwitchConnectionStatus { get; private set; } = "Not Connected";
    public BitmapImage ProfileImage { get; private set; }

    private bool _isEventSubInit = false;
    private readonly Random _random = new Random();

    private const int MaxReconnectAttempts = 7;
    private const int BaseDelayMilliseconds = 1000;
    private const int MaxDelayMilliseconds = 60000;

    private bool _disposedValue;

    public TwitchService(ILogger<TwitchService> logger,
        EventSubWebsocketClient eventSubWebsocketClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("TwitchService as IHostedService constructed!");
        _eventSubWebsocketClient = eventSubWebsocketClient ?? throw new ArgumentNullException(nameof(eventSubWebsocketClient));
        _api = new TwitchAPI();
        _api.Settings.ClientId = "yv4bdnndvd4gwsfw7jnfixp0mnofn7";
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_isEventSubInit)
        {
            _logger.LogInformation("EventSub wasn't initialized yet, skipping StartAsync.");
            return;
        }

        _logger.LogInformation("Starting _eventSubWebsocketClient...");
        var token = App.Settings.GeneralSettings.OAuthToken;
        var userId = App.Settings.GeneralSettings.ChannelID;

        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(userId))
        {
            _logger.LogWarning("Twitch credentials are not set in App.SettingsObject");
            return;
        }

        _api.Settings.AccessToken = token;
        _oauthUserId = userId;

        if (!await EnsureBroadcasterUserIdAsync())
        {
            _logger.LogWarning("Could not resolve broadcaster user id for channel.");
            return;
        }

        await _eventSubWebsocketClient.ConnectAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping _eventSubWebsocketClient...");
        await _eventSubWebsocketClient.DisconnectAsync();
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("InitializeAsync()");
        await ValidateTwitchConnectionAsync();
    }

    private async Task ValidateTwitchConnectionAsync()
    {
        var oAuth = App.Settings.GeneralSettings.OAuthToken;
        if (!string.IsNullOrEmpty(oAuth))
        {
            _api.Settings.AccessToken = oAuth;
            await FetchUserDataAsync();
            await ValidateAuthTokenAsync(oAuth);
            InitEventSub();
        }
    }

    private async void TwitchConnection_AccessTokenResponse(object? sender, string e)
    {
        App.Settings.GeneralSettings.OAuthToken = e;
        App.Settings.Persist();

        _api.Settings.AccessToken = e;
        await FetchUserDataAsync();
        await ValidateAuthTokenAsync(e);
        InitEventSub();
    }

    private async Task FetchUserDataAsync()
    {
        try
        {
            var getUser = await _api.Helix.Users.GetUsersAsync();
            GetUsersResponseCallback(getUser);
        }
        catch (Exception)
        {
            Growl.Warning("The Twitch connection may be invalid or expired, try reconnecting in settings.");
        }
    }

    private void GetUsersResponseCallback(GetUsersResponse e)
    {
        _logger.LogInformation("GetUsersResponseCallback");

        string userName = e.Users[0].DisplayName;
        string userID = e.Users[0].Id;
        string profileImageUrl = e.Users[0].ProfileImageUrl;

        TwitchConnectionStatus = $"Connected as {userName} ({userID})";
        ProfileImage = ImageHelpers.LoadFromUrl(profileImageUrl);

        App.Settings.GeneralSettings.ChannelID = userID;
        _oauthUserId = userID;

        UserDataFetched?.Invoke(this, new TwitchUserDataEventArgs
        {
            DisplayName = userName,
            UserId = userID,
            ProfileImageUrl = profileImageUrl,
            ProfileImage = ProfileImage
        });
    }

    private async Task ValidateAuthTokenAsync(string accessToken)
    {
        try
        {
            var t = await _api.Auth.ValidateAccessTokenAsync(accessToken);
            if (t != null)
            {
                AuthTokenValidatedCallback(t);
            }
            else
            {
                Growl.Warning("Could not validate the access token. The Twitch connection may need to be reconnected in settings.");
            }
        }
        catch (Exception)
        {
            Growl.Warning("Auth token invalid. The Twitch connection may be expired, try reconnecting in settings.");
        }
    }

    private void AuthTokenValidatedCallback(ValidateAccessTokenResponse e)
    {
        _logger.LogInformation("AuthTokenValidatedCallback");

        TimeSpan timeSpan = TimeSpan.FromSeconds(e.ExpiresIn);
        string expires = string.Format("{0} Days {1} Hours", timeSpan.Days, timeSpan.Hours);
        AuthTokenExpiration = "Expires in: " + expires;

        AccessTokenValidated?.Invoke(this, new AccessTokenValidatedEventArgs
        {
            Expires = expires,
            Login = e.Login,
            UserId = e.UserId,
            ClientId = e.ClientId,
        });
    }

    public bool ShouldUseEventSubChat()
    {
        return App.Settings.GeneralSettings.UseEventSubChat
            && !string.IsNullOrWhiteSpace(App.Settings.GeneralSettings.OAuthToken)
            && !string.IsNullOrWhiteSpace(App.Settings.GeneralSettings.ChannelID);
    }

    private void InitEventSub()
    {
        if (_isEventSubInit) return;

        bool wantsChat = ShouldUseEventSubChat();
        bool wantsRedemptions = App.Settings.GeneralSettings.RedemptionsEnabled;

        if (!wantsChat && !wantsRedemptions)
        {
            _logger.LogInformation("EventSub chat and redemptions are both disabled. Skipping initialization.");
            return;
        }

        if (string.IsNullOrEmpty(App.Settings.GeneralSettings.ChannelID))
        {
            _logger.LogWarning("OAuth user ID is not set. Cannot initialize EventSub.");
            Growl.Warning("Please connect your Twitch account in settings before enabling EventSub chat.");
            return;
        }

        if (string.IsNullOrEmpty(App.Settings.GeneralSettings.OAuthToken))
        {
            _logger.LogWarning("OAuth Token is not set. Cannot initialize EventSub.");
            Growl.Warning("Please connect your Twitch account in settings before enabling EventSub chat.");
            return;
        }

        _logger.LogInformation("Initializing Twitch EventSub WebSocket client.");

        UnsubscribeFromEvents();

        _eventSubWebsocketClient.WebsocketConnected += OnWebsocketConnected;
        _eventSubWebsocketClient.WebsocketDisconnected += OnWebsocketDisconnected;
        _eventSubWebsocketClient.WebsocketReconnected += OnWebsocketReconnected;
        _eventSubWebsocketClient.ErrorOccurred += OnErrorOccurred;
        _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd += OnChannelPointsCustomRewardRedemptionAdd;
        _eventSubWebsocketClient.ChannelChatMessage += OnChannelChatMessage;
        _eventSubWebsocketClient.ChannelChatMessageDelete += OnChannelChatMessageDelete;
        _eventSubWebsocketClient.ChannelChatClear += OnChannelChatClear;
        _eventSubWebsocketClient.ChannelChatClearUserMessages += OnChannelChatClearUserMessages;

        _isEventSubInit = true;
        _oauthUserId = App.Settings.GeneralSettings.ChannelID;

        _logger.LogInformation("Connecting to EventSub...");
        _ = StartAsync(CancellationToken.None);
    }

    public void RefreshEventSub()
    {
        DisableEventSub();
        InitEventSub();
    }

    public void DisableEventSub()
    {
        _logger.LogInformation("User requested disabling Event Sub.");

        _ = StopAsync(CancellationToken.None);

        _isEventSubInit = false;
        _oauthUserId = string.Empty;
        _broadcasterUserId = string.Empty;

        UnsubscribeFromEvents();
    }

    private async Task<bool> EnsureBroadcasterUserIdAsync()
    {
        if (!string.IsNullOrWhiteSpace(App.Settings.GeneralSettings.BroadcasterUserId))
        {
            _broadcasterUserId = App.Settings.GeneralSettings.BroadcasterUserId;
            return true;
        }

        string channelName = App.Settings.jChatSettings?.Channel;
        if (string.IsNullOrWhiteSpace(channelName))
            channelName = App.Settings.GeneralSettings.Username;

        if (string.IsNullOrWhiteSpace(channelName))
            return false;

        try
        {
            var users = await _api.Helix.Users.GetUsersAsync(logins: new List<string> { channelName });
            if (users?.Users != null && users.Users.Any())
            {
                _broadcasterUserId = users.Users[0].Id;
                App.Settings.GeneralSettings.BroadcasterUserId = _broadcasterUserId;
                App.Settings.Persist();
                return true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve broadcaster user id for channel {Channel}", channelName);
        }

        return false;
    }

    private Dictionary<string, string> BuildChatCondition()
    {
        return new Dictionary<string, string>
        {
            { "broadcaster_user_id", _broadcasterUserId },
            { "user_id", _oauthUserId }
        };
    }

    private async Task OnChannelChatMessage(object sender, ChannelChatMessageArgs args)
    {
        var evt = args.Notification.Payload.Event;
        var chatArgs = new TwitchChatMessageEventArgs
        {
            Nick = evt.ChatterUserName,
            Message = evt.Message.Text,
            MessageId = evt.MessageId,
            UserId = evt.ChatterUserId,
            Color = evt.Color ?? string.Empty,
            Badges = BuildBadgeString(evt.Badges),
            Emotes = BuildEmoteString(evt.Message),
            RoomId = evt.BroadcasterUserId,
            SourceRoomId = evt.SourceBroadcasterUserId
        };

        ChatMessageReceived?.Invoke(this, chatArgs);
        await Task.CompletedTask;
    }

    private async Task OnChannelChatMessageDelete(object sender, ChannelChatMessageDeleteArgs args)
    {
        var messageId = args.Notification.Payload.Event.MessageId;
        ChatMessageDeleted?.Invoke(this, new TwitchChatMessageDeleteEventArgs { MessageId = messageId });
        await Task.CompletedTask;
    }

    private async Task OnChannelChatClear(object sender, ChannelChatClearArgs args)
    {
        ChatCleared?.Invoke(this, EventArgs.Empty);
        await Task.CompletedTask;
    }

    private async Task OnChannelChatClearUserMessages(object sender, ChannelChatClearUserMessagesArgs args)
    {
        var evt = args.Notification.Payload.Event;
        ChatUserCleared?.Invoke(this, new TwitchChatClearUserEventArgs
        {
            Username = evt.TargetUserName
        });
        await Task.CompletedTask;
    }

    private static string BuildBadgeString(IEnumerable<ChatBadge> badges)
    {
        if (badges == null)
            return string.Empty;

        return string.Join(",", badges.Select(b => $"{b.SetId}/{b.Id}"));
    }

    private static string BuildEmoteString(ChatMessage message)
    {
        if (message?.Fragments == null || message.Fragments.Length == 0)
            return string.Empty;

        var emoteMap = new Dictionary<string, List<string>>();

        int offset = 0;
        foreach (var fragment in message.Fragments)
        {
            int length = fragment.Text?.Length ?? 0;
            if (fragment.Type == "emote" && fragment.Emote != null && length > 0)
            {
                string emoteId = fragment.Emote.Id;
                string range = $"{offset}-{(offset + length - 1)}";
                if (!emoteMap.ContainsKey(emoteId))
                    emoteMap[emoteId] = new List<string>();
                emoteMap[emoteId].Add(range);
            }
            offset += length;
        }

        if (emoteMap.Count == 0)
            return string.Empty;

        return string.Join("/", emoteMap.Select(kvp => $"{kvp.Key}:{string.Join(",", kvp.Value)}"));
    }

    private async Task OnChannelPointsCustomRewardRedemptionAdd(object sender, ChannelPointsCustomRewardRedemptionArgs e)
    {
        var eventData = e.Notification.Payload.Event;
        _logger.LogInformation($"ChannelPointsCustomRewardRedemptionAdd: {eventData.Reward.Title} redeemed by {eventData.UserName} ({eventData.Reward.Cost})");
        if (!string.IsNullOrWhiteSpace(eventData.UserInput))
            _logger.LogInformation($"User Input: {eventData.UserInput}");

        ChannelPointsRewardRedeemed?.Invoke(this, e);
    }

    private async Task OnWebsocketConnected(object sender, WebsocketConnectedArgs e)
    {
        _logger.LogInformation($"Websocket {_eventSubWebsocketClient.SessionId} connected!");
        Growl.Info("Twitch EventSub connection established successfully.");

        if (!e.IsRequestedReconnect)
        {
            await EnsureBroadcasterUserIdAsync();

            if (string.IsNullOrWhiteSpace(_broadcasterUserId))
            {
                Growl.Warning("Could not resolve the channel to watch. Set the channel name in Chat settings.");
                return;
            }

            var chatCondition = BuildChatCondition();
            string token = App.Settings.GeneralSettings.OAuthToken;

            if (App.Settings.GeneralSettings.UseEventSubChat && ShouldUseEventSubChat())
            {
                await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.chat.message",
                    "1",
                    chatCondition,
                    EventSubTransportMethod.Websocket,
                    _eventSubWebsocketClient.SessionId,
                    accessToken: token);

                await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.chat.message_delete",
                    "1",
                    chatCondition,
                    EventSubTransportMethod.Websocket,
                    _eventSubWebsocketClient.SessionId,
                    accessToken: token);

                await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.chat.clear",
                    "1",
                    chatCondition,
                    EventSubTransportMethod.Websocket,
                    _eventSubWebsocketClient.SessionId,
                    accessToken: token);

                await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.chat.clear_user_messages",
                    "1",
                    chatCondition,
                    EventSubTransportMethod.Websocket,
                    _eventSubWebsocketClient.SessionId,
                    accessToken: token);
            }

            if (App.Settings.GeneralSettings.RedemptionsEnabled)
            {
                var redemptionCondition = new Dictionary<string, string>
                {
                    { "broadcaster_user_id", _broadcasterUserId },
                    { "moderator_user_id", _oauthUserId }
                };

                await _api.Helix.EventSub.CreateEventSubSubscriptionAsync(
                    "channel.channel_points_custom_reward_redemption.add",
                    "1",
                    redemptionCondition,
                    EventSubTransportMethod.Websocket,
                    _eventSubWebsocketClient.SessionId,
                    accessToken: token);
            }
        }
    }

    private async Task OnWebsocketDisconnected(object sender, EventArgs e)
    {
        _logger.LogWarning($"[Twitch EventSub] Websocket {_eventSubWebsocketClient.SessionId} disconnected!");

        for (int attempt = 0; attempt < MaxReconnectAttempts; attempt++)
        {
            double exponentialDelay = BaseDelayMilliseconds * Math.Pow(2, attempt);
            int jitter = _random.Next(0, 1000);
            int delayMilliseconds = (int)Math.Min(exponentialDelay + jitter, MaxDelayMilliseconds);

            _logger.LogInformation($"[Twitch EventSub] Reconnect attempt {attempt + 1}/{MaxReconnectAttempts}. Waiting {delayMilliseconds}ms before next attempt...");
            await Task.Delay(delayMilliseconds);

            try
            {
                _logger.LogInformation($"[Twitch EventSub] Attempting to reconnect (Attempt {attempt + 1})...");
                if (await _eventSubWebsocketClient.ReconnectAsync())
                {
                    _logger.LogInformation($"[Twitch EventSub] Websocket {_eventSubWebsocketClient.SessionId} reconnected successfully on attempt {attempt + 1}!");
                    return;
                }
                else
                {
                    _logger.LogInformation($"[Twitch EventSub] Websocket {_eventSubWebsocketClient.SessionId} reconnect attempt {attempt + 1} failed.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInformation($"[Twitch EventSub] Websocket {_eventSubWebsocketClient.SessionId} reconnect attempt {attempt + 1} threw an exception: {ex.Message}");
            }
        }

        _logger.LogWarning($"[Twitch EventSub] Websocket {_eventSubWebsocketClient.SessionId} failed to reconnect after {MaxReconnectAttempts} attempts.");
        Growl.Error("Twitch EventSub connection lost. Please check your internet connection or Twitch Connection settings.");
    }

    private async Task OnWebsocketReconnected(object sender, EventArgs e)
    {
        _logger.LogInformation($"Websocket {_eventSubWebsocketClient.SessionId} reconnected");
        Growl.Info("Twitch EventSub connection re-established successfully.");
    }

    private async Task OnErrorOccurred(object sender, ErrorOccuredArgs e)
    {
        _logger.LogInformation($"Websocket {_eventSubWebsocketClient.SessionId} - Error occurred!\n{e.Message}\n{e.Exception}");
        Growl.Error($"Twitch EventSub Error: {e.Message}");
    }

    public async Task DisconnectTwitchConnectionAsync()
    {
        _logger.LogInformation("User requested Twitch disconnection.");

        await StopAsync(CancellationToken.None);

        App.Settings.GeneralSettings.ChannelID = string.Empty;
        App.Settings.GeneralSettings.BroadcasterUserId = string.Empty;
        App.Settings.GeneralSettings.OAuthToken = string.Empty;
        App.Settings.Persist();

        TwitchConnectionStatus = "Not Connected";
        AuthTokenExpiration = "...";
        ProfileImage = null;

        _isEventSubInit = false;
        _oauthUserId = string.Empty;
        _broadcasterUserId = string.Empty;

        UnsubscribeFromEvents();
    }

    private void UnsubscribeFromEvents()
    {
        _logger.LogTrace("Unsubscribing from TwitchService events.");

        if (_eventSubWebsocketClient != null)
        {
            _eventSubWebsocketClient.WebsocketConnected -= OnWebsocketConnected;
            _eventSubWebsocketClient.WebsocketDisconnected -= OnWebsocketDisconnected;
            _eventSubWebsocketClient.WebsocketReconnected -= OnWebsocketReconnected;
            _eventSubWebsocketClient.ErrorOccurred -= OnErrorOccurred;
            _eventSubWebsocketClient.ChannelPointsCustomRewardRedemptionAdd -= OnChannelPointsCustomRewardRedemptionAdd;
            _eventSubWebsocketClient.ChannelChatMessage -= OnChannelChatMessage;
            _eventSubWebsocketClient.ChannelChatMessageDelete -= OnChannelChatMessageDelete;
            _eventSubWebsocketClient.ChannelChatClear -= OnChannelChatClear;
            _eventSubWebsocketClient.ChannelChatClearUserMessages -= OnChannelChatClearUserMessages;
        }
    }

    public virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                _logger.LogInformation("Disposing TwitchService managed resources.");
                UnsubscribeFromEvents();
            }
            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class AccessTokenValidatedEventArgs : EventArgs
{
    public string Expires { get; set; }
    public string Login { get; set; }
    public string UserId { get; set; }
    public string ClientId { get; set; }
}

public class TwitchUserDataEventArgs : EventArgs
{
    public string DisplayName { get; set; }
    public string UserId { get; set; }
    public string ProfileImageUrl { get; set; }
    public BitmapImage ProfileImage { get; set; }
}
