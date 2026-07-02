using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TransparentTwitchChatWPF.ChatProviders;

public static class ChatProviderFactory
{
    public static IChatProvider Create(ChatTypes chatType)
    {
        // Personal fork: NativeChat only
        return new NativeChatProvider();
    }
}
