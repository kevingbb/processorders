// using System.Collections.Generic;
// using System.Net.Http;
// using System.Threading.Tasks;
// using Microsoft.Azure.WebJobs;
// using Microsoft.Azure.WebJobs.Extensions.DurableTask;
// using Microsoft.Azure.WebJobs.Extensions.Http;
// using Microsoft.Azure.WebJobs.Host;
// using Microsoft.Extensions.Logging;

// namespace processordersapi
// {
//     public static class orderMonitor
//     {
//         [FunctionName("orderMonitor")]
//         public static async Task Run(
//             [OrchestrationTrigger] IDurableOrchestrationContext context)
//         {
//             int jobId = context.GetInput<int>();
//             int pollingInterval = GetPollingInterval();
//             DateTime expiryTime = GetExpiryTime();

//             while (context.CurrentUtcDateTime < expiryTime)
//             {
//                 var jobStatus = await context.CallActivityAsync<string>("GetJobStatus", jobId);
//                 if (jobStatus == "Completed")
//                 {
//                     // Perform an action when a condition is met.
//                     await context.CallActivityAsync("SendAlert", machineId);
//                     break;
//                 }

//                 // Orchestration sleeps until this time.
//                 var nextCheck = context.CurrentUtcDateTime.AddSeconds(pollingInterval);
//                 await context.CreateTimer(nextCheck, CancellationToken.None);
//             }

//             // Perform more work here, or let the orchestration end.
//         }
//     }
// }