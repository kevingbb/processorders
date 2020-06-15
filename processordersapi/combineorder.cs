using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using Microsoft.Azure.Storage.Blob;

namespace processordersapi
{
    public static class CombineOrder
    {
        [FunctionName("combineorder")]
        public static async Task<List<string>> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            log.LogInformation("C# Orchestation Trigger function 'combineorder' processed a request.");

            var outputs = new List<string>();

            // Get OrderReceived Entity and check if complete
            string batchPrefix = context.GetInput<string>();
            log.LogInformation($"Orchestration combineorder {batchPrefix} getting processed.");
            var entityId = new EntityId(nameof(OrderReceived), batchPrefix);
            CombineOrderRequest orderReceived = await context.CallEntityAsync<CombineOrderRequest>(entityId, "CheckOrderReceived");
            if (orderReceived == null)
            {
                log.LogInformation($"Order {batchPrefix} not ready.");
            }
            else
            {
                // Build CombineOrder Content
                //string content = $"{{\"orderHeaderDetailsCSVUrl\": \"{orderReceived.headerURL}\", \"orderLineItemsCSVUrl\": \"{orderReceived.lineItemsURL}\", \"productInformationCSVUrl\": \"{orderReceived.productionInformationURL}\" }}";
                CombineOrderContent content = new CombineOrderContent();
                content.orderHeaderDetailsCSVUrl = orderReceived.headerURL;
                content.orderLineItemsCSVUrl = orderReceived.lineItemsURL;
                content.productInformationCSVUrl = orderReceived.productionInformationURL;
                string json = JsonConvert.SerializeObject(content);
                log.LogInformation($"CombineOrder Content: {json}");
                DurableHttpResponse response = await context.CallHttpAsync(HttpMethod.Post, new System.Uri("https://serverlessohmanagementapi.trafficmanager.net/api/order/combineOrderContent"), json);
                if (response.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    log.LogError($"Combine Order was not successful: {response.StatusCode} - {response.Content}");
                }
                else
                {
                    log.LogInformation($"Combine Order was successful: {response.StatusCode} - {orderReceived.entityKey}");
                    //log.LogInformation($"{response.Content}");
                    orderReceived.content = json;
                    outputs.Add(await context.CallActivityAsync<string>("savecombinedorders", orderReceived));
                    outputs.Add(await context.CallActivityAsync<string>("deleteorders", orderReceived));
                }
            }

            return outputs;
        }

        [FunctionName("savecombinedorders")]
        public static string SaveCombinedOrders([ActivityTrigger] CombineOrderRequest orderReceived,
            [Table("combinedorders"), StorageAccount("AzureWebJobsStorage")] ICollector<CombinedOrdersTable> msg,
            ILogger log)
        {
            log.LogInformation($"Saving CombinedOrders {orderReceived.entityKey}.");
            // Save Orders
            try 
            {
                CombinedOrdersTable combinedOrdersTable = new CombinedOrdersTable();
                combinedOrdersTable.PartitionKey = "BatchOrders";
                combinedOrdersTable.RowKey = orderReceived.entityKey;
                combinedOrdersTable.Text = orderReceived.content;
                msg.Add(combinedOrdersTable);
                log.LogInformation($"Added combinedorders entry {orderReceived.entityKey} to CombinedOrdersTeable completed.");
            }
            catch (Exception exc)
            {
                log.LogError($"Storing entry {orderReceived.entityKey} to combinedorders Table failed: {exc.Message}");
                return $"Storing entry {orderReceived.entityKey} to combinedorders Table failed: {exc.Message}";
            }

            return $"CombinedOrders {orderReceived.entityKey} saved.";
        }

        [FunctionName("deleteorders")]
        public static string DeleteOrders([ActivityTrigger] CombineOrderRequest orderReceived,
            [Blob("orders"), StorageAccount("OrdersStorage")] CloudBlobContainer blobContainer,
            ILogger log)
        {
            log.LogInformation($"Deleting CombinedOrders {orderReceived.entityKey}.");
            // Delete Orders
            try 
            {
                // This also does not make a service call, it only creates a local object.
                CloudBlockBlob blob = null;
                
                blob = blobContainer.GetBlockBlobReference(new CloudBlockBlob(new Uri (orderReceived.headerURL)).Name);
                blob.Delete();
                blob = blobContainer.GetBlockBlobReference(new CloudBlockBlob(new Uri (orderReceived.lineItemsURL)).Name);
                blob.Delete();
                blob = blobContainer.GetBlockBlobReference(new CloudBlockBlob(new Uri (orderReceived.productionInformationURL)).Name);
                blob.Delete();

                log.LogInformation($"Deleted combinedorders entry {orderReceived.entityKey} from blob storage.");
            }
            catch (Exception exc)
            {
                log.LogError($"Deleting order {orderReceived.entityKey} from blob storage failed: {exc.Message}");
                return $"Deleting order {orderReceived.entityKey} from blob storage failed: {exc.Message}";
            }

            return $"Deleting CombinedOrders {orderReceived.entityKey} completed.";
        }
    }

    public class CombineOrderRequest
    {
        [JsonProperty("entityKey")]
        public string entityKey { get; set; }
        [JsonProperty("headerURL")]
        public string headerURL { get; set; }
        [JsonProperty("lineItemsURL")]
        public string lineItemsURL { get; set; }
        [JsonProperty("productionInformationURL")]
        public string productionInformationURL { get; set; }
        [JsonProperty("content")]
        public string content { get; set; }
    }
    public class CombineOrderContent
    {
        [JsonProperty("orderHeaderDetailsCSVUrl")]
        public string orderHeaderDetailsCSVUrl { get; set; }
        [JsonProperty("orderLineItemsCSVUrl")]
        public string orderLineItemsCSVUrl { get; set; }
        [JsonProperty("productInformationCSVUrl")]
        public string productInformationCSVUrl { get; set; }
    }

    public partial class CombinedOrdersTable
    {
        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public string Text { get; set; }
    }
}