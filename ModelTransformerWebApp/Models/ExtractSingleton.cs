using System.Xml.Linq;
using System.Xml;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Linq.Expressions;
using Microsoft.DotNet.Scaffolding.Shared.Project;

namespace ModelTransformerWebApp.Models
{
    public sealed class ExtractSingleton
    {
        private List<string> regularGlossary;
        private List<string> irregularGlossary;

        private ExtractSingleton()
        {
            regularGlossary = new List<string> { "yes", "correct", "ok", "regular" };
            irregularGlossary = new List<string> { "no", "incorrect", "not ok", "irregular" };
        }

        private static ExtractSingleton _instance;

        public static ExtractSingleton GetInstance()
        {
            if (_instance == null)
            {
                _instance = new ExtractSingleton();
            }
            return _instance;
        }

        public BpmnExtraction Extract(BpmnExtraction bpmnExtraction, List<Pattern> patternsToProcess, string[] selectedItems)
        {
            var nodeOrder = 0;
            var usCount = 1;
            List<Pattern> patterns = patternsToProcess.Where(d => d.IsUSPattern == true).ToList();

            var xDoc = new XmlDocument();
            xDoc.Load(bpmnExtraction.FilePath);
            XmlNamespaceManager xmlnm = new XmlNamespaceManager(xDoc.NameTable);
            xmlnm.AddNamespace("bpmn", "http://www.omg.org/spec/BPMN/20100524/MODEL");

            if (xDoc.ChildNodes.Count == 0)
            {
                Console.WriteLine("Load xml successfully!");
            }

            XmlNode? definitions = xDoc.GetElementsByTagName("bpmn:definitions")[0];
            XmlNode? colaboration = xDoc.GetElementsByTagName("bpmn:collaboration")[0];
            Console.WriteLine("Collaboration");
            //Participants
            XmlNodeList? participants = colaboration.SelectNodes("//bpmn:participant", xmlnm);
            //Message Flows
            XmlNodeList? messageFlows = colaboration.SelectNodes("//bpmn:messageFlow", xmlnm);

            XmlNode? externalActor = null;

            //Key:Gateway Value:Process
            Dictionary<XmlNode, XmlNode> startParallelGateways = new Dictionary<XmlNode, XmlNode>();
            Dictionary<XmlNode, XmlNode> startInclusiveGateways = new Dictionary<XmlNode, XmlNode>();

            Dictionary<XmlNode, XmlNode> exclusiveGateways = new Dictionary<XmlNode, XmlNode>();


            #region Iterate trough definitions
            foreach (XmlNode participant in participants)
            {
                string processID = participant.Attributes["processRef"].Value;
                XmlNode process = definitions.ChildNodes.Cast<XmlNode>()
                    .Where(node => node.Attributes["id"].Value.Equals(processID))
                    .FirstOrDefault();

                //External Actor
                if (process.ChildNodes.Count <= 0)
                {
                    externalActor = participant;
                }
                else //Participant - Process
                {
                    bool gotNextNode = true;
                    //StartEvent
                    XmlNode currentXmlNode = process.SelectSingleNode("//bpmn:startEvent", xmlnm);

                    while (gotNextNode)
                    {
                        nodeOrder++;
                        string nodeType = currentXmlNode.Name.Replace("bpmn:", "");
                        if (nodeType == "intermediateThrowEvent" || nodeType == "intermediateCatchEvent")
                        {
                            //Check event sub-type (Message or Timer)
                            XmlNode messageNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                                .FirstOrDefault(node => node.Name.Equals("bpmn:messageEventDefinition"));
                            if (messageNode != null)
                            {
                                nodeType += "message";
                            }
                            else
                            {
                                XmlNode timerNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                                    .FirstOrDefault(node => node.Name.Equals("bpmn:timerEventDefinition"));
                                nodeType += timerNode != null ? "timer" : "";
                            }
                        }

                        if (nodeType != "parallelGateway" && nodeType != "inclusiveGateway")
                        {
                            //Get node lane
                            XmlNode laneSet = process.ChildNodes.Cast<XmlNode>()
                                .Where(node => node.Name.Equals("bpmn:laneSet"))
                                .FirstOrDefault();
                            string laneID = null;
                            string laneName = null;
                            if (laneSet != null)
                            {
                                string[] laneTemp = GetLane(laneSet, currentXmlNode.Attributes["id"].Value);
                                if (laneTemp != null && laneTemp.Length > 0)
                                {
                                    laneID = laneTemp[0];
                                    laneName = laneTemp[1];
                                }
                            }

                            //Update current node
                            string nodeName = currentXmlNode.Attributes["name"] != null ? currentXmlNode.Attributes["name"].Value : "";
                            Node currentNode = new Node(currentXmlNode.Attributes["id"].Value, nodeName,
                                nodeOrder, processID, participant.Attributes["name"].Value, laneID, laneName, nodeType);

                            if (currentNode.Type == "exclusiveGateway")
                            {
                                var outgoingFlowNodes = currentXmlNode.ChildNodes.Cast<XmlNode>()
                                    .Where(node => node.Name.Equals("bpmn:outgoing")).ToList();
                                Dictionary<string, bool> gatewayPaths = new Dictionary<string, bool>();

                                foreach (XmlNode outgoingpath in outgoingFlowNodes)
                                {
                                    XmlNode sequenceFlow = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                                        .FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingpath.InnerText));
                                    string outgoingName = "";
                                    if(sequenceFlow.Attributes["name"] != null)
                                    {
                                        outgoingName = sequenceFlow.Attributes["name"].Value.ToLower();
                                        if (!String.IsNullOrEmpty(outgoingName))
                                        {
                                            gatewayPaths.Add(outgoingName, false);
                                        }
                                    }
                                }
                                currentNode.ExclusiveGatewayPaths = gatewayPaths;
                            }

                            if (selectedItems == null || !selectedItems.Contains(currentNode.BpmId))
                            {
                                bpmnExtraction.Nodes.Add(currentNode);
                            }

                            XmlNode nextNode = NextNode(currentXmlNode, process, xmlnm);
                            if (nextNode == null)
                            {
                                gotNextNode = false;
                            }
                            else
                            {
                                currentXmlNode = nextNode;
                            }

                            if (currentXmlNode.Name.Replace("bpmn:", "").ToLower().Equals("exclusivegateway"))
                            {
                                var outgoingFlowNodes = currentXmlNode.ChildNodes.Cast<XmlNode>()
                                    .Where(node => node.Name.Equals("bpmn:outgoing")).ToList();

                                if (outgoingFlowNodes.Count > 1) {
                                    exclusiveGateways.Add(currentXmlNode, process);
                                }
                            }
                        }
                        else
                        {
                            if (nodeType == "parallelGateway")
                            {
                                startParallelGateways.Add(currentXmlNode, process);
                                XmlNode endParallelGateway = FindEndParallelOrInclusiveGateway(process, currentXmlNode, true);
                                currentXmlNode = endParallelGateway;
                            }else if (nodeType == "inclusiveGateway")
                            {
                                startInclusiveGateways.Add(currentXmlNode, process);
                                XmlNode endInclusiveGateway = FindEndParallelOrInclusiveGateway(process, currentXmlNode, false);
                                currentXmlNode = endInclusiveGateway;
                            }
                        }

                    }

                }
            }
            #endregion

