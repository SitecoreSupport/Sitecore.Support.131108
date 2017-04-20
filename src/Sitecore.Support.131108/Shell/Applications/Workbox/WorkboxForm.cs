using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Exceptions;
using Sitecore.Globalization;
using Sitecore.Pipelines;
using Sitecore.Resources;
using Sitecore.Shell.Framework;
using Sitecore.Web.UI.Sheer;
using Sitecore.Web.UI.XmlControls;
using Sitecore.Workflows;
using Sitecore.Workflows.Simple;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace Sitecore.Support.Shell.Applications.Workbox
{
    public class WorkboxForm : Sitecore.Shell.Applications.Workbox.WorkboxForm
    {
        public override void HandleMessage(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            switch (message.Name)
            {
                case "workflow:send":
                    this.Send(message);
                    return;

                case "workflow:sendselected":
                    this.SendSelected(message);
                    return;

                case "workflow:sendall":
                    this.SendAll(message);
                    return;

                case "window:close":
                    Windows.Close();
                    return;

                case "workflow:showhistory":
                    ShowHistory(message, Context.ClientPage.ClientRequest.Control);
                    return;

                case "workbox:hide":
                    Context.ClientPage.SendMessage(this, "pane:hide(id=" + message["id"] + ")");
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["id"], "checked", "false");
                    break;

                case "pane:hidden":
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["paneid"], "checked", "false");
                    break;

                case "workbox:show":
                    Context.ClientPage.SendMessage(this, "pane:show(id=" + message["id"] + ")");
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["id"], "checked", "true");
                    break;

                case "pane:showed":
                    Context.ClientPage.ClientResponse.SetAttribute("Check_Check_" + message["paneid"], "checked", "true");
                    break;
            }
            base.HandleMessage(message);
            string str = message["id"];
            if (!string.IsNullOrEmpty(str))
            {
                string name = StringUtil.GetString(new string[] { message["language"] });
                string str3 = StringUtil.GetString(new string[] { message["version"] });
                Item item = Context.ContentDatabase.Items[str, Language.Parse(name), Sitecore.Data.Version.Parse(str3)];
                if (item != null)
                {
                    Dispatcher.Dispatch(message, item);
                }
            }
        }

        private static void ShowHistory(Message message, string control)
        {
            Assert.ArgumentNotNull(message, "message");
            Assert.ArgumentNotNull(control, "control");
            XmlControl webControl = Resource.GetWebControl("WorkboxHistory") as XmlControl;
            Assert.IsNotNull(webControl, "history is null");
            webControl["ItemID"] = message["id"];
            webControl["Language"] = message["la"];
            webControl["Version"] = message["vs"];
            webControl["WorkflowID"] = message["wf"];
            Context.ClientPage.ClientResponse.ShowPopup(control, "below", webControl);
        }

        private void Send(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            bool itemWasMoved = false;

            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                var item = Context.ContentDatabase.Items[message["id"], Language.Parse(message["la"]), Sitecore.Data.Version.Parse(message["vs"])];
                if (item == null || (itemWasMoved = !CheckCommandValidity(item, message["command"])))
                {
                    if (itemWasMoved)
                    {
                        SheerResponse.Alert(Texts.TheItemHasBeenMovedToADifferentWorkflowStateSitecoreWillThereforReloadTheItem);
                    }
                    this.Refresh();
                    return;
                }
                if ((workflowProvider.GetWorkflow(workflowID) != null))
                {
                    Context.ClientPage.ServerProperties["id"] = message["id"];
                    Context.ClientPage.ServerProperties["language"] = message["la"];
                    Context.ClientPage.ServerProperties["version"] = message["vs"];
                    Context.ClientPage.ServerProperties["command"] = message["command"];
                    Context.ClientPage.ServerProperties["workflowid"] = workflowID;
                    NameValueCollection parameters = new NameValueCollection();
                    parameters.Add("ui", message["ui"]);
                    parameters.Add("suppresscomment", message["suppresscomment"]);
                    Context.ClientPage.Start(this, "Comment", parameters);
                }
            }
        }

        private void SendSelected(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            bool itemWasMoved = false;
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                string str2 = message["ws"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    int num = 0;
                    bool flag = false;
                    foreach (string str3 in Context.ClientPage.ClientRequest.Form.Keys)
                    {
                        if ((str3 != null) && str3.StartsWith("check_", StringComparison.InvariantCulture))
                        {
                            string str4 = "hidden_" + str3.Substring(6);
                            string[] strArray = Context.ClientPage.ClientRequest.Form[str4].Split(new char[] { ',' });
                            Item item = Context.ContentDatabase.Items[strArray[0], Language.Parse(strArray[1]), Sitecore.Data.Version.Parse(strArray[2])];
                            if (item == null || (itemWasMoved=!CheckCommandValidity(item, message["command"])))
                            {
                                continue;
                            }
                            WorkflowState state = workflow.GetState(item);
                            if (state.StateID == str2)
                            {
                                try
                                {
                                    workflow.Execute(message["command"], item, state.DisplayName, true, new object[0]);
                                }
                                catch (WorkflowStateMissingException)
                                {
                                    flag = true;
                                }
                                num++;
                            }

                        }
                    }
                    if (flag)
                    {
                        SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                    }
                    if (num == 0 && !itemWasMoved)
                    {
                        Context.ClientPage.ClientResponse.Alert("There are no selected items.");
                    }
                    else
                    {
                        if (itemWasMoved)
                        {
                            SheerResponse.Alert(Texts.TheItemHasBeenMovedToADifferentWorkflowStateSitecoreWillThereforReloadTheItem);
                        }
                        this.Refresh();
                    }
                }
            }
        }
        private void SendAll(Message message)
        {
            Assert.ArgumentNotNull(message, "message");
            bool itemWasMoved = false;
            IWorkflowProvider workflowProvider = Context.ContentDatabase.WorkflowProvider;
            if (workflowProvider != null)
            {
                string workflowID = message["wf"];
                string stateID = message["ws"];
                IWorkflow workflow = workflowProvider.GetWorkflow(workflowID);
                if (workflow != null)
                {
                    WorkflowState state = workflow.GetState(stateID);
                    DataUri[] items = this.GetItems(state, workflow);
                    Assert.IsNotNull(items, "uris is null");
                    if (state == null)
                    {
                    }
                    else
                    {
                        string displayName = state.DisplayName;
                    }
                    bool flag = false;
                    foreach (DataUri uri in items)
                    {
                        Item item = Context.ContentDatabase.Items[uri];
                        if (item == null || (itemWasMoved = !CheckCommandValidity(item, message["command"])))
                        {
                            continue;
                        }
                        try
                        {
                            Processor completionCallback = new Processor("Workflow complete refresh", this, "WorkflowCompleteRefresh");
                            WorkflowUIHelper.ExecuteCommand(item, workflow, message["command"], null, completionCallback);
                        }
                        catch (WorkflowStateMissingException)
                        {
                            flag = true;
                        }
                    }
                    if (flag)
                    {
                        SheerResponse.Alert("One or more items could not be processed because their workflow state does not specify the next step.", new string[0]);
                    }
                    if (itemWasMoved)
                    {
                        SheerResponse.Alert(Texts.TheItemHasBeenMovedToADifferentWorkflowStateSitecoreWillThereforReloadTheItem);
                        this.Refresh();
                    }
                    else
                    {
                        if (items.Count<DataUri>() == 0)
                        {
                            this.Refresh();
                        }
                    }
                }
            }
        }

        private bool CheckCommandValidity(Item item, string commandId)
        {
            Assert.ArgumentNotNullOrEmpty(commandId, "commandId");
            Assert.ArgumentNotNull(item, "item");
            var workflow = item.State.GetWorkflow();
            var state = item.State.GetWorkflowState();
            Assert.IsNotNull(workflow, "workflow");
            Assert.IsNotNull(state, "state");
            if (!workflow.GetCommands(state.StateID).Any(a => a.CommandID == commandId))
            {
                return false;
            }
            return true;
        }
        [UsedImplicitly]
        private void WorkflowCompleteRefresh(WorkflowPipelineArgs args)
        {
            this.Refresh();
        }


    }
}