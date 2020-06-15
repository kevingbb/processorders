using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Linq;

using System.Net;

namespace processordersapi
{
    public static class ProcessOrder
    {
        [FunctionName("processorder")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req,
            [DurableClient] IDurableEntityClient client,
            [DurableClient]IDurableClient starter,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function 'processorder' processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            // log.LogInformation($"Body: {data}");
            // log.LogInformation($"Body: {data.GetType()}");
            // Value coming back from EventGrid is an array.
            //JArray array = JArray.Parse(data);
            dynamic eventGridSoleItem = (data as JArray)?.SingleOrDefault();
            // Check if EventGrid Item Exists
            if (eventGridSoleItem == null)
            {
                return new BadRequestObjectResult($"Expecting one item in the Event Grid message.");
            }
            // Respond to EventGrid Subscription Validation Event
            if (eventGridSoleItem.eventType == @"Microsoft.EventGrid.SubscriptionValidationEvent")
            {
                log.LogTrace($"Event Grid Validation event received.");
                return new OkObjectResult($"{{\"validationResponse\" : \"{eventGridSoleItem.data.validationCode}\"}}");
            }

            string responseMessage = String.Empty;

            // Convert EventGrid Message to Order & Process
            OrderBlobAttributes orderFile = ParseEventGridPayload(eventGridSoleItem, log);
            if (orderFile != null)
            {
                var entityId = new EntityId(nameof (OrderReceived), orderFile.BatchPrefix);
                responseMessage = $"Entity Key: {data.ToString()}";
                switch (orderFile.Filetype)
                {
                    case "OrderHeaderDetails":
                        await client.SignalEntityAsync(entityId, "HeaderReceived", orderFile.FullUrl);
                        break;
                    case "OrderLineItems":
                        await client.SignalEntityAsync(entityId, "LineItemsReceived", orderFile.FullUrl);
                        break;
                    case "ProductInformation":
                        await client.SignalEntityAsync(entityId, "ProductInformationReceived", orderFile.FullUrl);
                        break;
                }

                // Start Orchestration
                string instanceId = await starter.StartNewAsync("combineorder", null, orderFile.BatchPrefix);

                // var instanceForBatchPrefix = await starter.GetStatusAsync(orderFile.BatchPrefix);
                // if (instanceForBatchPrefix == null)
                // {
                //     log.LogInformation($"New instance needed for prefix '{orderFile.BatchPrefix}'. Starting...");
                //     string instanceId = await starter.StartNewAsync("combineorder", orderFile.BatchPrefix, orderFile.BatchPrefix);
                //     log.LogInformation($"Started. {instanceId}");
                // }
                // else
                // {
                //     log.LogInformation($"Instance already waiting. Current status: {instanceForBatchPrefix.RuntimeStatus}. Firing 'newfile' event...");

                //     if (instanceForBatchPrefix.RuntimeStatus != OrchestrationRuntimeStatus.Running)
                //     {
                //         await starter.TerminateAsync(orderFile.BatchPrefix, @"bounce");
                //         string instanceId = await starter.StartNewAsync("combineorder", orderFile.BatchPrefix, orderFile.BatchPrefix);
                //         log.LogInformation($"Restarted listener for {orderFile.BatchPrefix}. {instanceId}");
                //     }
                //     else
                //     {
                //         await starter.RaiseEventAsync(orderFile.BatchPrefix, @"newfile", orderFile.Filename);
                //     }
                // }
            }
            else
            {
                log.LogError($"File not processed as it was not the correct event type: {eventGridSoleItem.eventType}");
            }

            return new AcceptedResult();
        }

        public static OrderBlobAttributes ParseEventGridPayload(dynamic eventGridItem, ILogger log)
        {
            log.LogError($"{eventGridItem.ToString()}");
            // if (eventGridItem.eventType == @"Microsoft.Storage.BlobCreated"
            //     && eventGridItem.data.api == @"PutBlob"
            //     && eventGridItem.data.contentType == @"text/csv")
            if (eventGridItem.eventType == @"Microsoft.Storage.BlobCreated"
                && eventGridItem.data.api == @"PutBlob")
            {
                try
                {
                    var retVal = OrderBlobAttributes.Parse((string)eventGridItem.data.url);
                    if (retVal == null)
                    {
                        throw new ArgumentException($"Problem parsing event for file {eventGridItem.data.url}");
                    }

                    return retVal;
                }
                catch (Exception ex)
                {
                    log.LogError($"Error parsing Event Grid payload for file {eventGridItem.data.url}: {ex.Message}");
                }
            }

            return null;
        }
    }
}
