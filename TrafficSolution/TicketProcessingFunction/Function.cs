using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Amazon.Translate;
using Amazon.Translate.Model;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace InsuranceDataFunction
{
    public class Function
    {
        /// <summary>
        /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
        /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
        /// region the Lambda function is executed in.
        /// </summary>
        public Function()
        {

        }


        /// <summary>
        /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
        /// to respond to SQS messages.
        /// </summary>
        /// <param name="evnt"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task FunctionHandler(SQSEvent evnt, ILambdaContext context)
        {
            foreach(var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context);
            }
        }

        private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context)
        {
            Ticket ticket = JsonSerializer.Deserialize<Ticket>(message.Body);
            Console.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9}",ticket.license,ticket.owner,ticket.date,ticket.address,ticket.violation,ticket.language,ticket.color,ticket.make,ticket.model,ticket.fine);
            AmazonTranslateClient client = new AmazonTranslateClient();

            string langCode = "en";
            if (ticket.language == "russian")
            {
                langCode = "ru";
            }else if (ticket.language == "spanish")
            {
                langCode = "es";
            }else if (ticket.language == "french")
            {
                langCode = "fr";
            }

            string statement = "Your vehicle was involved in a traffic violation. Please pay the specified ticket amount by 30 days: \n";
            string report = "Vehicle: " + ticket.color + " " + ticket.make + " " + ticket.model + "\n";
            report += "License plate: " + ticket.license + "\n";
            report += "Date: " + ticket.date +"\n";
            report += "Violation Address: " + ticket.address + "\n";
            report += "Violation type: " + ticket.violation + "\n";
            report += "Ticket Amount: " + ticket.fine + "\n";

            var request = new TranslateTextRequest
            {
                SourceLanguageCode = "en",
                TargetLanguageCode = langCode,
                Text = statement
            };

            var reponse = client.TranslateTextAsync(request);

            string finalReport = reponse.Result.TranslatedText + report;

            Console.WriteLine(finalReport);

            var clientSNS = new AmazonSimpleNotificationServiceClient();
            var requestTopic = new ListTopicsRequest();
            var responseTopic = clientSNS.ListTopicsAsync(requestTopic);
            var topic = responseTopic.Result.Topics[0];
            Console.WriteLine("Topic: {0}", topic.TopicArn);

            var publishRequest = new PublishRequest
            {
                Message = finalReport,
                TopicArn = topic.TopicArn
            };

            Task<PublishResponse> publishResponse = clientSNS.PublishAsync(publishRequest);
            Console.WriteLine(publishResponse.Result.MessageId);
            Console.WriteLine("Message sent");
            await Task.CompletedTask;
        }
    }

    public class Ticket
    {
        public String license
        {
            get; set;
        }

        public String owner
        {
            get; set;
        }

        public String date
        {
            get; set;
        }

        public String address
        {
            get; set;
        }

        public String violation
        {
            get; set;
        }

        public String language
        {
            get; set;
        }

        public String color
        {
            get; set;
        }

        public String make
        {
            get; set;
        }

        public String model
        {
            get; set;
        }

        public String fine
        {
            get; set;
        }
    }
}
