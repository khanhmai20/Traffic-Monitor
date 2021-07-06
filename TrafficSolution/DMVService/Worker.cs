using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Amazon;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace InsuranceDataService
{
    public class Worker : BackgroundService
    {
        private readonly ILogger<Worker> _logger;
        

        //log path
        private const string logPath = @"C:\InsuranceDataService.txt";

        public Worker(ILogger<Worker> logger)
        {
            _logger = logger;
            
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Starting service...");
            await base.StartAsync(cancellationToken);
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Console.WriteLine("Stopping service...");
            await base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {


            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine("service running");
                CredentialProfileOptions options = new CredentialProfileOptions()
                {
                    AccessKey = @"ASIASFJOOCKCWSOA6X4J",
                    SecretKey = @"rUD13uy+phUsazaX0XV8Lxii/sK4cGzTYebPB04u",
                    Token = @"FwoGZXIvYXdzECMaDEPCELc15clJa01rBSLOARZZENoTmgfppQeYiDIi8SGb6LqdLYen6Ji9sZXsm04hFU8ImrPCffKwiA6y4yhvRsaFtcBYuPsklbPFhhbN/aPrwvrWKGnEXEBMcrSeWdcbfVZvuDeng9nIsiILy04tahbeAw43u2FrUOe1Sz280KHoNFLkHGgQutmFvma/z0TeTGArDkYgs7naQ84RWN37SZmYYFP7f5LCHtesndqS5vj1pw7exNXYQ/LxYPavnWpbqgllE7y5vla+kZsAnQgKN4Yu8s4sGgQhTCY5FPJJKM/GyIYGMi3W2LxW457ZZVn0lMLvF1MNifWZeuyaDpwXtyBeE+NbnRSita3dtR4QzMJNMis="
                };
                AWSCredentials credentials = AWSCredentialsFactory.GetAWSCredentials(options, null);
                AmazonSQSClient client = new AmazonSQSClient(credentials, RegionEndpoint.USEast1);

                Console.WriteLine("finish getting object");

                var urlRequest = new GetQueueUrlRequest
                {
                    QueueName = "DownwardQueue"
                };
                var urlResponse = client.GetQueueUrlAsync(urlRequest);
                string inputQueue = urlResponse.Result.QueueUrl;
               
                var receiveMessageRequest = new ReceiveMessageRequest
                {
                    QueueUrl = inputQueue,
                    WaitTimeSeconds = 20
                };
                var receiveMessageResponse = await client.ReceiveMessageAsync(receiveMessageRequest);

                Console.WriteLine("start recieve message");
                Console.WriteLine(inputQueue);

                if (!receiveMessageResponse.Messages.Any())
                {
                    continue;
                }

                var url = new GetQueueUrlRequest
                {
                    QueueName = "UpwardQueue"
                };

                var urlrep = client.GetQueueUrlAsync(url);
                string outputQueue = urlrep.Result.QueueUrl;
                Console.WriteLine(outputQueue);


                XmlDocument xmlDoc = new XmlDocument();
                xmlDoc.Load(@"Data/DMVDatabase.xml");


                foreach (var message in receiveMessageResponse.Messages)
                {
                    XmlElement root = xmlDoc.DocumentElement;

                    Console.WriteLine($"Reading message : {message.Body}");
                    WriteToLog(message.Body);
                    Vehicle car = JsonSerializer.Deserialize<Vehicle>(message.Body);

                    Console.WriteLine("{0} {1} {2} {3}", car.license, car.location, car.date, car.type);
                    string plate = car.license;

                    string outputMessage = "";

                    XmlNode item = root.SelectSingleNode("vehicle[@plate=\"" + plate + "\"]");

                    string fine = ""; 
                    if (car.type.Contains("no_stop", StringComparison.OrdinalIgnoreCase))
                    {
                        fine = "$300";
                    }else if (car.type.Contains("no_full_stop_on_right", StringComparison.OrdinalIgnoreCase))
                    {
                        fine = "$75";
                    }else if (car.type.Contains("no_right_on_red"))
                    {
                        fine = "$125";
                    }

                    if (item != null)
                    {
                        XmlElement owner = root.SelectSingleNode("vehicle[@plate=\"" + plate + "\"]/owner") as XmlElement;
                        string lang = owner.GetAttribute("preferredLanguage");

                        outputMessage = "{ \"license\" : \"" + plate + "\", \"owner\" : \"" + item.SelectSingleNode("owner/contact").InnerText + "\", \"date\" : \"" + car.date + "\", \"address\" : \"" + car.location + "\", \"violation\" : \"" + car.type + "\", \"language\" : \"" + lang + "\", \"color\" : \"" + item.SelectSingleNode("color").InnerText + "\", \"make\" : \"" + item.SelectSingleNode("make").InnerText + "\", \"model\" : \"" + item.SelectSingleNode("model").InnerText + "\", \"fine\" : \"" + fine + "\"}";
                        WriteToLog(outputMessage);
                    }
                    else
                    {
                        outputMessage = "{ \"license\" : \"" + plate + "\", \"insurance\":\"doesn't exists\"}";
                        WriteToLog(outputMessage);
                    }

                    SendMessageRequest request = new SendMessageRequest
                    {
                        QueueUrl = outputQueue,
                        MessageBody = outputMessage
                    };

                    var reponse = await client.SendMessageAsync(request);

                    Console.WriteLine("Deleting the message.\n");
                    var deleteRequest = new DeleteMessageRequest { QueueUrl = inputQueue, ReceiptHandle = message.ReceiptHandle };
                    await client.DeleteMessageAsync(deleteRequest);
                }
                
                await Task.Delay(1000, stoppingToken);
            }
        }

        public void WriteToLog(string message)
        {
            string text = String.Format("{0}\t{1}", DateTime.Now, message);
            using (StreamWriter writer = new StreamWriter(logPath,append:true)) {
                writer.WriteLine(text);
            }
        }
    }

    public class Patient
    {
        public String id
        {
            get; set;
        }

        public String name
        {
            get; set;
        }
    }

    public class Vehicle
    {
        public String license
        {
            get; set;
        }

        public String location
        {
            get; set;
        }

        public String date
        {
            get; set;
        }

        public String type
        {
            get; set;
        }
    }
}
