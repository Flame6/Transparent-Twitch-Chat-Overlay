using System;
using System.Collections.Generic;

namespace TransparentTwitchChatWPF.Twitch;

public class TwitchChatMessageEventArgs : EventArgs
{
    public string Nick { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Badges { get; set; } = string.Empty;
    public string Emotes { get; set; } = string.Empty;
    public string RoomId { get; set; } = string.Empty;
    public string? SourceRoomId { get; set; }
}

public class TwitchChatMessageDeleteEventArgs : EventArgs
{
    public string MessageId { get; set; } = string.Empty;
}

public class TwitchChatClearUserEventArgs : EventArgs
{
    public string Username { get; set; } = string.Empty;
    public string? BanDuration { get; set; }
}
