using System;
using System.Threading.Tasks;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Connector;
using System.Threading;
using KylieBotPart9.Models;
using System.Net.Http;
using CRMApi.Models;
using System.Collections.Generic;
using KylieBotPart9.Helpers;

namespace KylieBotPart9.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        public int index;
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            Activity lastActivity = await result as Activity;

            index = MessagesController.ConverastionalUserList.FindIndex(u => u.Id == lastActivity.From.Id);

            if (string.IsNullOrEmpty(await context.GetAccessToken(AuthSettings.Scopes)))
            {
                MessagesController.ConverastionalUserList[index].searchTerm = lastActivity.Text;
                await context.Forward(new AzureAuthDialog(AuthSettings.Scopes), this.ResumeAfterAuth, lastActivity, CancellationToken.None);
            }
            else
            {
                //handle extra messages
                await context.PostAsync("break");
                context.Wait(MessageReceivedAsync);
            }

            if (!string.IsNullOrEmpty(MessagesController.ConverastionalUserList[index].Token) && lastActivity.Text == "logout")
            {
                await context.Logout();
            }
        }

        private async Task ResumeAfterAuth(IDialogContext context, IAwaitable<string> result)
        {
            var message = await result;
            AuthResult lResult = result as AuthResult;
            MessagesController.ConverastionalUserList[index].Token = await context.GetAccessToken(AuthSettings.Scopes);
            await context.PostAsync(message);

            await getCRMContact();

            #region CRM Knowledge Search
            List<Attachment> kbaList = await SearchKB();
            if (kbaList.Count > 0)
            {
                var reply = context.MakeMessage();

                reply.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                reply.Attachments = kbaList;

                await context.PostAsync("I found some Knowledge Articles: ");
                await context.PostAsync(reply);

                context.Wait(this.MessageReceivedAsync);
            }
            else
            {
                await context.PostAsync("I couldn't find anything :(");
                context.Wait(this.MessageReceivedAsync);
            }
            #endregion
        }

        public async Task getCRMContact()
        {
            if (MessagesController.ConverastionalUserList != null)
            {
                HttpClient cons = new HttpClient();
                cons.BaseAddress = new Uri("https://crmapikyliebot.azurewebsites.net/");
                cons.DefaultRequestHeaders.Accept.Clear();
                cons.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                cons.Timeout = TimeSpan.FromMinutes(1);

                using (cons)
                {
                    HttpResponseMessage res = await cons.GetAsync("CRM/GetContact/'" + MessagesController.ConverastionalUserList[index].AADEmail.ToString() + "'/");
                    if (res.IsSuccessStatusCode)
                    {
                        CRMContact contact = await res.Content.ReadAsAsync<CRMContact>();
                        MessagesController.ConverastionalUserList[index].CRMContactId = contact.ContactId;
                    }
                }
            }
        }

        public async Task<List<Attachment>> SearchKB()
        {
            List<Attachment> llist = new List<Attachment>();

            if (MessagesController.ConverastionalUserList != null)
            {
                HttpClient cons = new HttpClient();
                cons.BaseAddress = new Uri("https://crmapikyliebot.azurewebsites.net/");
                cons.DefaultRequestHeaders.Accept.Clear();
                cons.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                cons.Timeout = TimeSpan.FromMinutes(1);

                List<Models.CRMKnowledgeBaseArticle> crmKBA = new List<Models.CRMKnowledgeBaseArticle>();

                using (cons)
                {
                    HttpResponseMessage res = await cons.GetAsync("CRM/SearchKB/" + MessagesController.ConverastionalUserList[index].searchTerm + "/");
                    if (res.IsSuccessStatusCode)
                    {
                        crmKBA = await res.Content.ReadAsAsync<List<Models.CRMKnowledgeBaseArticle>>();
                    }
                }

                if (crmKBA.Count > 0)
                {
                    foreach (Models.CRMKnowledgeBaseArticle kb in crmKBA)
                    {
                        Attachment a = BotHelper.GetHeroCard(
                                        kb.title + " (" + kb.articleNumber + ")",
                                        "Published: " + kb.publishedDate.ToShortDateString(),
                                        kb.description,
                                        new CardImage(url: "https://azurecomcdn.azureedge.net/cvt-5daae9212bb433ad0510fbfbff44121ac7c759adc284d7a43d60dbbf2358a07a/images/page/services/functions/01-develop.png"),
                                        new CardAction(ActionTypes.OpenUrl, "Learn more", value: "https://askkylie.microsoftcrmportals.com/knowledgebase/article/" + kb.articleNumber));
                        llist.Add(a);
                    }
                }
            }
            return llist;
        }
    }
}