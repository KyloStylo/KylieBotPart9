using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System;
using System.Linq;
using KylieBotPart9.Dialogs;
using System.Collections.Generic;
using Microsoft.Bot.Builder.Dialogs.Internals;
using KylieBotPart9.Models;
using Autofac;
using KylieBotPart9.Helpers;

namespace KylieBotPart9
{
    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private static List<Models.User> converastionalUserList = new List<Models.User>();
        public static List<Models.User> ConverastionalUserList { get => converastionalUserList; set => converastionalUserList = value; }
        public MessagesController()
        {
            int counter = 0;
            if (ConverastionalUserList != null && ConverastionalUserList.Count > 0)
            {
                foreach (Models.User u in ConverastionalUserList)
                {
                    counter++;
                    if (u.dateAdded <= DateTime.Now.AddHours(-1))
                    {
                        converastionalUserList.RemoveAt(counter);
                    }
                }
            }
        }
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            if (activity.Type == ActivityTypes.Message)
            {
                int index = ConverastionalUserList.FindIndex(u => u.Id == activity.From.Id);

                if (index == -1)
                {
                    Models.User u = new Models.User();
                    u.MessageCount++;
                    u.ConversationId = activity.Conversation.Id;
                    u.Name = activity.From.Name;
                    u.Id = activity.From.Id;
                    u.MessageCount = 1;
                    u.dateAdded = DateTime.Now;
                    ConverastionalUserList.Add(u);
                }

                ConverastionalUserList[ConverastionalUserList.FindIndex(u => u.Id == activity.From.Id)].MessageCount++;

                if (ConverastionalUserList[ConverastionalUserList.FindIndex(u => u.Id == activity.From.Id)].MessageCount > 1)
                {
                    await new BotLogger().Log(activity, ConverastionalUserList[ConverastionalUserList.FindIndex(u => u.Id == activity.From.Id)]);
                }

                await Conversation.SendAsync(activity, () => new RootDialog());
            }
            else
            {
                await HandleSystemMessageAsync(activity);
            }
            var response = Request.CreateResponse(HttpStatusCode.OK);
            return response;
        }

        private async Task<Activity> HandleSystemMessageAsync(Activity message)
        {
            if (message.Type == ActivityTypes.DeleteUserData) { }
            else if (message.Type == ActivityTypes.ConversationUpdate)
            {
                ConnectorClient connector = new ConnectorClient(new Uri(message.ServiceUrl));
                List<User> memberList = new List<User>();

                using (var scope = DialogModule.BeginLifetimeScope(Conversation.Container, message))
                {
                    var client = scope.Resolve<IConnectorClient>();
                    var activityMembers = await client.Conversations.GetConversationMembersAsync(message.Conversation.Id);

                    foreach (var member in activityMembers)
                    {
                        memberList.Add(new User() { Id = member.Id, Name = member.Name });
                    }

                    if (message.MembersAdded != null && message.MembersAdded.Any(o => o.Id == message.Recipient.Id))
                    {
                        var intro = message.CreateReply("Hello **" + message.From.Name + "**! I am **Kylie Bot (KB)**. \n\n What can I assist you with?");
                        await connector.Conversations.ReplyToActivityAsync(intro);
                    }
                }

                if (message.MembersAdded != null && message.MembersAdded.Any() && memberList.Count > 2)
                {
                    var added = message.CreateReply(message.MembersAdded[0].Name + " joined the conversation");
                    await connector.Conversations.ReplyToActivityAsync(added);
                }

                if (message.MembersRemoved != null && message.MembersRemoved.Any())
                {
                    var removed = message.CreateReply(message.MembersRemoved[0].Name + " left the conversation");
                    await connector.Conversations.ReplyToActivityAsync(removed);
                }
            }
            else if (message.Type == ActivityTypes.ContactRelationUpdate) { }
            else if (message.Type == ActivityTypes.Typing) { }
            else if (message.Type == ActivityTypes.Ping)
            {
                Activity reply = message.CreateReply();
                reply.Type = ActivityTypes.Ping;
                return reply;
            }
            return null;
        }
    }
}