            #region Check Patterns and get User Stories
            List<Node> nodesTemp = new List<Node>(bpmnExtraction.Nodes);

            while (nodesTemp.Count > 1)
            {
                string userStory = null;

                List<Node> listFirstElems = nodesTemp.GetRange(0, 2);
                var firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                string combinedTypes = string.Join(";", firstNodesType);
                Pattern patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                //Try to find a pattern for the first 2 nodes
                userStory = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);

                if (userStory == null && nodesTemp.Count > 2)
                {
                    listFirstElems = nodesTemp.GetRange(0, 3);
                    firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                    combinedTypes = string.Join(";", firstNodesType);
                    patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                    //Try to find a pattern for the first 3 nodes
                    userStory = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);
                }

                //Case 2 gateways in listFirstElems
                bool is2Gateways = listFirstElems.Where(d => d.Type.ToLower().Contains("gateway")).ToList().Count > 1;
                if (userStory == null && is2Gateways)
                {
                    listFirstElems = nodesTemp.GetRange(1, 3);
                    firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                    combinedTypes = string.Join(";", firstNodesType);
                    patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                    //Try to find a pattern for the first 3 nodes
                    userStory = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);
                    if (userStory != null) nodesTemp.RemoveRange(0, 1);
                }

                if (userStory != null)
                {
                    string userStoryFormatted = "US" + usCount + ":" + userStory + ";";
                    bpmnExtraction.UserStoriesResult += userStoryFormatted;
                    if (patternToProcess != null)
                    {
                        bpmnExtraction.Patterns.Add(patternToProcess);
                        bpmnExtraction.PatternsIDs += patternToProcess.Id + ";";
                    }

                    //Remove Nodes from temp list
                    nodesTemp.RemoveRange(0, listFirstElems.Count());

                    usCount++;
                }
                else
                {
                    nodesTemp.RemoveAt(0);
                    //throw new Exception("Pattern not found!");
                }
            }

            #endregion

            #region Get General US
            //Get General US
            Node firstNode = bpmnExtraction.Nodes.FirstOrDefault();
            Node lastNode = bpmnExtraction.Nodes.LastOrDefault();

            if (firstNode.Type.ToUpper().Equals("STARTEVENT") && lastNode.Type.ToUpper().Equals("ENDEVENT"))
            {
                string userStory = null;

                List<Node> generalElems = new List<Node>();
                generalElems.Add(firstNode);
                generalElems.Add(lastNode);

                string combinedTypes = "STARTEVENT;ENDEVENT";

                Pattern patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                //Try to find user story
                userStory = GetUserStory(generalElems, combinedTypes, patterns, externalActor);

                if (userStory != null)
                {
                    string userStoryFormatted = "US" + usCount + ":" + userStory + ";";
                    bpmnExtraction.UserStoriesResult += userStoryFormatted;
                    if (patternToProcess != null)
                    {
                        bpmnExtraction.Patterns.Add(patternToProcess);
                        bpmnExtraction.PatternsIDs += patternToProcess.Id + ";";
                    }

                    usCount++;
                }
            }
            #endregion

            #region Get Parallel Gateways

            foreach (var pGatewayTemp in startParallelGateways)
            {
                List<string> lanes = new List<string>();
                List<string> insidePrallelNodes = new List<string>();

                var outgoingFlowNodes = pGatewayTemp.Key.ChildNodes.Cast<XmlNode>()
                    .Where(node => node.Name.Equals("bpmn:outgoing")).ToList();

                for (int i = 0; i < outgoingFlowNodes.Count; i++)
                {
                    XmlNode currentXmlNode = pGatewayTemp.Key;

                    bool gotNextNode = true;

                    while (gotNextNode)
                    {
                        XmlNode nextNode = NextNode(currentXmlNode, pGatewayTemp.Value, xmlnm, i);
                        if (nextNode == null)
                        {
                            gotNextNode = false;
                        }
                        else
                        {
                            currentXmlNode = nextNode;
                        }

                        nodeOrder++;
                        string nodeType = currentXmlNode.Name.Replace("bpmn:", "");
                        if (nodeType == "intermediateThrowEvent" || nodeType == "intermediateCatchEvent")
                        {
                            //Check event sub-type (Message or Timer)
                            XmlNode messageNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                                .FirstOrDefault(node => node.Name.Equals("bpmn:messageEventDefinition"));
                            if (messageNode != null)
                            {
                                nodeType += "message";
                            }
                            else
                            {
                                XmlNode timerNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                                    .FirstOrDefault(node => node.Name.Equals("bpmn:timerEventDefinition"));
                                nodeType += timerNode != null ? "timer" : "";
                            }
                        }

                        if (nodeType != "parallelGateway")
                        {
                            //Get Participant 
                            string participantName = GetParticipantName(xDoc, pGatewayTemp.Value.Attributes["id"].Value,xmlnm);
                            //Get node lane
                            XmlNode laneSet = pGatewayTemp.Value.ChildNodes.Cast<XmlNode>()
                                .Where(node => node.Name.Equals("bpmn:laneSet"))
                                .FirstOrDefault();
                            string laneID = null;
                            string laneName = null;
                            if (laneSet != null)
                            {
                                string[] laneTemp = GetLane(laneSet, currentXmlNode.Attributes["id"].Value);
                                if (laneTemp != null && laneTemp.Length > 0)
                                {
                                    laneID = laneTemp[0];
                                    laneName = laneTemp[1];
                                    lanes.Add(laneName);
                                }
                            }
                            else
                            {
                                lanes.Add(participantName);
                            }

                            //Update current node
                            string nodeName = currentXmlNode.Attributes["name"] != null ? currentXmlNode.Attributes["name"].Value : "";
                            Node currentNode = new Node(currentXmlNode.Attributes["id"].Value, nodeName,
                                nodeOrder, pGatewayTemp.Value.Attributes["id"].Value, participantName, laneID, laneName, nodeType);
                            bpmnExtraction.Nodes.Add(currentNode);
                            insidePrallelNodes.Add(nodeName);
                        }
                        else
                        {
                            gotNextNode = false;
                        }
                    }
                }

                //Add User Story
                string userStoryParallel = "As ";
                foreach (var laneParallel in lanes.Distinct().ToList())
                {
                    userStoryParallel += laneParallel + ", ";
                }
                userStoryParallel += " I want to execute ";
                foreach (var nodeParallel in insidePrallelNodes)
                {
                    userStoryParallel += nodeParallel + ", ";
                }
                userStoryParallel += " together.";

                if (userStoryParallel != null)
                {
                    Pattern patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals("PARALLELGATEWAY"));
                    string userStoryFormatted = "US" + usCount + ":" + userStoryParallel + ";";
                    bpmnExtraction.UserStoriesResult += userStoryFormatted;
                    if (patternToProcess != null)
                    {
                        bpmnExtraction.Patterns.Add(patternToProcess);
                        bpmnExtraction.PatternsIDs += patternToProcess.Id + ";";
                    }

                    usCount++;
                }
            }

            #endregion

            #region Get Inclusive Gateways

            foreach (var pGatewayTemp in startInclusiveGateways)
            {
                List<string> lanes = new List<string>();
                List<string> insideInclusiveNodes = new List<string>();
                List<string> outgoingNamesInclusiveGateway = new List<string>();

                var outgoingFlowNodes = pGatewayTemp.Key.ChildNodes.Cast<XmlNode>()
                    .Where(node => node.Name.Equals("bpmn:outgoing")).ToList();

                //Get outgoing flows names
                for (int i = 0; i < outgoingFlowNodes.Count; i++) { 
                    XmlNode sequenceFlow = (XmlNode)pGatewayTemp.Value.ChildNodes.Cast<XmlNode>()
                        .FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingFlowNodes[i].InnerText));
                    string outgoingName = sequenceFlow.Attributes["name"].Value.ToLower();
                    outgoingNamesInclusiveGateway.Add(outgoingName);
                }

                //Get next nodes
                for (int i = 0; i < outgoingFlowNodes.Count; i++)
                {
                    XmlNode currentXmlNode = pGatewayTemp.Key;

                    bool gotNextNode = true;

                    while (gotNextNode)
                    {
                        XmlNode nextNode = NextNode(currentXmlNode, pGatewayTemp.Value, xmlnm, i);
                        if (nextNode == null)
                        {
                            gotNextNode = false;
                        }
                        else
                        {
                            currentXmlNode = nextNode;
                        }

                        nodeOrder++;
                        string nodeType = currentXmlNode.Name.Replace("bpmn:", "");
                        if (nodeType == "intermediateThrowEvent" || nodeType == "intermediateCatchEvent")
                        {
                            //Check event sub-type (Message or Timer)
                            XmlNode messageNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                                .FirstOrDefault(node => node.Name.Equals("bpmn:messageEventDefinition"));
                            if (messageNode != null)
                            {
                                nodeType += "message";
                            }
                            else
                            {
                                XmlNode timerNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                                    .FirstOrDefault(node => node.Name.Equals("bpmn:timerEventDefinition"));
                                nodeType += timerNode != null ? "timer" : "";
                            }
                        }

                        if (nodeType != "inclusiveGateway")
                        {
                            //Get Participant 
                            string participantName = GetParticipantName(xDoc, pGatewayTemp.Value.Attributes["id"].Value, xmlnm);
                            //Get node lane
                            XmlNode laneSet = pGatewayTemp.Value.ChildNodes.Cast<XmlNode>()
                                .Where(node => node.Name.Equals("bpmn:laneSet"))
                                .FirstOrDefault();
                            string laneID = null;
                            string laneName = null;
                            if (laneSet != null)
                            {
                                string[] laneTemp = GetLane(laneSet, currentXmlNode.Attributes["id"].Value);
                                if (laneTemp != null && laneTemp.Length > 0)
                                {
                                    laneID = laneTemp[0];
                                    laneName = laneTemp[1];
                                    lanes.Add(laneName);
                                }
                            }
                            else
                            {
                                lanes.Add(participantName);
                            }

                            //Update current node
                            string nodeName = currentXmlNode.Attributes["name"] != null ? currentXmlNode.Attributes["name"].Value : "";
                            Node currentNode = new Node(currentXmlNode.Attributes["id"].Value, nodeName,
                                nodeOrder, pGatewayTemp.Value.Attributes["id"].Value, participantName, laneID, laneName, nodeType);
                            bpmnExtraction.Nodes.Add(currentNode);
                            insideInclusiveNodes.Add(nodeName);
                        }
                        else
                        {
                            gotNextNode = false;
                        }
                    }
                }

                //Add User Story
                string userStoryParallel = "As ";
                foreach (var laneParallel in lanes.Distinct().ToList())
                {
                    userStoryParallel += laneParallel + ", ";
                }
                userStoryParallel += " I will follow one or more of these paths: ";

                foreach (var pathInclusive in outgoingNamesInclusiveGateway)
                {
                    userStoryParallel += pathInclusive + ",";
                }
                userStoryParallel = userStoryParallel.Remove(userStoryParallel.Length - 1);

                userStoryParallel += " in order to execute one or more of the respective activities: ";

                foreach (var nodeParallel in insideInclusiveNodes)
                {
                    userStoryParallel += nodeParallel + ",";
                }
                userStoryParallel = userStoryParallel.Remove(userStoryParallel.Length - 1);

                userStoryParallel += ".";

                if (userStoryParallel != null)
                {
                    Pattern patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals("INCLUSIVEGATEWAY"));
                    string userStoryFormatted = "US" + usCount + ":" + userStoryParallel + ";";
                    bpmnExtraction.UserStoriesResult += userStoryFormatted;
                    if (patternToProcess != null)
                    {
                        bpmnExtraction.Patterns.Add(patternToProcess);
                        bpmnExtraction.PatternsIDs += patternToProcess.Id + ";";
                    }

                    usCount++;
                }
            }

            #endregion


            #region Get Scenarios
            int scenariosCount = 0;
            List<Pattern> patternsScenarios = patternsToProcess.Where(d => d.IsUSPattern == false).ToList();
            foreach (var gateway in exclusiveGateways)
            {
                string scenarios = GetScenarios(xDoc, gateway.Value, gateway.Key, xmlnm, patternsScenarios, externalActor
                    , bpmnExtraction.Nodes.Select(o => o.BpmId).ToArray());

                bpmnExtraction.ScenarioResult += scenarios;
                scenariosCount = scenarios.Split(';').Length;
            }
            #endregion

            #region Get Boundary Events
            XmlNodeList? boundaryEvents = colaboration.SelectNodes("//bpmn:boundaryEvent", xmlnm);
            foreach (XmlNode boundaryEvent in boundaryEvents)
            {
                var outgoingFlowNode = boundaryEvent.ChildNodes.Cast<XmlNode>()
                    .Where(node => node.Name.Equals("bpmn:outgoing")).FirstOrDefault();

                var attachedToNodeID = boundaryEvent.Attributes["attachedToRef"].Value;
                int attachedToNodeIndex = bpmnExtraction.Nodes.FindIndex(node => node.BpmId.Equals(attachedToNodeID));

                if (outgoingFlowNode != null && attachedToNodeIndex >= 0)
                {
                    string? outgoingFlow = outgoingFlowNode?.InnerText;
                    Node nextNode = null;
                    if (outgoingFlow == null)
                    {
                        return null;
                    }
                    else
                    {
                        XmlNode processTempSequence = GetNodeProcess(xDoc, attachedToNodeID);

                        XmlNode sequenceFlow = (XmlNode)processTempSequence.ChildNodes.Cast<XmlNode>()
                            .FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingFlow));

                        if (sequenceFlow != null)
                        {
                            string targetRef = sequenceFlow.Attributes["targetRef"].Value;

                            XmlNode processTempTarget = GetNodeProcess(xDoc, targetRef);
                            XmlNode nodeTemp = (XmlNode)processTempTarget.ChildNodes.Cast<XmlNode>()
                                .Where(node => node.Attributes["id"].Value.Equals(targetRef))
                                .FirstOrDefault();

                            string nodeTypeTemp = nodeTemp.Name.Replace("bpmn:", "");
                            XmlNode laneTemp = GetNodeLane(xDoc, nodeTemp.Attributes["id"].Value);

                            nextNode = new Node(nodeTemp.Attributes["id"].Value, nodeTemp.Attributes["name"].Value,
                            nodeOrder, processTempTarget.Attributes["id"].Value, null, laneTemp.Attributes["id"].Value,
                            laneTemp.Attributes["name"].Value, nodeTypeTemp);
                        }
                    }

                    string nodeName = boundaryEvent.Attributes["name"] != null ? boundaryEvent.Attributes["name"].Value : "";
                    string nodeType = boundaryEvent.Name.Replace("bpmn:", "");
                    Node attachedToNode = bpmnExtraction.Nodes[attachedToNodeIndex];
                    Node currentNode = new Node(boundaryEvent.Attributes["id"].Value, nodeName,
                        nodeOrder, attachedToNode.Process, attachedToNode.ProcessName, attachedToNode.Lane, attachedToNode.LaneName,
                        nodeType);
                    if (currentNode != null || attachedToNode != null || nextNode != null)
                    {
                        string userStoryTemp = null;
                        List<Node> listFirstElems = new List<Node>() { currentNode, attachedToNode, nextNode };
                        var firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                        string combinedTypes = string.Join(";", firstNodesType);
                        List<Pattern> patternsScenariosTemp = patternsToProcess.Where(d => d.IsUSPattern == false).ToList();
                        Pattern patternToProcess = patternsScenariosTemp.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                        //Try to find a pattern for the first 3 nodes
                        userStoryTemp = GetUserStory(listFirstElems, combinedTypes, patternsScenariosTemp, externalActor);
                        if (userStoryTemp != null)
                        {
                            string scenarioFormatted = "Scenario " + scenariosCount + ":" + userStoryTemp + ";";
                            bpmnExtraction.ScenarioResult += scenarioFormatted;

                            scenariosCount++;
                        }
                    }

                }
            }


            #endregion


            return bpmnExtraction;
        }

        private XmlNode NextNode(XmlNode currentNode, XmlNode process, XmlNamespaceManager xmlnm, int parallelOutgoingIndex = 0,bool isScenarioProcessing = false)
        {
            XmlNode nextNode = null;
            var outgoingFlowNodes = currentNode.ChildNodes.Cast<XmlNode>()
                .Where(node => node.Name.Equals("bpmn:outgoing")).ToList();
            XmlNode outgoingFlowNode = null;

            //Check if the node have more than one outgoing flows
            if (outgoingFlowNodes.Count <= 0)
            {
                return null;
            }
            else if (outgoingFlowNodes.Count == 1)
            {
                outgoingFlowNode = outgoingFlowNodes[0];
            }
            else
            {
                if (currentNode.Name.ToLower().Contains("parallelgateway") || currentNode.Name.ToLower().Contains("inclusivegateway"))
                {
                    outgoingFlowNode = outgoingFlowNodes[parallelOutgoingIndex];
                }
                else if (currentNode.Name.ToLower().Contains("eventbasedgateway"))
                {
                    outgoingFlowNode = outgoingFlowNodes.LastOrDefault();
                }
                else if (currentNode.Name.ToLower().Contains("gateway"))
                {
                    foreach (XmlNode outgoingpath in outgoingFlowNodes)
                    {
                        XmlNode sequenceFlow = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                            .FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingpath.InnerText));
                        string outgoingName = sequenceFlow.Attributes["name"].Value.ToLower();
                        if (regularGlossary.Contains(outgoingName) && !isScenarioProcessing)
                        {
                            outgoingFlowNode = outgoingpath;
                        } else if (isScenarioProcessing && irregularGlossary.Contains(outgoingName) ) {
                            outgoingFlowNode = outgoingpath;
                        }else if (outgoingFlowNodes[0] != null)
                        {
                            outgoingFlowNode = outgoingFlowNodes[0];

                        }
                    }
                }
            }

            string? outgoingFlow = outgoingFlowNode?.InnerText;
            if (outgoingFlow == null)
            {
                return null;
            }
            else
            {
                XmlNode sequenceFlow = (XmlNode)process.ChildNodes.Cast<XmlNode>()
.FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingFlow));
                if (sequenceFlow != null)
                {
                    string targetRef = sequenceFlow.Attributes["targetRef"].Value;
                    nextNode = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                        .Where(node => node.Attributes["id"].Value.Equals(targetRef))
                        .FirstOrDefault();
                }
                else
                {
                    return null;
                }
            }
            return nextNode;
        }

        private string GetUserStory(List<Node> nodesToProcess, string combinedTypes, List<Pattern> patterns, XmlNode externalActor)
        {
            string? userStory = null;
            bool nodesInSameLane = false;
            string lane = nodesToProcess.FirstOrDefault().Lane;
            if (lane != null)
            {
                if (nodesToProcess.Count == 3)
                {
                    if (nodesToProcess[1].Lane.Equals(lane) && nodesToProcess[2].Lane.Equals(lane))
                    {
                        nodesInSameLane = true;
                    }
                }
                else if (nodesToProcess.Count == 2)
                {
                    if (nodesToProcess[1].Lane.Equals(lane))
                    {
                        nodesInSameLane = true;
                    }
                }
                else if (nodesToProcess.Count == 4)
                {
                    if (nodesToProcess[1].Lane.Equals(lane))
                    {
                        nodesInSameLane = true;
                    }
                }
            }
            else { nodesInSameLane = true; }

            Pattern patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()) && d.IsSameLane == nodesInSameLane);

            if (patternToProcess != null && patternToProcess.IsSameLane == nodesInSameLane)
            {
                List<string> contentList = ExtractContent(patternToProcess.USTemplate);
                string userStoryTemp = patternToProcess.USTemplate;
                int nodesProcessedCount = 0;
                bool processedOneLane = false;

                foreach (string content in contentList)
                {
                    switch (content)
                    {
                        case "LANE":
                            // code block
                            string nodeLaneOrProcess = "";
                            if (!processedOneLane)
                            {
                                if (nodesToProcess[0] != null && !nodesToProcess[0].Type.ToLower().Contains("gateway"))
                                {
                                    if (nodesToProcess[0].LaneName != null) nodeLaneOrProcess = nodesToProcess[0].LaneName;
                                    else if (nodesToProcess[0].ProcessName != null) nodeLaneOrProcess = nodesToProcess[0].ProcessName;

                                    if (!String.IsNullOrEmpty(nodeLaneOrProcess))
                                    {
                                        var regex = new Regex(Regex.Escape("{LANE}"));
                                        var newText = regex.Replace(userStoryTemp, nodeLaneOrProcess, 1);
                                        userStoryTemp = newText;
                                    }
                                }
                                else if (nodesToProcess[1] != null && !nodesToProcess[1].Type.ToLower().Contains("gateway"))
                                {
                                    if (nodesToProcess[1].LaneName != null) nodeLaneOrProcess = nodesToProcess[1].LaneName;
                                    else if (nodesToProcess[1].ProcessName != null) nodeLaneOrProcess = nodesToProcess[1].ProcessName;

                                    if (!String.IsNullOrEmpty(nodeLaneOrProcess))
                                    {
                                        var regex = new Regex(Regex.Escape("{LANE}"));
                                        var newText = regex.Replace(userStoryTemp, nodeLaneOrProcess, 1);
                                        userStoryTemp = newText;
                                    }
                                }
                                processedOneLane = true;
                            }
                            else if ((bool)!patternToProcess.IsSameLane)
                            {
                                Node notSameLaneNodeTemp = nodesToProcess[nodesToProcess.Count - 1];
                                if (notSameLaneNodeTemp != null && !notSameLaneNodeTemp.Type.ToLower().Contains("gateway"))
                                {
                                    if (notSameLaneNodeTemp.LaneName != null) nodeLaneOrProcess = notSameLaneNodeTemp.LaneName;
                                    else if (notSameLaneNodeTemp.ProcessName != null) nodeLaneOrProcess = notSameLaneNodeTemp.ProcessName;

                                    if (!String.IsNullOrEmpty(nodeLaneOrProcess))
                                    {
                                        var regex = new Regex(Regex.Escape("{LANE}"));
                                        var newText = regex.Replace(userStoryTemp, nodeLaneOrProcess, 1);
                                        userStoryTemp = newText;
                                    }
                                }
                            }
                            else if ((bool)patternToProcess.IsSameLane)
                            {
                                Node notSameLaneNodeTemp = nodesToProcess[nodesToProcess.Count - 1];
                                if (notSameLaneNodeTemp != null && !notSameLaneNodeTemp.Type.ToLower().Contains("gateway"))
                                {
                                    if (notSameLaneNodeTemp.LaneName != null) nodeLaneOrProcess = notSameLaneNodeTemp.LaneName;
                                    else if (notSameLaneNodeTemp.ProcessName != null) nodeLaneOrProcess = notSameLaneNodeTemp.ProcessName;

                                    if (!String.IsNullOrEmpty(nodeLaneOrProcess))
                                    {
                                        var regex = new Regex(Regex.Escape("{LANE}"));
                                        var newText = regex.Replace(userStoryTemp, nodeLaneOrProcess, 1);
                                        userStoryTemp = newText;
                                    }
                                }
                            }

                            if (String.IsNullOrEmpty(nodeLaneOrProcess)) throw new Exception("Lane/Process not found!");
                            break;
                        case "STARTEVENT":
                            // code block
                            Node startEvent = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("STARTEVENT"));
                            if (startEvent != null) userStoryTemp = userStoryTemp.Replace("{STARTEVENT}", startEvent.Name);
                            nodesProcessedCount++;
                            break;
                        case "TASK":
                            // code block
                            Node task = null;
                            if (nodesProcessedCount <= 0 || !nodesToProcess.FirstOrDefault().Type.ToUpper().Equals("TASK"))
                            {
                                task = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("TASK"));
                            }
                            else
                            {
                                task = nodesToProcess.LastOrDefault(d => d.Type.ToUpper().Equals("TASK"));
                            }

                            if (task != null)
                            {
                                var regex = new Regex(Regex.Escape("{TASK}"));
                                var newText = regex.Replace(userStoryTemp, task.Name, 1);
                                userStoryTemp = newText;
                            }
                            nodesProcessedCount++;
                            break;
                        case "INTERMEDIATETHROWEVENTMESSAGE":
                            // code block
                            Node intermediateThrowEventMessage = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("INTERMEDIATETHROWEVENTMESSAGE"));
                            if (intermediateThrowEventMessage != null) userStoryTemp = userStoryTemp.Replace("{INTERMEDIATETHROWEVENTMESSAGE}", intermediateThrowEventMessage.Name);
                            nodesProcessedCount++;
                            break;
                        case "INTERMEDIATETHROWEVENTTIMER":
                            // code block
                            Node intermediateThrowEventTimer = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("INTERMEDIATETHROWEVENTTIMER"));
                            if (intermediateThrowEventTimer != null) userStoryTemp = userStoryTemp.Replace("{INTERMEDIATETHROWEVENTTIMER}", intermediateThrowEventTimer.Name);
                            nodesProcessedCount++;
                            break;
                        case "INTERMEDIATECATCHEVENTMESSAGE":
                            // code block
                            Node intermediateCatchEventMessage = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("INTERMEDIATECATCHEVENTMESSAGE"));
                            if (intermediateCatchEventMessage != null) userStoryTemp = userStoryTemp.Replace("{INTERMEDIATECATCHEVENTMESSAGE}", intermediateCatchEventMessage.Name);
                            nodesProcessedCount++;
                            break;
                        case "INTERMEDIATECATCHEVENTTIMER":
                            // code block
                            Node intermediateCatchEventTimer = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("INTERMEDIATECATCHEVENTTIMER"));
                            if (intermediateCatchEventTimer != null) userStoryTemp = userStoryTemp.Replace("{INTERMEDIATECATCHEVENTTIMER}", intermediateCatchEventTimer.Name);
                            nodesProcessedCount++;
                            break;
                        case "BOUNDARYEVENT":
                            // code block
                            Node boundaryEvent = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("BOUNDARYEVENT"));
                            if (boundaryEvent != null) userStoryTemp = userStoryTemp.Replace("{BOUNDARYEVENT}", boundaryEvent.Name);
                            nodesProcessedCount++;
                            break;
                        case "ENDEVENT":
                            // code block
                            Node endEvent = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("ENDEVENT"));
                            if (endEvent != null) userStoryTemp = userStoryTemp.Replace("{ENDEVENT}", endEvent.Name);
                            nodesProcessedCount++;
                            break;
                        case "PROCESS":
                            // code block

                            if (!externalActor.Attributes["processRef"].Value.Equals(nodesToProcess.LastOrDefault().Process))
                            {
                                userStoryTemp = userStoryTemp.Replace("{PROCESS}", externalActor.Attributes["name"].Value);
                            }
                            break;
                        case "EXCLUSIVEGATEWAYNAME":
                            // code block
                            Node exclusiveGatewayName = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("EXCLUSIVEGATEWAY"));

                            if (!externalActor.Attributes["processRef"].Value.Equals(nodesToProcess.LastOrDefault().Process))
                            {
                                userStoryTemp = userStoryTemp.Replace("{EXCLUSIVEGATEWAYNAME}", exclusiveGatewayName.Name);
                            }
                            break;
                        case "OUTGOINGFLOWN":
                            // code block
                            Node exclusiveGatewayTemp = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("EXCLUSIVEGATEWAY"));
                            string outgoingFlowN = "";
                            if (exclusiveGatewayTemp != null && exclusiveGatewayTemp.ExclusiveGatewayPaths != null
                                && exclusiveGatewayTemp.ExclusiveGatewayPaths.Count > 0)
                            {
                                foreach (var path in exclusiveGatewayTemp.ExclusiveGatewayPaths)
                                {
                                    if (!String.IsNullOrEmpty(path.Key))
                                    {
                                        outgoingFlowN += path.Key + " or ";
                                    }
                                }
                                outgoingFlowN = outgoingFlowN.Remove(outgoingFlowN.Length - 3);
                            }

                            if (!String.IsNullOrEmpty(outgoingFlowN))
                            {
                                userStoryTemp = userStoryTemp.Replace("{OUTGOINGFLOWN}", outgoingFlowN);
                            }

                            nodesProcessedCount++;
                            break;
                        case "GATEWAYPATH":
                            // code block
                            Node exclusiveGateway = nodesToProcess.FirstOrDefault(d => d.Type.ToUpper().Equals("EXCLUSIVEGATEWAY"));
                            string irregularPath = "";
                            if (exclusiveGateway != null && exclusiveGateway.ExclusiveGatewayPaths != null
                                && exclusiveGateway.ExclusiveGatewayPaths.Count > 0) {
                                foreach (var path in exclusiveGateway.ExclusiveGatewayPaths) {
                                    if (path.Value && !String.IsNullOrEmpty(path.Key)) {
                                        irregularPath = path.Key;
                                    } 
                                }
                            }

                            if (!String.IsNullOrEmpty(irregularPath)) {
                                userStoryTemp = userStoryTemp.Replace("{GATEWAYPATH}", irregularPath);
                            }
                            
                            nodesProcessedCount++;
                            break;
                        default:
                            // code block
                            break;
                    }

                }
                userStory = userStoryTemp;
            }

            return userStory;
        }

        private List<string> ExtractContent(string input)
        {
            List<string> contentList = new List<string>();

            Regex regex = new Regex(@"\{([^}]+)\}");
            MatchCollection matches = regex.Matches(input);

            foreach (Match match in matches)
            {
                string content = match.Groups[1].Value;
                contentList.Add(content);
            }

            return contentList;
        }

        private string[] GetLane(XmlNode laneSet, string nodeID)
        {
            string[] lane = new string[2];

            foreach (XmlNode laneToCheck in laneSet)
            {
                XmlNode nodeFound = (XmlNode)laneToCheck.ChildNodes.Cast<XmlNode>()
                    .FirstOrDefault(node => node.InnerText.Equals(nodeID));
                if (nodeFound != null)
                {
                    lane[0] = laneToCheck.Attributes["id"].Value;
                    lane[1] = laneToCheck.Attributes["name"].Value;
                    return lane;
                }
            }
            return lane;
        }

        private XmlNode GetNodeProcess(XmlDocument xDoc, string nodeID)
        {
            XmlNode process = null;
            XmlNodeList? processes = xDoc.GetElementsByTagName("bpmn:process");

            foreach (XmlNode pTemp in processes)
            {
                XmlNode elem = pTemp.ChildNodes.Cast<XmlNode>()
                .Where(node => node.Attributes["id"].Value.Equals(nodeID))
                .FirstOrDefault();
                if (elem != null)
                {
                    process = pTemp;
                    return process;
                }
            }

            return process;
        }

        private XmlNode GetNodeLane(XmlDocument xDoc, string nodeID)
        {
            XmlNode lane = null;
            XmlNodeList? lanes = xDoc.GetElementsByTagName("bpmn:lane");

            foreach (XmlNode lTemp in lanes)
            {
                XmlNode elem = lTemp.ChildNodes.Cast<XmlNode>()
                .Where(node => node.InnerText.Equals(nodeID))
                .FirstOrDefault();
                if (elem != null)
                {
                    lane = lTemp;
                    return lane;
                }
            }

            return lane;
        }

        private XmlNode FindEndParallelOrInclusiveGateway(XmlNode process, XmlNode parallelGatreway, bool isParallel = true)
        {
            XmlNode currNode = parallelGatreway;
            int isEndParallelGatreway = 0;

            while (isEndParallelGatreway <= 1)
            {
                XmlNode outgoingFlowNode = currNode.ChildNodes.Cast<XmlNode>()
                    .FirstOrDefault(node => node.Name.Equals("bpmn:outgoing"));

                if (outgoingFlowNode == null)
                {
                    return null;
                }
                else
                {
                    XmlNode sequenceFlow = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                        .FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingFlowNode.InnerText));
                    if (sequenceFlow != null)
                    {
                        string targetRef = sequenceFlow.Attributes["targetRef"].Value;
                        currNode = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                            .Where(node => node.Attributes["id"].Value.Equals(targetRef))
                            .FirstOrDefault();

                        if (isEndParallelGatreway == 1) return currNode;

                        if (isParallel && currNode.Name.Equals("bpmn:parallelGateway"))
                        {
                            isEndParallelGatreway++;
                        }else if(!isParallel && currNode.Name.Equals("bpmn:inclusiveGateway"))
                        {
                            isEndParallelGatreway++;
                        }
                    }
                }
            }

            return null;
        }

        private string GetParticipantName(XmlDocument xDoc, string processID, XmlNamespaceManager xmlnm)
        {
            string participantName = "";
            XmlNode? colaboration = xDoc.GetElementsByTagName("bpmn:collaboration")[0];

            //Participants
            XmlNodeList? participants = colaboration.SelectNodes("//bpmn:participant", xmlnm);

            foreach (XmlNode participant in participants)
            {
                string processIDTemp = participant.Attributes["processRef"].Value;
                if (processIDTemp.ToLower().Equals(processID.ToLower()))
                {
                    participantName = participant.Attributes["name"].Value;
                }
            }
            return participantName;
        }

        private string GetScenarios(XmlDocument xDoc, XmlNode process, XmlNode gateway, XmlNamespaceManager xmlnm, List<Pattern> patterns,
            XmlNode externalActor, string[] processedNodesIds) {

            string scenarionResult = "";
            int scenarioCount = 1;
            List<Node> nodesToProcess = new List<Node>();

            #region Get previous Node
            XmlNode previousNode = null;
            var incomingFlowNodes = gateway.ChildNodes.Cast<XmlNode>()
                .Where(node => node.Name.Equals("bpmn:incoming")).ToList();
            XmlNode incomingFlowNode = incomingFlowNodes.FirstOrDefault();
            string? incomingFlow = incomingFlowNode?.InnerText;
            if (incomingFlow == null)
            {
                return null;
            }
            else
            {
                XmlNode sequenceFlow = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                    .FirstOrDefault(node => node.Attributes["id"].Value.Equals(incomingFlow));
                if (sequenceFlow != null)
                {
                    string targetRef = sequenceFlow.Attributes["sourceRef"].Value;
                    previousNode = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                        .Where(node => node.Attributes["id"].Value.Equals(targetRef))
                        .FirstOrDefault();
                }
            }
            #endregion

            #region Get Nodes/Next Node
            bool gotNextNode = true;

            //StartEvent
            XmlNode currentXmlNode = previousNode;
            int nodesProcessedTemp = 0;
            while (gotNextNode)
            {
                string nodeType = currentXmlNode.Name.Replace("bpmn:", "");
                if (nodeType == "intermediateThrowEvent" || nodeType == "intermediateCatchEvent")
                {
                    //Check event sub-type (Message or Timer)
                    XmlNode messageNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                        .FirstOrDefault(node => node.Name.Equals("bpmn:messageEventDefinition"));
                    if (messageNode != null)
                    {
                        nodeType += "message";
                    }
                    else
                    {
                        XmlNode timerNode = (XmlNode)currentXmlNode.ChildNodes.Cast<XmlNode>()
                            .FirstOrDefault(node => node.Name.Equals("bpmn:timerEventDefinition"));
                        nodeType += timerNode != null ? "timer" : "";
                    }
                }

                if (nodeType != "parallelGateway")
                {
                    //Get node lane
                    XmlNode laneSet = process.ChildNodes.Cast<XmlNode>()
                        .Where(node => node.Name.Equals("bpmn:laneSet"))
                        .FirstOrDefault();
                    string laneID = null;
                    string laneName = null;
                    if (laneSet != null)
                    {
                        string[] laneTemp = GetLane(laneSet, currentXmlNode.Attributes["id"].Value);
                        if (laneTemp != null && laneTemp.Length > 0)
                        {
                            laneID = laneTemp[0];
                            laneName = laneTemp[1];
                        }
                    }

                    Dictionary<string,bool> gatewayPaths = new Dictionary<string, bool>();
                    if (nodeType == "exclusiveGateway") {
                        //Get Exclusive Gateway Paths
                        var outgoingFlowNodes = currentXmlNode.ChildNodes.Cast<XmlNode>()
                            .Where(node => node.Name.Equals("bpmn:outgoing")).ToList();

                        //Check if the node have more than one outgoing flows
                        if (outgoingFlowNodes.Count > 1)
                        {
                            foreach (XmlNode outgoingpath in outgoingFlowNodes)
                            {
                                XmlNode sequenceFlow = (XmlNode)process.ChildNodes.Cast<XmlNode>()
                                    .FirstOrDefault(node => node.Attributes["id"].Value.Equals(outgoingpath.InnerText));
                                string outgoingName = sequenceFlow.Attributes["name"].Value.ToLower();
                                if (regularGlossary.Contains(outgoingName))
                                {
                                    gatewayPaths.Add(outgoingName, false);
                                }
                                else if (irregularGlossary.Contains(outgoingName))
                                {
                                    gatewayPaths.Add(outgoingName, true);
                                }
                            }
                        }
                    }

                    //Update current node
                    string nodeName = currentXmlNode.Attributes["name"] != null ? currentXmlNode.Attributes["name"].Value : "";
                    string participantName = GetParticipantName(xDoc, process.Attributes["id"].Value, xmlnm);

                    Node currentNode = new Node(currentXmlNode.Attributes["id"].Value, nodeName,
                        0, process.Attributes["id"].Value, participantName, laneID, laneName, nodeType);

                    if (gatewayPaths != null && gatewayPaths.Count > 0) currentNode.ExclusiveGatewayPaths = gatewayPaths;

                    nodesToProcess.Add(currentNode);
                    nodesProcessedTemp++;

                    XmlNode nextNode = NextNode(currentXmlNode, process, xmlnm, 0, true);
                    if (nextNode == null)
                    {
                        gotNextNode = false;
                    }
                    else
                    {
                        currentXmlNode = nextNode;
                        string nodeTypeTemp = currentXmlNode.Name.Replace("bpmn:", "");
                        // && nodeTypeTemp != "exclusiveGateway"
                        if (processedNodesIds.Contains(currentXmlNode.Attributes["id"].Value) && nodesProcessedTemp > 2)
                        {
                            gotNextNode = false;
                        }
                    }
                }
            }
            #endregion

            #region Check Patterns and get User Stories

            while (nodesToProcess.Count > 1)
            {
                string scenario = null;

                List<Node> listFirstElems = nodesToProcess.GetRange(0, 2);
                var firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                string combinedTypes = string.Join(";", firstNodesType);
                Pattern patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                //Try to find a pattern for the first 2 nodes
                scenario = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);

                if (scenario == null)
                {
                    listFirstElems = nodesToProcess.GetRange(0, 3);
                    firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                    combinedTypes = string.Join(";", firstNodesType);
                    patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                    //Try to find a pattern for the first 3 nodes
                    scenario = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);
                }

                if (scenario == null)
                {
                    listFirstElems = nodesToProcess.GetRange(0, 4);
                    firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                    combinedTypes = string.Join(";", firstNodesType);
                    patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                    //Try to find a pattern for the first 4 nodes
                    scenario = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);
                }

                //Case 2 gateways in listFirstElems
                //bool is2Gateways = listFirstElems.Where(d => d.Type.ToLower().Contains("gateway")).ToList().Count > 1;
                //if (scenario == null && is2Gateways)
                //{
                //    listFirstElems = nodesToProcess.GetRange(1, 3);
                //    firstNodesType = listFirstElems.Select(item => item.Type?.ToUpper()).ToList();
                //    combinedTypes = string.Join(";", firstNodesType);
                //    patternToProcess = patterns.FirstOrDefault(d => d.NodeTypes.ToUpper().Equals(combinedTypes.ToUpper()));

                //    //Try to find a pattern for the first 3 nodes
                //    scenario = GetUserStory(listFirstElems, combinedTypes, patterns, externalActor);
                //    if (scenario != null) nodesToProcess.RemoveRange(0, 1);
                //}

                if (scenario != null)
                {
                    string scenarioFormatted = "Scenario " + scenarioCount + ":" + scenario + ";";
                    scenarionResult += scenarioFormatted;

                    //Get last node of scenario
                    var lastNodeIndex = listFirstElems.Count() - 1;
                    Node? lastNodeProcessed = nodesToProcess[lastNodeIndex];

                    //Remove Nodes from temp list
                    if (lastNodeProcessed.Type.Equals("task"))
                    {
                        nodesToProcess.RemoveRange(0, listFirstElems.Count());
                    }
                    else
                    {
                        nodesToProcess.RemoveRange(0, listFirstElems.Count() - 1);
                    }

                    scenarioCount++;
                }
                else {
                    nodesToProcess.Clear();
                }
            }

            #endregion

            return scenarionResult;
        }

        public Dictionary<string,string> GetTasks(BpmnExtraction bpmnExtraction)
        {
            Dictionary<string, string> tasks = new Dictionary<string, string>();
            var xDoc = new XmlDocument();
            xDoc.Load(bpmnExtraction.FilePath);
            XmlNamespaceManager xmlnm = new XmlNamespaceManager(xDoc.NameTable);
            xmlnm.AddNamespace("bpmn", "http://www.omg.org/spec/BPMN/20100524/MODEL");

            if (xDoc.ChildNodes.Count == 0)
            {
                Console.WriteLine("Load xml successfully!");
            }

            XmlNodeList xmlTasks = xDoc.GetElementsByTagName("bpmn:task");

            foreach (XmlNode xmlTask in xmlTasks)
            {
                string id = xmlTask.Attributes["id"].Value;
                string name = xmlTask.Attributes["name"].Value;

                tasks.Add(id,name);
            }

            return tasks;
        }
    }
}
