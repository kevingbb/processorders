using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace processordersapi
{
    [JsonObject(MemberSerialization.OptIn)]
    public class OrderReceived
    {
        private readonly ILogger _logger;

        public OrderReceived(string identifier, ILogger logger)
        {
            id = identifier;
            _logger = logger;
        }

        [JsonProperty("id")]
        public string id { get; set; }
        [JsonProperty("headerReceived")]
        public bool header { get; set; }
        [JsonProperty("headerURL")]
        public string headerURL { get; set; }
        [JsonProperty("lineItemsReceived")]
        public bool lineItems { get; set; }
        [JsonProperty("lineItemsURL")]
        public string lineItemsURL { get; set; }
        [JsonProperty("productInformationReceived")]
        public bool productInformation { get; set; }
        [JsonProperty("productInformationURL")]
        public string productInformationURL { get; set; }
        [JsonProperty("isCheckOrderCompleted")]
        public bool isCheckOrderReceived { get; set; }

        public void HeaderReceived(string url) 
        {
            _logger.LogInformation($"HeaderReceived for {id}");
            this.header = true;
            this.headerURL = url;
            CheckIfOrderReceived();
        }
        public void LineItemsReceived(string url) 
        {
            _logger.LogInformation($"LineItemsReceived for {id}");
            this.lineItems = true;
            this.lineItemsURL = url;
            CheckIfOrderReceived();
        }
        public void ProductInformationReceived(string url) 
        {
            _logger.LogInformation($"ProductInformationReceived for {id}");
            this.productInformation = true;
            this.productInformationURL = url;
            CheckIfOrderReceived();
        }
        private void CheckIfOrderReceived()
        {
            if (header && lineItems && productInformation)
            {
                isCheckOrderReceived = true;
                _logger.LogError($"CheckOrderReceived: {id}");
            }
            else
            {
                _logger.LogInformation($"IsCheckOrderCompleted is still waiting on files for {id}.");
            }
        }

        public CombineOrderRequest CheckOrderReceived()
        {
            if (isCheckOrderReceived)
            {
                CombineOrderRequest orderRequest = new CombineOrderRequest();
                orderRequest.entityKey = id;
                orderRequest.headerURL = headerURL;
                orderRequest.lineItemsURL = lineItemsURL;
                orderRequest.productionInformationURL = productInformationURL;
                return orderRequest;
            }
            else
            {
                return null;
            }
        }

        [FunctionName(nameof(OrderReceived))]
        public static Task Run([EntityTrigger] IDurableEntityContext ctx,
            ILogger logger)
                => ctx.DispatchAsync<OrderReceived>(ctx.EntityKey, logger);
    }
}