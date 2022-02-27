using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MobileChatWeb.Models;
using tools.Cache;
using Microsoft.AspNetCore.SignalR;

namespace MobileChatWeb.Hubs
{
    public class ChatHub : Hub
    {
        public static ChatInfo chatInfo = new ChatInfo();

        public override Task OnConnectedAsync()
        {
            chatInfo.totalUsers++;
            return base.OnConnectedAsync();
        }
        public override Task OnDisconnectedAsync(Exception exception)
        {
            chatInfo.totalUsers--;
            return base.OnDisconnectedAsync(exception);
        }
        public async Task JoinChat()
        {
            await ReceiveOldMessage();
            await Clients.All.SendAsync("JoinChat", chatInfo);
        }

        public async Task LeaveChat()
        {
            await Clients.All.SendAsync("LeaveChat", chatInfo);
        }

        public async Task SendMessage(ChatMessage chatMessage)
        {
            await Clients.All.SendAsync("ReceiveMessage", chatMessage);

            try
            {
                List<ChatMessage> chatmessages = SavingManager.JsonSerialization.ReadFromJsonFile<List<ChatMessage>>("chat");

                if (chatmessages == null)
                    chatmessages = new List<ChatMessage>();

                chatmessages.Add(chatMessage);

                SavingManager.JsonSerialization.WriteToJsonFile<List<ChatMessage>>("chat", chatmessages);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"ERROR-C-SendMessage\n{e.Message}");
            }
        }
        public async Task ReceiveOldMessage()
        {
            try
            {
                List<ChatMessage> chatmessages = SavingManager.JsonSerialization.ReadFromJsonFile<List<ChatMessage>>("chat");

                if (chatmessages == null)
                    chatmessages = new List<ChatMessage>();

                if (chatmessages.Count == 0)
                    return;

                List<ChatMessage> chatmessagestosend = new List<ChatMessage>();

                if(chatmessages.Count < 20)
                {
                    for (int i = chatmessages.Count - 1; i > 0; i--)
                    {
                        chatmessagestosend.Add(chatmessages[i]);
                    }
                }
                else
                {
                    for (int i = chatmessages.Count - 1; i > chatmessages.Count - 20; i--)
                    {
                        chatmessagestosend.Add(chatmessages[i]);
                    }
                }

                chatmessagestosend.Reverse();
                await Clients.Caller.SendAsync("ReceiveOldMessage", chatmessagestosend);
            }
            catch (Exception e)
            {
                Debug.WriteLine($"ERROR-C-JoinChat\n{e.Message}");
            }
        }
    }
}