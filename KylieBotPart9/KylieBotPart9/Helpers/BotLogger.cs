using KylieBotPart9.Models;
using Microsoft.Bot.Connector;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace KylieBotPart9.Helpers
{
    public class BotLogger
    {
        public async Task Log(Activity activity, User u)
        {
            var x = activity;
            BotChat chat = new BotChat(x.From.Name == null ? "" : x.From.Name.ToString() + "(" + 
                                        x.From.Id == null ? "" :  x.From.Id.ToString() + ")", 
                                        x.Text == null ? "" : x.Text, 
                                        x.ChannelId == null ? "" : x.ChannelId.ToString(), 
                                        x.Timestamp == null ? DateTime.Now : x.Timestamp.Value,
                                        x.Conversation.Id);

            if (u.existingChatID != Guid.Empty && u.ConversationId != string.Empty && u.ConversationId == activity.Conversation.Id)
            {
                chat.existingChatID = u.existingChatID;
            }
            if (u.CRMContactId != Guid.Empty)
            {
                chat.regardingId = u.CRMContactId;
            }

            HttpClient cons = new HttpClient();
            cons.BaseAddress = new Uri("https://crmapikyliebot.azurewebsites.net/");
            cons.DefaultRequestHeaders.Accept.Clear();
            cons.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            cons.Timeout = TimeSpan.FromMinutes(1);

            using (cons)
            {
                var content = new StringContent(JsonConvert.SerializeObject(chat), Encoding.UTF8, "application/json");
                HttpResponseMessage res = await cons.PostAsync("CRM/CreateBotChat", content);
                if (res.IsSuccessStatusCode)
                {
                    Tuple<bool, Guid> result = await res.Content.ReadAsAsync<Tuple<bool, Guid>>();
                    if (u.existingChatID == Guid.Empty && result.Item1 && result.Item2 != Guid.Empty)
                    {
                        int ind = MessagesController.ConverastionalUserList.FindIndex(y => y.Id == activity.From.Id);
                        MessagesController.ConverastionalUserList[ind].existingChatID = result.Item2;
                    }
                }
            }
        }
    }
}