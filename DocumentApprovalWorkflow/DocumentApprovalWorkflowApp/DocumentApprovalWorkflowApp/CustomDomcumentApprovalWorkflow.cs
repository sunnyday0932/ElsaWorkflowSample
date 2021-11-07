using System.Net;
using System.Net.Http;
using Elsa.Activities.ControlFlow;
using Elsa.Activities.Email;
using Elsa.Activities.Http;
using Elsa.Activities.Http.Extensions;
using Elsa.Activities.Http.Models;
using Elsa.Activities.Primitives;
using Elsa.Activities.Temporal;
using Elsa.Builders;
using NodaTime;

namespace DocumentApprovalWorkflowApp
{
    public class CustomDomcumentApprovalWorkflow : IWorkflow
    {
        public void Build(IWorkflowBuilder builder)
        {
            builder
                .WithDisplayName("Custom Document Approval Workflow")
                .HttpEndpoint(activity =>
                activity
                .WithPath("/custom/ducuments")
                .WithMethod(HttpMethod.Post.Method)
                .WithReadContent())
                .SetVariable("Document", context => context.GetInput<HttpRequestModel>()!.Body)
                .SendEmail(activity => activity
                    .WithSender("workflow@acme.com")
                    .WithRecipient(context => context.GetVariable<dynamic>("Document")!.SendFor.Email)
                    .WithSubject(context => $"Document received from {context.GetVariable<dynamic>("Document")!.Author.Name} Titel:{context.GetVariable<dynamic>("Document")!.Title}")
                    .WithBody(context =>
                    {
                        var document = context.GetVariable<dynamic>("Document")!;
                        var author = document!.Author;
                        var amount = document!.Body;
                        var title = document!.Title;
                        return $"{title} 由 {author.Name} 發送.{amount}<br><a href=\"{context.GenerateSignalUrl("Approve")}\">通過</a> or <a href=\"{context.GenerateSignalUrl("Reject")}\">拒絕</a>";
                    }))
                .WriteHttpResponse(
                    HttpStatusCode.OK,
                    "<h1>Request for Approval Sent</h1><p>Your document has been received and will be reviewed shortly.</p>",
                    "text/html")
                .Then<Fork>(activity => activity.WithBranches("Approve", "Reject"), fork =>
                {
                    fork
                        .When("Approve")
                        .SignalReceived("Approve")
                        .SendEmail(activity => activity
                            .WithSender("workflow@acme.com")
                            .WithRecipient(context => context.GetVariable<dynamic>("Document")!.Author.Email)
                            .WithSubject(context => $"Document {context.GetVariable<dynamic>("Document")!.Id} Approved!")
                            .WithBody(context => $" {context.GetVariable<dynamic>("Document")!.Author.Name}, 謝謝你的付出"))
                        .ThenNamed("Join");

                    fork
                        .When("Reject")
                        .SignalReceived("Reject")
                        .SendEmail(activity => activity
                            .WithSender("workflow@acme.com")
                            .WithRecipient(context => context.GetVariable<dynamic>("Document")!.Author.Email)
                            .WithSubject(context => $"Document {context.GetVariable<dynamic>("Document")!.Id} Rejected")
                            .WithBody(context => $"抱歉了 {context.GetVariable<dynamic>("Document")!.Author.Name}, 公司付不起"))
                        .ThenNamed("Join");
                })
                .Add<Join>(join => join.WithMode(Join.JoinMode.WaitAny)).WithName("Join")
                .WriteHttpResponse(HttpStatusCode.OK, "Thanks for the hard work!", "text/html");
        }
    }
}
